using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;
using static KickChaos.N;

namespace KickChaos
{
    /// <summary>
    /// KickChaos IV (version ScriptHookDotNet) — el chat de Kick controla lo que pasa en Liberty City
    /// mientras una camara cinematografica recorre la ciudad (fondo para pantalla verde).
    /// Compilado contra el ScriptHookDotNet del usuario para aCompleteEditionHook / Complete Edition.
    /// </summary>
    public class KickChaosScript : GTA.Script
    {
        public const string Version = "1.9.1-stream";

        Config cfg;
        KickClient kick;
        TriggerEngine engine;
        Director director;
        ChaosActions actions;
        SubNpcManager npcs;
        readonly ActionQueue queue = new ActionQueue();
        readonly StreamAdminBridge adminBridge = new StreamAdminBridge();
        readonly DelayedKickEvents delayedEvents = new DelayedKickEvents();
        readonly Queue<PendingAction> pendingActions = new Queue<PendingAction>();
        readonly List<QueuedAction> waitingForNpcSlot = new List<QueuedAction>();
        KickEvent incomingEvent;
        QueuedAction slotOverflow;
        double npcSlotRetryAt, npcSlotNoticeAt = -100;
        float aspect = 16f / 9f;
        double lastAspectRead = -100;

        string folder;
        readonly object logLock = new object();
        readonly ConcurrentQueue<Keys> keyQueue = new ConcurrentQueue<Keys>();
        readonly ConcurrentQueue<string[]> consoleQueue = new ConcurrentQueue<string[]>();

        KickMenu kmenu;
        MenuTheme theme = new MenuTheme();
        bool menuTookControl, menuBlockedInput;
        double menuClosedAt = -10;

        double readySince = -1, obsClearAt = -1, lastStatusLog, lastTick = -1, lastErrorLog = -100;
        bool autoStarted, initialized, focusWarningPending, wasReady, firstReadyDone, nativesOk = true;
        string lastError;

        public KickChaosScript()
        {
            Interval = 0; // cada frame: la camara se mueve suave
            Tick += OnTick;
            KeyDown += OnKeyDown;
            try
            {
                folder = Path.Combine(Path.Combine(GTA.Game.InstallFolder, "scripts"), "KickChaos");
                Directory.CreateDirectory(folder);
                Log("==== KickChaos IV " + Version + " (ScriptHookDotNet) iniciado ====");
                LoadAll(true);
                theme = MenuTheme.FromLibertysLegacy(GameFolder);
                kmenu = new KickMenu(this);
                PerFrameDrawing += OnDraw;
                BindConsoleCommand("kick", new GTA.ConsoleCommandDelegate(OnConsoleCommand), "KickChaos: kick estado | kick sub | kick chat <usuario> <mensaje> | kick accion <Accion> ...");
                CheckFusionFix();
                initialized = true;
            }
            catch (Exception ex)
            {
                Log("ERROR al iniciar: " + ex);
                try { GTA.Game.Console.Print("[KickChaos] error al iniciar: " + ex.Message); } catch { }
            }
        }

        void OnDraw(object sender, GTA.GraphicsEventArgs e)
        {
            try { if (npcs != null) npcs.DrawTags(e.Graphics); }
            catch { }
            try { if (npcs != null) npcs.DrawHud(e.Graphics); }
            catch { }
            try { if (kmenu == null || !kmenu.Menu.IsOpen) StreamHud.Draw(e.Graphics, aspect); }
            catch { }
            try { if (kmenu != null && kmenu.Menu.IsOpen) kmenu.Menu.Draw(e.Graphics, theme); }
            catch { }
        }

        // ---------------- lo que usa el menu ----------------
        internal Config Cfg { get { return cfg; } }
        internal Director Dir { get { return director; } }
        internal SubNpcManager Npcs { get { return npcs; } }

        /// <summary>Probar el NPC de suscriptor como si llegara un evento (meses o subs regaladas).</summary>
        internal void TestNpc(string source, int count)
        {
            HandleKickEvent(KickDonation.TestSubscription(source, count));
        }
        internal string Folder { get { return folder; } }
        internal string GameFolder { get { return GTA.Game.InstallFolder; } }
        internal int QueueCount() { return queue.Count + delayedEvents.Count + pendingActions.Count + waitingForNpcSlot.Count + (incomingEvent != null ? 1 : 0) + (slotOverflow != null ? 1 : 0); }
        internal void ClearQueue()
        {
            queue.Clear(); delayedEvents.Clear(); pendingActions.Clear(); waitingForNpcSlot.Clear();
            incomingEvent = null; slotOverflow = null;
        }

