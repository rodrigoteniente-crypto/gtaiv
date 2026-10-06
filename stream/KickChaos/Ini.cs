using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace KickChaos
{
    /// <summary>Una seccion [Nombre] de un .ini, con sus claves en el orden del archivo.</summary>
    public class IniSection
    {
        public string Name;
        public readonly List<KeyValuePair<string, string>> Entries = new List<KeyValuePair<string, string>>();

        public IniSection(string name) { Name = name; }

        public string Get(string key, string def = null)
        {
            // Si una clave aparece repetida gana la ultima.
            for (int i = Entries.Count - 1; i >= 0; i--)
                if (string.Equals(Entries[i].Key, key, StringComparison.OrdinalIgnoreCase))
                    return Entries[i].Value;
            return def;
        }

        public bool Has(string key) { return Get(key) != null; }
    }

    /// <summary>
    /// Lector de .ini simple y tolerante: comentarios con ; o #, claves sin distinguir mayusculas,
    /// secciones repetidas permitidas (se usan para la lista de camaras).
    /// </summary>
    public class IniFile
    {
        public readonly List<IniSection> Sections = new List<IniSection>();
        public string HeaderComment = "";

        public static IniFile Load(string path)
        {
            var ini = new IniFile();
            if (!File.Exists(path)) return ini;
            var header = new StringBuilder();
            IniSection cur = null;
            foreach (string raw in File.ReadAllLines(path, Encoding.UTF8))
            {
                string line = raw.Trim();
                if (line.Length > 0 && line[0] == '﻿') line = line.Substring(1).Trim();
                if (line.Length == 0 || line[0] == ';' || line[0] == '#')
                {
                    if (cur == null) header.AppendLine(raw);
                    continue;
                }
                if (line[0] == '[' && line.EndsWith("]"))
                {
                    cur = new IniSection(line.Substring(1, line.Length - 2).Trim());
                    ini.Sections.Add(cur);
                    continue;
                }
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                if (cur == null) { cur = new IniSection(""); ini.Sections.Add(cur); }
                string key = line.Substring(0, eq).Trim();
                string val = StripInlineComment(line.Substring(eq + 1)).Trim();
                cur.Entries.Add(new KeyValuePair<string, string>(key, val));
            }
            ini.HeaderComment = header.ToString();
            return ini;
        }

        /// <summary>Corta comentarios al final de la linea: "valor   ; comentario".</summary>
        public static string StripInlineComment(string v)
        {
            for (int k = 1; k < v.Length; k++)
                if ((v[k] == ';' || v[k] == '#') && (v[k - 1] == ' ' || v[k - 1] == '\t'))
                    return v.Substring(0, k);
            return v;
        }

        public IniSection Section(string name)
        {
            foreach (var s in Sections)
                if (string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)) return s;
            return null;
        }

        public string Get(string section, string key, string def = null)
        {
            var s = Section(section);
            if (s == null) return def;
            string v = s.Get(key);
            return string.IsNullOrEmpty(v) ? def : v;
        }

        public int GetInt(string section, string key, int def)
        {
            int r;
            string v = Get(section, key);
            if (v != null && int.TryParse(v.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out r)) return r;
            return def;
        }

        public float GetFloat(string section, string key, float def)
        {
            float r;
            string v = Get(section, key);
            if (v != null && TryFloat(v, out r)) return r;
            return def;
        }

        public bool GetBool(string section, string key, bool def)
        {
            string v = Get(section, key);
            if (v == null) return def;
            return ParseBool(v, def);
        }

        public static bool ParseBool(string v, bool def)
        {
            switch (v.Trim().ToLowerInvariant())
            {
                case "1": case "true": case "si": case "sí": case "yes": case "on": case "verdadero": return true;
                case "0": case "false": case "no": case "off": case "falso": return false;
                default: return def;
            }
        }

        public static bool TryFloat(string v, out float r)
        {
            return float.TryParse(v.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out r);
        }

        /// <summary>Lee "x, y, z" (acepta tambien espacios). Devuelve false si no hay 3 numeros.</summary>
        public static bool TryVec3(string v, out float x, out float y, out float z)
        {
            x = y = z = 0f;
            if (string.IsNullOrEmpty(v)) return false;
            string[] p = v.Split(new[] { ',', ' ', ';', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (p.Length < 3) return false;
            return TryFloat(p[0], out x) && TryFloat(p[1], out y) && TryFloat(p[2], out z);
        }

        public static string F(float v) { return v.ToString("0.###", CultureInfo.InvariantCulture); }
        public static string V3(float x, float y, float z) { return F(x) + ", " + F(y) + ", " + F(z); }
    }
}

namespace KickChaos
{
    /// <summary>
    /// Editor de .ini que respeta el archivo tal cual (comentarios, orden, espacios):
    /// solo cambia la linea de la clave que se modifica. Lo usa el menu para guardar.
    /// </summary>
    public class IniDoc
    {
        readonly List<string> lines = new List<string>();
        readonly string path;

        IniDoc(string path) { this.path = path; }

        public static IniDoc Load(string path)
        {
            var d = new IniDoc(path);
            if (File.Exists(path))
                foreach (string l in File.ReadAllLines(path, Encoding.UTF8)) d.lines.Add(l.TrimStart('﻿'));
            return d;
        }

        /// <summary>Guarda en un archivo temporal y lo reemplaza (si algo falla, el original queda bien).</summary>
        public void Save()
        {
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, string.Join("\r\n", lines.ToArray()) + "\r\n", new UTF8Encoding(false));
            try
            {
                if (File.Exists(path)) File.Replace(tmp, path, null);
                else File.Move(tmp, path);
            }
            catch
            {
                File.Copy(tmp, path, true);
                try { File.Delete(tmp); } catch { }
            }
        }

        static bool IsSection(string l, out string name)
        {
            string t = l.Trim();
            name = null;
            if (t.StartsWith("[") && t.EndsWith("]")) { name = t.Substring(1, t.Length - 2).Trim(); return true; }
            return false;
        }

        static bool IsKey(string l, string key)
        {
            string t = l.Trim();
            if (t.Length == 0 || t[0] == ';' || t[0] == '#' || t[0] == '[') return false;
            int eq = t.IndexOf('=');
            if (eq <= 0) return false;
            return string.Equals(t.Substring(0, eq).Trim(), key, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Indices [inicio, fin) de las lineas de una seccion (sin el encabezado). -1 si no existe.</summary>
        void SectionRange(string section, out int header, out int end)
        {
            header = -1; end = lines.Count;
            for (int i = 0; i < lines.Count; i++)
            {
                string name;
                if (!IsSection(lines[i], out name)) continue;
                if (header >= 0) { end = i; return; }
                if (string.Equals(name, section, StringComparison.OrdinalIgnoreCase)) header = i;
            }
        }

        public void Set(string section, string key, string value)
        {
            int header, end;
            SectionRange(section, out header, out end);
            if (header < 0)
            {
                if (lines.Count > 0 && lines[lines.Count - 1].Trim().Length > 0) lines.Add("");
                lines.Add("[" + section + "]");
                lines.Add(key + " = " + value);
                return;
            }
            for (int i = end - 1; i > header; i--) // la ultima aparicion es la que vale al leer
            {
                if (!IsKey(lines[i], key)) continue;
                string l = lines[i];
                int eq = l.IndexOf('=');
                string after = l.Substring(eq + 1);
                string stripped = IniFile.StripInlineComment(after);
                string comment = after.Substring(stripped.Length);
                string lead = after.Length - after.TrimStart().Length > 0 ? " " : " ";
                string newVal = lead + value;
                if (comment.Length > 0)
                {
                    // mantener el comentario alineado como estaba
                    int pad = Math.Max(1, stripped.Length - newVal.Length);
                    newVal += new string(' ', pad);
                }
                lines[i] = l.Substring(0, eq + 1) + newVal + comment.TrimEnd();
                return;
            }
            // no estaba: agregar despues de la ultima clave de la seccion
            int insert = header + 1;
            for (int i = header + 1; i < end; i++)
            {
                string t = lines[i].Trim();
                if (t.Length > 0 && t[0] != ';' && t[0] != '#') insert = i + 1;
            }
            lines.Insert(insert, key + " = " + value);
        }

        public void Remove(string section, string key)
        {
            int header, end;
            SectionRange(section, out header, out end);
            if (header < 0) return;
            for (int i = end - 1; i > header; i--)
                if (IsKey(lines[i], key)) lines.RemoveAt(i);
        }

        public void RenameKey(string section, string oldKey, string newKey)
        {
            int header, end;
            SectionRange(section, out header, out end);
            if (header < 0) return;
            for (int i = header + 1; i < end; i++)
            {
                if (!IsKey(lines[i], oldKey)) continue;
                int eq = lines[i].IndexOf('=');
                lines[i] = newKey + " " + lines[i].Substring(eq);
            }
        }
    }
}
