using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using System.Windows.Forms;

namespace KickChaos;

public class Director
{
	private enum Phase
	{
		Idle,
		FadingOut,
		Loading,
		Showing
	}

	private Config cfg;

	private readonly Action<string> log;

	private readonly Action<string, uint> subtitle;

	public List<CameraShot> Shots = new List<CameraShot>();

	public bool PoliceMode;

	public readonly TrafficCleaner Traffic = new TrafficCleaner();

	public readonly StreetRepair Repair = new StreetRepair();

	public ChaosActions Actions;

	public SubNpcManager Npcs;

	private bool repairAtSwitch;

	public double WeatherLockUntil = -1.0;

	public double TimeLockUntil = -1.0;

	private int cam;

	private Phase phase;

	private double phaseStart;

	private double shotStart;

	private double lastUpkeep;

	private double lastAnchorMove;

	private double lastInfo;

	private double lastFrame;

	private double lastAim;

	private double lastCamForce;

	private float shotDuration;

	private int index = -1;

	private CameraShot lastManualShot;

	private int switchCount;

	private bool requestNext;

	private bool pendingChosen;

	private CameraShot pendingShot;

	private bool pendingForced;

	private bool switchFade = true;

	private bool forceFade;

	private double holdUntil = -1.0;

	private Vector3 anchor;

	private Vector3 hub;

	private Vector3 returnPos;

	private float returnHeading;

	private int returnVehicle;

	private uint returnRoomKey;

	private bool returnWasHeli;

	private bool haveHub;

	private bool weatherForced;

	private bool densityTouched;

	private double shakeStart;

	private double shakeUntil;

	private double lastIgnoreApply = -100.0;

	private bool lastIgnore;

	private float shakeAmp;

	private float shakePhase1;

	private float shakePhase2;

	private Vector3 edPos;

	private Vector3 edRot;

	private Vector3 edAim;

	private bool edAimHit;

	private float edFov;

	private Vector3 lastEditorAnchor;

	public bool AutoStart = true;

	public float StartDelay = 8f;

	private bool randomOrder;

	private bool fade;

	private string mode = "lista";

	private float defDuration = 20f;

	private float defFov = 25f;

	private float sway = 0.25f;

	private float anchorHeight = 20f;

	private float holdAfterAction = 8f;

	private float maxHold = 60f;

	private int fadeMs = 500;

	private int loadMs = 1500;

	private int fixedHour = -1;

	private int fixedMinute;

	private int fixedWeather = -1;

	private float pedDensity = 1f;

	private float carDensity = 1f;

	private float autoRadius = 350f;

	private float autoDistMin = 35f;

	private float autoDistMax = 90f;

	private float autoHMin = 6f;

	private float autoHMax = 25f;

	private float autoFovMin = 18f;

	private float autoFovMax = 32f;

	private float autoDrift = 6f;

	private float mouseSens = 0.12f;

	private float edSpeed = 10f;

	private bool invertMouse;

	private float followChance = 50f;

	private float followDuration = 30f;

	private float followDist = 22f;

	private float followHeight = 6f;

	private float followFov = 30f;

	private Vector3 fCam;

	private Vector3 fLook;

	private Vector3 fPrevTarget;

	private Vector3 fVel;

	private float fAz;

	private float fH;

	private float lastDt = 0.016f;

	private double fLastLos = -100.0;

	private double fBlockedSince = -1.0;

	private double fGoneAt = -1.0;

	private double fLastAnchor = -100.0;

	private double fLastRelax = -100.0;

	public Vector3 CamPos;

	public Vector3 CamRot;

	public float CamFov = 25f;

	public bool Active { get; private set; }

	public bool EditorActive { get; private set; }

	public CameraShot Current { get; private set; }

	public Vector3 CurrentTarget { get; private set; }

	public int CurrentIndex => (Current == null || Current.IsAuto) ? (-1) : Shots.IndexOf(Current);

	public bool FollowingNpc => Active && Current != null && Current.IsFollow;

	public int FollowedPed => FollowingNpc ? Current.FollowPed : 0;

	public Director(Config cfg, Action<string> log, Action<string, uint> subtitle)
	{
		this.log = log;
		this.subtitle = subtitle;
		ApplyConfig(cfg);
	}

	public void ApplyConfig(Config c)
	{
		cfg = c;
		IniFile ini = c.Ini;
		AutoStart = ini.GetBool("Director", "AutoIniciar", def: true);
		StartDelay = MathX.Clamp(ini.GetFloat("Director", "EsperaAlCargar", 8f), 0f, 3600f, 8f);
		randomOrder = Config.Normalize(ini.Get("Director", "Orden", "secuencial")).StartsWith("alea");
		mode = Config.Normalize(ini.Get("Director", "Modo", "lista")).Trim();
		defDuration = MathX.Clamp(ini.GetFloat("Director", "DuracionPorDefecto", 20f), 3f, 86400f, 20f);
		defFov = MathX.Clamp(ini.GetFloat("Director", "FOVPorDefecto", 25f), 3f, 120f, 25f);
		fade = !Config.Normalize(ini.Get("Director", "Transicion", "fundido")).StartsWith("corte");
		fadeMs = Math.Max(0, ini.GetInt("Director", "FundidoMs", 500));
		loadMs = Math.Max(0, ini.GetInt("Director", "EsperaCargaMs", 1500));
		sway = MathX.Clamp(ini.GetFloat("Director", "Balanceo", 0.25f), 0f, 20f, 0.25f);
		anchorHeight = MathX.Clamp(ini.GetFloat("Director", "AlturaJugadorOculto", 20f), 0f, 500f, 20f);
		holdAfterAction = MathX.Clamp(ini.GetFloat("Director", "NoCortarTrasAccion", 8f), 0f, 3600f, 8f);
		maxHold = MathX.Clamp(ini.GetFloat("Director", "EsperaMaximaExtra", 60f), 0f, 3600f, 60f);
		CameraStore.ParseTime(ini.Get("Director", "HoraFija", string.Empty), out fixedHour, out fixedMinute);
		fixedWeather = CameraStore.ParseWeather(ini.Get("Director", "ClimaFijo", string.Empty));
		pedDensity = MathX.Clamp(ini.GetFloat("Director", "DensidadPeatones", 1f), 0f, 10f, 1f);
		carDensity = MathX.Clamp(ini.GetFloat("Director", "DensidadTrafico", 1f), 0f, 10f, 1f);
		Traffic.ApplyConfig(ini);
		Repair.ApplyConfig(ini);
		autoRadius = MathX.Clamp(ini.GetFloat("CamarasAutomaticas", "Radio", 350f), 0f, 5000f, 350f);
		autoDistMin = MathX.Clamp(ini.GetFloat("CamarasAutomaticas", "DistanciaMin", 35f), 1f, 2000f, 35f);
		autoDistMax = MathX.Clamp(ini.GetFloat("CamarasAutomaticas", "DistanciaMax", 90f), autoDistMin + 1f, 2001f, 90f);
		autoHMin = MathX.Clamp(ini.GetFloat("CamarasAutomaticas", "AlturaMin", 6f), 1f, 500f, 6f);
		autoHMax = MathX.Clamp(ini.GetFloat("CamarasAutomaticas", "AlturaMax", 25f), autoHMin + 0.5f, 500.5f, 25f);
		autoFovMin = MathX.Clamp(ini.GetFloat("CamarasAutomaticas", "FOVMin", 18f), 3f, 120f, 18f);
		autoFovMax = MathX.Clamp(ini.GetFloat("CamarasAutomaticas", "FOVMax", 32f), autoFovMin, 120f, 32f);
		autoDrift = MathX.Clamp(ini.GetFloat("CamarasAutomaticas", "Desplazamiento", 6f), 0f, 300f, 6f);
		followChance = MathX.Clamp(ini.GetFloat("Suscriptor", "CamaraChance", 50f), 0f, 100f, 50f);
		followDuration = MathX.Clamp(ini.GetFloat("Suscriptor", "CamaraDuracion", 30f), 5f, 86400f, 30f);
		followDist = MathX.Clamp(ini.GetFloat("Suscriptor", "CamaraDistancia", 22f), 5f, 150f, 22f);
		followHeight = MathX.Clamp(ini.GetFloat("Suscriptor", "CamaraAltura", 6f), 1f, 80f, 6f);
		followFov = MathX.Clamp(ini.GetFloat("Suscriptor", "CamaraFOV", 30f), 3f, 120f, 30f);
		mouseSens = MathX.Clamp(ini.GetFloat("Editor", "SensibilidadMouse", 0.12f), 0.001f, 10f, 0.12f);
		invertMouse = ini.GetBool("Editor", "InvertirMouse", def: false);
		edSpeed = MathX.Clamp(ini.GetFloat("Editor", "Velocidad", 10f), 0.1f, 1000f, 10f);
	}

