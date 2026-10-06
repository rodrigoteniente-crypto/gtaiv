using System.Diagnostics;
using KickChaos;

int checks = 0;
void Check(bool condition, string label)
{
    if (!condition) throw new Exception("FAIL: " + label);
    checks++;
}
KickEvent Event(KickEventKind kind, string user = "viewer", string name = "") =>
    new() { Kind = kind, User = user, EventName = name, Simulated = true };

var cfg = new Config();
var delayed = new DelayedKickEvents();
foreach (KickEventKind kind in Enum.GetValues<KickEventKind>())
{
    var ev = Event(kind);
    Check(delayed.TryEnqueue(ev, 100, cfg), "each event kind accepts delay: " + kind);
    Check(delayed.TryDequeue(109.999) == null, "each event kind waits full delay: " + kind);
    Check(ReferenceEquals(delayed.TryDequeue(110), ev), "each event kind emits exactly its entry: " + kind);
    Check(delayed.TryDequeue(500) == null, "each event kind emits only once: " + kind);
}
cfg.KickDelayOverrides["Chat"] = 0;
cfg.KickDelayOverrides["Kicks"] = 20;
var sub = Event(KickEventKind.Subscription, "Ana");
var chat = Event(KickEventKind.Chat, "Luis");
var donor = Event(KickEventKind.Other, "Ana", "KicksGifted");
delayed.TryEnqueue(sub, 200, cfg); delayed.TryEnqueue(donor, 200, cfg); delayed.TryEnqueue(chat, 200, cfg);
Check(ReferenceEquals(delayed.TryDequeue(200), chat), "zero-delay chat overtakes waiting alert without duplicating it");
Check(ReferenceEquals(delayed.TryDequeue(210), sub), "subscription keeps independent due time");
Check(delayed.TryDequeue(219) == null && ReferenceEquals(delayed.TryDequeue(220), donor), "donation override preserves twenty seconds");

cfg.KickDelayCapacity = 2;
delayed.TryEnqueue(Event(KickEventKind.Subscription, "first"), 300, cfg);
delayed.TryEnqueue(Event(KickEventKind.GiftedSubs, "second"), 300, cfg);
var third = Event(KickEventKind.Subscription, "third");
Check(!delayed.TryEnqueue(third, 300, cfg) && delayed.Count == 2, "full paid queue requests backpressure without eviction");
Check(delayed.TryDequeue(310).User == "first", "same due time preserves received order");
Check(delayed.TryEnqueue(third, 310, cfg), "retained paid event admits after space frees");
Check(delayed.TryDequeue(310).User == "second" && delayed.TryDequeue(320).User == "third", "retained events survive bounded backpressure exactly once");
cfg.KickDelayOverrides["Chat"] = 10;
delayed.TryEnqueue(Event(KickEventKind.Chat), 400, cfg); delayed.TryEnqueue(sub, 400, cfg);
Check(delayed.TryEnqueue(donor, 400, cfg) && delayed.Count == 2 && delayed.DroppedChats == 1, "donation protects a paid entry by removing excess chat");
delayed.Clear();

cfg.EventMap["Suscripcion"] = Config.ParseSteps("Npc");
cfg.EventMap["RegaloSubs"] = Config.ParseSteps("Npc+Explosion");
cfg.GiftNpcForRecipients = true;
var engine = new TriggerEngine(cfg);
var testSub = KickDonation.TestSubscription("Suscripcion", 3);
Check(testSub.Kind == KickEventKind.Subscription && testSub.Count == 3 && testSub.Simulated, "new subscription test uses actual subscription event pipeline");
delayed.TryEnqueue(testSub, 500, cfg);
Check(delayed.TryDequeue(509.999) == null, "subscription test waits the same configured alert delay");
var processed = engine.Process(delayed.TryDequeue(510), DateTime.UtcNow, out _);
Check(processed.Count == 1 && processed[0].User == "prueba" && processed[0].Count == 3, "subscription test retains NPC event source, owner and tenure");
var actions = new ActionQueue(); cfg.MaxQueue = 2;
actions.Enqueue(processed[0], cfg, ActionNames.Canonical, null);
var spawn = actions.TryDequeue(0, 0);
Check(spawn.Name == "Npc" && KickDonation.StartsDirector(spawn, true, false), "spawn action starts city director when spawn camera is enabled");
Check(!KickDonation.StartsDirector(spawn, false, false) && !KickDonation.StartsDirector(spawn, true, true), "disabled spawn camera and active director keep intended state");

