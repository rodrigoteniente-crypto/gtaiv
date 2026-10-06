using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace KickChaos;

public class TriggerEngine
{
	private Config cfg;

	private readonly Dictionary<string, DateTime> userLast = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

	private readonly Dictionary<string, DateTime> actionLast = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

	private readonly HashSet<string> chatters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	private readonly Queue<DateTime> chatTimes = new Queue<DateTime>();

	private DateTime lastHype = DateTime.MinValue;

	private static readonly Regex EmoteRx = new Regex("\\[emote:\\d+:[^\\]]*\\]", RegexOptions.Compiled);

	private static readonly Regex NonWordRx = new Regex("[^\\p{L}\\p{N}!]+", RegexOptions.Compiled);

	private static readonly Regex TrailingBangRx = new Regex("(?<=[\\p{L}\\p{N}])!+", RegexOptions.Compiled);

	public const float DefaultActionCooldown = 8f;

	public TriggerEngine(Config cfg)
	{
		this.cfg = cfg;
	}

	public void SetConfig(Config c)
	{
		cfg = c;
	}

	public static string CleanMessage(string text)
	{
		string s = EmoteRx.Replace(text ?? string.Empty, " ");
		s = Config.Normalize(s);
		s = NonWordRx.Replace(s, " ");
		s = TrailingBangRx.Replace(s, " ");
		s = Regex.Replace(s, "\\s+", " ");
		return " " + s.Trim() + " ";
	}

	public static bool Matches(ChatTrigger trig, string cleaned)
	{
		string text = CleanMessage(trig.Keyword).Trim();
		if (text.Length == 0)
		{
			return false;
		}
		if (trig.IsCommand)
		{
			return cleaned.StartsWith(" " + text + " ", StringComparison.Ordinal);
		}
		return cleaned.IndexOf(" " + text + " ", StringComparison.Ordinal) >= 0;
	}

	private bool Mapped(string key, out List<ActionStep> steps)
	{
		return cfg.EventMap.TryGetValue(key, out steps) && !Config.IsNothing(steps);
	}

	private void AddSynthetic(List<PendingAction> result, string key, string user, int count, DateTime now, bool bypass)
	{
		if (Mapped(key, out var steps))
		{
			string key2 = "evento:" + key + ":" + CooldownKey(steps);
			float num = CooldownFor(steps);
			if (bypass || !(num > 0f) || !actionLast.TryGetValue(key2, out var value) || !((now - value).TotalSeconds < (double)num))
			{
				actionLast[key2] = now;
				result.Add(new PendingAction
				{
					Steps = steps,
					User = user,
					Source = key,
					FromEvent = true,
					Count = Math.Max(1, count)
				});
			}
		}
	}

	private float CooldownFor(List<ActionStep> steps)
	{
		float num = 0f;
		foreach (ActionStep step in steps)
		{
			num = Math.Max(num, cfg.GetCooldown(ActionNames.Canonical(step.Name) ?? step.Name, 8f));
		}
		return num;
	}

	private string CooldownKey(List<ActionStep> steps)
	{
		return string.Join("+", steps.ConvertAll((ActionStep s) => (ActionNames.Canonical(s.Name) ?? s.Name).ToLowerInvariant()).ToArray());
	}