        internal string KickStatus()
        {
            if (kick == null) return "desconectado (apagado en la configuracion)";
            return kick.Status + "  -  " + kick.MessagesReceived + " mensajes";
        }

        internal List<string> SeenOtherEvents()
        {
            var l = new List<string>();
            if (kick != null) foreach (var kv in kick.SeenOther) l.Add(kv.Key);
            l.Sort();
            return l;
        }

        internal void ToggleDirector()
        {
            if (director.Active) { actions.StopAll(); director.Stop(); }
            else { if (director.EditorActive) director.ToggleEditor(); director.Start(); }
            autoStarted = true;
        }

        internal void SimulateChat(string user, string text)
        {
            HandleKickEvent(new KickEvent { Kind = KickEventKind.Chat, User = user, Text = text, Simulated = true, EventName = "ChatMessageEvent" });
        }

        /// <summary>Releer config.ini despues de un cambio del menu (sin reconectar a Kick ni tocar las camaras).</summary>
        internal void SoftReload()
        {
            var warnings = new List<string>();
            cfg = Config.Load(folder, warnings);
            director.ApplyConfig(cfg);
            engine.SetConfig(cfg);
            actions.SetConfig(cfg);
            npcs.ApplyConfig(cfg);
            StreamHud.ApplyConfig(cfg);
            foreach (string w in warnings) Log("AVISO: " + w);
        }

        internal void FullReload()
        {
            Reload();
            theme = MenuTheme.FromLibertysLegacy(GameFolder);
        }

        void OnConsoleCommand(GTA.ParameterCollection p)
        {
            var args = new string[p.Count];
            for (int i = 0; i < p.Count; i++) args[i] = p.ToString(i);
            consoleQueue.Enqueue(args);
        }

        void Sub(string text, uint ms) { GTA.Game.DisplayText(text, (int)ms); }

        void LoadAll(bool first)
        {
            var warnings = new List<string>();
            cfg = Config.Load(folder, warnings);

            if (director == null) director = new Director(cfg, Log, Sub);
            else director.ApplyConfig(cfg);
            director.LoadShots(warnings);

            if (engine == null) engine = new TriggerEngine(cfg); else engine.SetConfig(cfg);
            if (actions == null) actions = new ChaosActions(cfg, director, Log); else actions.SetConfig(cfg);
            director.Actions = actions;
            if (npcs == null) npcs = new SubNpcManager(cfg, director, Log, NpcNotice); else npcs.ApplyConfig(cfg);
            director.Npcs = npcs;
            actions.Npcs = npcs;
            StreamHud.ApplyConfig(cfg);

            foreach (var t in cfg.Triggers)
                foreach (var st in t.Steps)
                    if (ActionNames.Canonical(st.Name) == null) warnings.Add("Accion desconocida '" + st.Name + "' en el comando '" + t.Keyword + "'");
            foreach (var kv in cfg.EventMap)
                foreach (var st in kv.Value)
                    if (ActionNames.Canonical(st.Name) == null) warnings.Add("Accion desconocida '" + st.Name + "' en el evento '" + kv.Key + "'");

            foreach (string w in warnings) { Log("AVISO: " + w); try { GTA.Game.Console.Print("[KickChaos] " + w); } catch { } }
            Log("Config cargada: " + cfg.Triggers.Count + " comandos, " + cfg.EventMap.Count + " eventos, " + director.Shots.Count + " camaras");

            if (kick != null) kick.Stop();
            kick = null;
            // ScriptHookDotNet recarga los scripts (al cargar partida) sin avisar: cortamos la conexion
            // que haya dejado abierta la instancia anterior para no tener dos.
            try
            {
                var prev = AppDomain.CurrentDomain.GetData("KickChaos.StopKick") as Action;
                if (prev != null) prev();
                AppDomain.CurrentDomain.SetData("KickChaos.StopKick", null);
            }
            catch { }
            if (cfg.KickEnabled)
            {
                kick = new KickClient(cfg, Log);
                kick.Start();
                try { AppDomain.CurrentDomain.SetData("KickChaos.StopKick", (Action)kick.Stop); } catch { }
            }
            WriteObs("");
            if (!first) Sub("KickChaos: configuracion recargada", 2500);
        }

