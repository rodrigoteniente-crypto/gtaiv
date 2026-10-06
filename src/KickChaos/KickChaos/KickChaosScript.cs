using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using GTA;
using GTA.@base;

namespace KickChaos;

public class KickChaosScript : Script
{
	public const string Version = "1.6.3-community";

	private Config cfg;

	private KickClient kick;

	private TriggerEngine engine;

	private Director director;

	private ChaosActions actions;

	private SubNpcManager npcs;

	private readonly ActionQueue queue = new ActionQueue();

	private float aspect = 1.7777778f;

	private double lastAspectRead = -100.0;

	private string folder;

	private readonly object logLock = new object();

	private readonly ConcurrentQueue<Keys> keyQueue = new ConcurrentQueue<Keys>();

	private readonly ConcurrentQueue<string[]> consoleQueue = new ConcurrentQueue<string[]>();

	private KickMenu kmenu;

	private MenuTheme theme = new MenuTheme();

	private bool menuTookControl;

	private bool menuBlockedInput;

	private double menuClosedAt = -10.0;

	private double readySince = -1.0;

	private double obsClearAt = -1.0;

	private double lastStatusLog;

	private double lastTick = -1.0;

	private double lastErrorLog = -100.0;

	private readonly Dictionary<Keys, double> lastShortcutAt = new Dictionary<Keys, double>();

	private bool autoStarted;

	private bool initialized;

	private bool focusWarningPending;

	private bool wasReady;

	private bool firstReadyDone;

	private bool nativesOk = true;

	private string lastError;

	internal Config Cfg => cfg;

	internal Director Dir => director;

	internal SubNpcManager Npcs => npcs;

	internal string Folder => folder;

	internal string GameFolder => Game.InstallFolder;

	public KickChaosScript()
	{
		Interval = 0;
		((Script)this).Tick += OnTick;
		((Script)this).KeyDown += new KeyEventHandler(OnKeyDown);
		try
		{
			folder = Path.Combine(Path.Combine(Game.InstallFolder, "scripts"), "KickChaos");
			Directory.CreateDirectory(folder);
			Log("==== KickChaos IV " + Version + " (ScriptHookDotNet) iniciado ====");
			LoadAll(first: true);
			theme = MenuTheme.FromLibertysLegacy(GameFolder);
			kmenu = new KickMenu(this);
			((Script)this).PerFrameDrawing += new GraphicsEventHandler(OnDraw);
			BindConsoleCommand("kick", new ConsoleCommandDelegate(OnConsoleCommand), "KickChaos: kick estado | kick sub | kick chat <usuario> <mensaje> | kick accion <Accion> ...");
			CheckFusionFix();
			initialized = true;
		}
		catch (Exception ex)
		{
			Log("ERROR al iniciar: " + ex);
			try
			{
				Game.Console.Print("[KickChaos] error al iniciar: " + ex.Message);
			}
			catch
			{
			}
		}
	}

	private void OnDraw(object sender, GraphicsEventArgs e)
	{
		try
		{
			if (npcs != null)
			{
				npcs.DrawTags(e.Graphics);
			}
		}
		catch
		{
		}
		try
		{
			if (kmenu != null && kmenu.Menu.IsOpen)
			{
				kmenu.Menu.Draw(e.Graphics, theme);
			}
		}
		catch
		{
		}
	}

	internal void TestNpc(string source, int count)
	{
		Enqueue(new PendingAction
		{
			Steps = Config.ParseSteps("Npc"),
			User = "prueba",
			Source = source,
			Count = Math.Max(1, count),
			FromEvent = true
		});
	}

	internal int QueueCount()
	{
		return queue.Count;
	}

	internal void ClearQueue()
	{
		queue.Clear();
	}

	internal string KickStatus()
	{
		if (kick == null)
		{
			return "desconectado (apagado en la configuracion)";
		}
		return kick.Status + "  -  " + kick.MessagesReceived + " mensajes";
	}

