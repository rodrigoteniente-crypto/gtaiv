using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;

namespace KickChaos
{
    /// <summary>
    /// Reglas de las bandas de los suscriptores que no dependen del juego (se prueban fuera de el):
    /// las ordenes por chat ("!robarauto"), la votacion, el reloj, los mensajes arriba de la cabeza,
    /// donde cubrirse y cuanto pesa cada camara cerca del lio.
    /// </summary>
    public static class GangRules
    {
        // ------------------------------------------------------------------
        // Ordenes por chat
        // ------------------------------------------------------------------
        public const int CmdRobarAuto = 0, CmdAtacar = 1, CmdHuir = 2, CmdBailar = 3, CmdPinas = 4, CmdBanda = 5;

        /// <summary>Lo que se escribe en el chat (con "!").</summary>
        public static readonly string[] CmdKeys = { "robarauto", "atacar", "huir", "bailar", "pinas", "banda" };

        /// <summary>Para el menu.</summary>
        public static readonly string[] CmdLabels =
        {
            "!robarauto (roba un auto)", "!atacar (contra la policia)", "!huir (escapa en auto)",
            "!bailar (se ponen a bailar)", "!pinas (contra la gente)", "!banda (busca a la otra banda)"
        };

        /// <summary>Otras formas de escribir cada orden.</summary>
        static readonly string[][] CmdAliases =
        {
            new[] { "robarauto", "robar", "roba", "robo", "auto", "robaauto", "robarunauto", "robaunauto", "robarcoche", "coche" },
            new[] { "atacar", "ataca", "ataque", "tiros", "tiroteo", "policia", "poli", "polis", "matar", "mata" },
            new[] { "huir", "huye", "huyan", "escapar", "escapa", "escape", "rajar", "raja", "fuga" },
            new[] { "bailar", "baila", "bailen", "bailando", "baile", "dance", "perreo", "fiesta", "bailamos" },
            new[] { "pinas", "pina", "pelea", "pelear", "trompadas", "pinazos" },
            new[] { "banda", "bandas", "guerra", "rival", "rivales", "buscar" }
        };

        public static readonly string[] DefaultSlots = { "atacar", "huir", "bailar" };

        // ------------------------------------------------------------------
        // 1.9: comandos divertidos (siempre disponibles, no van en las 3 opciones)
        // ------------------------------------------------------------------
        public const int FunSkin = 0, FunRopa = 1, FunSaltar = 2, FunDesmayo = 3, FunManos = 4, FunMiedo = 5, FunTurbo = 6, FunFestejar = 7;
        public static readonly string[] FunKeys = { "skin", "ropa", "saltar", "desmayo", "manosarriba", "miedo", "turbo", "festejar" };
        public static readonly string[] FunLabels =
        {
            "!skin (otra persona, mismo nombre)", "!ropa (se cambia la ropa)", "!saltar", "!desmayo (se cae redondo)",
            "!manosarriba", "!miedo (se agacha muerto de miedo)", "!turbo (el auto sale disparado)", "!festejar"
        };
        static readonly string[][] FunAliases =
        {
            new[] { "skin", "skins", "cambiarskin", "personaje" },
            new[] { "ropa", "outfit", "pilcha", "cambiarropa", "vestir" },
            new[] { "saltar", "salta", "salto", "jump", "saltito" },
            new[] { "desmayo", "desmayar", "desmayate", "caer", "caete", "ragdoll", "muerto" },
            new[] { "manosarriba", "manos", "rendirse", "rendite", "arriba" },
            new[] { "miedo", "cagaso", "cobarde", "susto", "asustado" },
            new[] { "turbo", "nitro", "acelera", "acelerar", "rapido" },
            new[] { "festejar", "festeja", "festejo", "gol", "vamos", "fiestita" }
        };

        /// <summary>"!saltar" -> FunSaltar. -1 si no es un comando divertido.</summary>
        public static int FunCommand(string message)
        {
            if (string.IsNullOrEmpty(message)) return -1;
            string t = message.Trim();
            if (!t.StartsWith("!")) return -1;
            string[] words = t.Substring(1).Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) return -1;
            var tries = new List<string> { words[0] };
            if (words.Length > 1) tries.Add(words[0] + words[1]);
            for (int k = tries.Count - 1; k >= 0; k--)
            {
                string w = Clean(tries[k]);
                for (int i = 0; i < FunAliases.Length; i++)
                    foreach (string a in FunAliases[i]) if (a == w) return i;
            }
            return -1;
        }