        /// <summary>Al descargar el script (recarga de scripts o salir del juego): cortar la conexion a Kick.</summary>
        protected override void Dispose(bool disposing)
        {
            try { if (kick != null) kick.Stop(); } catch { }
            try { if (kmenu != null) kmenu.Menu.DisposeFonts(); } catch { }
            try { if (npcs != null) npcs.DisposeFonts(); } catch { }
            try { StreamHud.Dispose(); } catch { }
            try { if (npcs != null) npcs.StopRadio(); } catch { }
            Log("KickChaos detenido");
            base.Dispose(disposing);
        }

        /// <summary>Avisa si FusionFix pausa el juego cuando la ventana pierde el foco.</summary>
        void CheckFusionFix()
        {
            try
            {
                string path = Path.Combine(Path.Combine(GTA.Game.InstallFolder, "plugins"), "GTAIV.EFLC.FusionFix.cfg");
                if (!File.Exists(path)) return;
                var ini = IniFile.Load(path);
                if (ini.GetInt("MAIN", "BlockOnLostFocus", 0) != 1)
                {
                    // FusionFix a veces lo vuelve a 0 (por ejemplo al cambiar opciones de pantalla).
                    // Lo corregimos en el archivo para la proxima vez que se abra el juego.
                    try
                    {
                        var doc = IniDoc.Load(path);
                        doc.Set("MAIN", "BlockOnLostFocus", "1");
                        doc.Save();
                        Log("AVISO: FusionFix tenia BlockOnLostFocus = 0 (el juego se frena sin foco). Se corrigio a 1 en el archivo; vale desde la proxima vez que abras el juego. Para ahora: menu de pausa > Pantalla > 'Focus Loss'.");
                    }
                    catch (Exception ex) { Log("AVISO: no se pudo corregir BlockOnLostFocus en FusionFix: " + ex.Message); }
                    // con KickChaosSonido.asi el juego sigue activo igual: el aviso solo hace falta sin el plugin
                    if (!File.Exists(Path.Combine(GTA.Game.InstallFolder, "KickChaosSonido.asi"))) focusWarningPending = true;
                }
            }
            catch { }
        }

        // ------------------------------------------------------------------
        // Teclado: solo encolamos y lo procesamos en el Tick
        // ------------------------------------------------------------------
        void OnKeyDown(object sender, GTA.KeyEventArgs e)
        {
            keyQueue.Enqueue(e.KeyWithModifiers);
        }

        Keys lastHotkey = Keys.None;
        double lastHotkeyAt = -100;

        void HandleKey(Keys data)
        {
            if (!G.IsGameFocused()) return; // no reaccionar a teclas mientras escribis en OBS u otra ventana
            Keys code = data & Keys.KeyCode;
            bool shift = (data & Keys.Shift) != 0;
            if ((data & (Keys.Control | Keys.Alt)) != 0) return; // combinaciones de IV-SDK .NET (Ctrl+F10, etc.)
            // (de la 1.6.3 de GPT) tecla mantenida apretada: Windows la repite; las teclas rapidas cuentan una vez
            bool hotkey = code == cfg.KeyMenu || code == cfg.KeyDirector || code == cfg.KeyEditor || code == cfg.KeyNextCam ||
                          code == cfg.KeyReload || code == cfg.KeyFollow || code == cfg.KeyRanking || code == cfg.KeyFeed || code == cfg.KeyHud || cfg.TestKeys.ContainsKey(code);
            if (hotkey)
            {
                double t = G.Now;
                if (code == lastHotkey && t - lastHotkeyAt < 0.35) { lastHotkeyAt = t; return; }
                lastHotkey = code;
                lastHotkeyAt = t;
            }

            if (code == cfg.KeyMenu)
            {
                if (director.EditorActive) director.ToggleEditor();
                kmenu.Menu.Toggle();
                if (!kmenu.Menu.IsOpen) menuClosedAt = G.Now;
                return;
            }
            if (kmenu.Menu.IsOpen) return; // con el menu abierto las flechas/ENTER son del menu
            // la misma tecla que cerro el menu puede llegar un frame despues: no guardar una camara sin querer
            if (G.Now - menuClosedAt < 0.4 && (code == Keys.Return || code == Keys.Back || code == Keys.T)) return;

            if (director.EditorActive)
            {
                if (code == Keys.Return) { director.EditorSave(shift); return; }
                if (code == Keys.Back) { director.EditorDeleteLast(); return; }
                if (code == Keys.T) { director.EditorSetTarget(); return; }
            }

            if (code == cfg.KeyEditor) { director.ToggleEditor(); return; }
            if (code == cfg.KeyDirector) { ToggleDirector(); return; }
            if (code == cfg.KeyNextCam)
            {
                if (director.Active)
                {
                    if (director.FollowingNpc || director.TemporaryCameraActive) director.ReturnToCity();
                    else director.Next();
                }
                return;
            }
            if (code == cfg.KeyReload) { Reload(); return; }
            if (code == cfg.KeyFollow) { FollowNextNpc(); return; }
            if (code == cfg.KeyRanking) { bool on = npcs.ToggleRanking(); Sub(on ? "Ranking de kills: visible" : "Ranking de kills: oculto", 1500); return; }
            if (code == cfg.KeyFeed) { bool on = StreamHud.ToggleFeed(); Sub(on ? "Feed de eventos: visible" : "Feed de eventos: oculto", 1500); return; }
            if (code == cfg.KeyHud) { bool on = StreamHud.ToggleHud(); Sub(on ? "HUD: visible" : "HUD: oculto", 1500); return; }

            string test;
            if (cfg.TestKeys.TryGetValue(code, out test)) RunTest(test);
        }

