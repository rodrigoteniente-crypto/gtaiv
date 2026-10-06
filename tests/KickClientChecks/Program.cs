using System.Diagnostics;
using KickChaos;

int checks = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
    checks++;
}

string configFolder = Path.Combine(Path.GetTempPath(), "kickchaos-ai-" + Guid.NewGuid());
Directory.CreateDirectory(configFolder);
try
{
    string path = Path.Combine(configFolder, "config.ini");
    File.WriteAllText(path, "[Suscriptor]\n");
    var loaded = Config.Load(configFolder, new List<string>());
    Check(loaded.UseGameAi, "an existing config without IA defaults to game AI");
    Check(loaded.KeyFollowNpc == System.Windows.Forms.Keys.F4, "manual NPC camera defaults to F4");
    File.WriteAllText(path, "[Suscriptor]\nIA = Mod\n");
    Check(!Config.Load(configFolder, new List<string>()).UseGameAi, "explicit Mod mode survives config reload");
    File.WriteAllText(path, "[Suscriptor]\nIA = juego\n[Teclas]\nSeguirNpc = F5\n");
    loaded = Config.Load(configFolder, new List<string>());
    Check(loaded.UseGameAi, "persisted game mode is case insensitive");
    Check(loaded.KeyFollowNpc == System.Windows.Forms.Keys.F5, "manual NPC camera shortcut can be customized");
}
finally { Directory.Delete(configFolder, true); }

string Frame(string name, string data, bool encoded = false, string channel = "chatrooms.1.v2") =>
    "{\"event\":" + Json.Quote("App\\Events\\" + name) + ",\"channel\":" + Json.Quote(channel) +
    ",\"data\":" + (encoded ? Json.Quote(data) : data) + "}";

var cfg = new Config { Channel = "streamer", ChatroomId = 1, ChannelId = 2 };
var client = new KickClient(cfg, _ => { });
client.HandleFrame(Frame("SubscriptionEvent", "{\"username\":\"Ana\",\"months\":3}"));
client.HandleFrame(Frame("SubscriptionEvent", "{\"months\":3, \"username\":\"Ana\"}", true, "channel.2"));
client.HandleFrame(Frame("SubscriptionEvent", "{\"username\":\"Luis\",\"months\":1}", false, "channel_2"));
client.HandleFrame(Frame("ChannelSubscriptionEvent", "{\"user\":{\"username\":\"Ana\"},\"months\":3}"));
client.HandleFrame(Frame("ChannelSubscriptionEvent", "{\"channel_id\":2,\"user_ids\":[9]}"));
Check(client.Events.Count == 2, "four Pusher aliases deduplicate the same subscriber and preserve different subscribers");
Check(client.Events.TryDequeue(out var ana) && ana.User == "Ana" && ana.Count == 3, "subscription tenure preserved");
Check(client.Events.TryDequeue(out var luis) && luis.User == "Luis", "object-valued Pusher data preserves the next subscription");
client.HandleFrame(Frame("SubscriptionEvent", "{\"username\":\"Ana\",\"months\":3,\"event_id\":\"a\"}"));
client.HandleFrame(Frame("SubscriptionEvent", "{\"username\":\"Ana\",\"months\":3,\"event_id\":\"b\"}"));
Check(client.Events.Count == 2, "separate identified subscription events remain distinct");
while (client.Events.TryDequeue(out _)) { }

string chat = "{\"id\":\"chat-1\",\"sender\":{\"username\":\"Mod\",\"identity\":{\"badges\":[{\"type\":\"subscriber\"},{\"type\":\"moderator\"}]}},\"content\":\"!boom\"}";
Parallel.For(0, 40, _ => client.HandleFrame(Frame("ChatMessageEvent", chat)));
Check(client.Events.Count == 1, "concurrent duplicate chat deliveries are accepted once");
Check(client.Events.TryDequeue(out var mod) && mod.Level == 3 && mod.Text == "!boom", "chat roles/content preserved");
client.HandleFrame(Frame("SubscriptionEvent", "malformed", true));
client.HandleFrame("{\"event\":\"ChatMessageEvent\"} trailing junk");
Check(client.Events.IsEmpty, "malformed frames ignored without damaging the client");
client.HandleFrame(Frame("FollowersUpdated", "{\"followersCount\":123}"));
Check(client.Events.IsEmpty, "follower counter refresh does not create a nameless NPC event");

