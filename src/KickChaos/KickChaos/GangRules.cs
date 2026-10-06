using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;

namespace KickChaos;

public static class GangRules
{
	public class VoteBox
	{
		private readonly Dictionary<string, int> byUser = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

		private readonly int[] counts;

		private readonly double[] reachedAt;

		public double Start = -1.0;

		public double End = -1.0;

		public bool Open => Start >= 0.0;

		public int Total => byUser.Count;

		public VoteBox(int options)
		{
			counts = new int[options];
			reachedAt = new double[options];
		}

		public int Count(int option)
		{
			return (option >= 0 && option < counts.Length) ? counts[option] : 0;
		}

		public void Add(string user, int option, double now, double seconds)
		{
			if (option < 0 || option >= counts.Length)
			{
				return;
			}
			if (!Open)
			{
				Start = now;
				End = now + Math.Max(1.0, seconds);
			}
			string text = (user ?? string.Empty).Trim();
			if (text.Length == 0)
			{
				text = "?" + byUser.Count;
			}
			if (byUser.TryGetValue(text, out var value))
			{
				if (value == option)
				{
					return;
				}
				counts[value]--;
			}
			byUser[text] = option;
			counts[option]++;
			reachedAt[option] = now;
		}

		public bool Due(double now)
		{
			return Open && now >= End;
		}

		public int Winner()
		{
			int num = -1;
			for (int i = 0; i < counts.Length; i++)
			{
				if (counts[i] > 0 && (num < 0 || counts[i] > counts[num] || (counts[i] == counts[num] && reachedAt[i] < reachedAt[num])))
				{
					num = i;
				}
			}
			return num;
		}

		public void Reset()
		{
			byUser.Clear();
			for (int i = 0; i < counts.Length; i++)
			{
				counts[i] = 0;
				reachedAt[i] = 0.0;
			}
			Start = (End = -1.0);
		}
	}

	public const int CmdRobarAuto = 0;

	public const int CmdAtacar = 1;

	public const int CmdHuir = 2;

	public const int CmdComer = 3;

	public const int CmdPinas = 4;

	public const int CmdBanda = 5;

	public static readonly string[] CmdKeys = new string[6] { "robarauto", "atacar", "huir", "comer", "pinas", "banda" };

	public static readonly string[] CmdLabels = new string[6] { "!robarauto (roba un auto)", "!atacar (contra la policia)", "!huir (escapa en auto)", "!comer (se cura)", "!pinas (contra la gente)", "!banda (busca a la otra banda)" };

	private static readonly string[][] CmdAliases = new string[6][]
	{
		new string[10] { "robarauto", "robar", "roba", "robo", "auto", "robaauto", "robarunauto", "robaunauto", "robarcoche", "coche" },
		new string[10] { "atacar", "ataca", "ataque", "tiros", "tiroteo", "policia", "poli", "polis", "matar", "mata" },
		new string[9] { "huir", "huye", "huyan", "escapar", "escapa", "escape", "rajar", "raja", "fuga" },
		new string[8] { "comer", "come", "comida", "curar", "cura", "curarse", "hamburguesa", "morfar" },
		new string[6] { "pinas", "pina", "pelea", "pelear", "trompadas", "pinazos" },
		new string[6] { "banda", "bandas", "guerra", "rival", "rivales", "buscar" }
	};

	public static readonly string[] DefaultSlots = new string[3] { "robarauto", "atacar", "huir" };

	public const int ModeSub = 0;

	public const int ModeVote = 1;

	public const int ModeFirst = 2;

	public static readonly string[] ModeKeys = new string[3] { "Suscriptor", "Votacion", "Chat" };

	public static readonly string[] ModeLabels = new string[3] { "Solo el suscriptor", "Vota el chat", "El chat, el primero" };

	public const int UnlockOnGrow = 0;

	public const int UnlockAlways = 1;

	public const int UnlockNever = 2;

	public static readonly string[] UnlockKeys = new string[3] { "AlDuplicarse", "Siempre", "Nunca" };

	public static readonly string[] UnlockLabels = new string[3] { "Cuando se duplica", "Desde que aparece", "Nunca" };

	public const int GrowCops = 0;

	public const int GrowCopsAndGangs = 1;

	public const int GrowAnyone = 2;

	public static readonly string[] GrowKeys = new string[3] { "Policias", "PoliciasYBandas", "Cualquiera" };

	public static readonly string[] GrowLabels = new string[3] { "Matar un policia", "Policia u otra banda", "Matar a cualquiera" };

	private static readonly Regex Emote = new Regex("\\[emote:\\d+:([^\\]]*)\\]", RegexOptions.Compiled);

	public static int CommandIndex(string word)
	{
		string text = Clean(word);
		if (text.Length == 0)
		{
			return -1;
		}
		for (int i = 0; i < CmdAliases.Length; i++)
		{
			string[] array = CmdAliases[i];
			foreach (string text2 in array)
			{
				if (text2 == text)
				{
					return i;
				}
			}
		}
		return -1;
	}