        /// <summary>Camara al proximo NPC de suscriptor (tecla o menu).</summary>
        internal void FollowNextNpc()
        {
            if (director.EditorActive) director.ToggleEditor();
            if (!director.FollowNext()) Sub("No hay ningun NPC de suscriptor en la calle", 2000);
        }

        internal void Reload()
        {
            bool wasActive = director.Active;
            actions.StopAll();
            ClearQueue();
            if (director.EditorActive) director.ToggleEditor();
            if (wasActive) director.Stop();
            LoadAll(false);
            if (wasActive) director.Start();
        }

        /// <summary>"Explosion", "Bombardeo+Policia" o "evento:Suscripcion", "evento:RegaloSubs:5"...</summary>
        internal void RunTest(string what)
        {
            what = what.Trim();
            if (what.StartsWith("evento:", StringComparison.OrdinalIgnoreCase))
            {
                string[] p = what.Substring(7).Split(':');
                var ev = new KickEvent { User = "prueba", Simulated = true, EventName = p[0].Trim() };
                int n = 1;
                if (p.Length > 1) int.TryParse(p[1], out n);
                ev.Count = Math.Max(1, n);
                switch (Config.Normalize(p[0]).Trim())
                {
                    case "suscripcion": case "sub": ev.Kind = KickEventKind.Subscription; break;
                    case "regalosubs": case "regalo": ev.Kind = KickEventKind.GiftedSubs; break;
                    case "follow": ev.Kind = KickEventKind.Follow; break;
                    case "host": case "raid": ev.Kind = KickEventKind.Host; break;
                    case "kicks": case "kicksgifted": ev.Kind = KickEventKind.Other; ev.EventName = "KicksGifted"; break;
                    default: ev.Kind = KickEventKind.Other; break;
                }
                HandleKickEvent(ev);
                return;
            }
            var pa = new PendingAction { Steps = Config.ParseSteps(what), User = "prueba", Source = "tecla", FromEvent = true };
            Enqueue(pa);
        }

