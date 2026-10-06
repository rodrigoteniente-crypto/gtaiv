using System;
using System.Collections.Generic;

namespace KickChaos
{
    /// <summary>Nombres de las acciones y sus alias (sin dependencias del juego).</summary>
    public static class ActionNames
    {
        // nombre canonico -> nombre para mostrar
        public static readonly Dictionary<string, string> Display = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Explosion", "EXPLOSION" }, { "Bombardeo", "BOMBARDEO" }, { "ExplotarCoches", "COCHES BOMBA" },
            { "Policia", "POLICIA" }, { "Persecucion", "PERSECUCION" }, { "Bomberos", "BOMBEROS Y AMBULANCIA" },
            { "Fuego", "INCENDIO" }, { "LluviaDeCoches", "LLUVIA DE COCHES" }, { "Disturbio", "DISTURBIO" },
            { "Panico", "PANICO" }, { "Tormenta", "TORMENTA" }, { "Noche", "NOCHE" }, { "Dia", "DIA" },
            { "Atardecer", "ATARDECER" }, { "CamaraLenta", "CAMARA LENTA" }, { "Terremoto", "TERREMOTO" },
            { "SiguienteCamara", "CAMBIO DE CAMARA" }, { "Npc", "APARECE EN LIBERTY CITY" },
            { "NpcPasear", "SALE A PASEAR" }, { "NpcBatalla", "SALE A LA BATALLA" },
            { "AutosLocos", "AUTOS LOCOS" }, { "AutosVoladores", "AUTOS VOLADORES" }, { "Lluvia", "LLUVIA" }, { "Niebla", "NIEBLA" },
            { "Nada", "NADA" }
        };

        /// <summary>Opciones que se ofrecen en el menu, en orden, con un nombre lindo.</summary>
        public static readonly string[] Choices =
        {
            "Nada", "Npc", "NpcBatalla", "NpcPasear",
            "Explosion", "Bombardeo", "ExplotarCoches", "Policia", "Persecucion", "Bomberos", "Fuego",
            "LluviaDeCoches", "Disturbio", "Panico", "Tormenta", "Noche", "Dia", "Atardecer", "CamaraLenta",
            "Terremoto", "AutosLocos", "AutosVoladores", "Lluvia", "Niebla", "SiguienteCamara"
        };

        public static string Pretty(string canonical)
        {
            switch (canonical)
            {
                case "Nada": return "Nada";
                case "Explosion": return "Explosion";
                case "Bombardeo": return "Bombardeo";
                case "ExplotarCoches": return "Coches bomba";
                case "Policia": return "Policia (patrulleros)";
                case "Persecucion": return "Persecucion (estrellas)";
                case "Bomberos": return "Bomberos y ambulancia";
                case "Fuego": return "Incendio";
                case "LluviaDeCoches": return "Lluvia de autos";
                case "Disturbio": return "Disturbio";
                case "Panico": return "Panico";
                case "Tormenta": return "Tormenta";
                case "Noche": return "Noche";
                case "Dia": return "Dia";
                case "Atardecer": return "Atardecer";
                case "CamaraLenta": return "Camara lenta";
                case "Terremoto": return "Terremoto";
                case "SiguienteCamara": return "Cambiar de camara";
                case "Npc": return "NPC con su nombre";
                case "NpcPasear": return "NPC que pasea (no lo atacan)";
                case "NpcBatalla": return "NPC en modo batalla";
                case "AutosLocos": return "Autos locos";
                case "AutosVoladores": return "Autos voladores";
                case "Lluvia": return "Lluvia";
                case "Niebla": return "Niebla";
                default: return canonical;
            }
        }

        static readonly Dictionary<string, string> Aliases = BuildAliases();

        static Dictionary<string, string> BuildAliases()
        {
            var d = new Dictionary<string, string>();
            Action<string, string[]> add = (canon, names) =>
            {
                d[Key(canon)] = canon;
                foreach (string n in names) d[Key(n)] = canon;
            };
            add("Explosion", new[] { "explotar", "boom", "explode", "bomba" });
            add("Bombardeo", new[] { "airstrike", "ataque aereo", "bombas", "misiles" });
            add("ExplotarCoches", new[] { "coche bomba", "coches bomba", "carbomb", "explotar coches", "autos bomba" });
            add("Policia", new[] { "police", "poli", "patrulla", "patrullas", "cops" });
            add("Persecucion", new[] { "swat", "buscado", "wanted", "estrellas", "noose", "persecución" });
            add("Bomberos", new[] { "ambulancia", "emergencias", "firetruck", "emergency" });
            add("Fuego", new[] { "fire", "incendio" });
            add("LluviaDeCoches", new[] { "carrain", "car rain", "lluvia de autos", "lluvia de carros" });
            add("Disturbio", new[] { "riot", "caos", "pelea", "chaos" });
            add("Panico", new[] { "panic", "huida", "corran" });
            add("Tormenta", new[] { "storm", "rayos", "thunder" });
            add("Noche", new[] { "night" });
            add("Dia", new[] { "day", "mediodia" });
            add("Atardecer", new[] { "sunset", "ocaso" });
            add("CamaraLenta", new[] { "slowmo", "slow motion", "lento", "camara lenta" });
            add("Terremoto", new[] { "earthquake", "sismo", "temblor" });
            add("SiguienteCamara", new[] { "nextcam", "next camera", "camara", "cambiar camara" });
            add("Npc", new[] { "personaje", "avatar", "aparecer", "suscriptor", "npcs" });
            add("NpcPasear", new[] { "npc pasea", "npc paseo", "npc paz" });
            // los comportamientos viejos (1.7 y antes) ahora son todos "batalla"
            add("NpcBatalla", new[] { "npc batalla", "npc guerra", "npc gameplay", "NpcPelea", "npc pinas", "npc pelea", "NpcHuir", "npc huye",
                                      "npc escapa", "npc persecucion", "NpcTiroteo", "npc tiros", "NpcArrasar", "npc rambo", "npc arrasa",
                                      "NpcRobar", "npc roba", "npc ladron", "robar auto", "robo" });
            add("AutosLocos", new[] { "crazy cars", "locos", "conductores locos" });
            add("AutosVoladores", new[] { "flying cars", "volar autos", "autos vuelan" });
            add("Lluvia", new[] { "rain", "llueve" });
            add("Niebla", new[] { "fog", "neblina" });
            add("Nada", new[] { "ninguna", "none", "nothing", "off", "-" });
            return d;
        }

        static string Key(string s)
        {
            return Config.Normalize(s).Replace(" ", "").Replace("_", "").Replace("-", "");
        }

        public static string Canonical(string name)
        {
            string c;
            return Aliases.TryGetValue(Key(name ?? ""), out c) ? c : null;
        }
    }
}