	internal List<string> SeenOtherEvents()
	{
		List<string> list = new List<string>();
		if (kick != null)
		{
			foreach (KeyValuePair<string, DateTime> item in kick.SeenOther)
			{
				list.Add(item.Key);
			}
		}
		list.Sort();
		return list;
	}

	internal void ToggleDirector()
	{
		if (director.Active)
		{
			actions.StopAll();
			director.Stop();
		}
		else
		{
			if (director.EditorActive)
			{
				director.ToggleEditor();
			}
			director.Start();
		}
		autoStarted = true;
	}

	internal bool FollowNpcCamera()
	{
		bool following = director.FollowNextNpc();
		if (following) autoStarted = true;
		return following;
	}

	internal void SimulateChat(string user, string text)
	{
		HandleKickEvent(new KickEvent
		{
			Kind = KickEventKind.Chat,
			User = user,
			Text = text,
			Simulated = true,
			EventName = "ChatMessageEvent"
		});
	}

	internal void SoftReload()
	{
		List<string> list = new List<string>();
		cfg = Config.Load(folder, list);
		director.ApplyConfig(cfg);
		engine.SetConfig(cfg);
		actions.SetConfig(cfg);
		npcs.ApplyConfig(cfg);
		foreach (string item in list)
		{
			Log("AVISO: " + item);
		}
	}

	internal void FullReload()
	{
		Reload();
		theme = MenuTheme.FromLibertysLegacy(GameFolder);
	}

	private void OnConsoleCommand(ParameterCollection p)
	{
		string[] array = new string[((BaseCollection<string>)(object)p).Count];
		for (int i = 0; i < ((BaseCollection<string>)(object)p).Count; i++)
		{
			array[i] = p.ToString(i);
		}
		consoleQueue.Enqueue(array);
	}

	private void Sub(string text, uint ms)
	{
		Game.DisplayText(text, (int)ms);
	}

	private void LoadAll(bool first)
	{
		List<string> list = new List<string>();
		cfg = Config.Load(folder, list);
		if (director == null)
		{
			director = new Director(cfg, Log, Sub);
		}
		else
		{
			director.ApplyConfig(cfg);
		}
		director.LoadShots(list);
		if (engine == null)
		{
			engine = new TriggerEngine(cfg);
		}
		else
		{
			engine.SetConfig(cfg);
		}
		if (actions == null)
		{
			actions = new ChaosActions(cfg, director, Log);
		}
		else
		{
			actions.SetConfig(cfg);
		}
		director.Actions = actions;
		if (npcs == null)
		{
			npcs = new SubNpcManager(cfg, director, Log, NpcNotice);
		}
		else
		{
			npcs.ApplyConfig(cfg);
		}
		director.Npcs = npcs;
		director.Repair.IsActiveNpcZone = (position, radius) => npcs.AnyNear(position, radius);
		actions.Npcs = npcs;
		foreach (ChatTrigger trigger in cfg.Triggers)
		{
			foreach (ActionStep step in trigger.Steps)
			{
				if (ActionNames.Canonical(step.Name) == null)
				{
					list.Add("Accion desconocida '" + step.Name + "' en el comando '" + trigger.Keyword + "'");
				}
			}
		}
		foreach (KeyValuePair<string, List<ActionStep>> item in cfg.EventMap)
		{
			foreach (ActionStep item2 in item.Value)
			{
				if (ActionNames.Canonical(item2.Name) == null)
				{
					list.Add("Accion desconocida '" + item2.Name + "' en el evento '" + item.Key + "'");
				}
			}
		}
		foreach (string item3 in list)
		{
			Log("AVISO: " + item3);
			try
			{
				Game.Console.Print("[KickChaos] " + item3);
			}
			catch
			{
			}
		}
		Log("Config cargada: " + cfg.Triggers.Count + " comandos, " + cfg.EventMap.Count + " eventos, " + director.Shots.Count + " camaras");
		if (kick != null)
		{
			kick.Stop();
		}
		kick = null;
		try
		{
			if (AppDomain.CurrentDomain.GetData("KickChaos.StopKick") is Action action)
			{
				action();
			}
			AppDomain.CurrentDomain.SetData("KickChaos.StopKick", null);
		}
		catch
		{
		}
		if (cfg.KickEnabled)
		{
			kick = new KickClient(cfg, Log);
			kick.Start();
			try
			{
				AppDomain.CurrentDomain.SetData("KickChaos.StopKick", new Action(kick.Stop));
			}
			catch
			{
			}
		}
		WriteObs(string.Empty);
		if (!first)
		{
			Sub("KickChaos: configuracion recargada", 2500u);
		}
	}

