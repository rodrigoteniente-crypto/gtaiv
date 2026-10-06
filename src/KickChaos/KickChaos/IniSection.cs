using System;
using System.Collections.Generic;

namespace KickChaos;

public class IniSection
{
	public string Name;

	public readonly List<KeyValuePair<string, string>> Entries = new List<KeyValuePair<string, string>>();

	public IniSection(string name)
	{
		Name = name;
	}

	public string Get(string key, string def = null)
	{
		for (int num = Entries.Count - 1; num >= 0; num--)
		{
			if (string.Equals(Entries[num].Key, key, StringComparison.OrdinalIgnoreCase))
			{
				return Entries[num].Value;
			}
		}
		return def;
	}

	public bool Has(string key)
	{
		return Get(key) != null;
	}
}