var gifts = Event(KickEventKind.GiftedSubs, "Donor"); gifts.Count = 3;
gifts.Recipients.AddRange(new[] { "Ana", "Luis", "Marta" });
var mapped = engine.Process(gifts, DateTime.UtcNow, out _);
var output = new List<QueuedAction>(); double at = 10;
foreach (var pending in mapped)
{
    for (int guard = 0; !pending.QueueComplete && guard < 100; guard++)
    {
        actions.Enqueue(pending, cfg, ActionNames.Canonical, null);
        Check(actions.Count <= 2, "expanded gift remains inside action queue capacity");
        var next = actions.TryDequeue(at++, 0);
        if (next != null) output.Add(next);
    }
    Check(pending.QueueComplete, "expanded gift admits every action without retrying event parsing");
}
while (actions.Count > 0) { var next = actions.TryDequeue(at++, 0); if (next != null) output.Add(next); }
Check(output.Count(e => e.Name == "Explosion") == 3, "large expanded event does not lose or duplicate effects while backpressured");
Check(output.Where(e => e.Name == "Npc").Select(e => e.User).SequenceEqual(gifts.Recipients), "every gift recipient spawns once with their own name");
actions.TryDequeue(100, 100); actions.Clear();
var afterClear = new PendingAction { Steps = Config.ParseSteps("Npc"), FromEvent = true };
actions.Enqueue(afterClear, cfg, ActionNames.Canonical, null);
Check(actions.TryDequeue(0, 1) != null, "clearing queues resets old pacing");

string temp = Path.Combine(Path.GetTempPath(), "kickstream-event-" + Guid.NewGuid()); Directory.CreateDirectory(temp);
try
{
    File.WriteAllText(Path.Combine(temp, "config.ini"), "[DelayKick]\nSegundos=10\nChat=0\nKicks=20\n[CamaraKicks]\nCantidad=100\nSegundos=10\nMinimoSegundos=5\nMaximoSegundos=45\n");
    var loaded = Config.Load(temp, new List<string>());
    Check(loaded.EventDelay(chat) == 0 && loaded.EventDelay(donor) == 20 && loaded.EventDelay(sub) == 10, "delay configuration keeps per-kind overrides");
    Check(KickDonation.CameraSeconds(100, loaded) == 10 && KickDonation.CameraSeconds(250, loaded) == 25, "donation uses configurable proportional seconds");
    Check(KickDonation.CameraSeconds(1, loaded) == 5 && KickDonation.CameraSeconds(int.MaxValue, loaded) == 45, "donation camera is bounded at both ends without overflow");
    Check(KickDonation.CameraSeconds(0, loaded) == 0 && KickDonation.CameraSeconds(-1, loaded) == 0, "zero or negative donations never create a camera interval");
    File.WriteAllText(Path.Combine(temp, "config.ini"), "[DelayKick]\nSegundos=NaN\nChat=Infinity\n[CamaraKicks]\nCantidad=0\nSegundos=NaN\nMinimoSegundos=12\nMaximoSegundos=4\n");
    loaded = Config.Load(temp, new List<string>());
    Check(loaded.EventDelay(sub) == 10 && loaded.EventDelay(chat) == 10, "invalid nonfinite delays fall back safely");
    Check(KickDonation.CameraSeconds(100, loaded) == 12, "invalid donation ratio and inverted bounds remain finite and usable");
}
finally { Directory.Delete(temp, true); }

