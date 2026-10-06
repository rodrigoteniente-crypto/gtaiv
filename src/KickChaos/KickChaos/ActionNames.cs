using System;
using System.Collections.Generic;

namespace KickChaos;

public static class ActionNames
{
	public static readonly Dictionary<string, string> Display = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
	{
		{ "Explosion", "EXPLOSION" },
		{ "Bombardeo", "BOMBARDEO" },
		{ "ExplotarCoches", "COCHES BOMBA" },
		{ "Policia", "POLICIA" },
		{ "Persecucion", "PERSECUCION" },
		{ "Bomberos", "BOMBEROS Y AMBULANCIA" },
		{ "Fuego", "INCENDIO" },
		{ "LluviaDeCoches", "LLUVIA DE COCHES" },
		{ "Disturbio", "DISTURBIO" },
		{ "Panico", "PANICO" },
		{ "Tormenta", "TORMENTA" },
		{ "Noche", "NOCHE" },
		{ "Dia", "DIA" },
		{ "Atardecer", "ATARDECER" },
		{ "CamaraLenta", "CAMARA LENTA" },
		{ "Terremoto", "TERREMOTO" },
		{ "SiguienteCamara", "CAMBIO DE CAMARA" },
		{ "Npc", "APARECE EN LIBERTY CITY" },
		{ "NpcPasear", "SALE A PASEAR" },
		{ "NpcPelea", "SE AGARRA A PINAS" },
		{ "NpcHuir", "SE ESCAPA DE LA POLICIA" },
		{ "NpcTiroteo", "TIROTEO CON LA POLICIA" },
		{ "NpcArrasar", "ARRASA CON TODO" },
		{ "NpcRobar", "SE ROBA UN AUTO" },
		{ "AutosLocos", "AUTOS LOCOS" },
		{ "AutosVoladores", "AUTOS VOLADORES" },
		{ "Lluvia", "LLUVIA" },
		{ "Niebla", "NIEBLA" },
		{ "Nada", "NADA" }
	};

	public static readonly string[] Choices = new string[29]
	{
		"Nada", "Npc", "NpcPasear", "NpcPelea", "NpcHuir", "NpcTiroteo", "NpcArrasar", "NpcRobar", "Explosion", "Bombardeo",
		"ExplotarCoches", "Policia", "Persecucion", "Bomberos", "Fuego", "LluviaDeCoches", "Disturbio", "Panico", "Tormenta", "Noche",
		"Dia", "Atardecer", "CamaraLenta", "Terremoto", "AutosLocos", "AutosVoladores", "Lluvia", "Niebla", "SiguienteCamara"
	};

	private static readonly Dictionary<string, string> Aliases = BuildAliases();

	public static string Pretty(string canonical)
	{
		return canonical switch
		{
			"Nada" => "Nada", 
			"Explosion" => "Explosion", 
			"Bombardeo" => "Bombardeo", 
			"ExplotarCoches" => "Coches bomba", 
			"Policia" => "Policia (patrulleros)", 
			"Persecucion" => "Persecucion (estrellas)", 
			"Bomberos" => "Bomberos y ambulancia", 
			"Fuego" => "Incendio", 
			"LluviaDeCoches" => "Lluvia de autos", 
			"Disturbio" => "Disturbio", 
			"Panico" => "Panico", 
			"Tormenta" => "Tormenta", 
			"Noche" => "Noche", 
			"Dia" => "Dia", 
			"Atardecer" => "Atardecer", 
			"CamaraLenta" => "Camara lenta", 
			"Terremoto" => "Terremoto", 
			"SiguienteCamara" => "Cambiar de camara", 
			"Npc" => "NPC con su nombre", 
			"NpcPasear" => "NPC que pasea", 
			"NpcPelea" => "NPC a las pinas", 
			"NpcHuir" => "NPC escapa en auto", 
			"NpcTiroteo" => "NPC tiroteo", 
			"NpcArrasar" => "NPC arrasa", 
			"NpcRobar" => "NPC roba un auto", 
			"AutosLocos" => "Autos locos", 
			"AutosVoladores" => "Autos voladores", 
			"Lluvia" => "Lluvia", 
			"Niebla" => "Niebla", 
			_ => canonical, 
		};
	}

	private static Dictionary<string, string> BuildAliases()
	{
		Dictionary<string, string> d = new Dictionary<string, string>();
		Action<string, string[]> action = delegate(string canon, string[] names)
		{
			d[Key(canon)] = canon;
			foreach (string s in names)
			{
				d[Key(s)] = canon;
			}
		};
		action("Explosion", new string[4] { "explotar", "boom", "explode", "bomba" });
		action("Bombardeo", new string[4] { "airstrike", "ataque aereo", "bombas", "misiles" });
		action("ExplotarCoches", new string[5] { "coche bomba", "coches bomba", "carbomb", "explotar coches", "autos bomba" });
		action("Policia", new string[5] { "police", "poli", "patrulla", "patrullas", "cops" });
		action("Persecucion", new string[6] { "swat", "buscado", "wanted", "estrellas", "noose", "persecución" });
		action("Bomberos", new string[4] { "ambulancia", "emergencias", "firetruck", "emergency" });
		action("Fuego", new string[2] { "fire", "incendio" });
		action("LluviaDeCoches", new string[4] { "carrain", "car rain", "lluvia de autos", "lluvia de carros" });
		action("Disturbio", new string[4] { "riot", "caos", "pelea", "chaos" });
		action("Panico", new string[3] { "panic", "huida", "corran" });
		action("Tormenta", new string[3] { "storm", "rayos", "thunder" });
		action("Noche", new string[1] { "night" });
		action("Dia", new string[2] { "day", "mediodia" });
		action("Atardecer", new string[2] { "sunset", "ocaso" });
		action("CamaraLenta", new string[4] { "slowmo", "slow motion", "lento", "camara lenta" });
		action("Terremoto", new string[3] { "earthquake", "sismo", "temblor" });
		action("SiguienteCamara", new string[4] { "nextcam", "next camera", "camara", "cambiar camara" });
		action("Npc", new string[5] { "personaje", "avatar", "aparecer", "suscriptor", "npcs" });
		action("NpcPasear", new string[2] { "npc pasea", "npc paseo" });
		action("NpcPelea", new string[2] { "npc pinas", "npc pelea" });
		action("NpcHuir", new string[3] { "npc huye", "npc escapa", "npc persecucion" });
		action("NpcTiroteo", new string[1] { "npc tiros" });
		action("NpcArrasar", new string[2] { "npc rambo", "npc arrasa" });
		action("NpcRobar", new string[4] { "npc roba", "npc ladron", "robar auto", "robo" });
		action("AutosLocos", new string[3] { "crazy cars", "locos", "conductores locos" });
		action("AutosVoladores", new string[3] { "flying cars", "volar autos", "autos vuelan" });
		action("Lluvia", new string[2] { "rain", "llueve" });
		action("Niebla", new string[2] { "fog", "neblina" });
		action("Nada", new string[5] { "ninguna", "none", "nothing", "off", "-" });
		return d;
	}

	private static string Key(string s)
	{
		return Config.Normalize(s).Replace(" ", string.Empty).Replace("_", string.Empty)
			.Replace("-", string.Empty);
	}

	public static string Canonical(string name)
	{
		string value;
		return (!Aliases.TryGetValue(Key(name ?? string.Empty), out value)) ? null : value;
	}
}
