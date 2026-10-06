using System;
using System.Collections.Generic;
using System.IO;
using KickChaos;

int checks = 0;
void Check(bool pass, string name) { if (!pass) throw new Exception(name); checks++; }
string id = Guid.NewGuid().ToString("D"), file = id + ".json";
string sessionId = Guid.NewGuid().ToString("D");
var command = new StreamAdminCommand { SessionId = sessionId, Id = id, Action = "teleport", NpcId = 4, X = 100, Y = -400, Z = 15, Seconds = 15 };
string encoded = StreamWire.Serialize(command);
Check(StreamWire.TryCommand(encoded, file, out var decoded, out _), "command round trip");
Check(decoded.NpcId == 4 && decoded.Y == -400, "command values");
Check(StreamWire.TryCommandForSession(encoded, file, sessionId, out _, out _), "current session accepted");
Check(!StreamWire.TryCommandForSession(encoded, file, Guid.NewGuid().ToString("D"), out _, out _), "old command cannot affect reused NPC ID after reload");
Check(!StreamWire.TryCommand(encoded.Replace(sessionId, ""), file, out _, out _), "missing session rejected");
Check(!StreamWire.TryCommand(encoded.Substring(0, encoded.Length - 1), file, out _, out _), "partial command rejected");
Check(!StreamWire.TryCommand(encoded, "../../" + file, out _, out _), "path traversal filename rejected");
Check(!StreamWire.TryCommand(encoded, Guid.NewGuid().ToString("D") + ".json", out _, out _), "UUID mismatch rejected");
command.Action = "execute";
Check(!StreamWire.TryCommand(StreamWire.Serialize(command), file, out _, out _), "unknown action rejected");
command.Action = "observe"; command.NpcId = 0;
Check(!StreamWire.TryCommand(StreamWire.Serialize(command), file, out _, out _), "missing NPC rejected");
command.Action = "loop";
Check(StreamWire.TryCommand(StreamWire.Serialize(command), file, out _, out _), "loop no NPC accepted");
command.Action = "teleport"; command.NpcId = 4; command.X = 20000;
Check(!StreamWire.TryCommand(StreamWire.Serialize(command), file, out _, out _), "off-map position rejected");
command.X = 100; command.Seconds = -1;
Check(!StreamWire.TryCommand(StreamWire.Serialize(command), file, out _, out _), "negative duration rejected");
command.Seconds = 601;
Check(!StreamWire.TryCommand(StreamWire.Serialize(command), file, out _, out _), "huge duration rejected");
command.Seconds = 15; command.Value = new string('a', 81);
Check(!StreamWire.TryCommand(StreamWire.Serialize(command), file, out _, out _), "oversized value rejected");
command.Value = "";
Check(!StreamWire.TryCommand(StreamWire.Serialize(command).Replace("\"NpcId\":4", "\"NpcId\":4.5"), file, out _, out _), "fractional ID rejected");
Check(!StreamWire.TryCommand(StreamWire.Serialize(command).Replace("\"X\":100", "\"X\":1e999"), file, out _, out _), "nonfinite input rejected");
Check(!StreamWire.TryCommand(new string('x', 4097), file, out _, out _), "command bytes bounded");
Check(!StreamWire.ValidPosition(float.NaN, 0, 0), "NaN position rejected");
Check(!StreamWire.ValidPosition(0, float.PositiveInfinity, 0), "infinite position rejected");
DateTime now = DateTime.UtcNow;
Check(StreamWire.ValidCommandTime(now, now), "fresh command");
Check(!StreamWire.ValidCommandTime(now.AddMinutes(-3), now), "stale command rejected");
Check(!StreamWire.ValidCommandTime(now.AddMinutes(1), now), "future command rejected");
var snapshot = new StreamSnapshot { SessionId = sessionId, UpdatedUtc = now.ToString("o"), CameraX = 100, CameraHeading = 140, CameraFov = 35, CityRemainingSeconds = 234,
    Npcs = new List<StreamNpcRow> { new StreamNpcRow { Id = 4, Name = "sub \"hola\" ñ 🎮", Kind = "DUPLICADO", Health = 82.5f, Kills = 3, ParentId = 1, Characters = 2,
    InCar = true, CameraReadyIn = 7, X = 50, Y = -500, Z = 14 } }, Feed = new List<StreamFeedEntry> { new StreamFeedEntry { Text = "suscripción", Kind = "SUB", User = "Viewer" } },
    Markers = new List<StreamMarker> { new StreamMarker { Kind = "carrera", X = 18, Y = 12, Z = 14 } } };
Check(StreamWire.TrySnapshot(StreamWire.Serialize(snapshot), out var recovered), "snapshot round trip");
Check(StreamWire.SnapshotFresh(snapshot, now), "fresh snapshot permits commands");
Check(!StreamWire.SnapshotFresh(snapshot, now.AddSeconds(9)), "paused or stopped game disables commands");
Check(!StreamWire.SnapshotFresh(snapshot, now.AddMinutes(-1)), "future snapshot timestamp rejected");
Check(recovered.Npcs[0].Name == snapshot.Npcs[0].Name, "unicode and quoted name");
Check(recovered.SessionId == sessionId, "snapshot carries session identity");
Check(recovered.Npcs[0].Health == 82.5f && recovered.CityRemainingSeconds == 234 && recovered.Markers.Count == 1, "snapshot data intact");
Check(!StreamWire.TrySnapshot("{}x", out _), "malformed snapshot rejected");
Check(!StreamWire.TrySnapshot("{\"Npcs\":null}", out _), "null NPCs rejected");
for (int n = 0; n < 257; n++) snapshot.Npcs.Add(new StreamNpcRow());
Check(!StreamWire.TrySnapshot(StreamWire.Serialize(snapshot), out _), "snapshot row count bounded");
string dir = Path.Combine(Path.GetTempPath(), "kickchaos-panel-test-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(dir);
try
{
    string target = Path.Combine(dir, "snapshot.json");
    StreamWire.WriteAtomic(target, "old"); StreamWire.WriteAtomic(target, "new");
    Check(StreamWire.ReadBounded(target, 4) == "new", "atomic replacement works");
    Check(Directory.GetFiles(dir, "*.tmp").Length == 0, "temporary files removed");
    File.WriteAllText(target, "12345");
    bool threw = false; try { StreamWire.ReadBounded(target, 4); } catch (IOException) { threw = true; }
    Check(threw, "read byte bound enforced");
    string incomplete = Path.Combine(dir, ".unfinished.tmp"); File.WriteAllText(incomplete, "{\"Id\":");
    Check(Directory.GetFiles(dir, "*.json").Length == 1, "partial writes never counted as commands");
    Check(StreamWire.CommandsPerTick == 4, "game tick command budget");
    var acknowledgement = Json.ParseObject(StreamWire.Acknowledgement(id, false, "NPC \"muerto\""));
    Check(Json.GetString(acknowledgement, "Message") == "NPC \"muerto\"", "ack escaped correctly");
}
finally { Directory.Delete(dir, true); }
Console.WriteLine("PASS StreamAdmin: " + checks + " checks");