	public void LoadShots(List<string> warnings)
	{
		Shots = CameraStore.Load(cfg.Folder, defFov, defDuration, warnings);
		index = -1;
		lastManualShot = null;
		ClearPending();
	}

	public void Start()
	{
		if (!Active)
		{
			if (EditorActive)
			{
				ExitEditor();
			}
			RememberReturn();
			if (!haveHub)
			{
				hub = returnPos;
				haveHub = true;
			}
			CreateCam();
			Active = true;
			if (!pendingForced)
			{
				ClearPending();
				index = -1;
				lastManualShot = null;
			}
			switchCount = 0;
			requestNext = false;
			holdUntil = -1.0;
			switchFade = true;
			HidePlayer(returnPos + new Vector3(0f, 0f, anchorHeight));
			N.DISPLAY_HUD(v: false);
			N.DISPLAY_RADAR(v: false);
			N.SET_MAX_WANTED_LEVEL(0u);
			N.CLEAR_WANTED_LEVEL(G.PlayerIndex);
			log("Director encendido (" + Shots.Count + " camaras guardadas, modo " + mode + ")");
			N.DO_SCREEN_FADE_OUT(0u);
			BeginLoading();
		}
	}

	public void Stop()
	{
		if (Active)
		{
			Active = false;
			ClearPending();
			phase = Phase.Idle;
			N.DO_SCREEN_FADE_OUT(0u);
			DestroyCam();
			ResetWorldTweaks();
			RestorePlayer(loadScene: true);
			N.DO_SCREEN_FADE_IN(400u);
			log("Director apagado");
		}
	}

	public void QuickTeardown(bool loadScene = false)
	{
		if (Active || EditorActive)
		{
			Active = false;
			EditorActive = false;
			phase = Phase.Idle;
			ClearPending();
			DestroyCam();
			ResetWorldTweaks();
			RestorePlayer(loadScene);
			N.DO_SCREEN_FADE_IN(0u);
		}
	}

	public void ForgetState()
	{
		Active = false;
		EditorActive = false;
		PoliceMode = false;
		phase = Phase.Idle;
		cam = 0;
		weatherForced = false;
		densityTouched = false;
		haveHub = false;
		Current = null;
		lastManualShot = null;
		index = -1;
		ClearPending();
		holdUntil = (WeatherLockUntil = (TimeLockUntil = -1.0));
	}

	public void Shutdown()
	{
		try
		{
			QuickTeardown(loadScene: true);
		}
		catch
		{
		}
	}

	private void ResetWorldTweaks()
	{
		if (weatherForced)
		{
			N.RELEASE_WEATHER();
			weatherForced = false;
		}
		if (densityTouched)
		{
			N.SET_PED_DENSITY_MULTIPLIER(1f);
			N.SET_CAR_DENSITY_MULTIPLIER(1f);
			densityTouched = false;
		}
		PoliceMode = false;
	}

	public void SetHubHere()
	{
		hub = G.CharPos(G.PlayerPed);
		haveHub = true;
	}

	public void Next()
	{
		requestNext = true;
	}

	public void RepairSoon()
	{
		if (!Repair.IsDirty)
		{
			return;
		}
		if (!Active || Repair.Mode.StartsWith("direc"))
		{
			Repair.RepairNow(Actions);
			Traffic.Reset();
			return;
		}
		repairAtSwitch = true;
		if (Current != null && !Current.IsAuto)
		{
			pendingShot = Current;
			pendingChosen = true;
			pendingForced = true;
		}
		requestNext = true;
		forceFade = true;
	}

	public void Shake(float amplitudeDeg, float seconds)
	{
		double now = G.Now;
		if (!(now < shakeUntil) || !(amplitudeDeg < shakeAmp))
		{
			shakeAmp = amplitudeDeg;
			shakeStart = now;
			shakeUntil = now + (double)seconds;
			shakePhase1 = G.Rand(0f, 6.28f);
			shakePhase2 = G.Rand(0f, 6.28f);
		}
	}

	public void HoldFor(float seconds)
	{
		holdUntil = Math.Max(holdUntil, G.Now + (double)Math.Max(seconds, holdAfterAction));
	}

	public bool ReadyForAction()
	{
		if (EditorActive)
		{
			return false;
		}
		if (!Active)
		{
			return true;
		}
		return phase == Phase.Showing && G.Now - shotStart >= ((!fade) ? 0.2 : ((double)fadeMs / 1000.0));
	}

	public Vector3 ActionCenter()
	{
		if (Active && Current != null)
		{
			if (Current.IsFollow)
			{
				return G.RandomGroundPoint(CurrentTarget, 18f, 28f);
			}
			return CurrentTarget;
		}
		if (EditorActive)
		{
			return edAim;
		}
		N.GET_GAME_CAM(out var num);
		N.GET_CAM_POS(num, out var pos);
		N.GET_CAM_ROT(num, out var rot);
		Vector3 vector = G.FindTarget(pos, rot, 120f);
		if (Vector3.Distance(pos, vector) < 8f)
		{
			vector = G.CharPos(G.PlayerPed);
		}
		return vector;
	}

