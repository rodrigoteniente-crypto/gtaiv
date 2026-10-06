using System.Numerics;
using KickChaos;

int checks = 0;
void Check(bool pass, string why) { checks++; if (!pass) throw new Exception(why); }
string fixtures = Path.Combine(AppContext.BaseDirectory, "fixtures");
var warnings = new List<string>();
var shots = CameraStore.Load(fixtures, 25, 300, warnings);
Check(shots.Count == 33, "all user cameras from the earlier backup still load");
Check(warnings.Count == 0, "existing cameras load without warning");
string folder = Path.Combine(Path.GetTempPath(), "kick-stream-persist-" + Guid.NewGuid());
Directory.CreateDirectory(folder);
try
{
    File.WriteAllText(Path.Combine(folder, CameraStore.FileName), "; user camera header\n");
    CameraStore.Save(folder, shots);
    var loaded = CameraStore.Load(folder, 25, 300, warnings);
    Check(loaded.Count == shots.Count, "camera count survives save/load");
    for (int i = 0; i < shots.Count; i++)
    {
        Check(loaded[i].Name == shots[i].Name, "name preserved");
        Check(Vector3.Distance(loaded[i].Pos, shots[i].Pos) < 0.001, "position preserved");
        Check(Vector3.Distance(loaded[i].Rot, shots[i].Rot) < 0.001, "rotation preserved");
        Check(Math.Abs(loaded[i].Fov - shots[i].Fov) < 0.001, "custom FOV preserved");
        Check(loaded[i].Duration == shots[i].Duration, "saved per-shot duration preserved");
    }
    string path = Path.Combine(folder, CameraStore.FileName);
    string previous = File.ReadAllText(path);
    loaded[0].Fov = 43; CameraStore.Save(folder, loaded);
    Check(File.ReadAllText(path + ".bak") == previous, "previous complete file backed up");
    Check(File.ReadAllText(path).StartsWith("; user camera header"), "user header retained");
    Check(CameraStore.Load(folder, 25, 300, warnings)[0].Fov == 43, "new FOV installed");
    string persisted = File.ReadAllText(path);
    File.Delete(path + ".bak"); Directory.CreateDirectory(path + ".bak");
    loaded[0].Fov = 64; bool failed = false;
    try { CameraStore.Save(folder, loaded); }
    catch (IOException) { failed = true; }
    catch (UnauthorizedAccessException) { failed = true; }
    Check(failed && File.ReadAllText(path) == persisted, "failed backup does not destroy live cameras");
    Check(Directory.GetFiles(folder, "*.tmp").Length == 0, "failed save cleans temporary files");
    File.WriteAllText(path, "[broken]\nPos=NaN,0,0\nRot=0,0,0\n[good]\nPos=1,2,3\nRot=0,0,0\nFOV=900\nDuracion=Infinity\n");
    loaded = CameraStore.Load(folder, 25, 300, warnings);
    Check(loaded.Count == 1 && loaded[0].Fov == 120 && loaded[0].Duration == -1, "malformed camera skipped; FOV bounded and duration valid");
}
finally { Directory.Delete(folder, true); }
var rank = new KillRanking(); rank.Kill("Ana", KillRanking.Cop); rank.Kill("Ana", KillRanking.Npc); rank.Death("Ana"); rank.Level("Ana", 4);
var restored = KillRanking.Parse(rank.Serialize()).Get("ana");
Check(restored.Kills == 2 && restored.Cops == 1 && restored.Npcs == 1 && restored.Deaths == 1 && restored.Level == 4, "ranking survives roundtrip and casing");
var config = Config.Load(fixtures, new List<string>());
Check(config.KeyHud == System.Windows.Forms.Keys.F1 && config.KeyFollow == System.Windows.Forms.Keys.F4, "profile shortcuts load");
Check(config.MaxQueue == 64 && config.MaxRepeat == 100 && config.GiftNpcForRecipients, "profile supports bursts and recipient-owned NPCs");
foreach (string kind in new[] { "Suscripcion", "RegaloSubs", "Follow" })
    Check(config.EventMap[kind].Count == 1 && config.EventMap[kind][0].Name == "Npc", "profile event creates a character without automatic chaos");
Check(config.EventMap["KicksGifted"][0].Name == "Nada", "donations only request the camera by default");
Check(config.KickDelaySeconds == 10, "profile preserves alert delay");
Check(config.Ini.GetInt("Suscriptor", "Maximo", 0) >= 25 && config.Ini.GetInt("Suscriptor", "MaximoPersonajes", 0) == 48, "profile accommodates expected stream audience");
Check(!config.Ini.GetBool("Suscriptor", "PoliciaAgresiva", true) && !config.Ini.GetBool("Suscriptor", "PoliciaDisfrazada", true), "profile defaults to real police and native combat");
Check(config.Ini.GetFloat("CamaraStream", "DuracionCiudadSegundos", 0) == 300 && config.Ini.GetBool("CamaraStream", "UsarDuracionGlobal", false), "saved short durations do not shorten the stream cycle");
Check(!config.Ini.GetBool("CamaraStream", "SeguirFollows", true), "follows do not always seize the camera");
Check(config.Ini.GetFloat("Suscriptor", "VelocidadCarrera", 0) == 32 && config.Ini.GetFloat("Suscriptor", "ProbabilidadCarrera", 0) == 15, "quiet race settings actually appear in shipped profile");
Console.WriteLine($"PASS StreamPersistence: {checks} checks (user cameras, failed-save protection, ranking and shipped profile)");
