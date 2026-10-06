using System;

namespace KickChaos;

public static class MenuText
{
	public static float Aspect = 1.7777778f;

	public static void FitRow(ref string label, ref string value, float width)
	{
		float num = 0.013f / Math.Max(0.5f, Aspect);
		int num2 = Math.Max(10, (int)(width / num));
		int num3 = label.Length + ((value.Length > 0) ? (value.Length + 3) : 0);
		if (num3 > num2)
		{
			int num4 = num2 - label.Length - 3;
			if (num4 < 10)
			{
				int n = Math.Max(8, num2 - Math.Min(value.Length, 16) - 3);
				label = Cut(label, n);
				num4 = num2 - label.Length - 3;
			}
			value = Cut(value, Math.Max(6, num4));
		}
	}

	private static string Cut(string s, int n)
	{
		if (s.Length <= n)
		{
			return s;
		}
		if (s.StartsWith("<  ") && s.EndsWith("  >"))
		{
			string text = s.Substring(3, s.Length - 6);
			int num = Math.Max(2, n - 8);
			return "<  " + ((text.Length <= num) ? text : (text.Substring(0, num).TrimEnd(Array.Empty<char>()) + "..")) + "  >";
		}
		return s.Substring(0, Math.Max(2, n - 2)).TrimEnd(Array.Empty<char>()) + "..";
	}
}