	public void Update(bool actionsPending)
	{
		double now = G.Now;
		float dt = (float)Math.Min(0.1, Math.Max(0.0, now - lastFrame));
		lastFrame = now;
		lastDt = dt;
		if (EditorActive)
		{
			UpdateEditor(now, dt);
		}
		else
		{
			if (!Active)
			{
				return;
			}
			N.HIDE_HUD_AND_RADAR_THIS_FRAME();
			if (pedDensity != 1f)
			{
				N.SET_PED_DENSITY_MULTIPLIER(pedDensity);
				densityTouched = true;
			}
			if (carDensity != 1f)
			{
				N.SET_CAR_DENSITY_MULTIPLIER(carDensity);
				densityTouched = true;
			}
			if (now - lastUpkeep > 1.0)
			{
				lastUpkeep = now;
				Upkeep();
			}
			if (phase == Phase.Showing && Current != null)
			{
				bool calm = now >= holdUntil && !PoliceMode && !actionsPending;
				try
				{
					Traffic.Update(CurrentTarget, calm, secondary: false, InFrame);
					if (Vector3.Distance(Current.Pos, CurrentTarget) > Traffic.Radius)
					{
						Traffic.Update(Current.Pos, calm, secondary: true, InFrame);
					}
				}
				catch
				{
				}
			}
			switch (phase)
			{
			case Phase.FadingOut:
			{
				double num = now - phaseStart;
				if ((num >= (double)fadeMs / 1000.0 && N.IS_SCREEN_FADED_OUT()) || num > (double)fadeMs / 1000.0 + 2.0)
				{
					BeginLoading();
				}
				break;
			}
			case Phase.Loading:
				if (Current != null && now - phaseStart >= ((!switchFade) ? 0.0 : ((double)loadMs / 1000.0)))
				{
					if (switchFade)
					{
						N.DO_SCREEN_FADE_IN((uint)fadeMs);
					}
					else
					{
						N.DO_SCREEN_FADE_IN(0u);
					}
					phase = Phase.Showing;
					shotStart = now;
				}
				else if (Current == null && now - phaseStart > 3.0)
				{
					BeginLoading();
				}
				break;
			case Phase.Showing:
			{
				if (Current != null && Current.IsFollow)
				{
					bool inCar = false;
					bool dead = false;
					Vector3 pos;
					bool flag = Npcs != null && Npcs.FollowInfo(Current.FollowPed, out pos, out inCar, out dead);
					if (!flag || dead)
					{
						if (fGoneAt < 0.0)
						{
							fGoneAt = now;
						}
						if (now - fGoneAt > ((!flag) ? 1.5 : 5.0))
						{
							requestNext = true;
						}
					}
					else
					{
						fGoneAt = -1.0;
					}
				}
				if (!repairAtSwitch && Repair.IsDue() && !PoliceMode && (Npcs == null || !Npcs.Busy))
				{
					if (Repair.Mode.StartsWith("direc"))
					{
						Repair.RepairNow(Actions);
						Traffic.Reset();
					}
					else
					{
						repairAtSwitch = true;
						if (Repair.Mode.StartsWith("fund") && Current != null)
						{
							if (!Current.IsAuto)
							{
								pendingShot = Current;
								pendingChosen = true;
								pendingForced = true;
							}
							requestNext = true;
							forceFade = true;
						}
					}
				}
				bool flag2 = now - shotStart >= (double)shotDuration;
				bool flag3 = now < holdUntil || PoliceMode || actionsPending;
				if (now > shotStart + (double)shotDuration + (double)maxHold)
				{
					flag3 = false;
				}
				if (requestNext || (flag2 && !flag3))
				{
					requestNext = false;
					if (!pendingChosen)
					{
						ChoosePending();
					}
					switchFade = forceFade || TransitionIsFade(pendingShot);
					forceFade = false;
					if (switchFade && fadeMs > 0)
					{
						N.DO_SCREEN_FADE_OUT((uint)fadeMs);
						phase = Phase.FadingOut;
						phaseStart = now;
					}
					else
					{
						BeginLoading();
					}
				}
				break;
			}
			}
			ApplyCamera(now);
		}
	}

	private void BeginLoading()
	{
		ForceCamActive();
		DoSwitch();
		phase = Phase.Loading;
		phaseStart = G.Now;
		requestNext = false;
		holdUntil = -1.0;
	}

	private void Upkeep()
	{
		int playerPed = G.PlayerPed;
		int playerIndex = G.PlayerIndex;
		if (!N.DOES_CAM_EXIST(cam))
		{
			CreateCam();
		}
		if (!N.IS_CAM_ACTIVE(cam) || G.Now - lastCamForce > 10.0)
		{
			ForceCamActive();
		}
		N.DISPLAY_HUD(v: false);
		N.DISPLAY_RADAR(v: false);
		N.CLEAR_HELP();
		N.SET_CHAR_VISIBLE(playerPed, v: false);
		N.SET_CHAR_INVINCIBLE(playerPed, v: true);
		N.SET_PLAYER_CONTROL(playerIndex, v: false);
		bool flag = !PoliceMode;
		if (flag != lastIgnore || G.Now - lastIgnoreApply > 15.0)
		{
			N.SET_EVERYONE_IGNORE_PLAYER(playerIndex, flag);
			N.SET_POLICE_IGNORE_PLAYER(playerIndex, flag);
			N.SET_MAX_WANTED_LEVEL(flag ? 0u : 6u);
			lastIgnore = flag;
			lastIgnoreApply = G.Now;
		}
		if (N.IS_PLAYER_BEING_ARRESTED())
		{
			N.CLEAR_WANTED_LEVEL(playerIndex);
		}
		if (N.IS_CHAR_IN_ANY_CAR(playerPed))
		{
			N.WARP_CHAR_FROM_CAR_TO_COORD(playerPed, anchor);
		}
		if (Vector3.Distance(G.CharPos(playerPed), anchor) > 3f)
		{
			MovePlayer(anchor);
		}
	}

	private void ApplyCamera(double now)
	{
		CameraShot current = Current;
		if (current == null || !N.DOES_CAM_EXIST(cam))
		{
			return;
		}
		if (current.IsFollow)
		{
			UpdateFollow(current, now, lastDt);
		}
		float t = ((phase != Phase.Showing || !(shotDuration > 0f)) ? 0f : ((float)((now - shotStart) / (double)shotDuration)));
		float num = MathX.Smooth(t);
		Vector3 vector = current.Pos;
		Vector3 vector2 = current.Rot;
		float num2 = current.Fov;
		if (current.HasEnd && !current.IsFollow)
		{
			vector = Vector3.Lerp(current.Pos, current.EndPos, num);
			vector2 = new Vector3(MathX.LerpAngle(current.Rot.X, current.EndRot.X, num), MathX.LerpAngle(current.Rot.Y, current.EndRot.Y, num), MathX.LerpAngle(current.Rot.Z, current.EndRot.Z, num));
			if (current.EndFov > 0f)
			{
				num2 = current.Fov + (current.EndFov - current.Fov) * num;
			}
		}
		float num3 = ((!(current.Sway >= 0f)) ? sway : current.Sway);
		if (num3 > 0f)
		{
			vector2.X += (float)(Math.Sin(now * 0.31) * 0.6 + Math.Sin(now * 0.83 + 0.7) * 0.25) * num3;
			vector2.Z += (float)(Math.Sin(now * 0.23 + 1.3) + Math.Sin(now * 0.67 + 2.1) * 0.3) * num3;
		}
		if (now < shakeUntil)
		{
			float num4 = (float)((shakeUntil - now) / Math.Max(0.01, shakeUntil - shakeStart));
			float num5 = shakeAmp * num4 * (num2 / 50f);
			vector2.X += num5 * (float)(0.6 * Math.Sin(now * 23.0 + (double)shakePhase1) + 0.4 * Math.Sin(now * 37.0 + (double)shakePhase2));
			vector2.Z += num5 * (float)(0.6 * Math.Sin(now * 19.0 + (double)shakePhase2) + 0.4 * Math.Sin(now * 31.0 + (double)shakePhase1));
		}
		N.SET_CAM_POS(cam, vector);
		N.SET_CAM_ROT(cam, vector2);
		num2 = MathX.Clamp(num2, 3f, 120f, defFov);
		N.SET_CAM_FOV(cam, num2);
		CamPos = vector;
		CamRot = vector2;
		CamFov = num2;
	}

	public bool InFrame(Vector3 p, float margin)
	{
		Vector3 vector = p - CamPos;
		float num = vector.Length();
		if (num < 4f)
		{
			return true;
		}
		if (num > 600f)
		{
			return false;
		}
		Vector3 vector2 = MathX.Forward(CamRot);
		Vector3 vector3 = MathX.Right(CamRot);
		Vector3 vector4 = Vector3.Cross(vector3, vector2);
		float num2 = Vector3.Dot(vector, vector2);
		if (num2 <= 0f)
		{
			return false;
		}
		double a = (double)CamFov * 0.5 * Math.PI / 180.0;
		double a2 = Math.Atan(Math.Tan(a) * 16.0 / 9.0);
		float value = Vector3.Dot(vector, vector3);
		float value2 = Vector3.Dot(vector, vector4);
		float num3 = (float)Math.Tan(a2) * num2 + margin;
		float num4 = (float)Math.Tan(a) * num2 + margin;
		return Math.Abs(value) <= num3 && Math.Abs(value2) <= num4;
	}