	protected override void Dispose(bool disposing)
	{
		try
		{
			if (kick != null)
			{
				kick.Stop();
			}
		}
		catch
		{
		}
		try
		{
			if (kmenu != null)
			{
				kmenu.Menu.DisposeFonts();
			}
		}
		catch
		{
		}
		try
		{
			if (npcs != null)
			{
				npcs.DisposeFonts();
			}
		}
		catch
		{
		}
		Log("KickChaos detenido");
		base.Dispose(disposing);
	}

	private void CheckFusionFix()
	{
		try
		{
			string path = Path.Combine(Path.Combine(Game.InstallFolder, "plugins"), "GTAIV.EFLC.FusionFix.cfg");
			if (!File.Exists(path))
			{
				return;
			}
			IniFile iniFile = IniFile.Load(path);
			if (iniFile.GetInt("MAIN", "BlockOnLostFocus", 0) != 1)
			{
				try
				{
					IniDoc iniDoc = IniDoc.Load(path);
					iniDoc.Set("MAIN", "BlockOnLostFocus", "1");
					iniDoc.Save();
					Log("AVISO: FusionFix tenia BlockOnLostFocus = 0 (el juego se frena sin foco). Se corrigio a 1 en el archivo; vale desde la proxima vez que abras el juego. Para ahora: menu de pausa > Pantalla > 'Focus Loss'.");
				}
				catch (Exception ex)
				{
					Log("AVISO: no se pudo corregir BlockOnLostFocus en FusionFix: " + ex.Message);
				}
				if (!File.Exists(Path.Combine(Game.InstallFolder, "KickChaosSonido.asi")))
				{
					focusWarningPending = true;
				}
			}
		}
		catch
		{
		}
	}

	private void OnKeyDown(object sender, KeyEventArgs e)
	{
		keyQueue.Enqueue(e.KeyWithModifiers);
	}

	private void HandleKey(Keys data)
	{
		if (!G.IsGameFocused())
		{
			return;
		}
		Keys val = (Keys)(data & Keys.KeyCode);
		bool asEndPoint = (data & Keys.Shift) != 0;
		if ((data & (Keys.Control | Keys.Alt)) != 0)
		{
			return;
		}
		// GTA emits repeated key-down events while a key is held. Debounce toggles
		// without slowing down menu navigation or the free-camera movement keys.
		if (val == cfg.KeyMenu || val == cfg.KeyReload || val == cfg.KeyDirector || val == cfg.KeyEditor || val == cfg.KeyNextCam || val == cfg.KeyFollowNpc)
		{
			double previous;
			if (lastShortcutAt.TryGetValue(val, out previous) && G.Now - previous < 0.6) return;
			lastShortcutAt[val] = G.Now;
		}
		if (val == cfg.KeyMenu)
		{
			if (director.EditorActive)
			{
				director.ToggleEditor();
			}
			kmenu.Menu.Toggle();
			if (!kmenu.Menu.IsOpen)
			{
				menuClosedAt = G.Now;
			}
		}
		else
		{
			if (kmenu.Menu.IsOpen || (G.Now - menuClosedAt < 0.4 && ((int)val == 13 || (int)val == 8 || (int)val == 84)))
			{
				return;
			}
			if (director.EditorActive)
			{
				if ((int)val == 13)
				{
					director.EditorSave(asEndPoint);
					return;
				}
				if ((int)val == 8)
				{
					director.EditorDeleteLast();
					return;
				}
				if ((int)val == 84)
				{
					director.EditorSetTarget();
					return;
				}
			}
			string value;
			if (val == cfg.KeyEditor)
			{
				director.ToggleEditor();
			}
			else if (val == cfg.KeyDirector)
			{
				ToggleDirector();
			}
			else if (val == cfg.KeyNextCam)
			{
				if (director.Active)
				{
					director.Next();
				}
			}
			else if (val == cfg.KeyFollowNpc)
			{
				FollowNpcCamera();
			}
			else if (val == cfg.KeyReload)
			{
				Reload();
			}
			else if (cfg.TestKeys.TryGetValue(val, out value))
			{
				RunTest(value);
			}
		}
	}

