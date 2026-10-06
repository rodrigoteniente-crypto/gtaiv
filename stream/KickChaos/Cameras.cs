using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text;

namespace KickChaos
{
    /// <summary>Un plano de camara.</summary>
    public class CameraShot
    {
        public string Name = "";
        public Vector3 Pos, Rot;            // Rot = (pitch, roll, yaw) en grados
        public float Fov = 25f;
        public float Duration = -1f;        // -1 = la duracion general
        public bool Active = true;          // false = no entra en el loop
        public float Weight = 1f;           // chance relativa en modo al azar (2 = sale el doble)
        public int Transition = -1;         // -1 = la general, 0 = fundido, 1 = corte directo
        public bool HasEnd;                 // movimiento lento hasta PosFinal/RotFinal
        public Vector3 EndPos, EndRot;
        public float EndFov = -1f;
        public bool HasTarget;              // punto donde ocurren las acciones
        public Vector3 Target;
        public bool HasAnchor;              // donde se esconde al jugador (para que haya trafico y peatones)
        public Vector3 Anchor;
        public int Hour = -1, Minute;       // hora forzada (opcional)
        public int Weather = -1;            // clima forzado (opcional)
        public float Sway = -1f;            // balanceo propio (-1 = el de config)
        public bool IsAuto;
        public bool IsFollow;               // camara que sigue al NPC de un suscriptor (no se guarda)
        public int FollowPed;
        public bool IsBattle;               // camara del tiroteo: encuadra a todos los que pelean (no se guarda)
    }

    public static class CameraStore
    {
        public const string FileName = "camaras.ini";

        static readonly string[] WeatherNames =
            { "extrasoleado", "soleado", "ventoso", "nublado", "lluvia", "llovizna", "niebla", "tormenta" };

        public static int ParseWeather(string v)
        {
            if (string.IsNullOrEmpty(v)) return -1;
            string n = Config.Normalize(v).Trim();
            int i;
            if (int.TryParse(n, out i) && i >= 0 && i <= 7) return i;
            for (i = 0; i < WeatherNames.Length; i++) if (WeatherNames[i] == n) return i;
            switch (n)
            {
                case "sol": case "sunny": case "despejado": return 1;
                case "extrasunny": return 0;
                case "viento": case "windy": return 2;
                case "nubes": case "cloudy": return 3;
                case "rain": return 4;
                case "drizzle": return 5;
                case "fog": case "neblina": return 6;
                case "storm": case "tormenta electrica": case "rayos": return 7;
            }
            return -1;
        }

        public static string WeatherName(int w) { return w >= 0 && w < WeatherNames.Length ? WeatherNames[w] : ""; }

        public static bool ParseTime(string v, out int h, out int m)
        {
            h = -1; m = 0;
            if (string.IsNullOrEmpty(v)) return false;
            string[] p = v.Trim().Split(':', '.', 'h');
            if (!int.TryParse(p[0].Trim(), out h) || h < 0 || h > 23) { h = -1; return false; }
            if (p.Length > 1 && p[1].Trim().Length > 0 && !int.TryParse(p[1].Trim(), out m)) m = 0;
            m = Math.Max(0, Math.Min(59, m));
            return true;
        }

        static bool V(IniSection s, string key, out Vector3 v)
        {
            float x, y, z;
            v = Vector3.Zero;
            if (!IniFile.TryVec3(s.Get(key), out x, out y, out z)) return false;
            // valores rotos (NaN, infinito, coordenadas absurdas) no sirven: la camara se ignora
            if (!Finite(x) || !Finite(y) || !Finite(z)) return false;
            v = new Vector3(x, y, z);
            return true;
        }