        /// <summary>El sub cambia su modo: "!pasear" (0, nadie lo ataca) o "!batalla" (1). -1 si no es eso.</summary>
        public static int ModeSwitch(string message)
        {
            if (string.IsNullOrEmpty(message)) return -1;
            string t = message.Trim();
            if (!t.StartsWith("!")) return -1;
            string w = Clean(t.Substring(1).Trim().Split(' ')[0]);
            if (w == "pasear" || w == "paseo" || w == "paz" || w == "tranqui" || w == "tranquilo") return 0;
            if (w == "batalla" || w == "modobatalla" || w == "gameplay" || w == "combate") return 1;
            return -1;
        }

        /// <summary>"robar" -> 0 (robarauto). -1 si no es ninguna.</summary>
        public static int CommandIndex(string word)
        {
            string w = Clean(word);
            if (w.Length == 0) return -1;
            for (int i = 0; i < CmdAliases.Length; i++)
                foreach (string a in CmdAliases[i]) if (a == w) return i;
            return -1;
        }

        /// <summary>Nombre de config ("RobarAuto", "robar"...) -> indice. -1 si no existe.</summary>
        public static int ParseKey(string s) { return CommandIndex(s); }

        /// <summary>
        /// Mensaje del chat -> numero de opcion (0..slots-1) o -1. Tiene que empezar con "!".
        /// Acepta "!robarauto", "!ROBAR AUTO", "!robá", "! huir", "!1" / "!2" / "!3".
        /// </summary>
        public static int ParseChoice(string message, int[] slots)
        {
            if (string.IsNullOrEmpty(message) || slots == null || slots.Length == 0) return -1;
            string t = message.Trim();
            if (!t.StartsWith("!")) return -1;
            t = t.Substring(1).TrimStart();
            if (t.Length == 0) return -1;
            // "!1", "!2", "!3"
            if (char.IsDigit(t[0]) && (t.Length == 1 || !char.IsLetterOrDigit(t[1])))
            {
                int k = t[0] - '1';
                return k >= 0 && k < slots.Length ? k : -1;
            }
            string[] words = t.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            var tries = new List<string>();
            tries.Add(words[0]);
            if (words.Length > 1) tries.Add(words[0] + words[1]);   // "!robar auto"
            if (words.Length > 2) tries.Add(words[0] + words[1] + words[2]); // "!robar un auto"
            for (int i = tries.Count - 1; i >= 0; i--)
            {
                int cmd = CommandIndex(tries[i]);
                if (cmd < 0) continue;
                for (int s = 0; s < slots.Length; s++) if (slots[s] == cmd) return s;
            }
            return -1;
        }

        static string Clean(string s)
        {
            string n = Config.Normalize(s ?? "");
            var sb = new StringBuilder(n.Length);
            foreach (char c in n) if (c >= 'a' && c <= 'z') sb.Append(c);
            return sb.ToString();
        }

        /// <summary>Si dos opciones son la misma orden, la repetida pasa a ser la primera orden que no esta.</summary>
        public static void Dedupe(int[] slots)
        {
            for (int i = 1; i < slots.Length; i++)
            {
                bool dup = false;
                for (int j = 0; j < i; j++) if (slots[j] == slots[i]) dup = true;
                if (!dup) continue;
                for (int c = 0; c < CmdKeys.Length; c++)
                {
                    bool used = false;
                    for (int j = 0; j < slots.Length; j++) if (j != i && slots[j] == c) used = true;
                    if (!used) { slots[i] = c; break; }
                }
            }
        }

        /// <summary>"!robarauto  !atacar  !huir"</summary>
        public static string ChoicesLine(int[] slots)
        {
            var parts = new List<string>();
            foreach (int c in slots) if (c >= 0 && c < CmdKeys.Length) parts.Add("!" + CmdKeys[c]);
            return string.Join("   ", parts.ToArray());
        }

        // ------------------------------------------------------------------
        // Quien decide
        // ------------------------------------------------------------------
        public const int ModeSub = 0, ModeVote = 1, ModeFirst = 2;
        public static readonly string[] ModeKeys = { "Suscriptor", "Votacion", "Chat" };
        public static readonly string[] ModeLabels = { "Solo el suscriptor", "Vota el chat", "El chat, el primero" };

        public static int ParseMode(string s)
        {
            string n = Clean(s);
            if (n.StartsWith("vot")) return ModeVote;
            if (n.StartsWith("chat") || n.StartsWith("prim") || n.StartsWith("todos")) return ModeFirst;
            return ModeSub;
        }

        public const int UnlockOnGrow = 0, UnlockAlways = 1, UnlockNever = 2;
        public static readonly string[] UnlockKeys = { "AlDuplicarse", "Siempre", "Nunca" };
        public static readonly string[] UnlockLabels = { "Cuando se duplica", "Desde que aparece", "Nunca" };

        public static int ParseUnlock(string s)
        {
            string n = Clean(s);
            if (n.StartsWith("siem") || n.StartsWith("desde") || n == "si") return UnlockAlways;
            if (n.StartsWith("nun") || n == "no") return UnlockNever;
            return UnlockOnGrow;
        }