	private void DoSwitch()
	{
		if (repairAtSwitch)
		{
			repairAtSwitch = false;
			try
			{
				Repair.RepairNow(Actions);
				Traffic.Reset();
			}
			catch (Exception ex)
			{
				log("Error arreglando la calle: " + ex.Message);
			}
			log("Calle arreglada");
		}
		CameraShot cameraShot = PickNext();
		if (cameraShot == null)
		{
			log("No se pudo armar un plano; se reintenta.");
			return;
		}
		Current = cameraShot;
		shotDuration = ((!(cameraShot.Duration > 0f)) ? defDuration : MathX.Clamp(cameraShot.Duration, 3f, 86400f, defDuration));
		shotStart = G.Now;
		fGoneAt = -1.0;
		if (cameraShot.IsFollow)
		{
			if (Npcs == null || !Npcs.FollowInfo(cameraShot.FollowPed, out var pos, out var inCar, out var _))
			{
				pos = CurrentTarget;
				inCar = false;
			}
			MovePlayer(pos + new Vector3(0f, 0f, anchorHeight));
			N.REQUEST_COLLISION_AT_POSN(pos);
			N.LOAD_SCENE(pos);
			CurrentTarget = pos;
			InitFollow(cameraShot, pos, inCar);
		}
		else
		{
			if (!cameraShot.IsAuto)
			{
				MovePlayer((!cameraShot.HasAnchor) ? cameraShot.Pos : cameraShot.Anchor);
				if (cameraShot.HasTarget)
				{
					N.REQUEST_COLLISION_AT_POSN(cameraShot.Target);
				}
				N.REQUEST_COLLISION_AT_POSN(cameraShot.Pos);
				N.LOAD_SCENE(cameraShot.Pos);
				CurrentTarget = ((!cameraShot.HasTarget) ? G.FindTarget(cameraShot.Pos, cameraShot.Rot, 1500f) : cameraShot.Target);
			}
			else
			{
				CurrentTarget = cameraShot.Target;
			}
			anchor = ((!cameraShot.HasAnchor) ? (CurrentTarget + new Vector3(0f, 0f, anchorHeight)) : cameraShot.Anchor);
			MovePlayer(anchor);
			if (Npcs == null || !Npcs.AnyNear(CurrentTarget, Traffic.Radius + 30f))
			{
				try
				{
					Traffic.OnCameraSwitch(CurrentTarget);
					if (Vector3.Distance(cameraShot.Pos, CurrentTarget) > Traffic.Radius)
					{
						Traffic.OnCameraSwitch(cameraShot.Pos);
					}
				}
				catch
				{
				}
			}
		}
		if (G.Now > TimeLockUntil)
		{
			int num = ((cameraShot.Hour < 0) ? fixedHour : cameraShot.Hour);
			int m = ((cameraShot.Hour < 0) ? fixedMinute : cameraShot.Minute);
			if (num >= 0)
			{
				N.SET_TIME_OF_DAY((uint)num, (uint)m);
			}
		}
		ReapplyWeather();
		switchCount++;
		ApplyCamera(G.Now);
	}

	public void ReapplyWeather()
	{
		if (!Active)
		{
			if (weatherForced)
			{
				N.RELEASE_WEATHER();
				weatherForced = false;
			}
		}
		else if (!(G.Now < WeatherLockUntil))
		{
			int num = ((Current == null || Current.Weather < 0) ? fixedWeather : Current.Weather);
			if (num >= 0)
			{
				N.FORCE_WEATHER_NOW((uint)num);
				weatherForced = true;
			}
			else if (weatherForced)
			{
				N.RELEASE_WEATHER();
				weatherForced = false;
			}
		}
	}

	private bool TransitionIsFade(CameraShot s)
	{
		if (s != null && s.Transition == 0)
		{
			return true;
		}
		if (s != null && s.Transition == 1)
		{
			return false;
		}
		return fade;
	}

	private int ActiveCount()
	{
		int num = 0;
		foreach (CameraShot shot in Shots)
		{
			if (shot.Active && !shot.IsAuto)
			{
				num++;
			}
		}
		return num;
	}

	private int ChooseNextIndex()
	{
		int count = ActiveCount();
		if (mode.StartsWith("auto") || count == 0 || (mode.StartsWith("mix") && switchCount % 2 == 1))
		{
			return -1;
		}
		// Menus may move or delete cameras while running. Use the actual last
		// camera object, rather than its old numeric position in the list.
		int previous = lastManualShot == null ? index : Shots.IndexOf(lastManualShot);
		Func<CameraShot, double> hotspotWeight = null;
		Vector3 hotspot;
		if (count > 1 && Npcs != null && Npcs.Hotspot(out hotspot))
		{
			hotspotWeight = shot => GangRules.HotspotWeight(GangRules.ShotDistance(hotspot, shot.Pos, ShotLook(shot)));
		}
		return CameraShot.SelectNextIndex(Shots, previous, randomOrder, G.Rng, hotspotWeight);
	}

	private static Vector3 ShotLook(CameraShot s)
	{
		if (s.HasTarget)
		{
			return s.Target;
		}
		return s.Pos + MathX.Forward(s.Rot) * 60f;
	}

	private void ChoosePending()
	{
		if (Npcs != null && followChance > 0f)
		{
			int exclude = ((Current != null && Current.IsFollow) ? Current.FollowPed : 0);
			int num = Npcs.PickForCamera(exclude);
			if (num != 0 && G.Rng.NextDouble() * 100.0 < (double)followChance)
			{
				pendingShot = MakeFollowShot(num);
				pendingChosen = true;
				pendingForced = false;
				return;
			}
		}
		int num2 = ChooseNextIndex();
		pendingShot = ((num2 < 0) ? null : Shots[num2]);
		pendingChosen = true;
		pendingForced = false;
	}

	private void ClearPending()
	{
		pendingChosen = false;
		pendingShot = null;
		pendingForced = false;
	}

	private CameraShot PickNext()
	{
		if (pendingChosen && pendingShot != null)
		{
			if (pendingShot.IsFollow)
			{
				if (Npcs == null || !Npcs.FollowInfo(pendingShot.FollowPed, out var _, out var _, out var dead) || dead)
				{
					ClearPending();
				}
			}
			else
			{
				int num = Shots.IndexOf(pendingShot);
				if (num < 0 || (!pendingShot.Active && !pendingForced))
				{
					ClearPending();
				}
			}
		}
		if (!pendingChosen)
		{
			ChoosePending();
		}
		CameraShot cameraShot = pendingShot;
		ClearPending();
		if (cameraShot != null)
		{
			if (!cameraShot.IsFollow)
			{
				index = Shots.IndexOf(cameraShot);
				lastManualShot = cameraShot;
			}
			return cameraShot;
		}
		CameraShot cameraShot2 = BuildAutoShot();
		if (cameraShot2 != null)
		{
			return cameraShot2;
		}
		int fallbackIndex = CameraShot.SelectNextIndex(Shots, index, false, G.Rng);
		if (fallbackIndex < 0)
		{
			return null;
		}
		index = fallbackIndex;
		lastManualShot = Shots[index];
		return lastManualShot;
	}

