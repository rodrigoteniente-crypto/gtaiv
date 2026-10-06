using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace KickChaos;

public static class Json
{
	public static object Parse(string text)
	{
		if (text == null)
		{
			return null;
		}
		int i = 0;
		object value = ParseValue(text, ref i, 0);
		SkipWs(text, ref i);
		if (i != text.Length)
		{
			throw new FormatException("Datos despues del JSON");
		}
		return value;
	}

	public static Dictionary<string, object> ParseObject(string text)
	{
		try
		{
			return Parse(text) as Dictionary<string, object>;
		}
		catch
		{
			return null;
		}
	}

	public static object Get(object node, string path)
	{
		object value = node;
		string[] array = path.Split(new char[1] { '.' });
		foreach (string key in array)
		{
			if (!(value is Dictionary<string, object> dictionary))
			{
				return null;
			}
			if (!dictionary.TryGetValue(key, out value))
			{
				return null;
			}
		}
		return value;
	}

	public static string GetString(object node, string path)
	{
		object obj = Get(node, path);
		if (obj == null)
		{
			return null;
		}
		if (obj is string)
		{
			return (string)obj;
		}
		if (obj is long integer)
		{
			return integer.ToString(CultureInfo.InvariantCulture);
		}
		if (obj is double num)
		{
			return num.ToString(CultureInfo.InvariantCulture);
		}
		if (obj is bool)
		{
			return (!(bool)obj) ? "false" : "true";
		}
		return null;
	}