	internal void Reload()
	{
		bool active = director.Active;
		actions.StopAll();
		queue.Clear();
		if (director.EditorActive)
		{
			director.ToggleEditor();
		}
		if (active)
		{
			director.Stop();
		}
		LoadAll(first: false);
		if (active)
		{
			director.Start();
		}
	}

	internal void RunTest(string what)
	{
		what = what.Trim();
		if (what.StartsWith("evento:", StringComparison.OrdinalIgnoreCase))
		{
			string[] array = what.Substring(7).Split(new char[1] { ':' });
			KickEvent kickEvent = new KickEvent();
			kickEvent.User = "prueba";
			kickEvent.Simulated = true;
			kickEvent.EventName = array[0].Trim();
			KickEvent kickEvent2 = kickEvent;
			int result = 1;
			if (array.Length > 1)
			{
				int.TryParse(array[1], out result);
			}
			kickEvent2.Count = Math.Max(1, result);
			switch (Config.Normalize(array[0]).Trim())
			{
			case "suscripcion":
			case "sub":
				kickEvent2.Kind = KickEventKind.Subscription;
				break;
			case "regalosubs":
			case "regalo":
				kickEvent2.Kind = KickEventKind.GiftedSubs;
				break;
			case "follow":
				kickEvent2.Kind = KickEventKind.Follow;
				break;
			case "host":
			case "raid":
				kickEvent2.Kind = KickEventKind.Host;
				break;
			default:
				kickEvent2.Kind = KickEventKind.Other;
				break;
			}
			HandleKickEvent(kickEvent2);
		}
		else
		{
			PendingAction pendingAction = new PendingAction();
			pendingAction.Steps = Config.ParseSteps(what);
			pendingAction.User = "prueba";
			pendingAction.Source = "tecla";
			pendingAction.FromEvent = true;
			PendingAction pa = pendingAction;
			Enqueue(pa);
		}
	}