        public static List<CameraShot> Load(string folder, float defFov, float defDuration, List<string> warnings)
        {
            var list = new List<CameraShot>();
            var ini = IniFile.Load(Path.Combine(folder, FileName));
            foreach (var s in ini.Sections)
            {
                var c = new CameraShot { Name = s.Name, Fov = defFov, Duration = -1f };
                if (!V(s, "Pos", out c.Pos)) { warnings.Add("Camara '" + s.Name + "' sin Pos valida, se ignora"); continue; }
                Vector3 look;
                if (V(s, "MirarA", out look)) c.Rot = MathX.LookRotation(c.Pos, look);
                else if (!V(s, "Rot", out c.Rot)) { warnings.Add("Camara '" + s.Name + "' sin Rot ni MirarA, se ignora"); continue; }

                float f;
                if (IniFile.TryFloat(s.Get("FOV", ""), out f) && Finite(f)) c.Fov = Clamp(f, 3f, 120f);
                if (IniFile.TryFloat(s.Get("Duracion", ""), out f) && Finite(f) && f > 0) c.Duration = Math.Min(3600f, Math.Max(3f, f));
                c.Active = IniFile.ParseBool(s.Get("Activa", "si"), true);
                if (IniFile.TryFloat(s.Get("Chance", ""), out f) && f > 0) c.Weight = Math.Min(100f, f);
                string tr = Config.Normalize(s.Get("Transicion", ""));
                c.Transition = tr.StartsWith("fund") ? 0 : tr.StartsWith("corte") ? 1 : -1;
                if (IniFile.TryFloat(s.Get("Balanceo", ""), out f)) c.Sway = Math.Max(0f, f);

                if (V(s, "PosFinal", out c.EndPos))
                {
                    c.HasEnd = true;
                    Vector3 lookEnd;
                    if (V(s, "MirarAFinal", out lookEnd)) c.EndRot = MathX.LookRotation(c.EndPos, lookEnd);
                    else if (!V(s, "RotFinal", out c.EndRot)) c.EndRot = c.Rot;
                }
                else if (V(s, "RotFinal", out c.EndRot)) { c.HasEnd = true; c.EndPos = c.Pos; }
                if (IniFile.TryFloat(s.Get("FOVFinal", ""), out f)) { c.EndFov = Clamp(f, 3f, 120f); if (!c.HasEnd) { c.HasEnd = true; c.EndPos = c.Pos; c.EndRot = c.Rot; } }

                c.HasTarget = V(s, "Objetivo", out c.Target);
                c.HasAnchor = V(s, "Ancla", out c.Anchor);
                ParseTime(s.Get("Hora", ""), out c.Hour, out c.Minute);
                c.Weather = ParseWeather(s.Get("Clima", ""));
                list.Add(c);
            }
            return list;
        }

        static float Clamp(float v, float a, float b) { return v < a ? a : (v > b ? b : v); }

        static bool Finite(float f) { return !float.IsNaN(f) && !float.IsInfinity(f) && Math.Abs(f) < 1e6f; }

        public static string Serialize(CameraShot c)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[" + c.Name + "]");
            sb.AppendLine("Pos = " + IniFile.V3(c.Pos.X, c.Pos.Y, c.Pos.Z));
            sb.AppendLine("Rot = " + IniFile.V3(c.Rot.X, c.Rot.Y, c.Rot.Z));
            sb.AppendLine("FOV = " + IniFile.F(c.Fov));
            if (c.Duration > 0) sb.AppendLine("Duracion = " + IniFile.F(c.Duration));
            if (!c.Active) sb.AppendLine("Activa = no");
            if (Math.Abs(c.Weight - 1f) > 0.001f) sb.AppendLine("Chance = " + IniFile.F(c.Weight));
            if (c.Transition == 0) sb.AppendLine("Transicion = fundido");
            if (c.Transition == 1) sb.AppendLine("Transicion = corte");
            if (c.HasEnd)
            {
                sb.AppendLine("PosFinal = " + IniFile.V3(c.EndPos.X, c.EndPos.Y, c.EndPos.Z));
                sb.AppendLine("RotFinal = " + IniFile.V3(c.EndRot.X, c.EndRot.Y, c.EndRot.Z));
                if (c.EndFov > 0) sb.AppendLine("FOVFinal = " + IniFile.F(c.EndFov));
            }
            if (c.HasTarget) sb.AppendLine("Objetivo = " + IniFile.V3(c.Target.X, c.Target.Y, c.Target.Z));
            if (c.HasAnchor) sb.AppendLine("Ancla = " + IniFile.V3(c.Anchor.X, c.Anchor.Y, c.Anchor.Z));
            if (c.Hour >= 0) sb.AppendLine("Hora = " + c.Hour.ToString("00") + ":" + c.Minute.ToString("00"));
            if (c.Weather >= 0) sb.AppendLine("Clima = " + WeatherName(c.Weather));
            if (c.Sway >= 0) sb.AppendLine("Balanceo = " + IniFile.F(c.Sway));
            return sb.ToString();
        }