	public static int ParseKey(string s)
	{
		return CommandIndex(s);
	}

	public static int ParseChoice(string message, int[] slots)
	{
		if (string.IsNullOrEmpty(message) || slots == null || slots.Length == 0)
		{
			return -1;
		}
		string text = message.Trim();
		if (!text.StartsWith("!"))
		{
			return -1;
		}
		text = text.Substring(1).TrimStart(Array.Empty<char>());
		if (text.Length == 0)
		{
			return -1;
		}
		if (char.IsDigit(text[0]) && (text.Length == 1 || !char.IsLetterOrDigit(text[1])))
		{
			int num = text[0] - 49;
			return (num < 0 || num >= slots.Length) ? (-1) : num;
		}
		string[] array = text.Split(new char[2] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
		List<string> list = new List<string>();
		list.Add(array[0]);
		if (array.Length > 1)
		{
			list.Add(array[0] + array[1]);
		}
		if (array.Length > 2)
		{
			list.Add(array[0] + array[1] + array[2]);
		}
		for (int num2 = list.Count - 1; num2 >= 0; num2--)
		{
			int num3 = CommandIndex(list[num2]);
			if (num3 >= 0)
			{
				for (int i = 0; i < slots.Length; i++)
				{
					if (slots[i] == num3)
					{
						return i;
					}
				}
			}
		}
		return -1;
	}

	private static string Clean(string s)
	{
		string text = Config.Normalize(s ?? string.Empty);
		StringBuilder stringBuilder = new StringBuilder(text.Length);
		string text2 = text;
		foreach (char c in text2)
		{
			if (c >= 'a' && c <= 'z')
			{
				stringBuilder.Append(c);
			}
		}
		return stringBuilder.ToString();
	}

	public static void Dedupe(int[] slots)
	{
		for (int i = 1; i < slots.Length; i++)
		{
			bool flag = false;
			for (int j = 0; j < i; j++)
			{
				if (slots[j] == slots[i])
				{
					flag = true;
				}
			}
			if (!flag)
			{
				continue;
			}
			for (int k = 0; k < CmdKeys.Length; k++)
			{
				bool flag2 = false;
				for (int l = 0; l < slots.Length; l++)
				{
					if (l != i && slots[l] == k)
					{
						flag2 = true;
					}
				}
				if (!flag2)
				{
					slots[i] = k;
					break;
				}
			}
		}
	}

	public static string ChoicesLine(int[] slots)
	{
		List<string> list = new List<string>();
		foreach (int num in slots)
		{
			if (num >= 0 && num < CmdKeys.Length)
			{
				list.Add("!" + CmdKeys[num]);
			}
		}
		return string.Join("   ", list.ToArray());
	}

	public static int ParseMode(string s)
	{
		string text = Clean(s);
		if (text.StartsWith("vot"))
		{
			return 1;
		}
		if (text.StartsWith("chat") || text.StartsWith("prim") || text.StartsWith("todos"))
		{
			return 2;
		}
		return 0;
	}

	public static int ParseUnlock(string s)
	{
		string text = Clean(s);
		if (text.StartsWith("siem") || text.StartsWith("desde") || text == "si")
		{
			return 1;
		}
		if (text.StartsWith("nun") || text == "no")
		{
			return 2;
		}
		return 0;
	}

	public static int ParseGrow(string s)
	{
		string text = Clean(s);
		if (text.StartsWith("cual") || text.StartsWith("todo"))
		{
			return 2;
		}
		if (text.Contains("banda"))
		{
			return 1;
		}
		return 0;
	}

	public static bool Grows(int rule, bool cop, bool gang)
	{
		if (rule == 2)
		{
			return true;
		}
		if (cop)
		{
			return true;
		}
		return gang && rule == 1;
	}

	public static string VoteLine(int[] slots, VoteBox box, double now)
	{
		List<string> list = new List<string>();
		for (int i = 0; i < slots.Length; i++)
		{
			if (slots[i] >= 0 && slots[i] < CmdKeys.Length)
			{
				list.Add("!" + CmdKeys[slots[i]] + " " + box.Count(i));
			}
		}
		int num = (int)Math.Ceiling(Math.Max(0.0, box.End - now));
		return string.Join("   ", list.ToArray()) + "   (" + num + "s)";
	}

	public static string Clock(double seconds)
	{
		int num = (int)Math.Ceiling(Math.Max(0.0, seconds));
		return num / 60 + ":" + (num % 60).ToString("00");
	}

	public static string[] Bubble(string message, int width, int lines)
	{
		string text = Emote.Replace(message ?? string.Empty, (Match m) => m.Groups[1].Value);
		StringBuilder stringBuilder = new StringBuilder(text.Length);
		foreach (char c in text)
		{
			if (char.IsHighSurrogate(c) || char.IsLowSurrogate(c))
			{
				continue;
			}
			if (c < ' ' || (c >= '\u2000' && c <= '⯿') || c >= '\ufe00')
			{
				if (c == '\t')
				{
					stringBuilder.Append(' ');
				}
			}
			else
			{
				stringBuilder.Append(c);
			}
		}
		string text2 = Regex.Replace(stringBuilder.ToString(), "\\s+", " ").Trim();
		if (text2.Length == 0)
		{
			return new string[0];
		}
		width = Math.Max(8, width);
		lines = Math.Max(1, lines);
		List<string> list = new List<string>();
		string[] array = text2.Split(new char[1] { ' ' });
		string text3 = string.Empty;
		int num2 = 0;
		while (num2 < array.Length)
		{
			string text4 = array[num2];
			if (text4.Length > width)
			{
				if (text3.Length > 0)
				{
					list.Add(text3);
					text3 = string.Empty;
					if (list.Count >= lines)
					{
						break;
					}
				}
				list.Add(text4.Substring(0, width));
				array[num2] = text4.Substring(width);
				if (list.Count >= lines)
				{
					break;
				}
				continue;
			}
			string text5 = ((text3.Length != 0) ? (text3 + " " + text4) : text4);
			if (text5.Length <= width)
			{
				text3 = text5;
				num2++;
				continue;
			}
			list.Add(text3);
			text3 = string.Empty;
			if (list.Count < lines)
			{
				continue;
			}
			break;
		}
		bool flag = num2 < array.Length;
		if (list.Count < lines && text3.Length > 0)
		{
			list.Add(text3);
			text3 = string.Empty;
		}
		else if (text3.Length > 0)
		{
			flag = true;
		}
		if (list.Count > lines)
		{
			list.RemoveRange(lines, list.Count - lines);
		}
		if (flag && list.Count > 0)
		{
			string text6 = list[list.Count - 1];
			if (text6.Length > width - 2)
			{
				text6 = text6.Substring(0, width - 2).TrimEnd(Array.Empty<char>());
			}
			list[list.Count - 1] = text6 + "..";
		}
		return list.ToArray();
	}

	public static double BubbleSeconds(string message)
	{
		int length = (message ?? string.Empty).Length;
		return Math.Max(5.0, Math.Min(12.0, 3.5 + (double)length * 0.09));
	}

	public static Vector3 CoverBehind(Vector3 car, Vector3 threat, float gap)
	{
		Vector3 vector = new Vector3(car.X - threat.X, car.Y - threat.Y, 0f);
		float num = vector.Length();
		if (num < 0.01f)
		{
			vector = new Vector3(1f, 0f, 0f);
		}
		else
		{
			vector /= num;
		}
		return new Vector3(car.X + vector.X * gap, car.Y + vector.Y * gap, car.Z);
	}

	public static float CoverScore(Vector3 me, Vector3 car, Vector3 threat, float maxRun, bool advance)
	{
		Vector3 vector = CoverBehind(car, threat, 2.3f);
		float num = Flat(me, vector);
		if (num > maxRun || num < 1.2f)
		{
			return float.MaxValue;
		}
		float num2 = Flat(me, threat);
		float num3 = Flat(vector, threat);
		if (num3 < 6f)
		{
			return float.MaxValue;
		}
		if (num3 > num2 + 6f)
		{
			return float.MaxValue;
		}
		float num4 = num;
		if (advance)
		{
			num4 += (num3 - num2) * 1.5f;
		}
		return num4;
	}

	public static float Flat(Vector3 a, Vector3 b)
	{
		float num = a.X - b.X;
		float num2 = a.Y - b.Y;
		return (float)Math.Sqrt(num * num + num2 * num2);
	}

	public static Vector3 Strafe(Vector3 me, Vector3 threat, float side, float forward)
	{
		Vector3 vector = new Vector3(threat.X - me.X, threat.Y - me.Y, 0f);
		float num = vector.Length();
		if (num < 0.01f)
		{
			vector = new Vector3(1f, 0f, 0f);
		}
		else
		{
			vector /= num;
		}
		Vector3 vector2 = new Vector3(0f - vector.Y, vector.X, 0f);
		return new Vector3(me.X + vector2.X * side + vector.X * forward, me.Y + vector2.Y * side + vector.Y * forward, me.Z);
	}

	public static double HotspotWeight(float dist)
	{
		if (dist < 120f)
		{
			return 6.0;
		}
		if (dist < 250f)
		{
			return 2.5;
		}
		if (dist < 450f)
		{
			return 1.2;
		}
		return 1.0;
	}

	public static float ShotDistance(Vector3 hot, Vector3 camPos, Vector3 look)
	{
		return Math.Min(Flat(hot, camPos), Flat(hot, look));
	}

	public static string MemberName(string leader)
	{
		return "banda de " + leader;
	}

	public static string GangTitle(string leader, int members)
	{
		return "LA BANDA DE " + (leader ?? string.Empty).ToUpperInvariant() + ((members <= 1) ? string.Empty : (" (" + members + ")"));
	}
}
