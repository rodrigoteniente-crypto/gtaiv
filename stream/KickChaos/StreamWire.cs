using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

namespace KickChaos
{
    // This contract contains data only. No network listener or code from command files is executed.
    public static class StreamWire
    {
        public const int MaxSnapshotBytes = 1024 * 1024, MaxCommandBytes = 4096, CommandsPerTick = 4;
        static readonly HashSet<string> actions = new HashSet<string>(StringComparer.Ordinal)
        { "observe", "pin", "loop", "next", "relocate", "teleport", "build", "behavior", "kill", "remove", "debug" };
        public static string Serialize(object value) { return Json.Canonical(ToNode(value)); }
        static object ToNode(object value)
        {
            if (value == null || value is string || value is bool) return value;
            if (value is int) return (long)(int)value;
            if (value is float) return Finite((float)value) ? (double)(float)value : 0d;
            if (value is double) return Finite((double)value) ? value : 0d;
            if (value is IList values) { var list = new List<object>(); foreach (object item in values) list.Add(ToNode(item)); return list; }
            Type type = value.GetType();
            if (type != typeof(StreamSnapshot) && type != typeof(StreamNpcRow) && type != typeof(StreamFeedEntry) &&
                type != typeof(StreamMarker) && type != typeof(StreamAdminCommand)) throw new ArgumentException("Contrato desconocido");
            var map = new Dictionary<string, object>();
            foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance)) map[field.Name] = ToNode(field.GetValue(value));
            return map;
        }
        public static bool TrySnapshot(string json, out StreamSnapshot snapshot)
        {
            snapshot = null;
            if (json == null || Encoding.UTF8.GetByteCount(json) > MaxSnapshotBytes) return false;
            try { snapshot = (StreamSnapshot)FromNode(Json.Parse(json), typeof(StreamSnapshot)); return snapshot != null && snapshot.Npcs.Count <= 256; }
            catch { return false; }
        }
        static object FromNode(object node, Type type)
        {
            if (type == typeof(string)) return node as string ?? "";
            if (type == typeof(bool)) return node is bool && (bool)node;
            if (type == typeof(int))
            { if (!(node is long integer) || integer < int.MinValue || integer > int.MaxValue) throw new FormatException(); return (int)integer; }
            if (type == typeof(float)) { float n = Convert.ToSingle(node, CultureInfo.InvariantCulture); if (!Finite(n)) throw new FormatException(); return n; }
            if (type == typeof(double)) { double n = Convert.ToDouble(node, CultureInfo.InvariantCulture); if (!Finite(n)) throw new FormatException(); return n; }
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                var list = (IList)Activator.CreateInstance(type);
                if (!(node is List<object> source) || source.Count > 256) throw new FormatException();
                foreach (object item in source) list.Add(FromNode(item, type.GetGenericArguments()[0]));
                return list;
            }
            if (!(node is Dictionary<string, object> map)) throw new FormatException();
            object target = Activator.CreateInstance(type);
            foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                if (map.TryGetValue(field.Name, out object value)) field.SetValue(target, FromNode(value, field.FieldType));
            return target;
        }
        public static bool Finite(double n) { return !double.IsNaN(n) && !double.IsInfinity(n); }
        public static bool ValidPosition(float x, float y, float z)
        { return Finite(x) && Finite(y) && Finite(z) && Math.Abs(x) <= 10000 && Math.Abs(y) <= 10000 && z >= -150 && z <= 1500; }
        public static bool ValidCommandTime(DateTime writtenUtc, DateTime nowUtc)
        { TimeSpan age = nowUtc - writtenUtc; return age <= TimeSpan.FromMinutes(2) && age >= TimeSpan.FromSeconds(-10); }
        public static bool SnapshotFresh(StreamSnapshot snapshot, DateTime nowUtc)
        {
            if (snapshot == null || !DateTime.TryParse(snapshot.UpdatedUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime updated)) return false;
            TimeSpan age = nowUtc - updated.ToUniversalTime();
            return age < TimeSpan.FromSeconds(8) && age >= TimeSpan.FromSeconds(-10);
        }
        public static bool TryCommand(string json, string fileName, out StreamAdminCommand command, out string error)
        {
            command = null; error = "Comando incompleto o inválido";
            if (json == null || Encoding.UTF8.GetByteCount(json) > MaxCommandBytes) return false;
            try
            {
                command = (StreamAdminCommand)FromNode(Json.Parse(json), typeof(StreamAdminCommand));
                if (!Guid.TryParseExact(command.Id, "D", out Guid id) || !string.Equals(fileName, id.ToString("D") + ".json", StringComparison.OrdinalIgnoreCase))
                { error = "Identificador de comando inválido"; return false; }
                if (!Guid.TryParseExact(command.SessionId, "D", out _)) { error = "Sesión de juego inválida"; return false; }
                if (!actions.Contains(command.Action)) { error = "Acción desconocida"; return false; }
                if (command.Value.Length > 80 || command.Zone.Length > 80 || command.NpcId < 0 || command.NpcId > 100000000)
                { error = "Parámetros fuera de rango"; return false; }
                if (!Finite(command.Seconds) || command.Seconds < 1 || command.Seconds > 600 || !ValidPosition(command.X, command.Y, command.Z))
                { error = "Coordenadas o duración inválidas"; return false; }
                if (command.Action != "loop" && command.Action != "next" && command.NpcId == 0)
                { error = "Seleccioná un NPC vivo"; return false; }
                error = ""; return true;
            }
            catch { command = null; return false; }
        }
        public static bool TryCommandForSession(string json, string fileName, string sessionId, out StreamAdminCommand command, out string error)
        {
            if (!TryCommand(json, fileName, out command, out error)) return false;
            if (string.Equals(command.SessionId, sessionId, StringComparison.Ordinal)) return true;
            error = "El juego se recargó: este comando pertenece a otra sesión";
            return false;
        }
        public static void WriteAtomic(string path, string json)
        {
            string directory = Path.GetDirectoryName(Path.GetFullPath(path));
            Directory.CreateDirectory(directory);
            string temporary = Path.Combine(directory, "." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                File.WriteAllText(temporary, json, new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        public static string ReadBounded(string path, int maximum)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
            {
                if (stream.Length > maximum) throw new IOException("Archivo demasiado grande");
                byte[] bytes = new byte[maximum + 1];
                int count = 0, read;
                while (count <= maximum && (read = stream.Read(bytes, count, bytes.Length - count)) > 0) count += read;
                if (count > maximum) throw new IOException("Archivo demasiado grande");
                string text = new UTF8Encoding(false, true).GetString(bytes, 0, count);
                return text.Length > 0 && text[0] == '\uFEFF' ? text.Substring(1) : text;
            }
        }
        public static string Acknowledgement(string id, bool success, string message)
        { return "{\"Id\":" + Json.Quote(id) + ",\"Success\":" + (success ? "true" : "false") + ",\"Message\":" + Json.Quote(message ?? "") + "}"; }
    }
}