	private void HandleConsole(string[] args)
	{
		string text = ((args.Length <= 0) ? "ayuda" : Config.Normalize(args[0]));
		string text2 = ((args.Length <= 1) ? string.Empty : string.Join(" ", args, 1, args.Length - 1));
		switch (text)
		{
		case "estado":
		case "status":
			Print("Kick: " + ((kick != null) ? kick.Status : "desactivado") + " | mensajes: " + ((kick != null) ? kick.MessagesReceived : 0));
			Print("Director: " + director.StatusText() + " | camaras: " + director.Shots.Count + " | cola: " + queue.Count);
			break;
		case "chat":
		{
			string user = ((args.Length <= 1) ? "prueba" : args[1]);
			string text3 = ((args.Length <= 2) ? string.Empty : string.Join(" ", args, 2, args.Length - 2));
			HandleKickEvent(new KickEvent
			{
				Kind = KickEventKind.Chat,
				User = user,
				Text = text3,
				Simulated = true,
				EventName = "ChatMessageEvent"
			});
			break;
		}
		case "sub":
			RunTest("evento:Suscripcion");
			break;
		case "regalo":
			RunTest("evento:RegaloSubs:" + ((text2.Length <= 0) ? "5" : text2));
			break;
		case "follow":
			RunTest("evento:Follow");
			break;
		case "npc":
		{
			int result = 1;
			if (args.Length > 1)
			{
				int.TryParse(args[1], out result);
			}
			TestNpc("Suscripcion", result);
			break;
		}
		case "host":
			RunTest("evento:Host");
			break;
		case "accion":
		case "action":
			RunTest(text2);
			break;
		case "recargar":
		case "reload":
			Reload();
			break;
		case "director":
			if (director.Active)
			{
				actions.StopAll();
				director.Stop();
			}
			else
			{
				director.Start();
			}
			autoStarted = true;
			break;
		case "editor":
			director.ToggleEditor();
			break;
		case "camara":
		case "siguiente":
			director.Next();
			break;
		case "seguirnpc":
			FollowNpcCamera();
			break;
		case "loop":
			director.ReturnToCameraLoop();
			break;
		case "aqui":
		case "centro":
			director.SetHubHere();
			Print("Centro de las camaras automaticas = posicion actual");
			break;
		case "acciones":
			Print("Acciones: " + string.Join(", ", new List<string>(ActionNames.Display.Keys).ToArray()));
			break;
		default:
			Print("kick estado | kick chat <usuario> <mensaje> | kick sub | kick regalo <n> | kick follow | kick host | kick npc <meses>");
			Print("kick accion <Accion> | kick acciones | kick director | kick editor | kick camara | kick seguirnpc | kick loop | kick aqui | kick recargar");
			break;
		}
	}

	private void Print(string s)
	{
		try
		{
			Game.Console.Print("[KickChaos] " + s);
		}
		catch
		{
		}
	}