string Frame(string name, string data, bool encoded = false) => "{\"event\":" + Json.Quote("App\\Events\\" + name) + ",\"data\":" + (encoded ? Json.Quote(data) : data) + "}";
var client = new KickClient(new Config(), _ => { });
client.HandleFrame(Frame("SubscriptionEvent", "{\"username\":\"Ana\",\"months\":3,\"event_id\":\"sub-a\"}"));
client.HandleFrame(Frame("ChannelSubscriptionEvent", "{\"user\":{\"username\":\"Ana\"},\"months\":3,\"event_id\":\"sub-a\"}", true));
client.HandleFrame(Frame("SubscriptionEvent", "{\"username\":\"Ana\",\"months\":3,\"event_id\":\"sub-b\"}"));
Check(client.Events.Count == 2, "aliased subscription deliveries deduplicate but distinct event IDs survive");
while (client.Events.TryDequeue(out _)) { }
client.HandleFrame(Frame("KicksGifted", "{\"sender\":{\"username\":\"Ana\"},\"gift\":{\"amount\":100},\"event_id\":\"donation-a\"}"));
client.HandleFrame(Frame("KicksGiftedEvent", "{\"username\":\"Ana\",\"amount\":100,\"event_id\":\"donation-a\"}"));
client.HandleFrame(Frame("KicksGifted", "{\"username\":\"Ana\",\"amount\":100,\"event_id\":\"donation-b\"}"));
client.HandleFrame(Frame("KicksGifted", "{\"username\":\"Ana\",\"amount\":0}"));
Check(client.Events.Count == 2, "donation aliases normalize and deduplicate while preserving equal distinct donations");
Check(client.Events.TryDequeue(out var parsedDonation) && parsedDonation.EventName == "KicksGifted" && parsedDonation.Count == 100 && parsedDonation.User == "Ana", "donation amount and owner parse from structured gift");
var inbox = new KickEventInbox(16);
for (int i = 0; i < 16; i++) inbox.TryEnqueue(Event(KickEventKind.Subscription, "sub" + i));
Check(!inbox.TryEnqueue(sub) && inbox.Count == 16, "network ingress never exceeds its important-event capacity");
inbox.TryDequeue(out _); Check(inbox.TryEnqueue(sub) && inbox.Count == 16, "network ingress resumes without dropping important events");
inbox.TryEnqueue(chat); Check(inbox.Count == 16, "chat overflow cannot displace a subscription");

var multiCfg = new Config { OneActionPerMessage = false, UserCooldown = 15 };
multiCfg.Triggers.Add(new ChatTrigger { Keyword = "boom", ActionText = "Explosion", Steps = Config.ParseSteps("Explosion") });
multiCfg.Triggers.Add(new ChatTrigger { Keyword = "dia", ActionText = "Dia", Steps = Config.ParseSteps("Dia") });
var multi = new TriggerEngine(multiCfg); var message = Event(KickEventKind.Chat); message.Simulated = false; message.Text = "boom dia";
DateTime date = DateTime.UtcNow;
Check(multi.Process(message, date, out _).Count == 2, "multiple configured effects share message-level user cooldown");
Check(multi.Process(message, date.AddSeconds(1), out _).Count == 0, "next message respects user cooldown");

var failing = new KickClient(new Config { ChatroomId = 1, WebsocketUrlOverride = "ws://127.0.0.1:1/" }, _ => { });
failing.Start();
Check(SpinWait.SpinUntil(() => failing.Status.StartsWith("reconectando"), 3000), "connection failure enters cancellable retry");
var elapsed = Stopwatch.StartNew(); failing.Stop();
Check(elapsed.ElapsedMilliseconds < 1000 && !failing.Connected && failing.Status == "detenido", "cooperative shutdown does not abort a thread or block the game");
Console.WriteLine($"PASS: {checks} stream event delay, exact-once queue, donation, parsing and lifecycle checks.");