        public const int GrowCops = 0, GrowCopsAndGangs = 1, GrowAnyone = 2;
        public static readonly string[] GrowKeys = { "Policias", "PoliciasYBandas", "Cualquiera" };
        public static readonly string[] GrowLabels = { "Matar un policia", "Policia u otra banda", "Matar a cualquiera" };

        public static int ParseGrow(string s)
        {
            string n = Clean(s);
            if (n.StartsWith("cual") || n.StartsWith("todo")) return GrowAnyone;
            if (n.Contains("banda")) return GrowCopsAndGangs;
            return GrowCops;
        }

        /// <summary>Esa muerte hace crecer la banda?</summary>
        public static bool Grows(int rule, bool cop, bool gang)
        {
            if (rule == GrowAnyone) return true;
            if (cop) return true;
            return gang && rule == GrowCopsAndGangs;
        }

        // ------------------------------------------------------------------
        // Votacion: un voto por persona (vale el ultimo); gana el que llego primero al maximo
        // ------------------------------------------------------------------
        public class VoteBox
        {
            readonly Dictionary<string, int> byUser = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            readonly int[] counts;
            readonly double[] reachedAt;
            public double Start = -1, End = -1;

            public VoteBox(int options) { counts = new int[options]; reachedAt = new double[options]; }

            public bool Open { get { return Start >= 0; } }
            public int Total { get { return byUser.Count; } }
            public int Count(int option) { return option >= 0 && option < counts.Length ? counts[option] : 0; }

            /// <summary>Registra un voto. Abre la votacion si es el primero.</summary>
            public void Add(string user, int option, double now, double seconds)
            {
                if (option < 0 || option >= counts.Length) return;
                if (!Open) { Start = now; End = now + Math.Max(1.0, seconds); }
                string u = (user ?? "").Trim();
                if (u.Length == 0) u = "?" + byUser.Count;
                int prev;
                if (byUser.TryGetValue(u, out prev))
                {
                    if (prev == option) return;
                    counts[prev]--;
                }
                byUser[u] = option;
                counts[option]++;
                reachedAt[option] = now;
            }

            public bool Due(double now) { return Open && now >= End; }

            public int Winner()
            {
                int best = -1;
                for (int i = 0; i < counts.Length; i++)
                {
                    if (counts[i] <= 0) continue;
                    if (best < 0 || counts[i] > counts[best] || (counts[i] == counts[best] && reachedAt[i] < reachedAt[best])) best = i;
                }
                return best;
            }

            public void Reset()
            {
                byUser.Clear();
                for (int i = 0; i < counts.Length; i++) { counts[i] = 0; reachedAt[i] = 0; }
                Start = End = -1;
            }
        }

        /// <summary>"!robarauto 3   !atacar 1   !huir 0   (12s)"</summary>
        public static string VoteLine(int[] slots, VoteBox box, double now)
        {
            var parts = new List<string>();
            for (int i = 0; i < slots.Length; i++)
                if (slots[i] >= 0 && slots[i] < CmdKeys.Length) parts.Add("!" + CmdKeys[slots[i]] + " " + box.Count(i));
            int left = (int)Math.Ceiling(Math.Max(0.0, box.End - now));
            return string.Join("   ", parts.ToArray()) + "   (" + left + "s)";
        }

        // ------------------------------------------------------------------
        // Reloj y mensajes
        // ------------------------------------------------------------------
        /// <summary>185 -> "3:05". Negativo -> "0:00".</summary>
        public static string Clock(double seconds)
        {
            int s = (int)Math.Ceiling(Math.Max(0.0, seconds));
            return (s / 60) + ":" + (s % 60).ToString("00");
        }

        static readonly Regex Emote = new Regex(@"\[emote:\d+:([^\]]*)\]", RegexOptions.Compiled);