string gift = "{\"gifter_username\":\"Donor\",\"gifted_usernames\":[\"Ana\",\"Luis\"]}";
client.HandleFrame(Frame("GiftedSubscriptionsEvent", gift));
client.HandleFrame(Frame("GiftedSubscriptionsEvent", "{\"gifted_usernames\":[\"Luis\",\"Ana\"],\"gifter_username\":\"Donor\"}", true));
Check(client.Events.Count == 1, "reordered gift recipient lists deduplicate");
Check(client.Events.TryDequeue(out var gifts) && gifts.Count == 2 && gifts.Recipients.SequenceEqual(new[] { "Ana", "Luis" }), "gift recipient names retained");
cfg.EventMap["RegaloSubs"] = Config.ParseSteps("Npc+Explosion");
var engine = new TriggerEngine(cfg);
var mapped = engine.Process(gifts, DateTime.UtcNow, out _);
Check(mapped.Count == 3 && mapped.Count(p => p.User == "Donor") == 1, "NPC actions target recipients and other actions retain donor");
var queue = new ActionQueue();
foreach (var action in mapped) queue.Enqueue(action, cfg, ActionNames.Canonical, null);
var emitted = new List<QueuedAction>();
for (double at = 0; queue.Count > 0; at += 1)
{
    var action = queue.TryDequeue(at, 0);
    if (action != null) emitted.Add(action);
}
Check(emitted.Count(a => a.Name == "Explosion" && a.User == "Donor") == 2, "existing per-gift chaos multiplier preserved without amplification");
Check(emitted.Where(a => a.Name == "Npc").Select(a => a.User).SequenceEqual(new[] { "Ana", "Luis" }), "gift recipient NPCs have their own names");

var chatCfg = new Config { OneActionPerMessage = false, UserCooldown = 15 };
chatCfg.Triggers.Add(new ChatTrigger { Keyword = "boom", ActionText = "Explosion", Steps = Config.ParseSteps("Explosion") });
chatCfg.Triggers.Add(new ChatTrigger { Keyword = "dia", ActionText = "Dia", Steps = Config.ParseSteps("Dia") });
var chatEngine = new TriggerEngine(chatCfg);
DateTime now = DateTime.UtcNow;
Check(chatEngine.Process(new KickEvent { Kind = KickEventKind.Chat, User = "viewer", Text = "boom dia" }, now, out _).Count == 2,
    "multiple configured actions in one message share the pre-message user cooldown");
Check(chatEngine.Process(new KickEvent { Kind = KickEventKind.Chat, User = "viewer", Text = "boom dia" }, now.AddSeconds(1), out _).Count == 0,
    "next chat message still obeys user cooldown");

var smallCfg = new Config { MaxQueue = 2 };
PendingAction Pending(bool fromEvent) => new() { Steps = Config.ParseSteps("Npc"), FromEvent = fromEvent };
queue.Clear();
queue.Enqueue(Pending(false), smallCfg, ActionNames.Canonical, null);
queue.Enqueue(Pending(false), smallCfg, ActionNames.Canonical, null);
Check(queue.Enqueue(Pending(true), smallCfg, ActionNames.Canonical, null) == 1 && queue.Count == 2, "subscription events evict chat when the queue is full");
queue.Enqueue(Pending(true), smallCfg, ActionNames.Canonical, null);
Check(queue.Enqueue(Pending(true), smallCfg, ActionNames.Canonical, null) == 0 && queue.Count == 2, "event flood cannot exceed queue capacity");
queue.TryDequeue(100, 10);
queue.Clear();
queue.Enqueue(Pending(true), smallCfg, ActionNames.Canonical, null);
Check(queue.TryDequeue(0, 1) != null, "clearing/reloading resets queue pacing");

Check(Json.GetLong(Json.Parse("{\"id\":9007199254740993}"), "id") == 9007199254740993L, "large integer IDs retain exact precision");
foreach (string bad in new[] { "01", "1.", "1e", "1e9999", "+1", "true garbage", "\"\\q\"", "\"bad\ntext\"" })
{
    bool rejected = false;
    try { Json.Parse(bad); } catch (FormatException) { rejected = true; }
    Check(rejected, "invalid JSON rejected: " + bad);
}

var failingClient = new KickClient(new Config { ChatroomId = 1, ChannelId = 2, WebsocketUrlOverride = "ws://127.0.0.1:1/" }, _ => { });
for (int attempt = 0; attempt < 2; attempt++)
{
    failingClient.Start();
    Check(SpinWait.SpinUntil(() => failingClient.Status.StartsWith("reconectando", StringComparison.Ordinal), 3000),
        "connection failure enters retry backoff");
    var elapsed = Stopwatch.StartNew();
    failingClient.Stop();
    Check(elapsed.ElapsedMilliseconds < 3500 && !failingClient.Connected && failingClient.Status == "detenido",
        "failed-connection worker cancels promptly and can restart");
}
Console.WriteLine($"PASS: {checks} Kick parsing, recipient mapping, config, cooldown, queue and lifecycle checks.");
