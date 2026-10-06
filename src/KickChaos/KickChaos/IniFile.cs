using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace KickChaos;

public class IniFile
{
	public readonly List<IniSection> Sections = new List<IniSection>();

	public string HeaderComment = string.Empty;

	public static IniFile Load(string path)
	{
		IniFile iniFile = new IniFile();
		if (!File.Exists(path))
		{
			return iniFile;
		}
		StringBuilder stringBuilder = new StringBuilder();
		IniSection iniSection = null;
		string[] array = File.ReadAllLines(path, Encoding.UTF8);
		foreach (string text in array)
		{
			string text2 = text.Trim();
			if (text2.Length > 0 && text2[0] == '\ufeff')
			{
				text2 = text2.Substring(1).Trim();
			}
			if (text2.Length == 0 || text2[0] == ';' || text2[0] == '#')
			{
				if (iniSection == null)
				{
					stringBuilder.AppendLine(text);
				}
				continue;
			}
			if (text2[0] == '[' && text2.EndsWith("]"))
			{
				iniSection = new IniSection(text2.Substring(1, text2.Length - 2).Trim());
				iniFile.Sections.Add(iniSection);
				continue;
			}
			int num = text2.IndexOf('=');
			if (num > 0)
			{
				if (iniSection == null)
				{
					iniSection = new IniSection(string.Empty);
					iniFile.Sections.Add(iniSection);
				}
				string key = text2.Substring(0, num).Trim();
				string value = StripInlineComment(text2.Substring(num + 1)).Trim();
				iniSection.Entries.Add(new KeyValuePair<string, string>(key, value));
			}
		}
		iniFile.HeaderComment = stringBuilder.ToString();
		return iniFile;
	}

	public static string StripInlineComment(string v)
	{
		for (int i = 1; i < v.Length; i++)
		{
			if ((v[i] == ';' || v[i] == '#') && (v[i - 1] == ' ' || v[i - 1] == '\t'))
			{
				return v.Substring(0, i);
			}
		}
		return v;
	}

	public IniSection Section(string name)
	{
		foreach (IniSection section in Sections)
		{
			if (string.Equals(section.Name, name, StringComparison.OrdinalIgnoreCase))
			{
				return section;
			}
		}
		return null;
	}

	public string Get(string section, string key, string def = null)
	{
		IniSection iniSection = Section(section);
		if (iniSection == null)
		{
			return def;
		}
		string text = iniSection.Get(key);
		return (!string.IsNullOrEmpty(text)) ? text : def;
	}

	public int GetInt(string section, string key, int def)
	{
		string text = Get(section, key);
		if (text != null && int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
		{
			return result;
		}
		return def;
	}

	public float GetFloat(string section, string key, float def)
	{
		string text = Get(section, key);
		if (text != null && TryFloat(text, out var r))
		{
			return r;
		}
		return def;
	}

	public bool GetBool(string section, string key, bool def)
	{
		string text = Get(section, key);
		if (text == null)
		{
			return def;
		}
		return ParseBool(text, def);
	}

	public static bool ParseBool(string v, bool def)
	{
		switch (v.Trim().ToLowerInvariant())
		{
		case "1":
		case "true":
		case "si":
		case "sí":
		case "yes":
		case "on":
		case "verdadero":
			return true;
		case "0":
		case "false":
		case "no":
		case "off":
		case "falso":
			return false;
		default:
			return def;
		}
	}

	public static bool TryFloat(string v, out float r)
	{
		return float.TryParse(v.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out r);
	}

	public static bool TryVec3(string v, out float x, out float y, out float z)
	{
		x = (y = (z = 0f));
		if (string.IsNullOrEmpty(v))
		{
			return false;
		}
		string[] array = v.Split(new char[4] { ',', ' ', ';', '\t' }, StringSplitOptions.RemoveEmptyEntries);
		if (array.Length < 3)
		{
			return false;
		}
		return TryFloat(array[0], out x) && TryFloat(array[1], out y) && TryFloat(array[2], out z);
	}

	public static string F(float v)
	{
		return v.ToString("0.###", CultureInfo.InvariantCulture);
	}

	public static string V3(float x, float y, float z)
	{
		return F(x) + ", " + F(y) + ", " + F(z);
	}
}