        /// <summary>Reescribe el archivo completo conservando el comentario de cabecera.</summary>
        public static void Save(string folder, List<CameraShot> shots)
        {
            string path = Path.Combine(folder, FileName);
            string header = File.Exists(path) ? IniFile.Load(path).HeaderComment : "";
            var sb = new StringBuilder(header.TrimEnd());
            if (sb.Length > 0) sb.AppendLine().AppendLine();
            foreach (var c in shots)
            {
                if (c.IsAuto) continue;
                sb.Append(Serialize(c)).AppendLine();
            }
            // seguro (de la 1.6.3 de GPT): escribir a un temporal y reemplazar de una sola vez, con copia .bak.
            // Si algo falla a mitad de camino, camaras.ini queda como estaba.
            Directory.CreateDirectory(folder);
            string tmp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(tmp, sb.ToString(), new UTF8Encoding(false));
                if (File.Exists(path))
                {
                    try { File.Replace(tmp, path, path + ".bak"); }
                    catch (IOException) { File.Copy(path, path + ".bak", true); File.Copy(tmp, path, true); } // discos que no soportan Replace
                }
                else File.Move(tmp, path);
            }
            finally
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            }
        }

        /// <summary>
        /// Importa las camaras guardadas con Liberty's Legacy (GTAIV\Liberty's Legacy\Cameras\*.ini).
        /// Devuelve cuantas agrego (no repite nombres que ya existen).
        /// </summary>
        public static int ImportLibertysLegacy(string gameFolder, List<CameraShot> shots, List<string> log)
        {
            string dir = Path.Combine(Path.Combine(gameFolder, "Liberty's Legacy"), "Cameras");
            if (!Directory.Exists(dir)) return 0;
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in shots) names.Add(s.Name);
            int added = 0;
            var files = Directory.GetFiles(dir, "*.ini");
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            foreach (string file in files)
            {
                string name = Path.GetFileNameWithoutExtension(file);
                if (names.Contains(name)) continue;
                var ini = IniFile.Load(file);
                float x = ini.GetFloat("camera_data", "X", float.NaN), y = ini.GetFloat("camera_data", "Y", float.NaN), z = ini.GetFloat("camera_data", "Z", float.NaN);
                if (float.IsNaN(x) || float.IsNaN(y) || float.IsNaN(z)) { if (log != null) log.Add("sin posicion: " + name); continue; }
                var c = new CameraShot
                {
                    Name = name,
                    Pos = new Vector3(x, y, z),
                    Rot = new Vector3(ini.GetFloat("camera_data", "rotX", 0f), ini.GetFloat("camera_data", "rotY", 0f), ini.GetFloat("camera_data", "rotZ", 0f)),
                    Fov = ini.GetFloat("camera_data", "FOV", 25f)
                };
                int h = ini.GetInt("camera_data", "Hour", -1);
                if (h >= 0 && h <= 23) { c.Hour = h; c.Minute = Math.Max(0, Math.Min(59, ini.GetInt("camera_data", "Minute", 0))); }
                int w = ini.GetInt("camera_data", "Weather", -1);
                if (w >= 0 && w <= 7) c.Weather = w;
                shots.Add(c);
                names.Add(name);
                added++;
            }
            return added;
        }

        public static string NextName(List<CameraShot> shots)
        {
            int n = 1;
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in shots) names.Add(s.Name);
            while (names.Contains("Camara " + n)) n++;
            return "Camara " + n;
        }
    }
}