        // ------------------------------------------------------------------
        // Consola de IV-SDK .NET (F4):  kick estado | kick sub | kick chat <user> <msg> ...
        // ------------------------------------------------------------------
        void HandleConsole(string[] args)
        {
            string cmd = args.Length > 0 ? Config.Normalize(args[0]) : "ayuda";
            string rest = args.Length > 1 ? string.Join(" ", args, 1, args.Length - 1) : "";
            switch (cmd)
            {
                case "estado": case "status":
                    Print("Kick: " + (kick == null ? "desactivado" : kick.Status) + " | mensajes: " + (kick == null ? 0 : kick.MessagesReceived));
                    Print("Director: " + director.StatusText() + " | camaras: " + director.Shots.Count + " | cola: " + queue.Count);
                    break;
                case "chat":
                    {
                        string user = args.Length > 1 ? args[1] : "prueba";
                        string msg = args.Length > 2 ? string.Join(" ", args, 2, args.Length - 2) : "";
                        HandleKickEvent(new KickEvent { Kind = KickEventKind.Chat, User = user, Text = msg, Simulated = true, EventName = "ChatMessageEvent" });
                        break;
                    }
                case "sub": RunTest("evento:Suscripcion"); break;
                case "regalo": RunTest("evento:RegaloSubs:" + (rest.Length > 0 ? rest : "5")); break;
                case "follow": RunTest("evento:Follow"); break;
                case "npc":
                    {
                        int m = 1;
                        if (args.Length > 1) int.TryParse(args[1], out m);
                        TestNpc("Suscripcion", m);
                        break;
                    }
                case "host": RunTest("evento:Host"); break;
                case "accion": case "action": RunTest(rest); break;
                case "recargar": case "reload": Reload(); break;
                case "director": if (director.Active) { actions.StopAll(); director.Stop(); } else director.Start(); autoStarted = true; break;
                case "editor": director.ToggleEditor(); break;
                case "camara": case "siguiente": director.Next(); break;
                case "aqui": case "centro": director.SetHubHere(); Print("Centro de las camaras automaticas = posicion actual"); break;
                case "acciones":
                    Print("Acciones: " + string.Join(", ", new List<string>(ActionNames.Display.Keys).ToArray()));
                    break;
                default:
                    Print("kick estado | kick chat <usuario> <mensaje> | kick sub | kick regalo <n> | kick follow | kick host | kick npc <meses>");
                    Print("kick accion <Accion> | kick acciones | kick director | kick editor | kick camara | kick aqui | kick recargar");
                    break;
            }
        }

        void Print(string s) { try { GTA.Game.Console.Print("[KickChaos] " + s); } catch { } }

