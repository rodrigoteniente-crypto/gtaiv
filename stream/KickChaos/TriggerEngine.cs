using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace KickChaos
{
    /// <summary>Lo que hay que ejecutar en el juego como respuesta a un mensaje/evento.</summary>
    public class PendingAction
    {
        public List<ActionStep> Steps;
        public int Multiplier = 1;
        public string User = "";
        public string Source = "";     // "!boom", "Suscripcion", "prueba"...
        public bool FromEvent;
        public int Count;              // meses de sub, subs regaladas... (0 = mensaje del chat)
        internal List<QueuedAction> Prepared;
        internal int QueueOffset;
        internal bool QueueComplete;
        public override string ToString()
        {
            return string.Join("+", Steps.ConvertAll(s => s.ToString()).ToArray()) + (Multiplier > 1 ? " (x" + Multiplier + ")" : "");
        }
    }

    /// <summary>
    /// Decide que acciones disparar a partir de los eventos de Kick: busca palabras clave,
    /// revisa permisos (sub/vip/mod) y cooldowns. No toca el juego, asi que se puede probar fuera de el.
    /// </summary>
    public class TriggerEngine
    {
        Config cfg;
        readonly Dictionary<string, DateTime> userLast = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, DateTime> actionLast = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        readonly HashSet<string> chatters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        readonly Queue<DateTime> chatTimes = new Queue<DateTime>();
        DateTime lastHype = DateTime.MinValue;
        static readonly Regex EmoteRx = new Regex(@"\[emote:\d+:[^\]]*\]", RegexOptions.Compiled);
        static readonly Regex NonWordRx = new Regex(@"[^\p{L}\p{N}!]+", RegexOptions.Compiled);
        static readonly Regex TrailingBangRx = new Regex(@"(?<=[\p{L}\p{N}])!+", RegexOptions.Compiled);

        public const float DefaultActionCooldown = 8f;

        public TriggerEngine(Config cfg) { this.cfg = cfg; }
        public void SetConfig(Config c) { cfg = c; }

        public static string CleanMessage(string text)
        {
            string t = EmoteRx.Replace(text ?? "", " ");
            t = Config.Normalize(t);
            t = NonWordRx.Replace(t, " ");
            t = TrailingBangRx.Replace(t, " ");
            t = Regex.Replace(t, @"\s+", " ");
            return " " + t.Trim() + " ";
        }

        public static bool Matches(ChatTrigger trig, string cleaned)
        {
            string kw = CleanMessage(trig.Keyword).Trim();
            if (kw.Length == 0) return false;
            if (trig.IsCommand)
                return cleaned.StartsWith(" " + kw + " ", StringComparison.Ordinal);
            return cleaned.IndexOf(" " + kw + " ", StringComparison.Ordinal) >= 0;
        }

        bool Mapped(string key, out List<ActionStep> steps)
        {
            return cfg.EventMap.TryGetValue(key, out steps) && !Config.IsNothing(steps);
        }

        /// <summary>Eventos "inventados" a partir del chat: respetan el cooldown de sus acciones.</summary>
        void AddSynthetic(List<PendingAction> result, string key, string user, int count, DateTime now, bool bypass)
        {
            List<ActionStep> steps;
            if (!Mapped(key, out steps)) return;
            string ck = "evento:" + key + ":" + CooldownKey(steps);
            float cd = CooldownFor(steps);
            DateTime last;
            if (!bypass && cd > 0 && actionLast.TryGetValue(ck, out last) && (now - last).TotalSeconds < cd) return;
            actionLast[ck] = now;
            result.Add(new PendingAction { Steps = steps, User = user, Source = key, FromEvent = true, Count = Math.Max(1, count) });
        }

        float CooldownFor(List<ActionStep> steps)
        {
            float cd = 0f;
            foreach (var s in steps) cd = Math.Max(cd, cfg.GetCooldown(ActionNames.Canonical(s.Name) ?? s.Name, DefaultActionCooldown));
            return cd;
        }

        string CooldownKey(List<ActionStep> steps)
        {
            return string.Join("+", steps.ConvertAll(s => (ActionNames.Canonical(s.Name) ?? s.Name).ToLowerInvariant()).ToArray());
        }

        /// <summary>
        /// Devuelve las acciones a ejecutar (puede ser lista vacia). 'why' explica por que se ignoro, para el log.
        /// </summary>
        public List<PendingAction> Process(KickEvent e, DateTime now, out string why)
        {
            why = null;
            var result = new List<PendingAction>();
            if (e == null) return result;

            if (!string.IsNullOrEmpty(e.User) && cfg.IgnoredUsers.Contains(e.User))
            {
                why = "usuario ignorado";
                return result;
            }

            if (e.Kind == KickEventKind.Chat)
            {
                string cleaned = CleanMessage(e.Text);
                bool matched = false;
                DateTime previousUserLast;
                bool hadPreviousUser = userLast.TryGetValue(e.User ?? "", out previousUserLast);
                foreach (ChatTrigger t in cfg.Triggers)
                {
                    if (Config.IsNothing(t.Steps)) continue;
                    if (!Matches(t, cleaned)) continue;
                    matched = true;

                    if (e.Level < t.RequiredLevel)
                    {
                        why = "'" + t.Keyword + "' requiere nivel " + t.RequiredLevel;
                        continue;
                    }

                    bool bypass = e.Level >= 4 || e.Simulated; // el streamer y las pruebas no tienen cooldown
                    DateTime last;
                    if (!bypass && cfg.UserCooldown > 0 && hadPreviousUser &&
                        (now - previousUserLast).TotalSeconds < cfg.UserCooldown)
                    {
                        why = e.User + " en cooldown";
                        if (cfg.OneActionPerMessage) break;
                        continue;
                    }

                    float cd = t.CooldownSeconds >= 0 ? t.CooldownSeconds : CooldownFor(t.Steps);
                    string key = CooldownKey(t.Steps);
                    if (!bypass && cd > 0 && actionLast.TryGetValue(key, out last) && (now - last).TotalSeconds < cd)
                    {
                        why = "'" + t.ActionText + "' en cooldown (" + Math.Ceiling(cd - (now - last).TotalSeconds) + " s)";
                        if (cfg.OneActionPerMessage) break;
                        continue;
                    }

                    actionLast[key] = now;
                    userLast[e.User] = now;
                    result.Add(new PendingAction { Steps = t.Steps, User = e.User, Source = t.Keyword });
                    if (cfg.OneActionPerMessage) break;
                }

                // "Alguien escribe por primera vez" (desde que arranco el mod)
                if (!string.IsNullOrEmpty(e.User) && chatters.Add(e.User) && e.Level < 4)
                    AddSynthetic(result, "PrimerMensaje", e.User, 1, now, e.Simulated);
                // "El chat explota": muchos mensajes en pocos segundos
                {
                    float win = Math.Max(3f, cfg.Ini.GetFloat("Reglas", "ChatAFullSegundos", 20f));
                    int need = Math.Max(2, cfg.Ini.GetInt("Reglas", "ChatAFullMensajes", 15));
                    float hypeCd = Math.Max(0f, cfg.Ini.GetFloat("Reglas", "ChatAFullCooldown", 120f));
                    chatTimes.Enqueue(now);
                    while (chatTimes.Count > 0 && (now - chatTimes.Peek()).TotalSeconds > win) chatTimes.Dequeue();
                    if (chatTimes.Count >= need && (now - lastHype).TotalSeconds >= hypeCd)
                    {
                        List<ActionStep> hs;
                        if (Mapped("ChatAFull", out hs))
                        {
                            lastHype = now;
                            chatTimes.Clear();
                            AddSynthetic(result, "ChatAFull", "el chat", need, now, true);
                        }
                    }
                }

                // "Cualquier mensaje" del chat (si esta configurado y el mensaje no disparo otra cosa)
                List<ActionStep> any;
                if (!matched && cfg.EventMap.TryGetValue("Mensaje", out any) && !Config.IsNothing(any))
                {
                    bool bypass = e.Level >= 4 || e.Simulated;
                    DateTime last;
                    if (!bypass && cfg.UserCooldown > 0 && userLast.TryGetValue(e.User, out last) && (now - last).TotalSeconds < cfg.UserCooldown)
                        return result;
                    float cd = CooldownFor(any);
                    string key = "mensaje:" + CooldownKey(any);
                    if (!bypass && cd > 0 && actionLast.TryGetValue(key, out last) && (now - last).TotalSeconds < cd)
                        return result;
                    actionLast[key] = now;
                    userLast[e.User] = now;
                    result.Add(new PendingAction { Steps = any, User = e.User, Source = "mensaje" });
                }
                return result;
            }

            // Eventos (subs, regalos, follows, hosts, otros)
            string mapKey;
            switch (e.Kind)
            {
                case KickEventKind.Subscription: mapKey = "Suscripcion"; break;
                case KickEventKind.GiftedSubs: mapKey = "RegaloSubs"; break;
                case KickEventKind.Follow: mapKey = "Follow"; break;
                case KickEventKind.Host: mapKey = "Host"; break;
                case KickEventKind.Ban: mapKey = "Ban"; break;
                default: mapKey = e.EventName; break;
            }

            // regalos grandes / kicks grandes: si estan configurados, reemplazan al evento normal
            List<ActionStep> big;
            if (e.Kind == KickEventKind.GiftedSubs && e.Count >= Math.Max(2, cfg.Ini.GetInt("Reglas", "RegaloGrandeDesde", 5)) && Mapped("RegaloGrande", out big))
                mapKey = "RegaloGrande";
            else if (string.Equals(e.EventName, "KicksGifted", StringComparison.OrdinalIgnoreCase) &&
                     e.Count >= Math.Max(1, cfg.Ini.GetInt("Reglas", "KicksGrandesDesde", 100)) && Mapped("KicksGrandes", out big))
                mapKey = "KicksGrandes";

            List<ActionStep> steps;
            if ((!cfg.EventMap.TryGetValue(mapKey, out steps) && !cfg.EventMap.TryGetValue(e.EventName, out steps)) || Config.IsNothing(steps))
            {
                why = "evento '" + mapKey + "' sin accion asignada";
                return result;
            }

            if (!cfg.EventsBypassCooldown && !e.Simulated)
            {
                float cd = CooldownFor(steps);
                string key = CooldownKey(steps);
                DateTime last;
                if (cd > 0 && actionLast.TryGetValue(key, out last) && (now - last).TotalSeconds < cd)
                {
                    why = "evento en cooldown";
                    return result;
                }
                actionLast[key] = now;
            }

            int mult = 1;
            if (e.Kind == KickEventKind.GiftedSubs && cfg.RepeatPerGift) mult = Math.Min(Math.Max(1, e.Count), cfg.MaxRepeat);
            // el regalo grande es UNA cosa grande (no se repite por cada sub)
            if (mapKey == "RegaloGrande" || mapKey == "KicksGrandes") mult = 1;

            // opcional: en un regalo de subs, el NPC es de los que las reciben (cada uno con su nombre, como sub nueva)
            if (cfg.GiftNpcForRecipients && e.Kind == KickEventKind.GiftedSubs && e.Recipients.Count > 0)
            {
                var npc = steps.FindAll(st => (ActionNames.Canonical(st.Name) ?? st.Name).StartsWith("Npc", StringComparison.Ordinal));
                if (npc.Count > 0)
                {
                    var rest = steps.FindAll(st => !npc.Contains(st));
                    if (rest.Count > 0)
                        result.Add(new PendingAction { Steps = rest, Multiplier = mult, User = e.User, Source = mapKey, FromEvent = true, Count = Math.Max(1, e.Count) });
                    int added = 0;
                    foreach (string r in e.Recipients)
                    {
                        if (cfg.IgnoredUsers.Contains(r)) continue;
                        result.Add(new PendingAction { Steps = npc, User = r, Source = "Suscripcion", FromEvent = true, Count = 1 });
                        if (++added >= Math.Max(1, cfg.MaxRepeat)) break;
                    }
                    return result;
                }
            }
            result.Add(new PendingAction { Steps = steps, Multiplier = mult, User = e.User, Source = mapKey, FromEvent = true, Count = Math.Max(1, e.Count) });
            return result;
        }
    }
}

