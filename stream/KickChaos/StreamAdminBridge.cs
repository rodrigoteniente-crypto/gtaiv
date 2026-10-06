using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

namespace KickChaos
{
    public sealed class StreamAdminBridge
    {
        double nextSnapshot, nextCleanup, nextCommands;
        readonly string sessionId = Guid.NewGuid().ToString("D");
        readonly HashSet<string> processed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        readonly Queue<string> processedOrder = new Queue<string>();
        string lastError = "";
        public void Update(SubNpcManager npcs, Director director, Config config, string folder, string kickStatus, Action<string> log)
        {
            if (npcs == null || director == null || config == null) return;
            string panel = Path.Combine(folder, "panel");
            try
            {
                bool enabled = config.Ini.GetBool("PanelAdmin", "Activado", true);
                string commands = Path.Combine(panel, "commands"), acks = Path.Combine(panel, "acks");
                double now = G.Now;
                if (enabled && now >= nextCommands)
                {
                    nextCommands = now + 0.15;
                    Directory.CreateDirectory(commands); Directory.CreateDirectory(acks);
                    int count = 0;
                    foreach (string file in Directory.EnumerateFiles(commands, "*.json"))
                    {
                        if (++count > StreamWire.CommandsPerTick) break;
                        Process(file, acks, npcs, director, log);
                    }
                }
                if (now >= nextSnapshot)
                {
                    float interval = config.Ini.GetFloat("PanelAdmin", "IntervaloSegundos", 0.75f);
                    nextSnapshot = now + (StreamWire.Finite(interval) ? Math.Max(0.3, Math.Min(5, interval)) : 0.75);
                    Vector3 cameraPosition, cameraRotation;
                    float cameraFov;
                    director.ViewPose(out cameraPosition, out cameraRotation, out cameraFov);
                    var snapshot = new StreamSnapshot
                    {
                        SessionId = sessionId, UpdatedUtc = DateTime.UtcNow.ToString("o"), KickStatus = kickStatus ?? "", Active = director.Active,
                        Following = director.FollowingNpc, FollowedPed = director.FollowedPed,
                        CameraName = director.EditorActive ? "Editor" : director.Active && director.Current != null ? director.Current.Name : "Juego",
                        CameraX = cameraPosition.X, CameraY = cameraPosition.Y, CameraZ = cameraPosition.Z,
                        CameraHeading = cameraRotation.Z, CameraFov = cameraFov,
                        CityRemainingSeconds = director.CityRemainingSeconds,
                        Npcs = npcs.SnapshotRows(), Feed = StreamRuntime.Feed(now),
                        Debug = npcs.Describe()
                    };
                    foreach (StreamNpcRow npc in snapshot.Npcs)
                        if (npc.Racing) snapshot.Markers.Add(new StreamMarker { Kind = "carrera", Name = npc.Name, X = npc.X, Y = npc.Y, Z = npc.Z });
                    StreamRuntime.Snapshot = snapshot;
                    if (enabled) StreamWire.WriteAtomic(Path.Combine(panel, "snapshot.json"), StreamWire.Serialize(snapshot));
                }
                if (enabled && now >= nextCleanup)
                {
                    nextCleanup = now + 60;
                    int cleaned = 0;
                    foreach (string file in Directory.EnumerateFiles(acks, "*.json"))
                    {
                        if (++cleaned > 64) break;
                        if (DateTime.UtcNow - File.GetLastWriteTimeUtc(file) > TimeSpan.FromMinutes(10)) File.Delete(file);
                    }
                }
                lastError = "";
            }
            catch (Exception ex)
            {
                string error = ex.GetType().Name + ": " + ex.Message;
                if (lastError != error) { lastError = error; log("[Panel] " + error); }
            }
        }
        void Process(string file, string acks, SubNpcManager npcs, Director director, Action<string> log)
        {
            string stem = Path.GetFileNameWithoutExtension(file);
            if (!Guid.TryParseExact(stem, "D", out Guid id)) { File.Delete(file); return; }
            string key = id.ToString("D"), message = "Comando vencido", ack = Path.Combine(acks, key + ".json");
            if (processed.Contains(key) || File.Exists(ack)) { File.Delete(file); return; }
            bool success = false;
            StreamAdminCommand command;
            try
            {
                if (StreamWire.ValidCommandTime(File.GetLastWriteTimeUtc(file), DateTime.UtcNow) &&
                    StreamWire.TryCommandForSession(StreamWire.ReadBounded(file, StreamWire.MaxCommandBytes), Path.GetFileName(file), sessionId, out command, out message))
                {
                    if (command.Action == "loop") { director.ReturnToCity(); success = true; message = "Loop de ciudad"; }
                    else if (command.Action == "next") { director.Next(); success = true; message = "Próxima cámara"; }
                    else if (command.Action == "observe" || command.Action == "pin")
                    {
                        foreach (StreamNpcRow npc in npcs.SnapshotRows()) if (npc.Id == command.NpcId)
                        {
                            success = command.Action == "pin" ? director.PinNpc(npc.Ped) : director.ObserveNpc(npc.Ped, command.Seconds, "panel", 100, true);
                            message = success ? "Observando " + npc.Name : "Cámara no disponible"; break;
                        }
                    }
                    else success = npcs.AdminAction(command, out message);
                }
            }
            catch (Exception ex) { message = "No se pudo ejecutar: " + ex.Message; }
            processed.Add(key); processedOrder.Enqueue(key);
            while (processedOrder.Count > 1024) processed.Remove(processedOrder.Dequeue());
            StreamWire.WriteAtomic(ack, StreamWire.Acknowledgement(key, success, message));
            File.Delete(file);
            log("[Panel] " + message);
        }
    }
}