	public List<PendingAction> Process(KickEvent e, DateTime now, out string why)
	{
		why = null;
		List<PendingAction> list = new List<PendingAction>();
		if (e == null)
		{
			return list;
		}
		e.User = e.User ?? string.Empty;
		if (!string.IsNullOrEmpty(e.User) && cfg.IgnoredUsers.Contains(e.User))
		{
			why = "usuario ignorado";
			return list;
		}
		if (e.Kind == KickEventKind.Chat)
		{
			string cleaned = CleanMessage(e.Text);
			bool bypassUserCooldown = e.Level >= 4 || e.Simulated;
			// Evaluate once per message so PermitirVariasAcciones can execute every
			// matching action; writing userLast after the first match is not a new message.
			bool userCoolingDown = !bypassUserCooldown && cfg.UserCooldown > 0f &&
				userLast.TryGetValue(e.User, out DateTime lastUserMessage) &&
				(now - lastUserMessage).TotalSeconds < cfg.UserCooldown;
			bool flag = false;
			foreach (ChatTrigger trigger in cfg.Triggers)
			{
				if (Config.IsNothing(trigger.Steps) || !Matches(trigger, cleaned))
				{
					continue;
				}
				flag = true;
				if (e.Level < trigger.RequiredLevel)
				{
					why = "'" + trigger.Keyword + "' requiere nivel " + trigger.RequiredLevel;
					continue;
				}
				bool flag2 = e.Level >= 4 || e.Simulated;
				if (userCoolingDown)
				{
					why = e.User + " en cooldown";
					if (!cfg.OneActionPerMessage)
					{
						continue;
					}
					break;
				}
				float num = ((!(trigger.CooldownSeconds >= 0f)) ? CooldownFor(trigger.Steps) : trigger.CooldownSeconds);
				string key = CooldownKey(trigger.Steps);
				if (!flag2 && num > 0f && actionLast.TryGetValue(key, out DateTime value) && (now - value).TotalSeconds < (double)num)
				{
					why = "'" + trigger.ActionText + "' en cooldown (" + Math.Ceiling((double)num - (now - value).TotalSeconds) + " s)";
					if (cfg.OneActionPerMessage)
					{
						break;
					}
				}
				else
				{
					actionLast[key] = now;
					userLast[e.User] = now;
					list.Add(new PendingAction
					{
						Steps = trigger.Steps,
						User = e.User,
						Source = trigger.Keyword
					});
					if (cfg.OneActionPerMessage)
					{
						break;
					}
				}
			}
			if (!string.IsNullOrEmpty(e.User) && chatters.Add(e.User) && e.Level < 4)
			{
				AddSynthetic(list, "PrimerMensaje", e.User, 1, now, e.Simulated);
			}
			float num2 = Math.Max(3f, cfg.Ini.GetFloat("Reglas", "ChatAFullSegundos", 20f));
			int num3 = Math.Max(2, cfg.Ini.GetInt("Reglas", "ChatAFullMensajes", 15));
			float num4 = Math.Max(0f, cfg.Ini.GetFloat("Reglas", "ChatAFullCooldown", 120f));
			chatTimes.Enqueue(now);
			while (chatTimes.Count > 0 && (now - chatTimes.Peek()).TotalSeconds > (double)num2)
			{
				chatTimes.Dequeue();
			}
			if (chatTimes.Count >= num3 && (now - lastHype).TotalSeconds >= (double)num4 && Mapped("ChatAFull", out var _))
			{
				lastHype = now;
				chatTimes.Clear();
				AddSynthetic(list, "ChatAFull", "el chat", num3, now, bypass: true);
			}
			if (!flag && cfg.EventMap.TryGetValue("Mensaje", out var value2) && !Config.IsNothing(value2))
			{
				bool flag3 = e.Level >= 4 || e.Simulated;
				if (!flag3 && cfg.UserCooldown > 0f && userLast.TryGetValue(e.User, out var value3) && (now - value3).TotalSeconds < (double)cfg.UserCooldown)
				{
					return list;
				}
				float num5 = CooldownFor(value2);
				string key2 = "mensaje:" + CooldownKey(value2);
				if (!flag3 && num5 > 0f && actionLast.TryGetValue(key2, out value3) && (now - value3).TotalSeconds < (double)num5)
				{
					return list;
				}
				actionLast[key2] = now;
				userLast[e.User] = now;
				list.Add(new PendingAction
				{
					Steps = value2,
					User = e.User,
					Source = "mensaje"
				});
			}
			return list;
		}
		string text = e.Kind switch
		{
			KickEventKind.Subscription => "Suscripcion", 
			KickEventKind.GiftedSubs => "RegaloSubs", 
			KickEventKind.Follow => "Follow", 
			KickEventKind.Host => "Host", 
			KickEventKind.Ban => "Ban", 
			_ => e.EventName, 
		};
		if (e.Kind == KickEventKind.GiftedSubs && e.Count >= Math.Max(2, cfg.Ini.GetInt("Reglas", "RegaloGrandeDesde", 5)) && Mapped("RegaloGrande", out var steps2))
		{
			text = "RegaloGrande";
		}
		else if (string.Equals(e.EventName, "KicksGifted", StringComparison.OrdinalIgnoreCase) && e.Count >= Math.Max(1, cfg.Ini.GetInt("Reglas", "KicksGrandesDesde", 100)) && Mapped("KicksGrandes", out steps2))
		{
			text = "KicksGrandes";
		}
		if ((!cfg.EventMap.TryGetValue(text, out var value4) && !cfg.EventMap.TryGetValue(e.EventName, out value4)) || Config.IsNothing(value4))
		{
			why = "evento '" + text + "' sin accion asignada";
			return list;
		}
		if (!cfg.EventsBypassCooldown && !e.Simulated)
		{
			float num6 = CooldownFor(value4);
			string key3 = CooldownKey(value4);
			if (num6 > 0f && actionLast.TryGetValue(key3, out var value5) && (now - value5).TotalSeconds < (double)num6)
			{
				why = "evento en cooldown";
				return list;
			}
			actionLast[key3] = now;
		}
		int multiplier = 1;
		if (e.Kind == KickEventKind.GiftedSubs && cfg.RepeatPerGift)
		{
			multiplier = Math.Min(Math.Max(1, e.Count), cfg.MaxRepeat);
		}
		if (text == "RegaloGrande" || text == "KicksGrandes")
		{
			multiplier = 1;
		}
		if (e.Kind == KickEventKind.GiftedSubs && e.Recipients.Count > 0)
		{
			List<ActionStep> npcSteps = value4.FindAll(step =>
				(ActionNames.Canonical(step.Name) ?? step.Name).StartsWith("Npc", StringComparison.Ordinal));
			if (npcSteps.Count > 0)
			{
				List<ActionStep> otherSteps = value4.FindAll(step => !npcSteps.Contains(step));
				if (otherSteps.Count > 0)
				{
					list.Add(new PendingAction { Steps = otherSteps, Multiplier = multiplier,
						User = e.User, Source = text, FromEvent = true, Count = Math.Max(1, e.Count) });
				}
				int added = 0;
				foreach (string recipient in e.Recipients)
				{
					if (cfg.IgnoredUsers.Contains(recipient)) continue;
					list.Add(new PendingAction { Steps = npcSteps, User = recipient,
						Source = text, FromEvent = true, Count = 1 });
					if (++added >= Math.Max(1, cfg.MaxRepeat)) break;
				}
				return list;
			}
		}
		list.Add(new PendingAction
		{
			Steps = value4,
			Multiplier = multiplier,
			User = e.User,
			Source = text,
			FromEvent = true,
			Count = Math.Max(1, e.Count)
		});
		return list;
	}
}