	public void GoTo(int shotIndex)
	{
		if (shotIndex >= 0 && shotIndex < Shots.Count)
		{
			pendingShot = Shots[shotIndex];
			pendingChosen = true;
			pendingForced = true;
			if (!Active)
			{
				Start();
			}
			else
			{
				requestNext = true;
			}
		}
	}

	private CameraShot MakeFollowShot(int ped)
	{
		CameraShot cameraShot = new CameraShot();
		cameraShot.Name = "NPC";
		cameraShot.IsAuto = true;
		cameraShot.IsFollow = true;
		cameraShot.FollowPed = ped;
		cameraShot.Fov = followFov;
		cameraShot.Duration = followDuration;
		return cameraShot;
	}

	public void FollowNow(int ped)
	{
		if (Active && !EditorActive && ped != 0)
		{
			pendingShot = MakeFollowShot(ped);
			pendingChosen = true;
			pendingForced = true;
			requestNext = true;
		}
	}

	private static Vector3 Flat(float az)
	{
		return MathX.Forward(new Vector3(0f, 0f, az));
	}

	private Vector3 FollowPose(float az, float h, float dist, Vector3 look)
	{
		Vector3 vector = look + Flat(az) * dist + new Vector3(0f, 0f, h);
		if (G.GroundZ(vector, out var z) && z > vector.Z - 1.5f)
		{
			vector.Z = z + 1.5f;
		}
		return vector;
	}

	private static bool FollowClear(Vector3 look, Vector3 camPos)
	{
		Vector3 vector = camPos - look;
		float num = vector.Length();
		if (num < 3f)
		{
			return true;
		}
		Vector3 hit;
		return !G.Raycast(look + vector / num * 2.5f, camPos, out hit);
	}

	private bool FindFollowSpot(Vector3 look, float dist, float preferAz, out float az, out float h)
	{
		float[] array = new float[10] { 0f, 35f, -35f, 70f, -70f, 110f, -110f, 180f, 145f, -145f };
		float[] array2 = new float[3]
		{
			followHeight,
			followHeight * 2.5f + 4f,
			followHeight * 5f + 10f
		};
		float[] array3 = array2;
		foreach (float num in array3)
		{
			float[] array4 = array;
			foreach (float num2 in array4)
			{
				float num3 = preferAz + num2;
				if (FollowClear(look, FollowPose(num3, num, dist, look)))
				{
					az = num3;
					h = num;
					return true;
				}
			}
		}
		az = preferAz;
		h = array2[array2.Length - 1];
		return false;
	}

	private void InitFollow(CameraShot s, Vector3 target, bool inCar)
	{
		Vector3 look = target + new Vector3(0f, 0f, 0.5f);
		float h = 0f;
		try
		{
			N.GET_CHAR_HEADING(s.FollowPed, out h);
		}
		catch
		{
		}
		float preferAz = ((!inCar) ? (h + ((G.Rng.Next(2) != 0) ? (-35f) : 35f)) : (h + 180f + G.Rand(-20f, 20f)));
		float dist = followDist * ((!inCar) ? 1f : 1.6f);
		FindFollowSpot(look, dist, preferAz, out var az, out var h2);
		fAz = az;
		fH = h2;
		fCam = FollowPose(az, h2, dist, look);
		fLook = look;
		fPrevTarget = target;
		fVel = Vector3.Zero;
		fLastLos = (fLastRelax = G.Now);
		fBlockedSince = -1.0;
		fLastAnchor = G.Now;
		s.Pos = fCam;
		s.Rot = MathX.LookRotation(fCam, fLook);
		s.Fov = followFov;
	}

	private void UpdateFollow(CameraShot s, double now, float dt)
	{
		if (Npcs == null || !Npcs.FollowInfo(s.FollowPed, out var pos, out var inCar, out var _))
		{
			pos = fPrevTarget;
			inCar = false;
		}
		if (dt > 0.0005f)
		{
			Vector3 value = (pos - fPrevTarget) / dt;
			if (value.Length() > 90f)
			{
				value = Vector3.Zero;
			}
			fVel = Vector3.Lerp(fVel, value, 1f - (float)Math.Exp((double)(0f - dt) * 2.0));
		}
		fPrevTarget = pos;
		CurrentTarget = pos;
		Vector3 vector = pos + new Vector3(0f, 0f, 0.5f);
		float dist = followDist * ((!inCar) ? 1f : 1.6f);
		float num = (float)Math.Sqrt(fVel.X * fVel.X + fVel.Y * fVel.Y);
		if (num > 4f)
		{
			float num2 = (float)(Math.Atan2(0f - fVel.X, fVel.Y) * 180.0 / Math.PI);
			float num3 = num2 + 180f;
			float val = ((num3 - fAz) % 360f + 540f) % 360f - 180f;
			float num4 = ((!inCar) ? 20f : 40f) * dt;
			fAz += Math.Max(0f - num4, Math.Min(num4, val));
		}
		else
		{
			fAz += 2f * dt;
		}
		if (now - fLastLos > 0.3)
		{
			fLastLos = now;
			if (FollowClear(vector, fCam))
			{
				fBlockedSince = -1.0;
			}
			else if (fBlockedSince < 0.0)
			{
				fBlockedSince = now;
			}
			else if (now - fBlockedSince > 0.6)
			{
				FindFollowSpot(vector, dist, fAz, out var az, out var h);
				fAz = az;
				fH = h;
				fCam = FollowPose(az, h, dist, vector);
				fLook = vector;
				fBlockedSince = -1.0;
			}
		}
		if (fH > followHeight + 0.5f && now - fLastRelax > 3.0)
		{
			fLastRelax = now;
			if (FollowClear(vector, FollowPose(fAz, followHeight, dist, vector)))
			{
				fH = followHeight;
			}
		}
		Vector3 value2 = FollowPose(fAz, fH, dist, vector);
		float num5 = ((!inCar) ? 1.2f : 2.5f);
		fCam = Vector3.Lerp(fCam, value2, 1f - (float)Math.Exp((0f - dt) * num5));
		fLook = Vector3.Lerp(fLook, vector, 1f - (float)Math.Exp((double)(0f - dt) * 6.0));
		Vector3 vector2 = anchor;
		if (now - fLastAnchor > 1.5 && Math.Sqrt((vector2.X - pos.X) * (vector2.X - pos.X) + (vector2.Y - pos.Y) * (vector2.Y - pos.Y)) > 20.0)
		{
			fLastAnchor = now;
			MovePlayer(pos + new Vector3(0f, 0f, anchorHeight));
		}
		s.Pos = fCam;
		s.Rot = MathX.LookRotation(fCam, fLook);
	}

	public bool SceneVisible()
	{
		if (EditorActive)
		{
			return true;
		}
		if (!Active)
		{
			return !N.IS_SCREEN_FADED_OUT();
		}
		return phase == Phase.Showing && G.Now - shotStart >= ((!switchFade) ? 0.0 : ((double)fadeMs / 1000.0 * 0.6));
	}

	public void ViewPose(out Vector3 pos, out Vector3 rot, out float fov)
	{
		if (Active && Current != null)
		{
			pos = CamPos;
			rot = CamRot;
			fov = CamFov;
			return;
		}
		if (EditorActive)
		{
			pos = edPos;
			rot = edRot;
			fov = edFov;
			return;
		}
		N.GET_GAME_CAM(out var num);
		N.GET_CAM_POS(num, out pos);
		N.GET_CAM_ROT(num, out rot);
		float fov2 = 0f;
		try
		{
			N.GET_CAM_FOV(num, out fov2);
		}
		catch
		{
		}
		fov = ((!(fov2 > 1f) || !(fov2 < 170f)) ? 45f : fov2);
	}

