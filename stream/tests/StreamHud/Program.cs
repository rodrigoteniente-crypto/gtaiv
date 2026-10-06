using KickChaos;

int checks = 0;
void Check(bool result, string description) { checks++; if (!result) throw new Exception(description); }
var snapshot = new StreamSnapshot { Active = true, UpdatedUtc = DateTime.UtcNow.ToString("o"), CameraX = 100, CameraY = 200, CameraHeading = 0, CameraFov = 60 };
Check(StreamHudPolicy.Fresh(snapshot, DateTime.UtcNow), "fresh snapshot visible");
snapshot.UpdatedUtc = DateTime.UtcNow.AddSeconds(-11).ToString("o");
Check(!StreamHudPolicy.Fresh(snapshot, DateTime.UtcNow), "stale snapshot hidden");
snapshot.UpdatedUtc = "broken";
Check(!StreamHudPolicy.Fresh(snapshot, DateTime.UtcNow), "malformed timestamp safe");
snapshot.UpdatedUtc = DateTime.UtcNow.ToString("o"); snapshot.Active = false;
Check(!StreamHudPolicy.Fresh(snapshot, DateTime.UtcNow), "inactive director hidden"); snapshot.Active = true;
Check(!StreamHudPolicy.Fresh(null, DateTime.UtcNow), "empty safe");
Check(StreamHudPolicy.Clean("A\nB\t~r~😀", 20) == "ABr", "controls game format and unsupported glyphs sanitized");
Check(StreamHudPolicy.Clean("12345678", 5) == "123..", "long name clipped");
Check(StreamHudPolicy.Clean(null, 5) == "", "null name safe");
Check(StreamHudPolicy.Clean("hello", 1).Length == 1, "narrow clipping safe");
Check(StreamHudPolicy.Matches("Cámara de suscripción", "camara suscrip"), "accent insensitive multi word search");
Check(!StreamHudPolicy.Matches("Vida follower", "vida sub"), "search requires every word");
Check(StreamHudPolicy.Matches("NPCs", ""), "clear filter restores rows");
var sub = new StreamNpcRow { Id = 4, Ped = 4, Name = "new_user", Owner = "new_user", Kind = "SUB", Health = 82, Kills = 3, Build = "AGRESIVO", RecentUntil = 30 };
var follow = new StreamNpcRow { Id = 1, Ped = 1, Name = "follow", Kind = "FOLLOW", Health = 20 };
var duplicate = new StreamNpcRow { Id = 2, Ped = 2, Name = "clone", Kind = "DUPLICADO", Health = 60 };
snapshot.Npcs.AddRange(new[] { follow, duplicate, sub, null, new StreamNpcRow { Health = 0 }, new StreamNpcRow { Health = float.NaN } });
var rows = StreamHudPolicy.Rows(snapshot, 6, 20);
Check(rows.Count == 3 && rows[0] == sub, "recent subscriber first and deadinvalid excluded");
Check(StreamHudPolicy.Rows(snapshot, 1, 20).Count == 1, "configured row cap");
snapshot.Following = true; snapshot.FollowedPed = 1;
Check(StreamHudPolicy.Rows(snapshot, 6, 20)[0] == follow, "currently observed NPC first");
Check(StreamHudPolicy.Stats(sub).Contains("HP 82  K 3  AGR"), "compact stats preserve health kills build");
Check(StreamHudPolicy.CameraStatus(sub) == "!npc LISTO", "command ready visible");
sub.CameraReadyIn = 8.2;
Check(StreamHudPolicy.CameraStatus(sub) == "!npc 9s", "cooldown rounds up");
sub.CameraReadyIn = double.NaN;
Check(StreamHudPolicy.CameraStatus(sub) == "!npc LISTO", "nonfinite cooldown safe");
sub.CameraAvailable = false;
Check(StreamHudPolicy.CameraStatus(sub) == "CAM -", "unavailable camera explicit");
sub.CameraAvailable = true;
var feed = new List<StreamFeedEntry> { new StreamFeedEntry { Kind = "sub", User = "NEW_USER", At = 20 } };
Check(StreamHudPolicy.Spotlight(snapshot, feed, 24, 8) == sub, "newsub spotlight matches owner ignorecase");
Check(StreamHudPolicy.Spotlight(snapshot, feed, 29, 8) == null, "spotlight expires without recent activity renewing");
Check(StreamHudPolicy.Spotlight(snapshot, feed, 19, 8) == null, "future feed no spotlight");
Check(StreamHudPolicy.Spotlight(snapshot, feed, 20, 0) == null, "spotlight toggle via zero duration");
feed.Add(new StreamFeedEntry { Kind = "spawnsub", User = "new_user", At = 28 });
Check(StreamHudPolicy.Spotlight(snapshot, feed, 32, 8) == sub, "spotlight starts again only when delayed model actually spawned");
Check(StreamHudPolicy.FeedLabel(new StreamFeedEntry { Kind = "sub", User = "viewer", Text = "nuevo NPC" }).StartsWith("[SUB] viewer"), "feed kind and owner");
Check(StreamHudPolicy.FeedLabel(new StreamFeedEntry { Text = new string('x', 400) }).Length <= 60, "feed clipping prevents screen overflow");
foreach (float y in new[] { 0.01f, 0.055f, 0.30f })
foreach (int requested in Enumerable.Range(1, 12))
foreach (int lines in Enumerable.Range(0, 6))
{
    int capacity = StreamHudPolicy.Capacity(y, requested, lines, true);
    Check(capacity >= 1 && capacity <= requested, "capacity bounded");
    Check(y + .035f + capacity * .052f + .024f + .016f + lines * .025f + .060f <= .941f, "HUD stays onscreen at every configurable height/row count");
}
float px, py;
Check(StreamHudPolicy.MapPoint(100, 200, snapshot, 600, out px, out py) && px == 0 && py == 0, "camera centered");
Check(StreamHudPolicy.MapPoint(100, 800, snapshot, 600, out px, out py) && py == -1, "north at top");
Check(!StreamHudPolicy.MapPoint(100, 801, snapshot, 600, out px, out py), "nearby marker range bounded");
bool near;
Check(StreamHudPolicy.MapPointClamped(100, 5000, snapshot, 600, out px, out py, out near) && !near && py == -1, "distant NPC represented at map edge");
Check(!StreamHudPolicy.MapPoint(float.NaN, 200, snapshot, 600, out px, out py), "invalid coordinate ignored");
Check(StreamHudPolicy.InCone(100, 800, snapshot), "heading0 north in FOV");
Check(!StreamHudPolicy.InCone(100, -400, snapshot), "behind camera outside FOV");
var arriving = new StreamNpcRow { X = 160, Y = 300, Heading = 90, Driving = true };
Check(StreamHudPolicy.ApproachingCone(arriving, snapshot), "driving heading projects entry into cone");
arriving.Driving = false;
Check(!StreamHudPolicy.ApproachingCone(arriving, snapshot), "stationary heading no approach claim");
snapshot.CameraHeading = 90;
Check(StreamHudPolicy.InCone(-500, 200, snapshot), "heading90 west matches GTA camera rotation");
Check(!StreamHudPolicy.InCone(700, 200, snapshot), "east outside west cone");
snapshot.CameraHeading = 180;
Check(StreamHudPolicy.InCone(100, -400, snapshot), "heading180 south");
snapshot.CameraX = float.PositiveInfinity;
Check(!StreamHudPolicy.MapPointClamped(100, 200, snapshot, 600, out px, out py, out near), "invalid camera position safe");
Console.WriteLine($"Stream HUD: {checks} checks passed (layout, search, clipping, feed and minimap projection)");