        // ------------------------------------------------------------------
        // Tick
        // ------------------------------------------------------------------
        void OnTick(object sender, EventArgs e)
        {
            if (!initialized) return;
            try
            {
                bool ready = G.IsPlayerReady();
                if (!ready)
                {
                    if (wasReady)
                    {
                        // muerte / arresto / cambio de episodio: apagar todo prolijo y volver a arrancar despues
                        try { actions.StopAll(); director.QuickTeardown(); } catch { }
                        try { npcs.ReleaseAll(); } catch { }
                        director.ForgetState();
                        actions.ForgetState();
                        npcs.ForgetState();
                        // Los eventos demorados y pedidos pendientes sobreviven a una carga de partida.
                        autoStarted = false;
                    }
                    wasReady = false;
                    readySince = -1;
                    return;
                }
                wasReady = true;

                if (!firstReadyDone)
                {
                    firstReadyDone = true;
                    var missing = N.Validate();
                    if (missing.Count > 0)
                    {
                        Log("AVISO: el juego no tiene estos natives, se desactivan: " + string.Join(", ", missing.ToArray()));
                        string[] critical = { "CREATE_CAM", "SET_CAM_POS", "SET_CAM_ROT", "SET_CAM_FOV", "SET_CAM_ACTIVE", "ACTIVATE_SCRIPTED_CAMS",
                                              "SET_CHAR_COORDINATES", "FREEZE_CHAR_POSITION", "SET_CHAR_VISIBLE", "GET_PLAYER_CHAR", "GET_CHAR_COORDINATES" };
                        foreach (string c in critical)
                            if (missing.Contains(c)) nativesOk = false;
                        if (!nativesOk)
                        {
                            Log("ERROR: faltan natives basicos, el director no puede funcionar en este juego.");
                            Sub("KickChaos: faltan funciones del juego, mira scripts\\KickChaos\\log.txt", 8000);
                        }
                    }
                    director.RecoverFromPreviousInstance();
                    npcs.RecoverLeftovers();
                }
                if (IS_PAUSE_MENU_ACTIVE()) { npcs.ClearTags(); return; }

                string[] cargs;
                while (consoleQueue.TryDequeue(out cargs)) HandleConsole(cargs);

                double now = G.Now;
                if (readySince < 0) readySince = now;
                if (lastTick > 0 && now - lastTick > 3.0) Log("El script estuvo detenido " + (now - lastTick).ToString("0") + " s (pausa, carga o juego sin foco)");
                lastTick = now;
                if (focusWarningPending && now - readySince > 3)
                {
                    focusWarningPending = false;
                    Sub("KickChaos: FusionFix tenia el juego frenandose sin foco. Ya quedo corregido para la proxima vez; para ahora cambia 'Focus Loss' en Pantalla", 9000);
                }

                if (!nativesOk) return;
                if (!autoStarted && director.AutoStart && now - readySince > director.StartDelay)
                {
                    autoStarted = true;
                    director.Start();
                }

                Keys k;
                while (keyQueue.TryDequeue(out k)) HandleKey(k);

                bool focused = G.IsGameFocused();
                bool wasOpen = kmenu.Menu.IsOpen;
                kmenu.Menu.Update(focused);
                if (wasOpen && !kmenu.Menu.IsOpen) menuClosedAt = G.Now;
                // con el menu abierto el juego no recibe el teclado (ni la P de pausa, ni el telefono).
                // Tampoco cuando estas en otra ventana: KickChaosSonido.asi hace que el juego crea que
                // sigue activo, asi que lo que escribas en OBS o en el chat no le tiene que llegar.
                if (kmenu.Menu.IsOpen || !focused)
                {
                    SET_TEXT_INPUT_ACTIVE(true);
                    DISABLE_PAUSE_MENU(true);
                    menuBlockedInput = true;
                }
                else if (menuBlockedInput)
                {
                    menuBlockedInput = false;
                    SET_TEXT_INPUT_ACTIVE(false);
                    DISABLE_PAUSE_MENU(false);
                }
                // con el menu abierto Niko no se mueve con las flechas (si el director no lo tiene ya quieto)
                if ((kmenu.Menu.IsOpen || !focused) && !director.Active && !director.EditorActive)
                {
                    SET_PLAYER_CONTROL(G.PlayerIndex, false);
                    menuTookControl = true;
                }
                else if (menuTookControl)
                {
                    menuTookControl = false;
                    if (!director.Active && !director.EditorActive) SET_PLAYER_CONTROL(G.PlayerIndex, true);
                }

                DrainPendingActions();
                ReceiveKickEvents(now);
                int eventBudget = 25;
                while (pendingActions.Count == 0 && eventBudget-- > 0)
                {
                    KickEvent delayed = delayedEvents.TryDequeue(now);
                    if (delayed == null) break;
                    ProcessKickEvent(delayed, now);
                    DrainPendingActions();
                }
                RetryWaitingNpcs(now);
                if (director.ReadyForAction())
                {
                    if (slotOverflow == null)
                    {
                        QueuedAction qa = queue.TryDequeue(now, cfg.GlobalSpacing);
                        if (qa != null) RunAction(qa);
                    }
                }
                DrainPendingActions();

                director.Update(queue.Count > 0 || actions.PendingJobs > 0);
                actions.Update();
                UpdateNpcs(now);
                adminBridge.Update(npcs, director, cfg, folder, KickStatus(), Log);

                if (obsClearAt > 0 && now > obsClearAt) { obsClearAt = -1; WriteObs(""); }
            }
            catch (Exception ex)
            {
                string msg = ex.GetType().Name + ": " + ex.Message;
                double t = G.Now;
                if (msg != lastError || t - lastErrorLog > 10)
                {
                    lastError = msg;
                    lastErrorLog = t;
                    Log("ERROR en Tick: " + ex);
                }
            }
        }

        /// <summary>Los NPC de suscriptores y los carteles con su nombre (con la camara de este frame).</summary>
        void UpdateNpcs(double now)
        {
            if (now - lastAspectRead > 2.0)
            {
                lastAspectRead = now;
                try
                {
                    var r = GTA.Game.Resolution;
                    if (r.Width > 0 && r.Height > 0) { aspect = r.Width / (float)r.Height; Menu.Aspect = aspect; }
                }
                catch { }
            }
            npcs.Update();
            if (npcs.Count == 0) { npcs.ClearTags(); return; }
            System.Numerics.Vector3 cp, cr;
            float cf;
            director.ViewPose(out cp, out cr, out cf);
            npcs.BuildTags(cp, cr, cf, aspect, director.SceneVisible());
        }

        void NpcNotice(string text)
        {
            WriteObs(text);
            obsClearAt = cfg.ObsClearSeconds > 0 ? G.Now + cfg.ObsClearSeconds : -1;
            if (cfg.ShowInGameNotice) Sub(text, 3000);
        }