	public static int GetInt(object node, string path, int def)
	{
		object obj = Get(node, path);
		if (obj is long integer && integer >= int.MinValue && integer <= int.MaxValue)
		{
			return (int)integer;
		}
		if (obj is double number && number >= int.MinValue && number <= int.MaxValue)
		{
			return (int)number;
		}
		if (obj is string && int.TryParse((string)obj, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
		{
			return result;
		}
		return def;
	}

	public static long GetLong(object node, string path, long def = 0)
	{
		object value = Get(node, path);
		if (value is long integer) return integer;
		if (value is double number && number >= long.MinValue && number < (double)long.MaxValue) return (long)number;
		if (value is string text && long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long result)) return result;
		return def;
	}

	public static List<object> GetList(object node, string path)
	{
		return Get(node, path) as List<object>;
	}

	private static void SkipWs(string s, ref int i)
	{
		while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\r' || s[i] == '\n'))
		{
			i++;
		}
	}

	private static object ParseValue(string s, ref int i, int depth)
	{
		if (depth > 64) throw new FormatException("JSON demasiado anidado");
		SkipWs(s, ref i);
		if (i >= s.Length)
		{
			throw new FormatException("JSON incompleto");
		}
		char c = s[i];
		switch (c)
		{
		case '{':
			return ParseObj(s, ref i, depth + 1);
		case '[':
			return ParseArr(s, ref i, depth + 1);
		case '"':
			return ParseStr(s, ref i);
		case 't':
			if (Match(s, i, "true"))
			{
				i += 4;
				return true;
			}
			break;
		}
		if (c == 'f' && Match(s, i, "false"))
		{
			i += 5;
			return false;
		}
		if (c == 'n' && Match(s, i, "null"))
		{
			i += 4;
			return null;
		}
		return ParseNum(s, ref i);
	}

	private static bool Match(string s, int i, string word)
	{
		return i + word.Length <= s.Length && string.CompareOrdinal(s, i, word, 0, word.Length) == 0;
	}

	private static Dictionary<string, object> ParseObj(string s, ref int i, int depth)
	{
		Dictionary<string, object> dictionary = new Dictionary<string, object>();
		i++;
		SkipWs(s, ref i);
		if (i < s.Length && s[i] == '}')
		{
			i++;
			return dictionary;
		}
		while (true)
		{
			SkipWs(s, ref i);
			if (i >= s.Length || s[i] != '"')
			{
				throw new FormatException("Se esperaba clave");
			}
			string key = ParseStr(s, ref i);
			SkipWs(s, ref i);
			if (i >= s.Length || s[i] != ':')
			{
				throw new FormatException("Se esperaba ':'");
			}
			i++;
			dictionary[key] = ParseValue(s, ref i, depth);
			SkipWs(s, ref i);
			if (i >= s.Length)
			{
				throw new FormatException("Objeto incompleto");
			}
			if (s[i] != ',')
			{
				break;
			}
			i++;
		}
		if (s[i] == '}')
		{
			i++;
			return dictionary;
		}
		throw new FormatException("Se esperaba ',' o '}'");
	}

	private static List<object> ParseArr(string s, ref int i, int depth)
	{
		List<object> list = new List<object>();
		i++;
		SkipWs(s, ref i);
		if (i < s.Length && s[i] == ']')
		{
			i++;
			return list;
		}
		while (true)
		{
			list.Add(ParseValue(s, ref i, depth));
			SkipWs(s, ref i);
			if (i >= s.Length)
			{
				throw new FormatException("Array incompleto");
			}
			if (s[i] != ',')
			{
				break;
			}
			i++;
		}
		if (s[i] == ']')
		{
			i++;
			return list;
		}
		throw new FormatException("Se esperaba ',' o ']'");
	}

	private static string ParseStr(string s, ref int i)
	{
		StringBuilder stringBuilder = new StringBuilder();
		i++;
		while (i < s.Length)
		{
			char c = s[i++];
			switch (c)
			{
			case '"':
				return stringBuilder.ToString();
			default:
				if (c < ' ') throw new FormatException("Caracter de control en string JSON");
				stringBuilder.Append(c);
				continue;
			case '\\':
				break;
			}
			if (i >= s.Length)
			{
				break;
			}
			char c2 = s[i++];
			switch (c2)
			{
			case '"':
				stringBuilder.Append('"');
				break;
			case '\\':
				stringBuilder.Append('\\');
				break;
			case '/':
				stringBuilder.Append('/');
				break;
			case 'b':
				stringBuilder.Append('\b');
				break;
			case 'f':
				stringBuilder.Append('\f');
				break;
			case 'n':
				stringBuilder.Append('\n');
				break;
			case 'r':
				stringBuilder.Append('\r');
				break;
			case 't':
				stringBuilder.Append('\t');
				break;
			case 'u':
				if (i + 4 > s.Length)
				{
					throw new FormatException("Escape \\u incompleto");
				}
				stringBuilder.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
				i += 4;
				break;
			default:
				throw new FormatException("Escape JSON invalido");
			}
		}
		throw new FormatException("String sin cerrar");
	}

	private static object ParseNum(string s, ref int i)
	{
		int start = i;
		if (i < s.Length && s[i] == '-') i++;
		if (i >= s.Length || s[i] < '0' || s[i] > '9') throw new FormatException("Numero JSON invalido");
		if (s[i] == '0') i++;
		else while (i < s.Length && s[i] >= '0' && s[i] <= '9') i++;
		bool integer = true;
		if (i < s.Length && s[i] == '.')
		{
			integer = false;
			i++;
			int digits = i;
			while (i < s.Length && s[i] >= '0' && s[i] <= '9') i++;
			if (i == digits) throw new FormatException("Fraccion JSON incompleta");
		}
		if (i < s.Length && (s[i] == 'e' || s[i] == 'E'))
		{
			integer = false;
			i++;
			if (i < s.Length && (s[i] == '+' || s[i] == '-')) i++;
			int digits = i;
			while (i < s.Length && s[i] >= '0' && s[i] <= '9') i++;
			if (i == digits) throw new FormatException("Exponente JSON incompleto");
		}
		string token = s.Substring(start, i - start);
		if (integer && long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out long result)) return result;
		if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) ||
			double.IsInfinity(number) || double.IsNaN(number)) throw new FormatException("Numero JSON fuera de rango");
		return number;
	}

	// Stable key ordering makes duplicate Pusher deliveries independent of JSON formatting.
	public static string Canonical(object node)
	{
		if (node == null) return "null";
		if (node is string text) return Quote(text);
		if (node is bool flag) return flag ? "true" : "false";
		if (node is long integer) return integer.ToString(CultureInfo.InvariantCulture);
		if (node is double number) return number.ToString("R", CultureInfo.InvariantCulture);
		if (node is Dictionary<string, object> map)
		{
			List<string> keys = new List<string>(map.Keys);
			keys.Sort(StringComparer.Ordinal);
			List<string> entries = new List<string>();
			foreach (string key in keys) entries.Add(Quote(key) + ":" + Canonical(map[key]));
			return "{" + string.Join(",", entries.ToArray()) + "}";
		}
		if (node is List<object> items)
		{
			List<string> entries = new List<string>();
			foreach (object item in items) entries.Add(Canonical(item));
			return "[" + string.Join(",", entries.ToArray()) + "]";
		}
		throw new FormatException("Tipo JSON no soportado");
	}

	public static string Quote(string v)
	{
		if (v == null) return "null";
		StringBuilder stringBuilder = new StringBuilder("\"");
		foreach (char c in v)
		{
			switch (c)
			{
			case '\n':
				stringBuilder.Append("\\n");
				continue;
			case '\r':
				stringBuilder.Append("\\r");
				continue;
			case '\t':
				stringBuilder.Append("\\t");
				continue;
			}
			if (c != '"')
			{
				if (c == '\\')
				{
					stringBuilder.Append("\\\\");
				}
				else if (c < ' ')
				{
					StringBuilder stringBuilder2 = stringBuilder.Append("\\u");
					int num = c;
					stringBuilder2.Append(num.ToString("x4"));
				}
				else
				{
					stringBuilder.Append(c);
				}
			}
			else
			{
				stringBuilder.Append("\\\"");
			}
		}
		return stringBuilder.Append('"').ToString();
	}
}