	private CameraShot BuildAutoShot()
	{
		if (!haveHub)
		{
			hub = G.CharPos(G.PlayerPed);
			haveHub = true;
		}
		Vector3 vector = hub;
		float max = autoRadius;
		if (Npcs != null && Npcs.Hotspot(out var p) && G.Rng.NextDouble() < 0.75)
		{
			vector = p;
			max = Math.Min(autoRadius, 70f);
		}
		Vector3 vector2 = vector;
		float num = G.Rand(0f, 360f);
		bool flag = false;
		for (int i = 0; i < 3; i++)
		{
			double num2 = G.Rng.NextDouble() * Math.PI * 2.0;
			float num3 = G.Rand(0f, max);
			Vector3 p2 = new Vector3(vector.X + (float)Math.Cos(num2) * num3, vector.Y + (float)Math.Sin(num2) * num3, vector.Z);
			if (!N.GET_NTH_CLOSEST_CAR_NODE_WITH_HEADING(p2, (uint)G.Rng.Next(1, 4), out var result, out var heading) || result == Vector3.Zero)
			{
				continue;
			}
			vector2 = result;
			num = heading;
			flag = true;
			MovePlayer(vector2 + new Vector3(0f, 0f, anchorHeight));
			N.REQUEST_COLLISION_AT_POSN(vector2);
			N.LOAD_SCENE(vector2);
			if (G.GroundZ(vector2, out var z) && Math.Abs(z - vector2.Z) < 6f)
			{
				vector2.Z = z;
			}
			if (G.TopZ(vector2.X, vector2.Y, out var z2) && z2 - vector2.Z > 3f)
			{
				continue;
			}
			Vector3 vector3 = vector2 + new Vector3(0f, 0f, 1.2f);
			for (int j = 0; j < 16; j++)
			{
				Vector3 vector4;
				if (j < 11)
				{
					float z3 = num + G.Rand(-25f, 25f) + ((G.Rng.Next(2) != 0) ? 180f : 0f);
					vector4 = MathX.Forward(new Vector3(0f, 0f, z3));
				}
				else
				{
					double num4 = G.Rng.NextDouble() * Math.PI * 2.0;
					vector4 = new Vector3((float)Math.Cos(num4), (float)Math.Sin(num4), 0f);
				}
				float num5 = G.Rand(autoDistMin, autoDistMax);
				float z4 = G.Rand(autoHMin, autoHMax);
				Vector3 vector5 = vector2 + vector4 * num5 + new Vector3(0f, 0f, z4);
				Vector3 vector6 = Vector3.Normalize(vector5 - vector3);
				if (!G.Raycast(vector3 + vector6 * 4f, vector5, out var _) && (!G.GroundZ(vector5, out var z5) || !(z5 > vector5.Z - 1.5f)))
				{
					return MakeAutoShot(vector5, vector3, vector2, allowDrift: true);
				}
			}
		}
		if (!flag)
		{
			MovePlayer(vector2 + new Vector3(0f, 0f, anchorHeight));
			N.LOAD_SCENE(vector2);
		}
		Vector3 vector7 = vector2 + new Vector3(0f, 0f, 1.2f);
		float[] array = new float[3] { 45f, 90f, 150f };
		float[] array2 = array;
		foreach (float num6 in array2)
		{
			for (int l = 0; l < 6; l++)
			{
				Vector3 vector8 = MathX.Forward(new Vector3(0f, 0f, num + 180f + (float)l * 60f));
				Vector3 vector9 = vector2 + vector8 * num6 + new Vector3(0f, 0f, num6);
				if (!G.Raycast(vector7 + Vector3.Normalize(vector9 - vector7) * 4f, vector9, out var _))
				{
					return MakeAutoShot(vector9, vector7, vector2, allowDrift: false);
				}
			}
		}
		return MakeAutoShot(vector2 + new Vector3(0f, 0f, 150f) + MathX.Forward(new Vector3(0f, 0f, num)) * 60f, vector7, vector2, allowDrift: false);
	}

	private CameraShot MakeAutoShot(Vector3 camPos, Vector3 look, Vector3 node, bool allowDrift)
	{
		CameraShot cameraShot = new CameraShot();
		cameraShot.Name = "Auto";
		cameraShot.IsAuto = true;
		cameraShot.Pos = camPos;
		cameraShot.Rot = MathX.LookRotation(camPos, look);
		cameraShot.Fov = G.Rand(autoFovMin, autoFovMax);
		cameraShot.Duration = defDuration;
		cameraShot.HasTarget = true;
		cameraShot.Target = node;
		CameraShot cameraShot2 = cameraShot;
		if (allowDrift && autoDrift > 0f)
		{
			Vector3 vector = MathX.Right(cameraShot2.Rot) * ((G.Rng.Next(2) != 0) ? autoDrift : (0f - autoDrift));
			Vector3 vector2 = camPos + vector;
			if (!G.Raycast(camPos, vector2, out var _))
			{
				cameraShot2.HasEnd = true;
				cameraShot2.EndPos = vector2;
				cameraShot2.EndRot = MathX.LookRotation(vector2, look);
			}
		}
		return cameraShot2;
	}

	private void RememberReturn()
	{
		int playerPed = G.PlayerPed;
		returnPos = G.CharPos(playerPed);
		N.GET_CHAR_HEADING(playerPed, out returnHeading);
		returnVehicle = 0;
		returnWasHeli = false;
		if (N.IS_CHAR_IN_ANY_CAR(playerPed))
		{
			N.GET_CAR_CHAR_IS_USING(playerPed, out returnVehicle);
			N.GET_CAR_MODEL(returnVehicle, out var model);
			returnWasHeli = N.IS_THIS_MODEL_A_HELI(model);
		}
		returnRoomKey = 0u;
		try
		{
			N.GET_KEY_FOR_CHAR_IN_ROOM(playerPed, out returnRoomKey);
		}
		catch
		{
		}
	}

	private void SetHiddenMarker(bool hidden)
	{
		try
		{
			AppDomain.CurrentDomain.SetData("KickChaos.Hidden", (!hidden) ? null : new float[7] { returnPos.X, returnPos.Y, returnPos.Z, anchor.X, anchor.Y, anchor.Z, returnHeading });
		}
		catch
		{
		}
	}

	private void MovePlayer(Vector3 p)
	{
		int playerPed = G.PlayerPed;
		if (N.IS_CHAR_IN_ANY_CAR(playerPed))
		{
			N.WARP_CHAR_FROM_CAR_TO_COORD(playerPed, p);
		}
		N.FREEZE_CHAR_POSITION(playerPed, v: false);
		N.SET_CHAR_COORDINATES(playerPed, p);
		N.FREEZE_CHAR_POSITION(playerPed, v: true);
		N.CLEAR_ROOM_FOR_CHAR(playerPed);
		anchor = p;
		lastAnchorMove = G.Now;
		SetHiddenMarker(hidden: true);
	}

	private void HidePlayer(Vector3 p)
	{
		int playerPed = G.PlayerPed;
		int playerIndex = G.PlayerIndex;
		N.SET_PLAYER_CONTROL(playerIndex, v: false);
		N.SET_CURRENT_CHAR_WEAPON(playerPed, 0, b: true);
		N.SET_CHAR_VISIBLE(playerPed, v: false);
		N.SET_CHAR_INVINCIBLE(playerPed, v: true);
		N.SET_PLAYER_INVINCIBLE(playerIndex, v: true);
		N.SET_CHAR_PROOFS(playerPed, a: true, b: true, c: true, d: true, e: true);
		N.SET_CHAR_COLLISION(playerPed, v: false);
		N.SET_EVERYONE_IGNORE_PLAYER(playerIndex, v: true);
		N.SET_POLICE_IGNORE_PLAYER(playerIndex, v: true);
		MovePlayer(p);
	}