	private void OnTick(object sender, EventArgs e)
	{
		if (!initialized)
		{
			return;
		}
		try
		{
			if (!G.IsPlayerReady())
			{
				if (wasReady)
				{
					try
					{
						actions.StopAll();
						director.QuickTeardown();
					}
					catch
					{
					}
					try
					{
						npcs.ReleaseAll();
					}
					catch
					{
					}
					director.ForgetState();
					actions.ForgetState();
					npcs.ForgetState();
					queue.Clear();
					autoStarted = false;
				}
				wasReady = false;
				readySince = -1.0;
				return;
			}
			wasReady = true;
			if (!firstReadyDone)
			{
				firstReadyDone = true;
				List<string> list = N.Validate();
				if (list.Count > 0)
				{
					Log("AVISO: el juego no tiene estos natives, se desactivan: " + string.Join(", ", list.ToArray()));
					string[] array = new string[11]
					{
						"CREATE_CAM", "SET_CAM_POS", "SET_CAM_ROT", "SET_CAM_FOV", "SET_CAM_ACTIVE", "ACTIVATE_SCRIPTED_CAMS", "SET_CHAR_COORDINATES", "FREEZE_CHAR_POSITION", "SET_CHAR_VISIBLE", "GET_PLAYER_CHAR",
						"GET_CHAR_COORDINATES"
					};
					string[] array2 = array;
					foreach (string item in array2)
					{
						if (list.Contains(item))
						{
							nativesOk = false;
						}
					}
					if (!nativesOk)
					{
						Log("ERROR: faltan natives basicos, el director no puede funcionar en este juego.");
						Sub("KickChaos: faltan funciones del juego, mira scripts\\KickChaos\\log.txt", 8000u);
					}
				}
				director.RecoverFromPreviousInstance();
				npcs.RecoverLeftovers();
			}
			if (N.IS_PAUSE_MENU_ACTIVE())
			{
				npcs.ClearTags();
				return;
			}
			string[] result;
			while (consoleQueue.TryDequeue(out result))
			{
				HandleConsole(result);
			}
			double now = G.Now;
			if (readySince < 0.0)
			{
				readySince = now;
			}
			if (lastTick > 0.0 && now - lastTick > 3.0)
			{
				Log("El script estuvo detenido " + (now - lastTick).ToString("0") + " s (pausa, carga o juego sin foco)");
			}
			lastTick = now;
			if (focusWarningPending && now - readySince > 3.0)
			{
				focusWarningPending = false;
				Sub("KickChaos: FusionFix tenia el juego frenandose sin foco. Ya quedo corregido para la proxima vez; para ahora cambia 'Focus Loss' en Pantalla", 9000u);
			}
			if (!nativesOk)
			{
				return;
			}
			if (!autoStarted && director.AutoStart && now - readySince > (double)director.StartDelay)
			{
				autoStarted = true;
				director.Start();
			}
			Keys result2;
			while (keyQueue.TryDequeue(out result2))
			{
				HandleKey(result2);
			}
			bool flag = G.IsGameFocused();
			bool isOpen = kmenu.Menu.IsOpen;
			kmenu.Menu.Update(flag);
			if (isOpen && !kmenu.Menu.IsOpen)
			{
				menuClosedAt = G.Now;
			}
			if (kmenu.Menu.IsOpen || !flag)
			{
				N.SET_TEXT_INPUT_ACTIVE(v: true);
				N.DISABLE_PAUSE_MENU(v: true);
				menuBlockedInput = true;
			}
			else if (menuBlockedInput)
			{
				menuBlockedInput = false;
				N.SET_TEXT_INPUT_ACTIVE(v: false);
				N.DISABLE_PAUSE_MENU(v: false);
			}
			if ((kmenu.Menu.IsOpen || !flag) && !director.Active && !director.EditorActive)
			{
				N.SET_PLAYER_CONTROL(G.PlayerIndex, v: false);
				menuTookControl = true;
			}
			else if (menuTookControl)
			{
				menuTookControl = false;
				if (!director.Active && !director.EditorActive)
				{
					N.SET_PLAYER_CONTROL(G.PlayerIndex, v: true);
				}
			}
			if (kick != null)
			{
				int num = 50;
				KickEvent result3;
				while (num-- > 0 && kick.Events.TryDequeue(out result3))
				{
					HandleKickEvent(result3);
				}
				if (now - lastStatusLog > 300.0)
				{
					lastStatusLog = now;
					Log("[Estado] Kick: " + kick.Status + ", mensajes: " + kick.MessagesReceived);
				}
			}
			if (director.ReadyForAction())
			{
				QueuedAction queuedAction = queue.TryDequeue(now, cfg.GlobalSpacing);
				if (queuedAction != null)
				{
					RunAction(queuedAction);
				}
			}
			director.Update(queue.Count > 0 || actions.PendingJobs > 0);
			actions.Update();
			UpdateNpcs(now);
			if (obsClearAt > 0.0 && now > obsClearAt)
			{
				obsClearAt = -1.0;
				WriteObs(string.Empty);
			}
		}
		catch (Exception ex)
		{
			string text = ex.GetType().Name + ": " + ex.Message;
			double now2 = G.Now;
			if (text != lastError || now2 - lastErrorLog > 10.0)
			{
				lastError = text;
				lastErrorLog = now2;
				Log("ERROR en Tick: " + ex);
			}
		}
	}

	private void UpdateNpcs(double now)
	{
		if (now - lastAspectRead > 2.0)
		{
			lastAspectRead = now;
			try
			{
				Size resolution = Game.Resolution;
				if (resolution.Width > 0 && resolution.Height > 0)
				{
					aspect = (float)resolution.Width / (float)resolution.Height;
					Menu.Aspect = aspect;
				}
			}
			catch
			{
			}
		}
		npcs.Update();
		if (npcs.Count == 0)
		{
			npcs.ClearTags();
			return;
		}
		director.ViewPose(out var pos, out var rot, out var fov);
		npcs.BuildTags(pos, rot, fov, aspect, director.SceneVisible());
	}

