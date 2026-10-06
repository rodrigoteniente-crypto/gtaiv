using System;
using System.Numerics;

namespace KickChaos
{
    /// <summary>
    /// Que hace el NPC de un suscriptor. Dos modos nada mas:
    /// Pasear = anda por la ciudad (a pie o en auto) y nadie lo ataca.
    /// Batalla = se busca con las otras bandas para matarse y subir de nivel (y la policia, si esta prendida).
    /// </summary>
    public enum NpcBehavior { Pasear, Batalla }

    /// <summary>Reglas del NPC de suscriptores que no dependen del juego (se prueban fuera de el).</summary>
    public static class NpcRules
    {
        /// <summary>"#RRGGBB" (color de Kick) -> ARGB. Los muy oscuros se aclaran para que se lean. 0 = no se entiende.</summary>
        public static int ParseColor(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return 0;
            string h = hex.Trim().TrimStart('#');
            if (h.Length == 3) h = new string(new[] { h[0], h[0], h[1], h[1], h[2], h[2] });
            if (h.Length != 6) return 0;
            int v;
            if (!int.TryParse(h, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out v)) return 0;
            int r = (v >> 16) & 255, g = (v >> 8) & 255, b = v & 255;
            float lum = (0.299f * r + 0.587f * g + 0.114f * b) / 255f;
            if (lum < 0.45f)
            {
                float k = (0.45f - lum) / 0.45f * 0.6f;
                r += (int)((255 - r) * k); g += (int)((255 - g) * k); b += (int)((255 - b) * k);
            }
            return unchecked((int)0xFF000000) | (r << 16) | (g << 8) | b;
        }

        public static readonly string[] BehaviorKeys = { "Pasear", "Batalla" };
        public static readonly string[] BehaviorLabels = { "Paseo (no lo atacan)", "Batalla (buscan a los otros)" };
        public const int Count = 2;

        /// <summary>
        /// "Batalla" / "Pasear". Los nombres viejos (Pelea, Huir, Tiroteo, Arrasar, Robar) ahora son Batalla.
        /// Desconocido = null.
        /// </summary>
        public static NpcBehavior? Parse(string s)
        {
            string n = Config.Normalize(s ?? "").Trim();
            if (n.Length == 0) return null;
            if (n.StartsWith("pase") || n.StartsWith("camin") || n == "walk" || n.StartsWith("paz") || n.StartsWith("tranq")) return NpcBehavior.Pasear;
            string[] battle = { "batal", "gameplay", "juego", "guerra", "pele", "pina", "trom", "fight", "hui", "escap", "perse", "flee",
                                "tiro", "poli", "shoot", "arras", "ramp", "rambo", "masac", "roba", "robo", "ladr", "steal", "combat" };
            foreach (string b in battle) if (n.StartsWith(b)) return NpcBehavior.Batalla;
            return null;
        }

        /// <summary>Nombre que se guarda en config.ini.</summary>
        public static string Key(NpcBehavior b) { return BehaviorKeys[(int)b]; }

        /// <summary>El modo segun de donde viene: suscripcion, regalo de subs, o follow (y cualquier otra cosa).</summary>
        public static NpcBehavior ChooseMode(string source, NpcBehavior sub, NpcBehavior gift, NpcBehavior follow)
        {
            string s = Config.Normalize(source ?? "");
            if (s == "suscripcion") return sub;
            if (s == "regalosubs" || s == "regalogrande") return gift;
            return follow;
        }

        public static NpcBehavior Resolve(string name, NpcBehavior def)
        {
            NpcBehavior? b = Parse(name);
            return b.HasValue ? b.Value : def;
        }

        /// <summary>Segunda linea del cartel (arriba de la cabeza).</summary>
        public static string Subtitle(string source, int count)
        {
            string s = Config.Normalize(source ?? "");
            if (s == "suscripcion") return count <= 1 ? "NUEVO SUB" : "SUB " + count + " MESES";
            if (s == "regalosubs" || s == "regalogrande") return "REGALO " + Math.Max(1, count) + (count == 1 ? " SUB" : " SUBS");
            if (s == "kicksgifted" || s == "kicksgrandes") return Math.Max(1, count) + " KICKS";
            if (s == "primermensaje") return "PRIMER MENSAJE";
            if (s == "rewardredeemedevent") return "RECOMPENSA";
            if (s == "follow") return "FOLLOW";
            if (s == "host") return "HOST";
            return "";
        }

        /// <summary>Texto para OBS / log.</summary>
        public static string Label(NpcBehavior b)
        {
            return b == NpcBehavior.Pasear ? "PASEA POR LIBERTY CITY" : "SALE A LA BATALLA";
        }

        /// <summary>Accion "NpcBatalla" / "NpcPasear" -> modo fijo (null = "Npc": el modo del evento).</summary>
        public static NpcBehavior? FromAction(string action)
        {
            if (action == null || !action.StartsWith("Npc", StringComparison.Ordinal) || action.Length <= 3) return null;
            return Parse(action.Substring(3));
        }

        /// <summary>
        /// Proyecta un punto del mundo a la pantalla (0..1 en x e y, y hacia abajo). FOV vertical.
        /// Devuelve false si esta detras de la camara o fuera de la pantalla.
        /// </summary>
        public static bool Project(Vector3 p, Vector3 camPos, Vector3 camRot, float fovDeg, float aspect, out float sx, out float sy, out float depth)
        {
            sx = sy = 0f;
            Vector3 d = p - camPos;
            Vector3 fwd = MathX.Forward(camRot);
            Vector3 right = MathX.Right(camRot);
            Vector3 up = Vector3.Cross(right, fwd);
            depth = Vector3.Dot(d, fwd);
            if (depth < 0.5f) return false;
            double tanV = Math.Tan(Math.Max(1.0, Math.Min(170.0, fovDeg)) * 0.5 * Math.PI / 180.0);
            double tanH = tanV * (aspect > 0.1f ? aspect : 16f / 9f);
            double x = Vector3.Dot(d, right) / (depth * tanH);
            double y = Vector3.Dot(d, up) / (depth * tanV);
            sx = (float)(0.5 + 0.5 * x);
            sy = (float)(0.5 - 0.5 * y);
            return sx > -0.05f && sx < 1.05f && sy > -0.05f && sy < 1.1f;
        }

        /// <summary>Que tan grande se ve una persona (alto en pantalla, 0..1) a esa distancia con ese FOV.</summary>
        public static float ApparentHeight(float depth, float fovDeg)
        {
            double tanV = Math.Tan(Math.Max(1.0, fovDeg) * 0.5 * Math.PI / 180.0);
            return (float)(1.8 / (2.0 * Math.Max(0.5, depth) * tanV));
        }
    }
}
