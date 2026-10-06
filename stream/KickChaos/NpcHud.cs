using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace KickChaos
{
    /// <summary>Kills de un suscriptor (para el ranking).</summary>
    public class KillStat
    {
        public string Name = "";
        public int Kills, Cops, Npcs, Civilians, Deaths, Level = 1;
    }

    /// <summary>
    /// Ranking de kills de los NPC de suscriptores. Se guarda en ranking.ini (sigue de un stream al otro).
    /// No depende del juego (se prueba fuera de el).
    /// </summary>
    public class KillRanking
    {
        public const int Civilian = 0, Cop = 1, Npc = 2;
        readonly Dictionary<string, KillStat> map = new Dictionary<string, KillStat>(StringComparer.OrdinalIgnoreCase);
        public bool Dirty;
        public int Count { get { return map.Count; } }

        public KillStat Get(string name)
        {
            string n = Clean(name);
            KillStat s;
            if (!map.TryGetValue(n, out s)) { s = new KillStat { Name = n }; map[n] = s; }
            return s;
        }

        static string Clean(string name)
        {
            string n = (name ?? "").Trim().Replace("=", "").Replace(";", "").Replace("[", "").Replace("]", "");
            return n.Length == 0 ? "?" : n;
        }

        public void Kill(string name, int kind)
        {
            KillStat s = Get(name);
            s.Kills++;
            if (kind == Cop) s.Cops++;
            else if (kind == Npc) s.Npcs++;
            else s.Civilians++;
            Dirty = true;
        }

        public void Death(string name) { Get(name).Deaths++; Dirty = true; }

        public void Level(string name, int level)
        {
            KillStat s = Get(name);
            if (level > s.Level) { s.Level = level; Dirty = true; }
        }

        public void Clear() { map.Clear(); Dirty = true; }

        /// <summary>Los mejores: mas kills, despues mas NPC, mas policias, menos muertes.</summary>
        public List<KillStat> Top(int n)
        {
            var l = new List<KillStat>(map.Values);
            l.RemoveAll(x => x.Kills == 0 && x.Deaths == 0);
            l.Sort((a, b) =>
            {
                if (a.Kills != b.Kills) return b.Kills.CompareTo(a.Kills);
                if (a.Npcs != b.Npcs) return b.Npcs.CompareTo(a.Npcs);
                if (a.Cops != b.Cops) return b.Cops.CompareTo(a.Cops);
                if (a.Deaths != b.Deaths) return a.Deaths.CompareTo(b.Deaths);
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
            if (l.Count > n) l.RemoveRange(n, l.Count - n);
            return l;
        }

        /// <summary>ranking.ini: usuario = kills, policias, npc, civiles, muertes, nivel</summary>
        public string Serialize()
        {
            var sb = new StringBuilder();
            sb.Append("; Ranking de kills de los NPC de suscriptores (KickChaos). Se borra desde el menu.\r\n");
            sb.Append("; usuario = kills, policias, npc, civiles, muertes, mejor nivel de banda\r\n");
            sb.Append("[Ranking]\r\n");
            foreach (var s in Top(int.MaxValue))
                sb.Append(s.Name).Append(" = ").Append(s.Kills).Append(',').Append(s.Cops).Append(',').Append(s.Npcs).Append(',')
                  .Append(s.Civilians).Append(',').Append(s.Deaths).Append(',').Append(s.Level).Append("\r\n");
            return sb.ToString();
        }

        public static KillRanking Parse(string text)
        {
            var r = new KillRanking();
            if (string.IsNullOrEmpty(text)) return r;
            foreach (string raw in text.Replace("\r", "").Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("[")) continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string name = line.Substring(0, eq).Trim();
                string[] v = line.Substring(eq + 1).Split(',');
                Func<int, int> num = i =>
                {
                    int x;
                    return i < v.Length && int.TryParse(v[i].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out x) ? Math.Max(0, x) : 0;
                };
                KillStat s = r.Get(name);
                s.Kills = num(0); s.Cops = num(1); s.Npcs = num(2); s.Civilians = num(3); s.Deaths = num(4); s.Level = Math.Max(1, num(5));
            }
            r.Dirty = false;
            return r;
        }

        /// <summary>Texto para OBS (ranking.txt).</summary>
        public string ObsText(int n)
        {
            var sb = new StringBuilder();
            int i = 1;
            foreach (var s in Top(n))
                sb.Append(i++).Append(". ").Append(s.Name).Append(" - ").Append(s.Kills).Append(s.Kills == 1 ? " kill" : " kills").Append("\r\n");
            return sb.ToString();
        }

        /// <summary>"12 (4 polis, 3 npc)"</summary>
        public static string Detail(KillStat s)
        {
            var parts = new List<string>();
            if (s.Cops > 0) parts.Add(s.Cops + (s.Cops == 1 ? " poli" : " polis"));
            if (s.Npcs > 0) parts.Add(s.Npcs + " npc");
            if (s.Level > 1) parts.Add("nv " + s.Level);
            return parts.Count == 0 ? "" : string.Join(", ", parts.ToArray());
        }
    }

    /// <summary>Una linea del kill feed: "rodsquare  >  POLICIA" (o un aviso, si B esta vacio).</summary>
    public class FeedLine
    {
        public string A = "", B = "";
        public int ColorA, ColorB;   // 0 blanco, 1 verde Kick (sub), 2 azul policia, 3 amarillo (aviso), 4 rojo (muerte)
        public double At;
    }

    public static class GangLevels
    {
        /// <summary>Nivel de la banda segun cuantos NPC de otras bandas mato (1 + uno por cada kill, hasta 10).</summary>
        public static int FromNpcKills(int npcKills) { return Math.Max(1, Math.Min(10, 1 + npcKills)); }

        // ------------------------------------------------------------------
        // Armas (las que se pueden elegir en el menu). Numeros de arma de GTA IV.
        // ------------------------------------------------------------------
        public const int Grenade = 4, Molotov = 5, Rpg = 18;
        public static readonly string[] WeaponKeys =
            { "Pistola", "Deagle", "MicroUzi", "MP5", "Escopeta", "EscopetaCombate", "AK47", "M4", "Francotirador", "RPG", "Granadas", "Molotov", "Bate", "Cuchillo", "AMano" };
        public static readonly int[] WeaponIds = { 7, 9, 12, 13, 10, 11, 14, 15, 16, 18, 4, 5, 1, 3, 0 };
        public static readonly string[] WeaponLabels =
            { "Pistola", "Desert Eagle", "Micro Uzi", "MP5", "Escopeta", "Escopeta de combate", "AK47", "M4", "Rifle de francotirador", "RPG", "Granadas", "Molotov", "Bate", "Cuchillo", "A mano" };

        /// <summary>"ak", "AK47", "m4", "granada", "rpg"... -> numero de arma. -1 si no se entiende.</summary>
        public static int ParseWeapon(string s)
        {
            string n = Config.Normalize(s ?? "").Replace(" ", "").Replace("-", "").Replace("_", "").Replace(".", "");
            if (n.Length == 0) return -1;
            for (int i = 0; i < WeaponKeys.Length; i++)
                if (Config.Normalize(WeaponKeys[i]) == n) return WeaponIds[i];
            if (n.StartsWith("pisto") || n == "glock") return 7;
            if (n.StartsWith("deag") || n.StartsWith("desert")) return 9;
            if (n.StartsWith("uzi") || n.StartsWith("micro")) return 12;
            if (n.StartsWith("mp5") || n == "smg") return 13;
            if (n.StartsWith("escopetacomb") || n.StartsWith("combate") || n.StartsWith("baretta") || n.StartsWith("beretta")) return 11;
            if (n.StartsWith("escop") || n.StartsWith("shotgun")) return 10;
            if (n.StartsWith("ak") || n.StartsWith("kalash")) return 14;
            if (n.StartsWith("m4") || n.StartsWith("carab")) return 15;
            if (n.StartsWith("franco") || n.StartsWith("sniper") || n.StartsWith("rifle")) return 16;
            if (n.StartsWith("rpg") || n.StartsWith("cohete") || n.StartsWith("bazoo") || n.StartsWith("lanzacoh")) return 18;
            if (n.StartsWith("grana")) return 4;
            if (n.StartsWith("molo")) return 5;
            if (n.StartsWith("bate")) return 1;
            if (n.StartsWith("cuchi")) return 3;
            if (n.StartsWith("amano") || n.StartsWith("pina") || n == "nada" || n == "ninguna") return 0;
            return -1;
        }

        /// <summary>Granadas, molotov y RPG: no se llevan en la mano, se usan de vez en cuando.</summary>
        public static bool IsSpecial(int weapon) { return weapon == Grenade || weapon == Molotov || weapon == Rpg; }

        /// <summary>Que tan buena es un arma de mano (para quedarse con la mejor).</summary>
        public static int Rank(int weapon)
        {
            switch (weapon)
            {
                case 7: return 1;
                case 9: case 12: return 2;
                case 10: case 13: return 3;
                case 11: return 4;
                case 14: case 16: return 5;
                case 15: case 17: return 6;
                default: return 0; // a mano, bate, cuchillo (y las especiales)
            }
        }

        /// <summary>La que sigue en la escalera de armas (para la mejora "mejor arma").</summary>
        public static int Upgrade(int weapon)
        {
            int[] ladder = { 7, 9, 12, 13, 11, 14, 15 };
            int r = Rank(weapon);
            foreach (int w in ladder) if (Rank(w) > r) return w;
            return weapon == 0 || weapon == 1 || weapon == 3 ? 7 : weapon;
        }

        public static string WeaponName(int weapon)
        {
            for (int i = 0; i < WeaponIds.Length; i++) if (WeaponIds[i] == weapon) return WeaponLabels[i];
            return weapon == 17 ? "Rifle M40" : "a mano";
        }

        public static int WeaponIndex(int weapon)
        {
            for (int i = 0; i < WeaponIds.Length; i++) if (WeaponIds[i] == weapon) return i;
            return -1;
        }

        // ------------------------------------------------------------------
        // Armas segun los meses (5 rangos) y segun el nivel de la banda (2 a 10)
        // ------------------------------------------------------------------
        public const int Tiers = 5;
        public static readonly int[] DefaultTierFrom = { 1, 2, 6, 12, 24 };
        public static readonly string[] DefaultTierWeapons = { "Pistola", "MicroUzi", "EscopetaCombate", "AK47", "M4" };

        /// <summary>Rango de meses (0..4). from[0] siempre es 1.</summary>
        public static int TierFor(int months, int[] from)
        {
            int m = Math.Max(1, months), t = 0;
            for (int i = 1; i < from.Length; i++) if (from[i] > 0 && m >= from[i]) t = i;
            return t;
        }

        /// <summary>Lo que gana la banda al llegar a cada nivel (indice 0 = nivel 2).</summary>
        public static readonly string[] DefaultLevelWeapons = { "Granadas", "EscopetaCombate", "Molotov", "AK47", "Granadas", "M4", "RPG", "Molotov", "RPG" };

        /// <summary>Cuantas trae: las especiales vienen de a poco.</summary>
        /// <summary>Balas por cargador (para la municion limitada).</summary>
        public static int ClipSize(int weapon)
        {
            switch (weapon)
            {
                case 7: return 17;
                case 9: return 10;
                case 10: return 8;
                case 11: return 10;
                case 12: return 50;
                case 13: return 30;
                case 14: case 15: return 30;
                case 16: case 17: return 5;
                default: return 0;
            }
        }

        public static int AmmoFor(int weapon)
        {
            if (weapon == Rpg) return 3;
            if (weapon == Grenade || weapon == Molotov) return 4;
            return 600;
        }

        /// <summary>Meses "equivalentes": suscripcion = sus meses; regalo de N subs = N*3 (hasta 36).</summary>
        public static int MonthsFor(string source, int count)
        {
            string s = Config.Normalize(source ?? "");
            if (s == "suscripcion") return Math.Max(1, count);
            if (s == "regalosubs" || s == "regalogrande") return Math.Max(1, Math.Min(36, count * 3));
            return 1;
        }
    }

    /// <summary>
    /// Mejoras "roguelike": cada vez que la banda mata a alguien aparecen 3 al azar arriba del NPC
    /// (!1 !2 !3) y el sub (o el chat) elige una. No depende del juego.
    /// </summary>
    public static class PerkRules
    {
        public const int Chaleco = 0, Vida = 1, Arma = 2, Granadas = 3, Rpg = 4, Molotov = 5, Punteria = 6, Velocidad = 7,
                         Refuerzo = 8, Regeneracion = 9, Escudo = 10, Tiempo = 11,
                         Municion = 12, Cadencia = 13, Vampiro = 14, Botiquin = 15, AutoBlindado = 16;
        public const int Count = 17;
        public static readonly string[] Labels =
            { "CHALECO", "+VIDA", "MEJOR ARMA", "GRANADAS", "RPG", "MOLOTOVS", "PUNTERIA", "VELOCIDAD", "REFUERZO", "REGENERACION", "ESCUDO 20s", "+60 SEGUNDOS",
              "MUNICION", "CADENCIA", "VAMPIRO", "BOTIQUIN", "AUTO BLINDADO" };
        public static readonly string[] Help =
            { "+100 de chaleco a toda la banda", "mas vida maxima y se curan", "un arma mejor para todos", "+4 granadas", "+2 cohetes de RPG",
              "+4 molotovs", "tiran mejor", "corren un poco mas rapido", "se suma uno a la banda", "se curan solos", "20 s sin que les pase nada", "+60 s al reloj",
              "recargan y llevan mas balas", "tiran mas seguido", "cada muerte los cura", "vida y chaleco al maximo", "su auto aguanta las balas" };
        /// <summary>Nombres para config.ini ([Mejoras] Chaleco = si ...).</summary>
        public static readonly string[] Keys =
            { "Chaleco", "Vida", "MejorArma", "Granadas", "Rpg", "Molotovs", "Punteria", "Velocidad", "Refuerzo", "Regeneracion", "Escudo", "Tiempo",
              "Municion", "Cadencia", "Vampiro", "Botiquin", "AutoBlindado" };

        /// <summary>3 mejoras distintas al azar (sin las que no sirven ahora: 'skip').</summary>
        public static int[] Offer(Random rng, int n, ICollection<int> skip)
        {
            var pool = new List<int>();
            for (int i = 0; i < Count; i++) if (skip == null || !skip.Contains(i)) pool.Add(i);
            var res = new List<int>();
            while (res.Count < n && pool.Count > 0)
            {
                int k = rng.Next(pool.Count);
                res.Add(pool[k]);
                pool.RemoveAt(k);
            }
            return res.ToArray();
        }

        /// <summary>"!1 CHALECO   !2 RPG   !3 VELOCIDAD"</summary>
        public static string Line(int[] offer)
        {
            if (offer == null) return "";
            var parts = new List<string>();
            for (int i = 0; i < offer.Length; i++) parts.Add("!" + (i + 1) + " " + Labels[offer[i]]);
            return string.Join("   ", parts.ToArray());
        }

        /// <summary>Lo que se ve arriba del NPC: "MEJORA (12): !1 CHALECO (2)   !2 RPG   !3 VELOCIDAD (1)".</summary>
        public static string TagLine(int[] offer, GangRules.VoteBox votes, double secondsLeft, bool showVotes)
        {
            if (offer == null || offer.Length == 0) return "";
            var parts = new List<string>();
            for (int i = 0; i < offer.Length; i++)
            {
                int v = votes != null ? votes.Count(i) : 0;
                parts.Add("!" + (i + 1) + " " + Labels[offer[i]] + (showVotes && v > 0 ? " (" + v + ")" : ""));
            }
            return "MEJORA (" + Math.Max(0, (int)Math.Ceiling(secondsLeft)) + "): " + string.Join("   ", parts.ToArray());
        }

        /// <summary>"!1" "!2" "!3" (o el nombre de la mejora: "!rpg") -> 0..2. -1 si no es una eleccion.</summary>
        public static int ParsePick(string message, int[] offer)
        {
            if (string.IsNullOrEmpty(message) || offer == null) return -1;
            string t = message.Trim();
            if (!t.StartsWith("!")) return -1;
            t = t.Substring(1).Trim();
            if (t.Length == 0) return -1;
            if (char.IsDigit(t[0]) && (t.Length == 1 || !char.IsLetterOrDigit(t[1])))
            {
                int k = t[0] - '1';
                return k >= 0 && k < offer.Length ? k : -1;
            }
            string w = Config.Normalize(t.Split(' ')[0]);
            if (w.Length < 3) return -1;
            for (int i = 0; i < offer.Length; i++)
            {
                string l = Config.Normalize(Labels[offer[i]]).Replace("+", "");
                if (l.Length > 0 && (l.StartsWith(w) || w.StartsWith(l.Split(' ')[0]))) return i;
            }
            return -1;
        }
    }
}
