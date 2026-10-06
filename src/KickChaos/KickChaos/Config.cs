using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace KickChaos;

public class Config
{
	public string Folder;

	public IniFile Ini = new IniFile();

	public bool KickEnabled = true;

	public string Channel = "rodsquare";

	public long ChatroomId = 31759082L;

	public long ChannelId = 32047403L;

	public string PusherKey = "32cbd69e4b950bf97679";

	public string PusherCluster = "us2";

	public string WebsocketUrlOverride = string.Empty;

	public HashSet<string> IgnoredUsers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	public bool OneActionPerMessage = true;

	public float GlobalSpacing = 2f;

	public float UserCooldown = 15f;

	public int MaxQueue = 15;

	public bool EventsBypassCooldown = true;

	public int MaxRepeat = 10;

	public bool RepeatPerGift = true;

	public bool ShowInGameNotice;

	public string ObsFile = "ultimo_evento.txt";

	public int ObsClearSeconds = 12;

	public readonly List<ChatTrigger> Triggers = new List<ChatTrigger>();

	public readonly Dictionary<string, List<ActionStep>> EventMap = new Dictionary<string, List<ActionStep>>(StringComparer.OrdinalIgnoreCase);

	public readonly Dictionary<string, float> ActionCooldowns = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

	public Keys KeyMenu = (Keys)119;

	public Keys KeyDirector = (Keys)120;

	public Keys KeyEditor = (Keys)118;

	public Keys KeyNextCam = (Keys)117;

	public Keys KeyFollowNpc = Keys.F4;

	public bool UseGameAi;

	public Keys KeyReload = (Keys)116;

	public readonly Dictionary<Keys, string> TestKeys = new Dictionary<Keys, string>();

	public static string Normalize(string s)
	{
		if (string.IsNullOrEmpty(s))
		{
			return string.Empty;
		}
		string text = s.ToLowerInvariant().Normalize(NormalizationForm.FormD);
		StringBuilder stringBuilder = new StringBuilder(text.Length);
		string text2 = text;
		foreach (char c in text2)
		{
			if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
			{
				stringBuilder.Append(c);
			}
		}
		return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
	}

	public static bool IsNothing(List<ActionStep> steps)
	{
		if (steps == null)
		{
			return true;
		}
		foreach (ActionStep step in steps)
		{
			if (ActionNames.Canonical(step.Name) != "Nada")
			{
				return false;
			}
		}
		return true;
	}

	public static List<ActionStep> ParseSteps(string text)
	{
		List<ActionStep> list = new List<ActionStep>();
		if (string.IsNullOrEmpty(text))
		{
			return list;
		}
		string[] array = text.Split(new char[1] { '+' });
		foreach (string text2 in array)
		{
			string text3 = text2.Trim();
			if (text3.Length == 0)
			{
				continue;
			}
			ActionStep actionStep = new ActionStep();
			int num = text3.LastIndexOfAny(new char[3] { '*', 'x', 'X' });
			if (num > 0)
			{
				string s = text3.Substring(num + 1).Trim();
				if (int.TryParse(s, out var result) && result > 0)
				{
					actionStep.Repeat = result;
					text3 = text3.Substring(0, num).Trim();
				}
			}
			actionStep.Name = text3;
			list.Add(actionStep);
		}
		return list;
	}

	public static int ParseRole(string r)
	{
		switch (Normalize(r).Trim())
		{
		case "sub":
		case "subs":
		case "suscriptor":
		case "suscriptores":
		case "subscriber":
			return 1;
		case "vip":
			return 2;
		case "mod":
		case "mods":
		case "moderador":
		case "moderadores":
			return 3;
		case "streamer":
		case "yo":
		case "broadcaster":
			return 4;
		default:
			return 0;
		}
	}

	public static Keys ParseKey(string v, Keys def)
	{
		if (string.IsNullOrEmpty(v))
		{
			return def;
		}
		if (Enum.TryParse<Keys>(v.Trim(), ignoreCase: true, out Keys result))
		{
			return result;
		}
		return def;
	}

	public float GetCooldown(string action, float def)
	{
		float value;
		return (!ActionCooldowns.TryGetValue(action, out value)) ? def : value;
	}

	public float P(string action, string key, float def)
	{
		return Ini.GetFloat("Accion." + action, key, def);
	}

	public int PI(string action, string key, int def)
	{
		return Ini.GetInt("Accion." + action, key, def);
	}

	public string PS(string action, string key, string def)
	{
		return Ini.Get("Accion." + action, key, def);
	}