        void HandleKickEvent(KickEvent ev)
        {
            if (ev == null) return;
            // Sólo se filtra chat viejo antes de entrar: el delay configurado no lo vuelve viejo.
            if (ev.Kind == KickEventKind.Chat && !ev.Simulated && (DateTime.UtcNow - ev.ReceivedUtc).TotalSeconds > 30) return;
            if (!delayedEvents.TryEnqueue(ev, G.Now, cfg))
            {
                // Las pruebas se piden desde el juego; el lector conserva la misma
                // entrada hasta poder admitirla sin descartar una suscripción.
                if (incomingEvent == null) incomingEvent = ev;
                else Log("[Kick] cola llena; no se aceptó la prueba adicional de " + ev.User);
            }
        }

        void ReceiveKickEvents(double now)
        {
            if (incomingEvent != null)
            {
                if (!delayedEvents.TryEnqueue(incomingEvent, now, cfg)) return;
                incomingEvent = null;
            }
            if (kick == null) return;
            int budget = 50;
            KickEvent ev;
            while (budget-- > 0 && kick.Events.TryDequeue(out ev))
            {
                if (ev.Kind == KickEventKind.Chat && !ev.Simulated && (DateTime.UtcNow - ev.ReceivedUtc).TotalSeconds > 30) continue;
                if (!delayedEvents.TryEnqueue(ev, now, cfg)) { incomingEvent = ev; break; }
            }
            if (now - lastStatusLog > 300)
            {
                lastStatusLog = now;
                Log("[Estado] Kick: " + kick.Status + ", mensajes: " + kick.MessagesReceived + ", eventos demorados: " + delayedEvents.Count);
            }
        }

        void ProcessKickEvent(KickEvent ev, double now)
        {
            if (cfg.IgnoredUsers.Contains(ev.User ?? "")) return;
            if (ev.Kind == KickEventKind.Chat && npcs != null)
            {
                try { npcs.SetUserColor(ev.User, ev.Color); npcs.OnChat(ev.User, ev.Text, ev.Level); }
                catch (Exception ex) { Log("[NPC] error con un mensaje del chat: " + ex.Message); }
            }
            else
            {
                string kind = "evento", text = ev.EventName;
                switch (ev.Kind)
                {
                    case KickEventKind.Subscription: kind = "sub"; text = "se suscribió"; break;
                    case KickEventKind.GiftedSubs: kind = "sub"; text = "regaló " + ev.Count + " suscripciones"; break;
                    case KickEventKind.Follow: kind = "follow"; text = "comenzó a seguir"; break;
                    case KickEventKind.Host: kind = "host"; text = "llegó con " + ev.Count + " viewers"; break;
                }
                if (KickDonation.IsDonation(ev))
                {
                    kind = "kicks"; text = "donó " + ev.Count + " Kicks";
                    double seconds = KickDonation.CameraSeconds(ev.Count, cfg);
                    int ped = npcs.FindOwnedNpc(ev.User, true);
                    if (ped != 0 && seconds > 0)
                        director.ObserveNpc(ped, seconds, "Kicks", 70, false);
                }
                StreamRuntime.AddFeed(kind, ev.User, text, now, ev.Count);
                npcs.MarkRecentEvent(ev.User, now + 60);
                foreach (string recipient in ev.Recipients) npcs.MarkRecentEvent(recipient, now + 60);
            }
            string why;
            List<PendingAction> list = engine.Process(ev, DateTime.UtcNow, out why);
            if (ev.Kind != KickEventKind.Chat)
                Log("[Kick] evento procesado " + ev.Kind + " de " + ev.User + " (x" + ev.Count + ")" + (list.Count == 0 && why != null ? " -> " + why : ""));
            foreach (var pa in list) Enqueue(pa);
        }

        void Enqueue(PendingAction pa)
        {
            if (pa == null) return;
            // Una entrada puede tener varios destinatarios. Sólo se procesa otra
            // entrada de Kick después de admitir completamente ésta.
            if (pendingActions.Count >= 128)
            {
                Log("Cola de pedidos completa, se rechaza la prueba/chat adicional de " + pa.User);
                return;
            }
            pendingActions.Enqueue(pa);
            DrainPendingActions();
        }