	private void RestorePlayer(bool loadScene)
	{
		int playerPed = G.PlayerPed;
		int playerIndex = G.PlayerIndex;
		PoliceMode = false;
		N.CLEAR_WANTED_LEVEL(playerIndex);
		N.SET_MAX_WANTED_LEVEL(6u);
		N.FREEZE_CHAR_POSITION(playerPed, v: false);
		N.SET_CHAR_COLLISION(playerPed, v: true);
		Vector3 vector = returnPos;
		N.REQUEST_COLLISION_AT_POSN(vector);
		if (loadScene)
		{
			N.LOAD_SCENE(vector);
		}
		bool flag = false;
		if (returnVehicle != 0 && N.DOES_VEHICLE_EXIST(returnVehicle) && !N.IS_CAR_DEAD(returnVehicle) && Vector3.Distance(G.CarPos(returnVehicle), vector) < 50f)
		{
			N.WARP_CHAR_INTO_CAR(playerPed, returnVehicle);
			flag = true;
		}
		if (!flag)
		{
			if (loadScene && returnWasHeli && returnRoomKey == 0 && G.GroundZ(vector, out var z) && vector.Z - z > 3f && (!N.GET_WATER_HEIGHT(new Vector3(vector.X, vector.Y, vector.Z), out var h) || !(h > z)))
			{
				vector.Z = z + 1f;
			}
			N.SET_CHAR_COORDINATES(playerPed, vector);
			N.SET_CHAR_HEADING(playerPed, returnHeading);
			if (returnRoomKey != 0)
			{
				try
				{
					N.SET_ROOM_FOR_CHAR_BY_KEY(playerPed, returnRoomKey);
				}
				catch
				{
				}
			}
		}
		N.SET_CHAR_VISIBLE(playerPed, v: true);
		N.SET_CHAR_INVINCIBLE(playerPed, v: false);
		N.SET_PLAYER_INVINCIBLE(playerIndex, v: false);
		N.SET_CHAR_PROOFS(playerPed, a: false, b: false, c: false, d: false, e: false);
		N.SET_EVERYONE_IGNORE_PLAYER(playerIndex, v: false);
		N.SET_POLICE_IGNORE_PLAYER(playerIndex, v: false);
		N.SET_PLAYER_CONTROL(playerIndex, v: true);
		N.DISPLAY_HUD(v: true);
		N.DISPLAY_RADAR(v: true);
		SetHiddenMarker(hidden: false);
	}

	public void RecoverFromPreviousInstance()
	{
		N.SET_TIME_SCALE(1f);
		N.SET_TEXT_INPUT_ACTIVE(v: false);
		N.DISABLE_PAUSE_MENU(v: false);
		N.SET_PED_DENSITY_MULTIPLIER(1f);
		N.SET_CAR_DENSITY_MULTIPLIER(1f);
		float[] array = null;
		try
		{
			array = AppDomain.CurrentDomain.GetData("KickChaos.Hidden") as float[];
		}
		catch
		{
		}
		if (array != null && array.Length >= 7)
		{
			log("Recuperando el estado que dejo una instancia anterior del mod");
			N.ACTIVATE_SCRIPTED_CAMS(a: false, b: false);
			int playerPed = G.PlayerPed;
			Vector3 value = new Vector3(array[3], array[4], array[5]);
			returnPos = new Vector3(array[0], array[1], array[2]);
			returnHeading = array[6];
			returnVehicle = 0;
			returnRoomKey = 0u;
			returnWasHeli = false;
			if (Vector3.Distance(G.CharPos(playerPed), value) < 10f)
			{
				RestorePlayer(loadScene: true);
			}
			else
			{
				int playerIndex = G.PlayerIndex;
				N.FREEZE_CHAR_POSITION(playerPed, v: false);
				N.SET_CHAR_COLLISION(playerPed, v: true);
				N.SET_CHAR_VISIBLE(playerPed, v: true);
				N.SET_CHAR_INVINCIBLE(playerPed, v: false);
				N.SET_PLAYER_INVINCIBLE(playerIndex, v: false);
				N.SET_CHAR_PROOFS(playerPed, a: false, b: false, c: false, d: false, e: false);
				N.SET_EVERYONE_IGNORE_PLAYER(playerIndex, v: false);
				N.SET_POLICE_IGNORE_PLAYER(playerIndex, v: false);
				N.SET_PLAYER_CONTROL(playerIndex, v: true);
				N.SET_MAX_WANTED_LEVEL(6u);
				N.DISPLAY_HUD(v: true);
				N.DISPLAY_RADAR(v: true);
				SetHiddenMarker(hidden: false);
			}
			N.DO_SCREEN_FADE_IN(0u);
		}
	}

	private void ForceCamActive()
	{
		if (cam == 0 || !N.DOES_CAM_EXIST(cam))
		{
			CreateCam();
			return;
		}
		N.SET_CAM_ACTIVE(cam, v: true);
		N.SET_CAM_PROPAGATE(cam, v: true);
		N.ACTIVATE_SCRIPTED_CAMS(a: true, b: true);
		lastCamForce = G.Now;
	}

	private void CreateCam()
	{
		if (cam == 0 || !N.DOES_CAM_EXIST(cam))
		{
			N.CREATE_CAM(14, out cam);
			N.SET_CAM_FOV(cam, defFov);
			N.SET_CAM_ACTIVE(cam, v: true);
			N.SET_CAM_PROPAGATE(cam, v: true);
			N.ACTIVATE_SCRIPTED_CAMS(a: true, b: true);
			lastCamForce = G.Now;
		}
	}

	private void DestroyCam()
	{
		if (cam != 0 && N.DOES_CAM_EXIST(cam))
		{
			N.SET_CAM_ACTIVE(cam, v: false);
			N.SET_CAM_PROPAGATE(cam, v: false);
			N.ACTIVATE_SCRIPTED_CAMS(a: false, b: false);
			N.DESTROY_CAM(cam);
		}
		else
		{
			N.ACTIVATE_SCRIPTED_CAMS(a: false, b: false);
		}
		cam = 0;
	}

	public void ToggleEditor()
	{
		if (EditorActive)
		{
			ExitEditor();
		}
		else
		{
			EnterEditor();
		}
	}

	private void EnterEditor()
	{
		if (Active)
		{
			Stop();
		}
		RememberReturn();
		N.GET_GAME_CAM(out var num);
		N.GET_CAM_POS(num, out edPos);
		N.GET_CAM_ROT(num, out edRot);
		edRot.Y = 0f;
		edFov = defFov;
		edAim = edPos;
		edAimHit = false;
		CreateCam();
		HidePlayer(edPos);
		lastEditorAnchor = edPos;
		N.DISPLAY_RADAR(v: false);
		EditorActive = true;
		log("Editor de camaras abierto");
	}

	private void ExitEditor()
	{
		EditorActive = false;
		DestroyCam();
		RestorePlayer(loadScene: true);
		subtitle(" ", 1u);
		log("Editor de camaras cerrado");
	}