        /// <summary>
        /// Mensaje del chat listo para el globito: sin emotes de Kick (queda el nombre), sin emojis
        /// (la fuente no los tiene), en lineas de hasta 'width' letras y como mucho 'lines' lineas.
        /// </summary>
        public static string[] Bubble(string message, int width, int lines)
        {
            string s = Emote.Replace(message ?? "", m => m.Groups[1].Value);
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (char.IsHighSurrogate(c) || char.IsLowSurrogate(c)) continue; // emoji
                if (c < ' ' || (c >= 0x2000 && c <= 0x2BFF) || c >= 0xFE00) { if (c == '\t') sb.Append(' '); continue; }
                sb.Append(c);
            }
            string clean = Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
            if (clean.Length == 0) return new string[0];
            width = Math.Max(8, width);
            lines = Math.Max(1, lines);
            var result = new List<string>();
            string[] words = clean.Split(' ');
            string cur = "";
            int w = 0;
            while (w < words.Length)
            {
                string word = words[w];
                if (word.Length > width) // palabra larguisima: cortarla
                {
                    if (cur.Length > 0) { result.Add(cur); cur = ""; if (result.Count >= lines) break; }
                    result.Add(word.Substring(0, width));
                    words[w] = word.Substring(width);
                    if (result.Count >= lines) break;
                    continue;
                }
                string next = cur.Length == 0 ? word : cur + " " + word;
                if (next.Length <= width) { cur = next; w++; continue; }
                result.Add(cur);
                cur = "";
                if (result.Count >= lines) break;
            }
            bool cut = w < words.Length;
            if (result.Count < lines && cur.Length > 0) { result.Add(cur); cur = ""; }
            else if (cur.Length > 0) cut = true;
            if (result.Count > lines) result.RemoveRange(lines, result.Count - lines);
            if (cut && result.Count > 0)
            {
                string last = result[result.Count - 1];
                if (last.Length > width - 2) last = last.Substring(0, width - 2).TrimEnd();
                result[result.Count - 1] = last + "..";
            }
            return result.ToArray();
        }

        /// <summary>Cuanto se ve el globito: mas largo el mensaje, mas tiempo (entre 5 y 12 s).</summary>
        public static double BubbleSeconds(string message)
        {
            int n = (message ?? "").Length;
            return Math.Max(5.0, Math.Min(12.0, 3.5 + n * 0.09));
        }

        // ------------------------------------------------------------------
        // Pelea
        // ------------------------------------------------------------------
        /// <summary>Lugar para cubrirse detras de un auto: del lado contrario a la amenaza (en el plano).</summary>
        public static Vector3 CoverBehind(Vector3 car, Vector3 threat, float gap)
        {
            Vector3 d = new Vector3(car.X - threat.X, car.Y - threat.Y, 0f);
            float len = d.Length();
            if (len < 0.01f) d = new Vector3(1f, 0f, 0f); else d /= len;
            return new Vector3(car.X + d.X * gap, car.Y + d.Y * gap, car.Z);
        }

        /// <summary>
        /// Puntaje de un auto como cobertura (mas bajo = mejor, float.MaxValue = no sirve).
        /// Sirve si queda cerca y no nos deja mas lejos ni pegados a la amenaza. Si 'advance', premia
        /// los autos que nos acercan a la amenaza (avanzar de auto en auto).
        /// </summary>
        public static float CoverScore(Vector3 me, Vector3 car, Vector3 threat, float maxRun, bool advance)
        {
            Vector3 spot = CoverBehind(car, threat, 2.3f);
            float run = Flat(me, spot);
            if (run > maxRun || run < 1.2f) return float.MaxValue;
            float myDist = Flat(me, threat), spotDist = Flat(spot, threat);
            if (spotDist < 6f) return float.MaxValue;              // demasiado encima
            if (spotDist > myDist + 6f) return float.MaxValue;     // nos aleja
            float score = run;
            if (advance) score += (spotDist - myDist) * 1.5f;      // mas cerca de la amenaza = mejor
            return score;
        }

        public static float Flat(Vector3 a, Vector3 b)
        {
            float dx = a.X - b.X, dy = a.Y - b.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>Correrse al costado (sin autos cerca): perpendicular a la amenaza y un poco hacia ella.</summary>
        public static Vector3 Strafe(Vector3 me, Vector3 threat, float side, float forward)
        {
            Vector3 d = new Vector3(threat.X - me.X, threat.Y - me.Y, 0f);
            float len = d.Length();
            if (len < 0.01f) d = new Vector3(1f, 0f, 0f); else d /= len;
            Vector3 perp = new Vector3(-d.Y, d.X, 0f);
            return new Vector3(me.X + perp.X * side + d.X * forward, me.Y + perp.Y * side + d.Y * forward, me.Z);
        }

        // ------------------------------------------------------------------
        // Camaras
        // ------------------------------------------------------------------
        /// <summary>Cuanto mas chance tiene una camara segun que tan lejos mira del lio.</summary>
        public static double HotspotWeight(float dist)
        {
            if (dist < 120f) return 6.0;
            if (dist < 250f) return 2.5;
            if (dist < 450f) return 1.2;
            return 1.0;
        }

        /// <summary>Distancia (en el plano) entre el lio y lo que mira una camara: su posicion o su objetivo.</summary>
        public static float ShotDistance(Vector3 hot, Vector3 camPos, Vector3 look)
        {
            return Math.Min(Flat(hot, camPos), Flat(hot, look));
        }

        /// <summary>Nombre que se ve arriba de un integrante de la banda.</summary>
        public static string MemberName(string leader) { return "banda de " + leader; }

        public static string GangTitle(string leader, int members)
        {
            return "LA BANDA DE " + (leader ?? "").ToUpperInvariant() + (members > 1 ? " (" + members + ")" : "");
        }
    }
}
