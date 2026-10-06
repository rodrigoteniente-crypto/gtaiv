using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace KickChaos;

public class IniDoc
{
	private readonly List<string> lines = new List<string>();

	private readonly string path;

	private IniDoc(string path)
	{
		this.path = path;
	}

	public static IniDoc Load(string path)
	{
		IniDoc iniDoc = new IniDoc(path);
		if (File.Exists(path))
		{
			string[] array = File.ReadAllLines(path, Encoding.UTF8);
			foreach (string text in array)
			{
				iniDoc.lines.Add(text.TrimStart(new char[1] { '\ufeff' }));
			}
		}
		return iniDoc;
	}

	public void Save()
	{
		string sourceFileName = path + ".tmp";
		File.WriteAllText(sourceFileName, string.Join("\r\n", lines.ToArray()) + "\r\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
		try
		{
			if (File.Exists(path))
			{
				File.Replace(sourceFileName, path, null);
			}
			else
			{
				File.Move(sourceFileName, path);
			}
		}
		catch
		{
			File.Copy(sourceFileName, path, overwrite: true);
			try
			{
				File.Delete(sourceFileName);
			}
			catch
			{
			}
		}
	}

	private static bool IsSection(string l, out string name)
	{
		string text = l.Trim();
		name = null;
		if (text.StartsWith("[") && text.EndsWith("]"))
		{
			name = text.Substring(1, text.Length - 2).Trim();
			return true;
		}
		return false;
	}

	private static bool IsKey(string l, string key)
	{
		string text = l.Trim();
		if (text.Length == 0 || text[0] == ';' || text[0] == '#' || text[0] == '[')
		{
			return false;
		}
		int num = text.IndexOf('=');
		if (num <= 0)
		{
			return false;
		}
		return string.Equals(text.Substring(0, num).Trim(), key, StringComparison.OrdinalIgnoreCase);
	}

	private void SectionRange(string section, out int header, out int end)
	{
		header = -1;
		end = lines.Count;
		for (int i = 0; i < lines.Count; i++)
		{
			if (IsSection(lines[i], out var name))
			{
				if (header >= 0)
				{
					end = i;
					break;
				}
				if (string.Equals(name, section, StringComparison.OrdinalIgnoreCase))
				{
					header = i;
				}
			}
		}
	}

	public void Set(string section, string key, string value)
	{
		SectionRange(section, out var header, out var end);
		if (header < 0)
		{
			if (lines.Count > 0 && lines[lines.Count - 1].Trim().Length > 0)
			{
				lines.Add(string.Empty);
			}
			lines.Add("[" + section + "]");
			lines.Add(key + " = " + value);
			return;
		}
		for (int num = end - 1; num > header; num--)
		{
			if (IsKey(lines[num], key))
			{
				string text = lines[num];
				int num2 = text.IndexOf('=');
				string text2 = text.Substring(num2 + 1);
				string text3 = IniFile.StripInlineComment(text2);
				string text4 = text2.Substring(text3.Length);
				string text5 = ((text2.Length - text2.TrimStart(Array.Empty<char>()).Length <= 0) ? " " : " ");
				string text6 = text5 + value;
				if (text4.Length > 0)
				{
					int count = Math.Max(1, text3.Length - text6.Length);
					text6 += new string(' ', count);
				}
				lines[num] = text.Substring(0, num2 + 1) + text6 + text4.TrimEnd(Array.Empty<char>());
				return;
			}
		}
		int index = header + 1;
		for (int i = header + 1; i < end; i++)
		{
			string text7 = lines[i].Trim();
			if (text7.Length > 0 && text7[0] != ';' && text7[0] != '#')
			{
				index = i + 1;
			}
		}
		lines.Insert(index, key + " = " + value);
	}

	public void Remove(string section, string key)
	{
		SectionRange(section, out var header, out var end);
		if (header < 0)
		{
			return;
		}
		for (int num = end - 1; num > header; num--)
		{
			if (IsKey(lines[num], key))
			{
				lines.RemoveAt(num);
			}
		}
	}

	public void RenameKey(string section, string oldKey, string newKey)
	{
		SectionRange(section, out var header, out var end);
		if (header < 0)
		{
			return;
		}
		for (int i = header + 1; i < end; i++)
		{
			if (IsKey(lines[i], oldKey))
			{
				int startIndex = lines[i].IndexOf('=');
				lines[i] = newKey + " " + lines[i].Substring(startIndex);
			}
		}
	}
}