namespace KickChaos
{
    public class QueuedAction
    {
        public string Name;       // nombre canonico
        public string User = "";
        public string Source = "";
        public bool FromEvent;
        public int Count;         // meses de sub, subs regaladas... (para el NPC)
        public bool ChainNext;    // la siguiente accion es parte del mismo pedido (va casi enseguida)
    }

    /// <summary>Cola de acciones con separacion entre ellas para que no se pisen.</summary>
    public class ActionQueue
    {
        readonly LinkedList<QueuedAction> q = new LinkedList<QueuedAction>();
        double nextAt;
        public const double ChainGap = 0.45;

        public int Count { get { return q.Count; } }
        public void Clear() { q.Clear(); nextAt = 0; }

        /// <summary>Agrega un pedido. Devuelve cuantas acciones entraron (0 si la cola esta llena).</summary>
        public int Enqueue(PendingAction pa, Config cfg, Func<string, string> canonical, List<string> unknown)
        {
            if (pa == null || pa.QueueComplete) return 0;
            var list = pa.Prepared;
            if (list == null)
            {
                list = new List<QueuedAction>();
                foreach (ActionStep st in pa.Steps)
                {
                    string canon = canonical(st.Name);
                    if (canon == null) { if (unknown != null) unknown.Add(st.Name); continue; }
                    if (canon == "Nada") continue;
                    int mult = canon.StartsWith("Npc", StringComparison.Ordinal) ? 1 : Math.Max(1, pa.Multiplier);
                    int n = (int)Math.Min((long)Math.Max(1, st.Repeat) * mult, Math.Max(1, Math.Min(100, cfg.MaxRepeat)));
                    for (int i = 0; i < n; i++)
                        list.Add(new QueuedAction { Name = canon, User = pa.User, Source = pa.Source, FromEvent = pa.FromEvent, Count = pa.Count, ChainNext = true });
                }
                if (list.Count > 0) list[list.Count - 1].ChainNext = false;
                pa.Prepared = list;
            }
            if (list.Count == 0) { pa.QueueComplete = true; return 0; }
            int capacity = Math.Max(1, Math.Min(512, cfg.MaxQueue));
            int remaining = list.Count - pa.QueueOffset;
            if (!pa.FromEvent && q.Count + remaining > capacity)
            {
                pa.QueueComplete = true; // el chat respeta el límite sin entrar a medias
                return 0;
            }
            if (pa.FromEvent)
            {
                var node = q.First;
                while (q.Count + remaining > capacity && node != null)
                {
                    var nextNode = node.Next;
                    if (!node.Value.FromEvent) q.Remove(node);
                    node = nextNode;
                }
            }
            int added = Math.Min(remaining, Math.Max(0, capacity - q.Count));
            for (int i = 0; i < added; i++) q.AddLast(list[pa.QueueOffset++]);
            pa.QueueComplete = pa.QueueOffset == list.Count;
            return added;
        }

        public QueuedAction TryDequeue(double now, double spacing)
        {
            if (q.Count == 0 || now < nextAt) return null;
            QueuedAction a = q.First.Value;
            q.RemoveFirst();
            nextAt = now + (a.ChainNext ? ChainGap : spacing);
            return a;
        }
    }
}
