using System;
using System.Numerics;

namespace KickChaos;

public static class NpcRules
{
	public static readonly string[] BehaviorKeys = new string[7] { "Pasear", "Pelea", "Huir", "Tiroteo", "Arrasar", "Robar", "Azar" };

	public static readonly string[] BehaviorLabels = new string[7] { "Pasea", "Pinas", "Persecucion", "Tiroteo", "Arrasa", "Roba un auto", "Al azar" };

	public const int Count = 6;

	public static NpcBehavior? Parse(string s)
	{
		string text = Config.Normalize(s ?? string.Empty).Trim();
		if (text.StartsWith("pase") || text.StartsWith("camin") || text == "walk")
		{
			return NpcBehavior.Pasear;
		}
		if (text.StartsWith("pele") || text.StartsWith("pina") || text.StartsWith("trom") || text == "fight")
		{
			return NpcBehavior.Pelea;
		}
		if (text.StartsWith("hui") || text.StartsWith("escap") || text.StartsWith("perse") || text == "flee")
		{
			return NpcBehavior.Huir;
		}
		if (text.StartsWith("tiro") || text.StartsWith("poli") || text == "shootout")
		{
			return NpcBehavior.Tiroteo;
		}
		if (text.StartsWith("arras") || text.StartsWith("ramp") || text.StartsWith("rambo") || text.StartsWith("masac"))
		{
			return NpcBehavior.Arrasar;
		}
		if (text.StartsWith("roba") || text.StartsWith("robo") || text.StartsWith("ladr") || text == "steal")
		{
			return NpcBehavior.Robar;
		}
		return null;
	}

	public static string Key(NpcBehavior b)
	{
		return BehaviorKeys[(int)b];
	}

	public static string ChooseName(string source, int count, string[] tiers, int[] from, string gift, string other)
	{
		if (string.Equals(source, "Suscripcion", StringComparison.OrdinalIgnoreCase))
		{
			int num = Math.Max(1, count);
			int num2 = 0;
			for (int i = 1; i < tiers.Length && i < from.Length; i++)
			{
				if (from[i] > 0 && num >= from[i])
				{
					num2 = i;
				}
			}
			return tiers[num2];
		}
		if (string.Equals(source, "RegaloSubs", StringComparison.OrdinalIgnoreCase) || string.Equals(source, "RegaloGrande", StringComparison.OrdinalIgnoreCase))
		{
			return gift;
		}
		return other;
	}

	public static NpcBehavior Resolve(string name, Random rng)
	{
		NpcBehavior? npcBehavior = Parse(name);
		if (npcBehavior.HasValue)
		{
			return npcBehavior.Value;
		}
		return (NpcBehavior)rng.Next(6);
	}

	public static string Subtitle(string source, int count)
	{
		switch (Config.Normalize(source ?? string.Empty))
		{
		case "suscripcion":
			return (count > 1) ? ("SUB " + count + " MESES") : "NUEVO SUB";
		case "regalosubs":
		case "regalogrande":
			return "REGALO " + Math.Max(1, count) + ((count != 1) ? " SUBS" : " SUB");
		case "kicksgifted":
		case "kicksgrandes":
			return Math.Max(1, count) + " KICKS";
		case "primermensaje":
			return "PRIMER MENSAJE";
		case "rewardredeemedevent":
			return "RECOMPENSA";
		case "follow":
			return "FOLLOW";
		case "host":
			return "HOST";
		default:
			return string.Empty;
		}
	}

	public static string Label(NpcBehavior b)
	{
		return b switch
		{
			NpcBehavior.Pasear => "PASEA POR LIBERTY CITY", 
			NpcBehavior.Pelea => "SE AGARRA A PINAS", 
			NpcBehavior.Huir => "SE ESCAPA DE LA POLICIA", 
			NpcBehavior.Tiroteo => "TIROTEO CON LA POLICIA", 
			NpcBehavior.Robar => "SE ROBA UN AUTO", 
			_ => "ARRASA CON TODO", 
		};
	}

	public static bool InvolvesPolice(NpcBehavior b)
	{
		return b != NpcBehavior.Pasear;
	}

	public static NpcBehavior? FromAction(string action)
	{
		if (action == null || !action.StartsWith("Npc", StringComparison.Ordinal) || action.Length <= 3)
		{
			return null;
		}
		return Parse(action.Substring(3));
	}

	public static bool Project(Vector3 p, Vector3 camPos, Vector3 camRot, float fovDeg, float aspect, out float sx, out float sy, out float depth)
	{
		sx = (sy = 0f);
		Vector3 vector = p - camPos;
		Vector3 vector2 = MathX.Forward(camRot);
		Vector3 vector3 = MathX.Right(camRot);
		Vector3 vector4 = Vector3.Cross(vector3, vector2);
		depth = Vector3.Dot(vector, vector2);
		if (depth < 0.5f)
		{
			return false;
		}
		double num = Math.Tan(Math.Max(1.0, Math.Min(170.0, fovDeg)) * 0.5 * Math.PI / 180.0);
		double num2 = num * (double)((!(aspect > 0.1f)) ? 1.7777778f : aspect);
		double num3 = (double)Vector3.Dot(vector, vector3) / ((double)depth * num2);
		double num4 = (double)Vector3.Dot(vector, vector4) / ((double)depth * num);
		sx = (float)(0.5 + 0.5 * num3);
		sy = (float)(0.5 - 0.5 * num4);
		return sx > -0.05f && sx < 1.05f && sy > -0.05f && sy < 1.1f;
	}

	public static float ApparentHeight(float depth, float fovDeg)
	{
		double num = Math.Tan(Math.Max(1.0, fovDeg) * 0.5 * Math.PI / 180.0);
		return (float)(1.8 / (2.0 * Math.Max(0.5, depth) * num));
	}
}