	private void UpdateEditor(double now, float dt)
	{
		if (!N.DOES_CAM_EXIST(cam))
		{
			CreateCam();
		}
		N.HIDE_HUD_AND_RADAR_THIS_FRAME();
		if (G.IsGameFocused())
		{
			float num = edSpeed;
			if (IsDown((Keys)16))
			{
				num *= 5f;
			}
			if (IsDown((Keys)17))
			{
				num *= 0.2f;
			}
			Vector3 vector = MathX.Forward(edRot);
			Vector3 vector2 = MathX.Right(edRot);
			Vector3 zero = Vector3.Zero;
			if (IsDown((Keys)87))
			{
				zero += vector;
			}
			if (IsDown((Keys)83))
			{
				zero -= vector;
			}
			if (IsDown((Keys)68))
			{
				zero += vector2;
			}
			if (IsDown((Keys)65))
			{
				zero -= vector2;
			}
			if (IsDown((Keys)69))
			{
				zero += Vector3.UnitZ;
			}
			if (IsDown((Keys)81))
			{
				zero -= Vector3.UnitZ;
			}
			if (zero != Vector3.Zero)
			{
				edPos += Vector3.Normalize(zero) * num * dt;
			}
			float num2 = 60f * dt * ((!IsDown((Keys)17)) ? 1f : 0.25f) * (edFov / 50f + 0.2f);
			if (IsDown((Keys)74))
			{
				edRot.Z += num2;
			}
			if (IsDown((Keys)76))
			{
				edRot.Z -= num2;
			}
			if (IsDown((Keys)73))
			{
				edRot.X += num2;
			}
			if (IsDown((Keys)75))
			{
				edRot.X -= num2;
			}
			try
			{
				N.GET_MOUSE_INPUT(out var delta);
				float num3 = edFov / 50f;
				edRot.Z -= (float)delta.X * mouseSens * num3;
				edRot.X += (float)((!invertMouse) ? (-delta.Y) : delta.Y) * mouseSens * num3;
			}
			catch
			{
			}
			if (IsDown((Keys)90))
			{
				edFov -= 15f * dt;
			}
			if (IsDown((Keys)88))
			{
				edFov += 15f * dt;
			}
			edFov = MathX.Clamp(edFov, 3f, 120f, defFov);
			edRot.X = Math.Max(-89f, Math.Min(89f, edRot.X));
			edRot.Z = (edRot.Z % 360f + 360f) % 360f;
		}
		N.SET_CAM_POS(cam, edPos);
		N.SET_CAM_ROT(cam, edRot);
		N.SET_CAM_FOV(cam, edFov);
		if (now - lastAnchorMove > 0.5 && Vector3.Distance(lastEditorAnchor, edPos) > 15f)
		{
			MovePlayer(edPos);
			lastEditorAnchor = edPos;
		}
		N.SET_PLAYER_CONTROL(G.PlayerIndex, v: false);
		if (now - lastAim > 0.5)
		{
			lastAim = now;
			edAimHit = G.TryFindTarget(edPos, edRot, 1500f, fine: false, out edAim);
		}
		if (edAimHit)
		{
			N.DRAW_CHECKPOINT(edAim, 1.5f, Color.FromArgb(255, 255, 60, 30));
		}
		if (!(now - lastInfo > 0.25))
		{
			return;
		}
		lastInfo = now;
		int num4 = 0;
		foreach (CameraShot shot in Shots)
		{
			if (!shot.IsAuto)
			{
				num4++;
			}
		}
		string text = ((!edAimHit) ? "SIN OBJETIVO (muy lejos)" : ("objetivo a " + Vector3.Distance(edPos, edAim).ToString("0") + " m"));
		subtitle($"EDITOR | FOV {edFov:0} | {text} | camaras: {num4} | ENTER guardar - SHIFT+ENTER punto final - T fijar objetivo - RETROCESO borrar - {cfg.KeyEditor} salir", 400u);
	}

	private bool IsDown(Keys k)
	{
		return G.KeyDown(k);
	}

	public void EditorSave(bool asEndPoint)
	{
		if (!EditorActive)
		{
			return;
		}
		if (asEndPoint)
		{
			CameraShot cameraShot = LastManualShot();
			if (cameraShot == null)
			{
				subtitle("Primero guarda una camara con ENTER", 2500u);
				return;
			}
			if (Vector3.Distance(cameraShot.Pos, edPos) > 300f)
			{
				subtitle("'" + cameraShot.Name + "' esta a mas de 300 m: el punto final tiene que estar cerca del inicial", 3500u);
				return;
			}
			cameraShot.HasEnd = true;
			cameraShot.EndPos = edPos;
			cameraShot.EndRot = edRot;
			cameraShot.EndFov = ((!(Math.Abs(edFov - cameraShot.Fov) > 0.5f)) ? (-1f) : edFov);
			if (!Persist())
			{
				return;
			}
			subtitle("Punto final guardado para '" + cameraShot.Name + "' (se movera durante el plano)", 3000u);
			return;
		}
		Vector3 target;
		bool flag = G.TryFindTarget(edPos, edRot, 1500f, fine: true, out target);
		CameraShot cameraShot2 = new CameraShot();
		cameraShot2.Name = CameraStore.NextName(Shots);
		cameraShot2.Pos = edPos;
		cameraShot2.Rot = edRot;
		cameraShot2.Fov = (float)Math.Round(edFov, 1);
		cameraShot2.Duration = -1f;
		cameraShot2.HasTarget = flag;
		cameraShot2.Target = target;
		CameraShot cameraShot3 = cameraShot2;
		Shots.Add(cameraShot3);
		if (!Persist())
		{
			return;
		}
		if (flag)
		{
			subtitle("Guardada '" + cameraShot3.Name + "' (FOV " + cameraShot3.Fov.ToString("0") + ")", 2500u);
		}
		else
		{
			subtitle("Guardada '" + cameraShot3.Name + "' pero SIN OBJETIVO: acercate al lugar, apuntalo y apreta T", 5000u);
		}
		log("Camara guardada: " + cameraShot3.Name + " en " + IniFile.V3(edPos.X, edPos.Y, edPos.Z) + ((!flag) ? " (objetivo estimado)" : string.Empty));
	}

	public void EditorSetTarget()
	{
		if (EditorActive)
		{
			CameraShot cameraShot = LastManualShot();
			if (cameraShot == null)
			{
				subtitle("Primero guarda una camara con ENTER", 2500u);
				return;
			}
			if (!G.TryFindTarget(edPos, edRot, 1500f, fine: true, out var target))
			{
				subtitle("No encuentro el suelo ahi: acercate mas al lugar", 2500u);
				return;
			}
			cameraShot.Target = target;
			cameraShot.HasTarget = true;
			if (!Persist())
			{
				return;
			}
			subtitle("Objetivo de '" + cameraShot.Name + "' fijado (a " + Vector3.Distance(cameraShot.Pos, target).ToString("0") + " m de la camara)", 3000u);
		}
	}

	public void EditorDeleteLast()
	{
		if (EditorActive)
		{
			CameraShot cameraShot = LastManualShot();
			if (cameraShot == null)
			{
				subtitle("No hay camaras para borrar", 2000u);
				return;
			}
			Shots.Remove(cameraShot);
			if (!Persist())
			{
				return;
			}
			subtitle("Borrada '" + cameraShot.Name + "'", 2500u);
		}
	}

	private CameraShot LastManualShot()
	{
		for (int num = Shots.Count - 1; num >= 0; num--)
		{
			if (!Shots[num].IsAuto)
			{
				return Shots[num];
			}
		}
		return null;
	}

	public bool Persist()
	{
		try
		{
			CameraStore.Save(cfg.Folder, Shots);
			return true;
		}
		catch (Exception ex)
		{
			log("No se pudo guardar camaras.ini: " + ex.Message);
			subtitle("ERROR guardando camaras.ini: cambios solo en esta sesion", 4500u);
			return false;
		}
	}

	public string StatusText()
	{
		if (EditorActive)
		{
			return "editor de camaras";
		}
		if (!Active)
		{
			return "apagado";
		}
		string text = ((Current == null) ? "-" : (Current.IsFollow ? "siguiendo a un NPC" : ((!Current.IsAuto) ? Current.Name : "automatica")));
		return "encendido, plano: " + text + ", fase " + phase;
	}
}
