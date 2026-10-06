using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace KickChaos
{
    /// <summary>Un paso de una accion compuesta: "Bombardeo+Policia*2" -> [Bombardeo x1, Policia x2]</summary>
    public class ActionStep
    {
        public string Name;
        public int Repeat = 1;
        public override string ToString() { return Repeat > 1 ? Name + "*" + Repeat : Name; }
    }

    /// <summary>Palabra o comando del chat que dispara una accion.</summary>
    public class ChatTrigger
    {
        public string RawKey;           // como esta escrita en config.ini
        public string Keyword;          // ya normalizada (minusculas, sin tildes)
        public bool IsCommand;          // empieza con "!": tiene que ir al principio del mensaje
        public List<ActionStep> Steps;
        public string ActionText;       // texto original de la accion (para mostrar)
        public int RequiredLevel;       // 0 todos, 1 sub, 2 vip, 3 mod, 4 streamer
        public float CooldownSeconds = -1f; // -1 = usar el de la accion
    }

    public class Config
    {
        public string Folder;
        public IniFile Ini = new IniFile();

        // [Kick]
        public bool KickEnabled = true;
        public string Channel = "rodsquare";
        public long ChatroomId = 31759082;
        public long ChannelId = 32047403;
        public string PusherKey = "32cbd69e4b950bf97679";
        public string PusherCluster = "us2";
        public string WebsocketUrlOverride = "";
        public HashSet<string> IgnoredUsers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // [Reglas]
        public bool OneActionPerMessage = true;
        public float GlobalSpacing = 2f;
        public float UserCooldown = 15f;
        public int MaxQueue = 15;
        public bool EventsBypassCooldown = true;
        public int MaxRepeat = 10;
        public bool RepeatPerGift = true;
        public bool GiftNpcForRecipients;   // regalo de subs: NPC de los que las reciben (en vez del que regala)
        public bool ShowInGameNotice = false;
        public string ObsFile = "ultimo_evento.txt";
        public int ObsClearSeconds = 12;

        // [DelayKick] El chat, las alertas y las acciones comparten la misma entrada.
        public float KickDelaySeconds = 10f;
        public int KickDelayCapacity = 256;
        public readonly Dictionary<string, float> KickDelayOverrides = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

        public float EventDelay(KickEvent ev)
        {
            float delay;
            string key = DelayKey(ev);
            if (!KickDelayOverrides.TryGetValue(key, out delay) &&
                !KickDelayOverrides.TryGetValue(ev.EventName ?? "", out delay)) delay = KickDelaySeconds;
            return SafeSeconds(delay, 10f, 3600f);
        }

        public static string DelayKey(KickEvent ev)
        {
            if (ev == null) return "Otros";
            switch (ev.Kind)
            {
                case KickEventKind.Chat: return "Chat";
                case KickEventKind.Subscription: return "Suscripcion";
                case KickEventKind.GiftedSubs: return "RegaloSubs";
                case KickEventKind.Follow: return "Follow";
                case KickEventKind.Host: return "Host";
                case KickEventKind.Ban: return "Ban";
                default: return KickDonation.IsDonation(ev) ? "Kicks" : "Otros";
            }
        }

        public static float SafeSeconds(float value, float fallback, float max)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : Math.Max(0f, Math.Min(max, value));
        }

        // Comandos / eventos
        public readonly List<ChatTrigger> Triggers = new List<ChatTrigger>();
        public readonly Dictionary<string, List<ActionStep>> EventMap = new Dictionary<string, List<ActionStep>>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, float> ActionCooldowns = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

        // [Teclas]
        public Keys KeyMenu = Keys.F8;
        public Keys KeyDirector = Keys.F9;
        public Keys KeyEditor = Keys.F7;
        public Keys KeyNextCam = Keys.F6;
        public Keys KeyReload = Keys.F5;
        public Keys KeyFollow = Keys.F4;     // seguir al proximo NPC de suscriptor
        public Keys KeyRanking = Keys.F3;    // mostrar/ocultar el ranking de kills
        public Keys KeyFeed = Keys.F2;       // mostrar/ocultar el kill feed
        public Keys KeyHud = Keys.F1;        // HUD compacto del stream
        public readonly Dictionary<Keys, string> TestKeys = new Dictionary<Keys, string>();

        public static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            string d = s.ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(d.Length);
            foreach (char c in d)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
                sb.Append(c);
            }
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        /// <summary>True si los pasos no hacen nada (todos "Nada" o vacio).</summary>
        public static bool IsNothing(List<ActionStep> steps)
        {
            if (steps == null) return true;
            foreach (var s in steps) if (ActionNames.Canonical(s.Name) != "Nada") return false;
            return true;
        }

        public static List<ActionStep> ParseSteps(string text)
        {
            var list = new List<ActionStep>();
            if (string.IsNullOrEmpty(text)) return list;
            foreach (string partRaw in text.Split('+'))
            {
                string part = partRaw.Trim();
                if (part.Length == 0) continue;
                var st = new ActionStep();
                int star = part.LastIndexOfAny(new[] { '*', 'x', 'X' });
                // "Explosion*3" o "Explosion x3": solo si lo que sigue es un numero
                if (star > 0)
                {
                    int n;
                    string num = part.Substring(star + 1).Trim();
                    if (int.TryParse(num, out n) && n > 0)
                    {
                        st.Repeat = n;
                        part = part.Substring(0, star).Trim();
                    }
                }
                st.Name = part;
                list.Add(st);
            }
            return list;
        }

        public static int ParseRole(string r)
        {
            switch (Normalize(r).Trim())
            {
                case "sub": case "subs": case "suscriptor": case "suscriptores": case "subscriber": return 1;
                case "vip": return 2;
                case "mod": case "mods": case "moderador": case "moderadores": return 3;
                case "streamer": case "yo": case "broadcaster": return 4;
                default: return 0;
            }
        }

        public static Keys ParseKey(string v, Keys def)
        {
            if (string.IsNullOrEmpty(v)) return def;
            Keys k;
            if (Enum.TryParse(v.Trim(), true, out k)) return k;
            return def;
        }

        public float GetCooldown(string action, float def)
        {
            float v;
            return ActionCooldowns.TryGetValue(action, out v) ? v : def;
        }

        /// <summary>Parametro de una accion: seccion [Accion.Nombre].</summary>
        public float P(string action, string key, float def) { return Ini.GetFloat("Accion." + action, key, def); }
        public int PI(string action, string key, int def) { return Ini.GetInt("Accion." + action, key, def); }
        public string PS(string action, string key, string def) { return Ini.Get("Accion." + action, key, def); }

        public static Config Load(string folder, List<string> warnings)
        {
            var c = new Config();
            c.Folder = folder;
            c.Ini = IniFile.Load(Path.Combine(folder, "config.ini"));
            IniFile ini = c.Ini;

            // [Kick]
            c.KickEnabled = ini.GetBool("Kick", "Conectar", true);
            c.Channel = ini.Get("Kick", "Canal", c.Channel).Trim().ToLowerInvariant();
            long l;
            if (long.TryParse(ini.Get("Kick", "ChatroomId", "").Trim(), out l)) c.ChatroomId = l;
            if (long.TryParse(ini.Get("Kick", "ChannelId", "").Trim(), out l)) c.ChannelId = l;
            c.PusherKey = ini.Get("Kick", "PusherKey", c.PusherKey).Trim();
            c.PusherCluster = ini.Get("Kick", "PusherCluster", c.PusherCluster).Trim();
            c.WebsocketUrlOverride = ini.Get("Kick", "UrlWebsocket", "").Trim();
            foreach (string u in ini.Get("Kick", "IgnorarUsuarios", "").Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                c.IgnoredUsers.Add(u.Trim().TrimStart('@'));

            // [Reglas]
            c.OneActionPerMessage = ini.GetBool("Reglas", "UnaAccionPorMensaje", true);
            c.GlobalSpacing = Math.Max(0f, ini.GetFloat("Reglas", "SeparacionEntreAcciones", 2f));
            c.UserCooldown = Math.Max(0f, ini.GetFloat("Reglas", "CooldownPorUsuario", 15f));
            c.MaxQueue = Math.Max(1, Math.Min(512, ini.GetInt("Reglas", "ColaMaxima", 15)));
            c.EventsBypassCooldown = ini.GetBool("Reglas", "EventosIgnoranCooldown", true);
            c.MaxRepeat = Math.Max(1, Math.Min(100, ini.GetInt("Reglas", "MaxRepeticiones", 10)));
            c.RepeatPerGift = ini.GetBool("Reglas", "RepetirPorCadaRegalo", true);
            c.GiftNpcForRecipients = ini.GetBool("Reglas", "NpcDeLosQueReciben", false);
            c.ShowInGameNotice = ini.GetBool("Reglas", "AvisoEnJuego", false);
            c.ObsFile = ini.Get("Reglas", "ArchivoOBS", "ultimo_evento.txt").Trim();
            c.ObsClearSeconds = ini.GetInt("Reglas", "BorrarTextoOBSTras", 12);

            c.KickDelaySeconds = SafeSeconds(ini.GetFloat("DelayKick", "Segundos", 10f), 10f, 3600f);
            c.KickDelayCapacity = Math.Max(16, Math.Min(2048, ini.GetInt("DelayKick", "ColaMaxima", 256)));
            var delays = ini.Section("DelayKick");
            if (delays != null)
                foreach (var entry in delays.Entries)
                {
                    if (string.Equals(entry.Key, "Segundos", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(entry.Key, "ColaMaxima", StringComparison.OrdinalIgnoreCase)) continue;
                    float seconds;
                    if (IniFile.TryFloat(entry.Value, out seconds))
                        c.KickDelayOverrides[entry.Key] = SafeSeconds(seconds, c.KickDelaySeconds, 3600f);
                    else warnings.Add("Delay invalido: " + entry.Key + " = " + entry.Value);
                }

            // [Cooldowns]
            var cds = ini.Section("Cooldowns");
            if (cds != null)
                foreach (var kv in cds.Entries)
                {
                    float f;
                    if (IniFile.TryFloat(kv.Value, out f)) c.ActionCooldowns[ActionNames.Canonical(kv.Key) ?? kv.Key.Trim()] = f;
                    else warnings.Add("Cooldown invalido: " + kv.Key + " = " + kv.Value);
                }

            // [Comandos]   palabra = Accion | rol | cooldown
            var cmds = ini.Section("Comandos");
            if (cmds != null)
                foreach (var kv in cmds.Entries)
                {
                    string[] parts = kv.Value.Split('|');
                    var t = new ChatTrigger();
                    t.RawKey = kv.Key.Trim();
                    t.Keyword = Normalize(kv.Key).Trim();
                    if (t.Keyword.Length == 0) continue;
                    t.IsCommand = t.Keyword.StartsWith("!");
                    t.ActionText = parts[0].Trim();
                    t.Steps = ParseSteps(t.ActionText);
                    if (t.Steps.Count == 0) { warnings.Add("Comando sin accion: " + kv.Key); continue; }
                    if (parts.Length > 1) t.RequiredLevel = ParseRole(parts[1]);
                    float cd;
                    if (parts.Length > 2 && IniFile.TryFloat(parts[2], out cd)) t.CooldownSeconds = cd;
                    c.Triggers.Add(t);
                }

            // [Eventos]   Suscripcion = Bombardeo, etc.
            var evs = ini.Section("Eventos");
            if (evs != null)
                foreach (var kv in evs.Entries)
                {
                    var steps = ParseSteps(kv.Value);
                    if (steps.Count > 0) c.EventMap[kv.Key.Trim()] = steps;
                }

            // [Teclas]
            c.KeyMenu = ParseKey(ini.Get("Teclas", "Menu"), Keys.F8);
            c.KeyDirector = ParseKey(ini.Get("Teclas", "Director"), Keys.F9);
            c.KeyEditor = ParseKey(ini.Get("Teclas", "Editor"), Keys.F7);
            c.KeyNextCam = ParseKey(ini.Get("Teclas", "SiguienteCamara"), Keys.F6);
            c.KeyReload = ParseKey(ini.Get("Teclas", "Recargar"), Keys.F5);
            c.KeyFollow = ParseKey(ini.Get("Teclas", "SeguirNpc"), Keys.F4);
            c.KeyRanking = ParseKey(ini.Get("Teclas", "Ranking"), Keys.F3);
            c.KeyFeed = ParseKey(ini.Get("Teclas", "KillFeed"), Keys.F2);
            c.KeyHud = ParseKey(ini.Get("Teclas", "HUDStream"), Keys.F1);
            var tk = ini.Section("TeclasDePrueba");
            if (tk != null)
                foreach (var kv in tk.Entries)
                {
                    Keys k = ParseKey(kv.Key, Keys.None);
                    if (k == Keys.None) { warnings.Add("Tecla desconocida: " + kv.Key); continue; }
                    c.TestKeys[k] = kv.Value.Trim();
                }

            return c;
        }
    }
}