	public static Config Load(string folder, List<string> warnings)
	{
		Config config = new Config();
		config.Folder = folder;
		config.Ini = IniFile.Load(Path.Combine(folder, "config.ini"));
		IniFile ini = config.Ini;
		config.KickEnabled = ini.GetBool("Kick", "Conectar", def: true);
		config.Channel = ini.Get("Kick", "Canal", config.Channel).Trim().ToLowerInvariant();
		if (long.TryParse(ini.Get("Kick", "ChatroomId", string.Empty).Trim(), out var result))
		{
			config.ChatroomId = result;
		}
		if (long.TryParse(ini.Get("Kick", "ChannelId", string.Empty).Trim(), out result))
		{
			config.ChannelId = result;
		}
		config.PusherKey = ini.Get("Kick", "PusherKey", config.PusherKey).Trim();
		config.PusherCluster = ini.Get("Kick", "PusherCluster", config.PusherCluster).Trim();
		config.WebsocketUrlOverride = ini.Get("Kick", "UrlWebsocket", string.Empty).Trim();
		string[] array = ini.Get("Kick", "IgnorarUsuarios", string.Empty).Split(new char[2] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
		foreach (string text in array)
		{
			config.IgnoredUsers.Add(text.Trim().TrimStart(new char[1] { '@' }));
		}
		config.OneActionPerMessage = ini.GetBool("Reglas", "UnaAccionPorMensaje", def: true);
		config.GlobalSpacing = Math.Max(0f, ini.GetFloat("Reglas", "SeparacionEntreAcciones", 2f));
		config.UserCooldown = Math.Max(0f, ini.GetFloat("Reglas", "CooldownPorUsuario", 15f));
		config.MaxQueue = Math.Max(1, ini.GetInt("Reglas", "ColaMaxima", 15));
		config.EventsBypassCooldown = ini.GetBool("Reglas", "EventosIgnoranCooldown", def: true);
		config.MaxRepeat = Math.Max(1, ini.GetInt("Reglas", "MaxRepeticiones", 10));
		config.RepeatPerGift = ini.GetBool("Reglas", "RepetirPorCadaRegalo", def: true);
		config.ShowInGameNotice = ini.GetBool("Reglas", "AvisoEnJuego", def: false);
		config.ObsFile = ini.Get("Reglas", "ArchivoOBS", "ultimo_evento.txt").Trim();
		config.ObsClearSeconds = ini.GetInt("Reglas", "BorrarTextoOBSTras", 12);
		IniSection iniSection = ini.Section("Cooldowns");
		if (iniSection != null)
		{
			foreach (KeyValuePair<string, string> entry in iniSection.Entries)
			{
				if (IniFile.TryFloat(entry.Value, out var r))
				{
					config.ActionCooldowns[ActionNames.Canonical(entry.Key) ?? entry.Key.Trim()] = r;
				}
				else
				{
					warnings.Add("Cooldown invalido: " + entry.Key + " = " + entry.Value);
				}
			}
		}
		IniSection iniSection2 = ini.Section("Comandos");
		if (iniSection2 != null)
		{
			foreach (KeyValuePair<string, string> entry2 in iniSection2.Entries)
			{
				string[] array2 = entry2.Value.Split(new char[1] { '|' });
				ChatTrigger chatTrigger = new ChatTrigger();
				chatTrigger.RawKey = entry2.Key.Trim();
				chatTrigger.Keyword = Normalize(entry2.Key).Trim();
				if (chatTrigger.Keyword.Length == 0)
				{
					continue;
				}
				chatTrigger.IsCommand = chatTrigger.Keyword.StartsWith("!");
				chatTrigger.ActionText = array2[0].Trim();
				chatTrigger.Steps = ParseSteps(chatTrigger.ActionText);
				if (chatTrigger.Steps.Count == 0)
				{
					warnings.Add("Comando sin accion: " + entry2.Key);
					continue;
				}
				if (array2.Length > 1)
				{
					chatTrigger.RequiredLevel = ParseRole(array2[1]);
				}
				if (array2.Length > 2 && IniFile.TryFloat(array2[2], out var r2))
				{
					chatTrigger.CooldownSeconds = r2;
				}
				config.Triggers.Add(chatTrigger);
			}
		}
		IniSection iniSection3 = ini.Section("Eventos");
		if (iniSection3 != null)
		{
			foreach (KeyValuePair<string, string> entry3 in iniSection3.Entries)
			{
				List<ActionStep> list = ParseSteps(entry3.Value);
				if (list.Count > 0)
				{
					config.EventMap[entry3.Key.Trim()] = list;
				}
			}
		}
		config.KeyMenu = ParseKey(ini.Get("Teclas", "Menu"), (Keys)119);
		config.KeyDirector = ParseKey(ini.Get("Teclas", "Director"), (Keys)120);
		config.KeyEditor = ParseKey(ini.Get("Teclas", "Editor"), (Keys)118);
		config.KeyNextCam = ParseKey(ini.Get("Teclas", "SiguienteCamara"), (Keys)117);
		config.KeyFollowNpc = ParseKey(ini.Get("Teclas", "SeguirNpc"), Keys.F4);
		config.UseGameAi = string.Equals(ini.Get("Suscriptor", "IA", "Juego").Trim(), "Juego", StringComparison.OrdinalIgnoreCase);
		config.KeyReload = ParseKey(ini.Get("Teclas", "Recargar"), (Keys)116);
		IniSection iniSection4 = ini.Section("TeclasDePrueba");
		if (iniSection4 != null)
		{
			foreach (KeyValuePair<string, string> entry4 in iniSection4.Entries)
			{
				Keys val = ParseKey(entry4.Key, (Keys)0);
				if ((int)val == 0)
				{
					warnings.Add("Tecla desconocida: " + entry4.Key);
				}
				else
				{
					config.TestKeys[val] = entry4.Value.Trim();
				}
			}
		}
		return config;
	}
}