        void DrainPendingActions()
        {
            int budget = 128;
            while (pendingActions.Count > 0 && budget-- > 0)
            {
                PendingAction pa = pendingActions.Peek();
                var unknown = new List<string>();
                int n = queue.Enqueue(pa, cfg, ActionNames.Canonical, unknown);
                foreach (string u in unknown) Log("Accion desconocida: " + u);
                if (n > 0) Log("En cola: " + pa + " (" + pa.User + ", " + pa.Source + ")");
                if (!pa.QueueComplete) break;
                pendingActions.Dequeue();
                if (n == 0 && pa.Prepared != null && pa.Prepared.Count > 0 && !pa.FromEvent)
                    Log("Cola llena, se descarta chat de " + pa.User);
            }
        }

        void RetryWaitingNpcs(double now)
        {
            if (now < npcSlotRetryAt) return;
            npcSlotRetryAt = now + 5;
            if (slotOverflow != null && waitingForNpcSlot.Count < 32)
            {
                waitingForNpcSlot.Add(slotOverflow); slotOverflow = null;
            }
            for (int i = 0; i < waitingForNpcSlot.Count; i++)
            {
                QueuedAction qa = waitingForNpcSlot[i];
                if (npcs.SpawnCapacityFull(qa)) continue;
                waitingForNpcSlot.RemoveAt(i);
                RunAction(qa);
                break; // un solo spawn por intento mantiene la carga suave
            }
            if ((waitingForNpcSlot.Count > 0 || slotOverflow != null) && now - npcSlotNoticeAt >= 60)
            {
                npcSlotNoticeAt = now;
                Log("[NPC] " + (waitingForNpcSlot.Count + (slotOverflow != null ? 1 : 0)) + " apariciones esperando lugar; los NPC vivos se conservan");
            }
        }

        void RunAction(QueuedAction qa)
        {
            bool npcAction = qa.Name.StartsWith("Npc", StringComparison.Ordinal);
            if (npcAction && qa.FromEvent && npcs.SpawnCapacityFull(qa))
            {
                if (waitingForNpcSlot.Count < 32) waitingForNpcSlot.Add(qa);
                else slotOverflow = qa;
                Log("[NPC] " + qa.User + " espera lugar para aparecer; la cola lo conserva");
                StreamRuntime.AddFeed("espera", qa.User, "espera un lugar en la ciudad", G.Now);
                return;
            }
            if (KickDonation.StartsDirector(qa, npcs.FollowOnSpawn, director.Active))
            {
                if (director.EditorActive) director.ToggleEditor();
                director.Start();
                autoStarted = true;
            }
            bool ok;
            try { ok = actions.Execute(qa); }
            catch (Exception ex) { Log("ERROR ejecutando " + qa.Name + ": " + ex.Message); return; }
            if (!ok)
            {
                Log("[Accion] no se pudo ejecutar " + qa.Name + " de " + qa.User);
                StreamRuntime.AddFeed("error", qa.User, "no pudo aparecer/ejecutar " + qa.Name + "; revisar log", G.Now);
                return;
            }

            string label = actions.LastLabel;
            if (label == null && !ActionNames.Display.TryGetValue(qa.Name, out label)) label = qa.Name.ToUpperInvariant();
            string who = string.IsNullOrEmpty(qa.User) ? "" : qa.User;
            string text = who.Length > 0 ? who + " -> " + label : label;
            WriteObs(text);
            obsClearAt = cfg.ObsClearSeconds > 0 ? G.Now + cfg.ObsClearSeconds : -1;
            if (cfg.ShowInGameNotice) Sub(text, 3000);
            Log("Accion: " + qa.Name + " (" + who + ")");
        }

        // ------------------------------------------------------------------
        // Archivos
        // ------------------------------------------------------------------
        void WriteObs(string text)
        {
            if (cfg == null || string.IsNullOrEmpty(cfg.ObsFile) || folder == null) return;
            try { File.WriteAllText(Path.Combine(folder, cfg.ObsFile), text, new UTF8Encoding(false)); }
            catch { }
        }

        void Log(string s)
        {
            if (folder == null) return;
            string line = DateTime.Now.ToString("HH:mm:ss") + "  " + s;
            lock (logLock)
            {
                try
                {
                    string path = Path.Combine(folder, "log.txt");
                    var fi = new FileInfo(path);
                    if (fi.Exists && fi.Length > 2 * 1024 * 1024) File.Delete(path);
                    File.AppendAllText(path, line + Environment.NewLine, new UTF8Encoding(false));
                }
                catch { }
            }
        }
    }
}