	private void NpcNotice(string text)
	{
		WriteObs(text);
		obsClearAt = ((cfg.ObsClearSeconds <= 0) ? (-1.0) : (G.Now + (double)cfg.ObsClearSeconds));
		if (cfg.ShowInGameNotice)
		{
			Sub(text, 3000u);
		}
	}

	private void HandleKickEvent(KickEvent ev)
	{
		if (ev.Kind == KickEventKind.Chat && !ev.Simulated && (DateTime.UtcNow - ev.ReceivedUtc).TotalSeconds > 30.0)
		{
			return;
		}
		if (ev.Kind == KickEventKind.Chat && npcs != null)
		{
			try
			{
				npcs.OnChat(ev.User, ev.Text, ev.Level);
			}
			catch (Exception ex)
			{
				Log("[NPC] error con un mensaje del chat: " + ex.Message);
			}
		}
		string why;
		List<PendingAction> list = engine.Process(ev, DateTime.UtcNow, out why);
		if (ev.Kind != KickEventKind.Chat)
		{
			Log(string.Concat("[Kick] evento ", ev.Kind, " de ", ev.User, " (x", ev.Count, ")", (list.Count != 0 || why == null) ? string.Empty : (" -> " + why)));
		}
		foreach (PendingAction item in list)
		{
			Enqueue(item);
		}
	}

	private void Enqueue(PendingAction pa)
	{
		List<string> list = new List<string>();
		int num = queue.Enqueue(pa, cfg, ActionNames.Canonical, list);
		foreach (string item in list)
		{
			Log("Accion desconocida: " + item);
		}
		if (num == 0 && list.Count == 0)
		{
			Log(string.Concat("Cola llena, se descarta ", pa, " de ", pa.User));
		}
		else if (num > 0)
		{
			Log(string.Concat("En cola: ", pa, " (", pa.User, ", ", pa.Source, ")"));
		}
	}

	private void RunAction(QueuedAction qa)
	{
		bool flag;
		try
		{
			flag = actions.Execute(qa);
		}
		catch (Exception ex)
		{
			Log("ERROR ejecutando " + qa.Name + ": " + ex.Message);
			return;
		}
		if (flag)
		{
			string value = actions.LastLabel;
			if (value == null && !ActionNames.Display.TryGetValue(qa.Name, out value))
			{
				value = qa.Name.ToUpperInvariant();
			}
			string text = ((!string.IsNullOrEmpty(qa.User)) ? qa.User : string.Empty);
			string text2 = ((text.Length <= 0) ? value : (text + " -> " + value));
			WriteObs(text2);
			obsClearAt = ((cfg.ObsClearSeconds <= 0) ? (-1.0) : (G.Now + (double)cfg.ObsClearSeconds));
			if (cfg.ShowInGameNotice)
			{
				Sub(text2, 3000u);
			}
			Log("Accion: " + qa.Name + " (" + text + ")");
		}
	}

	private void WriteObs(string text)
	{
		if (cfg == null || string.IsNullOrEmpty(cfg.ObsFile) || folder == null)
		{
			return;
		}
		try
		{
			File.WriteAllText(Path.Combine(folder, cfg.ObsFile), text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
		}
		catch
		{
		}
	}

	private void Log(string s)
	{
		if (folder == null)
		{
			return;
		}
		string text = DateTime.Now.ToString("HH:mm:ss") + "  " + s;
		lock (logLock)
		{
			try
			{
				string text2 = Path.Combine(folder, "log.txt");
				FileInfo fileInfo = new FileInfo(text2);
				if (fileInfo.Exists && fileInfo.Length > 2097152)
				{
					File.Delete(text2);
				}
				File.AppendAllText(text2, text + Environment.NewLine, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			}
			catch
			{
			}
		}
	}
}
