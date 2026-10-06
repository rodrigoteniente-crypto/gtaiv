using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using System.Windows.Forms;
using static KickChaos.N;

namespace KickChaos
{
    /// <summary>
    /// Director de camaras: esconde al jugador, pone una camara con FOV bajo y la va
    /// cambiando entre los planos guardados (o planos automaticos), con fundidos.
    /// Tambien incluye el editor (camara libre) para guardar planos nuevos.
    /// </summary>
    public class Director
    {
        enum Phase { Idle, FadingOut, Loading, Showing }

        Config cfg;
        readonly Action<string> log;
        readonly Action<string, uint> subtitle;

        public List<CameraShot> Shots = new List<CameraShot>();
        public bool Active { get; private set; }
        public bool EditorActive { get; private set; }
        public CameraShot Current { get; private set; }
        public Vector3 CurrentTarget { get; private set; }
        public bool PoliceMode;              // la persecucion activa: la policia puede "ver" al jugador
        public readonly TrafficCleaner Traffic = new TrafficCleaner();
        public readonly StreetRepair Repair = new StreetRepair();
        public ChaosActions Actions;          // para apagar los incendios al arreglar
        public SubNpcManager Npcs;            // NPC de suscriptores (camara que los sigue)
        bool repairAtSwitch;                  // arreglar en el proximo cambio (con la pantalla en negro)
        public double WeatherLockUntil = -1; // una accion (Tormenta) manda sobre el clima
        public double TimeLockUntil = -1;    // una accion (Noche/Dia) manda sobre la hora

        int cam;
        Phase phase = Phase.Idle;
        double phaseStart, shotStart, lastUpkeep, lastAnchorMove, lastInfo, lastFrame, lastAim, lastCamForce;
        float shotDuration;
        int index = -1;
        int switchCount;
        bool requestNext;
        bool pendingChosen;         // ya se eligio el proximo plano
        CameraShot pendingShot;     // null = automatica
        bool pendingForced;         // lo pidieron desde el menu ("ver esta camara")
        bool switchFade = true;     // la transicion que se esta usando ahora
        bool forceFade;             // el proximo cambio usa fundido si o si (para arreglar la calle)
        double holdUntil = -1;
        Vector3 anchor, hub, returnPos;
        float returnHeading;
        int returnVehicle;
        uint returnRoomKey;
        bool returnWasHeli;
        bool haveHub, weatherForced, densityTouched;
        double shakeStart, shakeUntil, lastIgnoreApply = -100;
        bool lastIgnore;
        float shakeAmp, shakePhase1, shakePhase2;

        readonly CameraInterruptionPolicy interruption = new CameraInterruptionPolicy();
        bool streamInterruptions = true, returnToCity;
        double streamDuration = 10, spawnDuration = 15;
        Observation observation;
        CameraResume resume;
        CameraResume temporaryResume;
        bool resumingShot;
        double resumeElapsed;
        readonly Dictionary<int, double> cameraSkipUntil = new Dictionary<int, double>();
        sealed class Observation
        {
            public int Ped, Priority;
            public double Seconds, Expires;
            public string Reason;
            public bool Manual, Pin;
        }
        sealed class CameraResume
        {
            public CameraShot Shot;
            public Vector3 Target, Anchor;
            public double Elapsed, HoldRemaining;
            public float Duration;
            public int Index, SwitchCount;
            public bool Pinned;
        }

        public bool TemporaryCameraActive { get { return interruption.Active; } }
        public double CityRemainingSeconds
        {
            get
            {
                if (resume != null && !resume.Shot.IsFollow && !resume.Shot.IsBattle)
                    return CityCameraClock.Remaining(resume.Duration, resume.Elapsed);
                if (!Active || Current == null || Current.IsFollow || Current.IsBattle) return 0;
                return CityCameraClock.Remaining(shotDuration, CityCameraClock.Elapsed(G.Now, shotStart));
            }
        }

        // editor
        Vector3 edPos, edRot, edAim;
        bool edAimHit;
        float edFov;
        Vector3 lastEditorAnchor;

        // ajustes
        public bool AutoStart = true;
        public float StartDelay = 8f;
        bool randomOrder, fade, globalCityDuration = true;
        string mode = "lista";
        float defDuration = 20f, defFov = 25f, sway = 0.25f, anchorHeight = 20f, holdAfterAction = 8f, maxHold = 60f;
        int fadeMs = 500, loadMs = 1500;
        int fixedHour = -1, fixedMinute, fixedWeather = -1;
        float pedDensity = 1f, carDensity = 1f;
        float autoRadius = 350f, autoDistMin = 35f, autoDistMax = 90f, autoHMin = 6f, autoHMax = 25f, autoFovMin = 18f, autoFovMax = 32f, autoDrift = 6f;
        float mouseSens = 0.12f, edSpeed = 10f;
        bool invertMouse;

        // camara que sigue al NPC de un suscriptor
        float followDuration = 30f, followDist = 22f, followHeight = 6f, followFov = 30f;
        Vector3 fCam, fLook, fPrevTarget, fVel;
        float fAz, fH, lastDt = 0.016f;
        int collisionPed;
        Vector3 fLastGoodCam;
        bool fHaveGoodCam;
        double fUnsafeSince = -1;
        double fLastLos = -100, fBlockedSince = -1, fGoneAt = -1, fLastAnchor = -100, fLastRelax = -100;
        // camara dinamica: cada tantos segundos cambia de "toma" (orbita, cerca, de costado, alta, de frente)
        bool followDynamic = true;
        bool followLock;                 // "seguir NPC" (tecla): se queda con los NPC hasta que no quede ninguno o F6
        // camara que apunta a quien le esta tirando (mientras dispara)
        bool aimCam = true;
        // 1.9: NPC en un tunel / bajo tierra: camara cerca y el jugador oculto "en la misma sala"
        bool underCam = true, fUnder;
        double fUnderCheck = -100;
        uint fRoomKey;
        float fDuelW, fDuelSide = 28f;
        Vector3 fDuelTarget;
        // camara del tiroteo (encuadra a todos)
        bool battleCam = true;
        readonly List<Vector3> bPts = new List<Vector3>();
        Vector3 bCam, bLook;
        float bAz, bH, bDist = 30f, bFov = 40f, bOrbit = 3f;
        double bLastEval = -100, bGoneAt = -1, bLastAnchor = -100;
        int fBeat = -1;
        double fBeatUntil = -1;
        float fDistMul = 1f, fHMul = 1f, fFovAdd = 0f, fOrbit = 2f, fSide = 0f;          // lo que se usa ahora
        float tDistMul = 1f, tHMul = 1f, tFovAdd = 0f, tOrbit = 2f, tSide = 0f;          // a donde va

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
            const string S = "Director";
            AutoStart = ini.GetBool(S, "AutoIniciar", true);
            StartDelay = ini.GetFloat(S, "EsperaAlCargar", 8f);
            randomOrder = Config.Normalize(ini.Get(S, "Orden", "secuencial")).StartsWith("alea");
            mode = Config.Normalize(ini.Get(S, "Modo", "lista")).Trim();
            defDuration = Math.Max(3f, ini.GetFloat("CamaraStream", "DuracionCiudadSegundos", ini.GetFloat(S, "DuracionPorDefecto", 300f)));
            globalCityDuration = ini.GetBool("CamaraStream", "UsarDuracionGlobal", true);
            defFov = ini.GetFloat(S, "FOVPorDefecto", 25f);
            fade = !Config.Normalize(ini.Get(S, "Transicion", "fundido")).StartsWith("corte");
            fadeMs = Math.Max(0, ini.GetInt(S, "FundidoMs", 500));
            loadMs = Math.Max(0, ini.GetInt(S, "EsperaCargaMs", 1500));
            sway = Math.Max(0f, ini.GetFloat(S, "Balanceo", 0.25f));
            anchorHeight = ini.GetFloat(S, "AlturaJugadorOculto", 20f);
            holdAfterAction = Math.Max(0f, ini.GetFloat(S, "NoCortarTrasAccion", 8f));
            maxHold = Math.Max(0f, ini.GetFloat(S, "EsperaMaximaExtra", 60f));
            CameraStore.ParseTime(ini.Get(S, "HoraFija", ""), out fixedHour, out fixedMinute);
            realClock = ini.GetBool(S, "HoraReal", true);
            lastClock = -100;
            fixedWeather = CameraStore.ParseWeather(ini.Get(S, "ClimaFijo", ""));
            pedDensity = ini.GetFloat(S, "DensidadPeatones", 1f);
            carDensity = ini.GetFloat(S, "DensidadTrafico", 1f);

            Traffic.ApplyConfig(ini);
            Repair.ApplyConfig(ini);

            const string A = "CamarasAutomaticas";
            autoRadius = ini.GetFloat(A, "Radio", 350f);
            autoDistMin = ini.GetFloat(A, "DistanciaMin", 35f);
            autoDistMax = Math.Max(autoDistMin + 1f, ini.GetFloat(A, "DistanciaMax", 90f));
            autoHMin = ini.GetFloat(A, "AlturaMin", 6f);
            autoHMax = Math.Max(autoHMin + 0.5f, ini.GetFloat(A, "AlturaMax", 25f));
            autoFovMin = ini.GetFloat(A, "FOVMin", 18f);
            autoFovMax = Math.Max(autoFovMin, ini.GetFloat(A, "FOVMax", 32f));
            autoDrift = Math.Max(0f, ini.GetFloat(A, "Desplazamiento", 6f));

            const string SU = "Suscriptor";
            followDuration = Math.Max(5f, ini.GetFloat(SU, "CamaraDuracion", 30f));
            followDist = Math.Max(5f, Math.Min(150f, ini.GetFloat(SU, "CamaraDistancia", 22f)));
            followHeight = Math.Max(1f, Math.Min(80f, ini.GetFloat(SU, "CamaraAltura", 6f)));
            followFov = Math.Max(5f, Math.Min(90f, ini.GetFloat(SU, "CamaraFOV", 30f)));
            followDynamic = ini.GetBool(SU, "CamaraDinamica", true);
            aimCam = ini.GetBool(SU, "CamaraApuntaAlObjetivo", true);
            underCam = ini.GetBool(SU, "CamaraBajoTierra", true);
            battleCam = ini.GetBool(SU, "CamaraAlTiroteo", true);

            streamInterruptions = ini.GetBool("CamaraStream", "Activa", true);
            interruption.EvaluationSeconds = Math.Max(1, ini.GetFloat("CamaraStream", "EvaluarCadaSegundos", 20));
            interruption.Probability = Math.Max(0, Math.Min(100, ini.GetFloat("CamaraStream", "Probabilidad", 30)));
            interruption.CooldownSeconds = Math.Max(0, ini.GetFloat("CamaraStream", "CooldownSegundos", 120));
            streamDuration = Math.Max(1, Math.Min(120, ini.GetFloat("CamaraStream", "DuracionSegundos", 10)));
            spawnDuration = Math.Max(1, Math.Min(120, ini.GetFloat("CamaraStream", "DuracionSpawnSegundos", 15)));

            const string E = "Editor";
            mouseSens = ini.GetFloat(E, "SensibilidadMouse", 0.12f);
            invertMouse = ini.GetBool(E, "InvertirMouse", false);
            edSpeed = ini.GetFloat(E, "Velocidad", 10f);
        }

        public void LoadShots(List<string> warnings)
        {
            Shots = CameraStore.Load(cfg.Folder, defFov, defDuration, warnings);
            index = -1;
            ClearPending();
        }

        // ------------------------------------------------------------------
        // Encendido / apagado
        // ------------------------------------------------------------------
        public void Start()
        {
            if (Active) return;
            if (EditorActive) ExitEditor();
            RememberReturn();
            if (!haveHub) { hub = returnPos; haveHub = true; }
            CreateCam();
            Active = true;
            if (!pendingForced) { ClearPending(); index = -1; }
            switchCount = 0;
            requestNext = false;
            holdUntil = -1;
            interruption.Reset(G.Now);
            resume = null;
            temporaryResume = null;
            returnToCity = false;
            switchFade = true;
            HidePlayer(returnPos + new Vector3(0, 0, anchorHeight));
            DISPLAY_HUD(false);
            DISPLAY_RADAR(false);
            SET_MAX_WANTED_LEVEL(6);
            CLEAR_WANTED_LEVEL(G.PlayerIndex);
            lastIgnoreApply = -100;
            log("Director encendido (" + Shots.Count + " camaras guardadas, modo " + mode + ")");
            // primer plano: arrancamos en negro y cargamos
            DO_SCREEN_FADE_OUT(0);
            BeginLoading();
        }

        public void Stop()
        {
            followLock = false;
            ResetObservations();
            if (!Active) return;
            Active = false;
            ClearPending();
            phase = Phase.Idle;
            DO_SCREEN_FADE_OUT(0);
            DestroyCam();
            ResetWorldTweaks();
            RestorePlayer(true);
            DO_SCREEN_FADE_IN(400);
            log("Director apagado");
        }

        /// <summary>Apagado rapido sin cargas bloqueantes (al descargar el script o antes de cargar partida).</summary>
        public void QuickTeardown(bool loadScene = false)
        {
            if (!Active && !EditorActive) return;
            Active = false;
            EditorActive = false;
            followLock = false;
            ResetObservations();
            phase = Phase.Idle;
            ClearPending();
            DestroyCam();
            ResetWorldTweaks();
            RestorePlayer(loadScene);
            DO_SCREEN_FADE_IN(0);
        }

        /// <summary>Olvidar el estado sin llamar al juego (despues de cargar una partida).</summary>
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
            followLock = false;
            ResetObservations();
            ClearPending();
            holdUntil = WeatherLockUntil = TimeLockUntil = -1;
        }

        /// <summary>Al descargar el script (Ctrl+F11): apaga todo y deja a Niko con el suelo cargado.</summary>
        public void Shutdown()
        {
            try { QuickTeardown(true); } catch { }
        }

        void ResetWorldTweaks()
        {
            if (weatherForced) { RELEASE_WEATHER(); weatherForced = false; }
            if (densityTouched) { SET_PED_DENSITY_MULTIPLIER(1f); SET_CAR_DENSITY_MULTIPLIER(1f); densityTouched = false; }
            PoliceMode = false;
        }

        /// <summary>Usar el punto actual del jugador como centro de las camaras automaticas.</summary>
        public void SetHubHere()
        {
            hub = G.CharPos(G.PlayerPed);
            haveHub = true;
        }

        public void Next()
        {
            if (returnToCity) return;
            if (interruption.Active || followLock)
            {
                interruption.Finish(G.Now);
                observation = null;
                resume = temporaryResume = null;
                followLock = false;
                ClearPending();
            }
            requestNext = true;
        }

        void ResetObservations()
        {
            observation = null;
            resume = null;
            temporaryResume = null;
            returnToCity = false;
            interruption.Reset(G.Now);
            cameraSkipUntil.Clear();
            resumingShot = false;
        }

        /// <summary>Arreglar ya (desde el menu): con fundido si el director esta prendido.</summary>
        public void RepairSoon()
        {
            if (!Repair.IsDirty) return;
            if (!Active || Repair.Mode.StartsWith("direc")) { Repair.RepairNow(Actions); Traffic.Reset(); return; }
            repairAtSwitch = true;
            if (Current != null && !Current.IsAuto) { pendingShot = Current; pendingChosen = true; pendingForced = true; }
            requestNext = true;
            forceFade = true;
        }

        /// <summary>Sacudida de camara. La amplitud esta pensada para FOV 50 y se escala con el FOV actual.</summary>
        public void Shake(float amplitudeDeg, float seconds)
        {
            double now = G.Now;
            if (now < shakeUntil && amplitudeDeg < shakeAmp) return;
            shakeAmp = amplitudeDeg;
            shakeStart = now;
            shakeUntil = now + seconds;
            shakePhase1 = G.Rand(0f, 6.28f);
            shakePhase2 = G.Rand(0f, 6.28f);
        }

        /// <summary>No cambiar de plano por X segundos (para que se vea lo que esta pasando).</summary>
        public void HoldFor(float seconds)
        {
            holdUntil = Math.Max(holdUntil, G.Now + Math.Max(seconds, holdAfterAction));
        }

        /// <summary>True cuando conviene disparar acciones (no durante un fundido o una carga).</summary>
        public bool ReadyForAction()
        {
            if (EditorActive) return false;
            if (!Active) return true;
            return phase == Phase.Showing && G.Now - shotStart >= (fade ? fadeMs / 1000.0 : 0.2);
        }

        /// <summary>Punto donde deben ocurrir las acciones (lo que ve la camara).</summary>
        public Vector3 ActionCenter()
        {
            if (Active && Current != null)
            {
                // siguiendo a un NPC: las acciones pasan cerca de el, no encima (si no, lo mata la primera explosion)
                if (Current.IsFollow || Current.IsBattle) return G.RandomGroundPoint(CurrentTarget, 18f, 28f);
                return CurrentTarget;
            }
            if (EditorActive) return edAim;
            int gc;
            Vector3 p, r;
            GET_GAME_CAM(out gc);
            GET_CAM_POS(gc, out p);
            GET_CAM_ROT(gc, out r);
            Vector3 t = G.FindTarget(p, r, 120f);
            if (Vector3.Distance(p, t) < 8f) t = G.CharPos(G.PlayerPed);
            return t;
        }

        // ------------------------------------------------------------------
        // Tick
        // ------------------------------------------------------------------
        // 1.9: la hora del juego igual a la de Windows
        bool realClock;
        double lastClock = -100;
        bool clockLogged;

        void RealClock(double now)
        {
            if (!realClock || now - lastClock < 3.0 || now < TimeLockUntil) return;
            lastClock = now;
            try
            {
                DateTime t = DateTime.Now;
                SET_TIME_OF_DAY((uint)t.Hour, (uint)t.Minute);
                if (!clockLogged) { clockLogged = true; log("[Director] hora del juego = hora de Windows (" + t.ToString("HH:mm") + ")"); }
            }
            catch { }
        }

        public void Update(bool actionsPending)
        {
            double now = G.Now;
            float dt = (float)Math.Min(0.1, Math.Max(0.0, now - lastFrame));
            lastFrame = now;
            lastDt = dt;

            if (EditorActive) { UpdateEditor(now, dt); return; }
            if (!Active) return;
            RealClock(now);

            HIDE_HUD_AND_RADAR_THIS_FRAME();
            if (pedDensity != 1f) { SET_PED_DENSITY_MULTIPLIER(pedDensity); densityTouched = true; }
            if (carDensity != 1f) { SET_CAR_DENSITY_MULTIPLIER(carDensity); densityTouched = true; }

            if (now - lastUpkeep > 1.0)
            {
                lastUpkeep = now;
                Upkeep();
            }
            if (phase == Phase.Showing)
            {
                interruption.Visible(now);
                if (returnToCity || (interruption.Active && (interruption.Expired(now) || !LiveCameraPed(FollowedPed))))
                    RestoreInterruptedView(now);
                if (phase == Phase.Showing)
                {
                    ProcessObservation(now);
                    if (phase == Phase.Showing) EvaluateStreamCamera(now);
                    if (phase == Phase.Showing) ProcessObservation(now);
                }
            }
            if (phase == Phase.Showing && Current != null)
            {
                bool calm = now >= holdUntil && !PoliceMode && !actionsPending;
                try
                {
                    Traffic.Update(CurrentTarget, calm, false, InFrame);
                    // con FOV bajo el objetivo puede estar lejos: revisar tambien cerca de la camara
                    if (Vector3.Distance(Current.Pos, CurrentTarget) > Traffic.Radius) Traffic.Update(Current.Pos, calm, true, InFrame);
                }
                catch { }
            }

            switch (phase)
            {
                case Phase.FadingOut:
                    {
                        double waited = now - phaseStart;
                        if ((waited >= fadeMs / 1000.0 && IS_SCREEN_FADED_OUT()) || waited > fadeMs / 1000.0 + 2.0)
                            BeginLoading();
                        break;
                    }
                case Phase.Loading:
                    if (Current != null && (now - phaseStart >= (switchFade ? loadMs / 1000.0 : 0.0) || now - phaseStart > 10))
                    {
                        if (switchFade) DO_SCREEN_FADE_IN((uint)fadeMs);
                        else DO_SCREEN_FADE_IN(0);
                        phase = Phase.Showing;
                        shotStart = resumingShot ? CityCameraClock.ResumeStart(now, resumeElapsed) : now;
                        resumingShot = false;
                        interruption.Visible(now);
                    }
                    else if (Current == null && now - phaseStart > 10.0)
                    {
                        log("[Camara] No se pudo cargar una toma: se recupera la camara del juego");
                        Stop();
                    }
                    break;
                case Phase.Showing:
                    {
                        if (Current != null && Current.IsBattle)
                        {
                            // se termino el tiroteo: unos segundos mas y a otra camara
                            Vector3 bc;
                            bool on = Npcs != null && Npcs.BattleInfo(null, out bc);
                            if (on) bGoneAt = -1;
                            else
                            {
                                if (bGoneAt < 0) bGoneAt = now;
                                if (now - bGoneAt > 5 && now - shotStart > 6) requestNext = true;
                            }
                        }
                        if (Current != null && Current.IsFollow)
                        {
                            // el NPC murio o ya no esta: unos segundos mas y a otra camara
                            Vector3 tp;
                            bool ic = false, dead = false;
                            bool exists = Npcs != null && Npcs.FollowInfo(Current.FollowPed, out tp, out ic, out dead);
                            if (!exists || dead || !LiveCameraPed(Current.FollowPed))
                            {
                                if (fGoneAt < 0) fGoneAt = now;
                                if (now - fGoneAt > (exists ? 5.0 : 1.5)) requestNext = true;
                            }
                        }
                        if (!interruption.Active && !followLock && !repairAtSwitch && Repair.IsDue() && !PoliceMode && (Npcs == null || !Npcs.Busy))
                        {
                            if (Repair.Mode.StartsWith("direc")) { Repair.RepairNow(Actions); Traffic.Reset(); }
                            else
                            {
                                repairAtSwitch = true;
                                if (Repair.Mode.StartsWith("fund") && Current != null)
                                {
                                    // fundido y volver a la misma camara
                                    if (!Current.IsAuto) { pendingShot = Current; pendingChosen = true; pendingForced = true; }
                                    requestNext = true;
                                    forceFade = true;
                                }
                            }
                        }
                        bool timeUp = now - shotStart >= shotDuration;
                        bool hold = now < holdUntil || PoliceMode || actionsPending;
                        if (now > shotStart + shotDuration + maxHold) hold = false; // nunca quedarse para siempre
                        if (requestNext || (!interruption.Active && !followLock && timeUp && !hold))
                        {
                            requestNext = false;
                            if (!pendingChosen) ChoosePending();
                            switchFade = forceFade || TransitionIsFade(pendingShot);
                            forceFade = false;
                            if (switchFade && fadeMs > 0)
                            {
                                DO_SCREEN_FADE_OUT((uint)fadeMs);
                                phase = Phase.FadingOut;
                                phaseStart = now;
                            }
                            else BeginLoading();
                        }
                        break;
                    }
            }

            ApplyCamera(now);
        }

        void BeginLoading()
        {
            ForceCamActive(); // la pantalla esta en negro: buen momento para asegurar la camara
            DoSwitch();
            phase = Phase.Loading;
            phaseStart = G.Now; // DoSwitch puede tardar (LOAD_SCENE)
            requestNext = false;
            holdUntil = -1;
        }

        void Upkeep()
        {
            int ped = G.PlayerPed;
            int pl = G.PlayerIndex;
            if (!DOES_CAM_EXIST(cam)) CreateCam();
            if (!IS_CAM_ACTIVE(cam) || G.Now - lastCamForce > 10.0) ForceCamActive();
            DISPLAY_HUD(false);
            DISPLAY_RADAR(false);
            CLEAR_HELP();
            SET_CHAR_VISIBLE(ped, false);
            SET_CHAR_INVINCIBLE(ped, true);
            SET_PLAYER_CONTROL(pl, false);
            // la policia solo se toca cuando cambia algo (o cada tanto): tocarla todo el tiempo
            // puede hacer que los patrulleros reconsideren a cada rato a donde van
            bool ignore = !PoliceMode;
            if (ignore != lastIgnore || G.Now - lastIgnoreApply > 15.0)
            {
                SET_EVERYONE_IGNORE_PLAYER(pl, ignore);
                SET_POLICE_IGNORE_PLAYER(pl, ignore);
                SET_MAX_WANTED_LEVEL(6);
                lastIgnore = ignore;
                lastIgnoreApply = G.Now;
            }
            if (IS_PLAYER_BEING_ARRESTED()) CLEAR_WANTED_LEVEL(pl);
            if (IS_CHAR_IN_ANY_CAR(ped)) WARP_CHAR_FROM_CAR_TO_COORD(ped, anchor);
            if (Vector3.Distance(G.CharPos(ped), anchor) > 3f) MovePlayer(anchor);
        }

        void ApplyCamera(double now)
        {
            CameraShot s = Current;
            if (s == null || !DOES_CAM_EXIST(cam)) return;
            int desiredCollisionPed = s.IsFollow ? s.FollowPed : 0;
            if (collisionPed != desiredCollisionPed)
            {
                collisionPed = desiredCollisionPed;
                ENABLE_CAM_COLLISION(cam, collisionPed != 0);
                if (collisionPed != 0) SET_CAM_TARGET_PED(cam, collisionPed);
            }
            if (s.IsFollow) UpdateFollow(s, now, lastDt);
            else if (s.IsBattle) UpdateBattle(s, now, lastDt);
            float t = shotDuration > 0 ? (resumingShot ? (float)(resumeElapsed / shotDuration) : phase == Phase.Showing ? (float)((now - shotStart) / shotDuration) : 0f) : 0f;
            float k = MathX.Smooth(t);
            Vector3 pos = s.Pos, rot = s.Rot;
            float fov = s.Fov;
            if (s.HasEnd && !s.IsFollow)
            {
                pos = Vector3.Lerp(s.Pos, s.EndPos, k);
                rot = new Vector3(MathX.LerpAngle(s.Rot.X, s.EndRot.X, k), MathX.LerpAngle(s.Rot.Y, s.EndRot.Y, k), MathX.LerpAngle(s.Rot.Z, s.EndRot.Z, k));
                if (s.EndFov > 0) fov = s.Fov + (s.EndFov - s.Fov) * k;
            }
            float sw = s.Sway >= 0 ? s.Sway : sway;
            if (sw > 0)
            {
                rot.X += (float)(Math.Sin(now * 0.31) * 0.6 + Math.Sin(now * 0.83 + 0.7) * 0.25) * sw;
                rot.Z += (float)(Math.Sin(now * 0.23 + 1.3) + Math.Sin(now * 0.67 + 2.1) * 0.3) * sw;
            }
            if (now < shakeUntil)
            {
                // sacudida suave (no aleatoria por frame) y proporcional al FOV
                float rem = (float)((shakeUntil - now) / Math.Max(0.01, shakeUntil - shakeStart));
                float a = shakeAmp * rem * (fov / 50f);
                rot.X += a * (float)(0.6 * Math.Sin(now * 23.0 + shakePhase1) + 0.4 * Math.Sin(now * 37.0 + shakePhase2));
                rot.Z += a * (float)(0.6 * Math.Sin(now * 19.0 + shakePhase2) + 0.4 * Math.Sin(now * 31.0 + shakePhase1));
            }
            SET_CAM_POS(cam, pos);
            SET_CAM_ROT(cam, rot);
            SET_CAM_FOV(cam, fov);
            CamPos = pos; CamRot = rot; CamFov = fov;
        }

        /// <summary>Pose actual de la camara del director (para saber que se ve).</summary>
        public Vector3 CamPos, CamRot;
        public float CamFov = 25f;

        /// <summary>Adentro de la parte central del cuadro ('frac' = 0.8: el 80 % del medio).</summary>
        public bool InFrameCenter(Vector3 p, float frac)
        {
            Vector3 d = p - CamPos;
            Vector3 fwd = MathX.Forward(CamRot);
            float z = Vector3.Dot(d, fwd);
            if (z <= 1f) return false;
            Vector3 right = MathX.Right(CamRot);
            Vector3 up = Vector3.Cross(right, fwd);
            double halfV = CamFov * 0.5 * Math.PI / 180.0;
            double halfH = Math.Atan(Math.Tan(halfV) * 16.0 / 9.0);
            return Math.Abs(Vector3.Dot(d, right)) <= Math.Tan(halfH) * z * frac && Math.Abs(Vector3.Dot(d, up)) <= Math.Tan(halfV) * z * frac;
        }

        /// <summary>True si un punto cae dentro del encuadre actual (con margen; no tiene en cuenta paredes).</summary>
        public bool InFrame(Vector3 p, float margin)
        {
            Vector3 d = p - CamPos;
            float dist = d.Length();
            if (dist < 4f) return true;
            if (dist > 600f) return false;
            Vector3 fwd = MathX.Forward(CamRot);
            Vector3 right = MathX.Right(CamRot);
            Vector3 up = Vector3.Cross(right, fwd);
            float z = Vector3.Dot(d, fwd);
            if (z <= 0) return false;
            double halfV = CamFov * 0.5 * Math.PI / 180.0;
            double halfH = Math.Atan(Math.Tan(halfV) * 16.0 / 9.0);
            float x = Vector3.Dot(d, right), y = Vector3.Dot(d, up);
            float mx = (float)Math.Tan(halfH) * z + margin, my = (float)Math.Tan(halfV) * z + margin;
            return Math.Abs(x) <= mx && Math.Abs(y) <= my;
        }

        void DoSwitch()
        {
            if (repairAtSwitch)
            {
                repairAtSwitch = false;
                try { Repair.RepairNow(Actions); Traffic.Reset(); } catch (Exception ex) { log("Error arreglando la calle: " + ex.Message); }
                log("Calle arreglada");
            }
            CameraShot next = PickNext();
            if (next == null)
            {
                log("No se pudo armar un plano; se reintenta.");
                return;
            }
            Current = next;
            shotDuration = !next.IsFollow && !next.IsBattle && globalCityDuration ? defDuration :
                next.Duration > 0 ? next.Duration : defDuration;
            shotStart = G.Now;
            fGoneAt = -1;

            if (next.IsBattle)
            {
                // camara del tiroteo: cargamos donde es y buscamos desde donde se vean todos
                Vector3 c = next.Target;
                MovePlayer(c + new Vector3(0, 0, anchorHeight));
                REQUEST_COLLISION_AT_POSN(c);
                if (switchFade) LOAD_SCENE(c);
                CurrentTarget = c;
                InitBattle(next);
            }
            else if (next.IsFollow)
            {
                // camara que sigue al NPC: cargamos donde esta y buscamos un angulo libre
                Vector3 np;
                bool inCar, dead;
                if (Npcs == null || !Npcs.FollowInfo(next.FollowPed, out np, out inCar, out dead)) { np = CurrentTarget; inCar = false; }
                MovePlayer(np + new Vector3(0, 0, anchorHeight));
                REQUEST_COLLISION_AT_POSN(np);
                LOAD_SCENE(np);
                CurrentTarget = np;
                InitFollow(next, np, inCar);
            }
            else
            {
                if (!next.IsAuto)
                {
                    // Llevamos al jugador cerca para que cargue la zona, despues calculamos el objetivo.
                    MovePlayer(next.HasAnchor ? next.Anchor : next.Pos);
                    if (next.HasTarget) REQUEST_COLLISION_AT_POSN(next.Target);
                    REQUEST_COLLISION_AT_POSN(next.Pos);
                    LOAD_SCENE(next.Pos);
                    CurrentTarget = next.HasTarget ? next.Target : G.FindTarget(next.Pos, next.Rot, 1500f);
                }
                else CurrentTarget = next.Target;

                anchor = next.HasAnchor ? next.Anchor : CurrentTarget + new Vector3(0, 0, anchorHeight);
                MovePlayer(anchor);
                // no vaciar la calle donde anda un NPC (se llevaria su auto o los patrulleros)
                if (Npcs == null || !Npcs.AnyNear(CurrentTarget, Traffic.Radius + 30f))
                {
                    try
                    {
                        Traffic.OnCameraSwitch(CurrentTarget);
                        if (Vector3.Distance(next.Pos, CurrentTarget) > Traffic.Radius) Traffic.OnCameraSwitch(next.Pos);
                    }
                    catch { }
                }
            }

            if (G.Now > TimeLockUntil && !realClock)
            {
                int h = next.Hour >= 0 ? next.Hour : fixedHour;
                int m = next.Hour >= 0 ? next.Minute : fixedMinute;
                if (h >= 0) SET_TIME_OF_DAY((uint)h, (uint)m);
            }
            ReapplyWeather();

            switchCount++;
            ApplyCamera(G.Now);
        }

        public void ReapplyWeather()
        {
            if (!Active)
            {
                if (weatherForced) { RELEASE_WEATHER(); weatherForced = false; }
                return;
            }
            if (G.Now < WeatherLockUntil) return;
            int w = Current != null && Current.Weather >= 0 ? Current.Weather : fixedWeather;
            if (w >= 0) { FORCE_WEATHER_NOW((uint)w); weatherForced = true; }
            else if (weatherForced) { RELEASE_WEATHER(); weatherForced = false; }
        }

        bool TransitionIsFade(CameraShot s)
        {
            if (s != null && s.Transition == 0) return true;
            if (s != null && s.Transition == 1) return false;
            return fade;
        }

        int ActiveCount()
        {
            int n = 0;
            foreach (var s in Shots) if (s.Active && !s.IsAuto) n++;
            return n;
        }

        /// <summary>Elige el proximo plano: indice en Shots, o -1 para uno automatico.</summary>
        int ChooseNextIndex()
        {
            int active = ActiveCount();
            bool wantAuto = mode.StartsWith("auto") || active == 0 || (mode.StartsWith("mix") && switchCount % 2 == 1);
            if (wantAuto) return -1;
            // hay lio (bandas, tiros): sorteo con mas chance para las camaras que miran esa zona
            Vector3 hot;
            if (active > 1 && Npcs != null && Npcs.Hotspot(out hot))
            {
                double total = 0;
                var w = new double[Shots.Count];
                for (int n = 0; n < Shots.Count; n++)
                {
                    if (n == index || !Shots[n].Active || Shots[n].IsAuto) continue;
                    w[n] = Math.Max(0.01, Shots[n].Weight) * GangRules.HotspotWeight(GangRules.ShotDistance(hot, Shots[n].Pos, ShotLook(Shots[n])));
                    total += w[n];
                }
                double r = G.Rng.NextDouble() * total;
                for (int n = 0; n < Shots.Count; n++)
                {
                    if (w[n] <= 0) continue;
                    r -= w[n];
                    if (r <= 0) return n;
                }
            }
            if (randomOrder && active > 1)
            {
                // sorteo con la "chance" de cada camara (sin repetir la que se esta viendo)
                double total = 0;
                for (int n = 0; n < Shots.Count; n++)
                    if (n != index && Shots[n].Active && !Shots[n].IsAuto) total += Math.Max(0.01, Shots[n].Weight);
                double r = G.Rng.NextDouble() * total;
                for (int n = 0; n < Shots.Count; n++)
                {
                    if (n == index || !Shots[n].Active || Shots[n].IsAuto) continue;
                    r -= Math.Max(0.01, Shots[n].Weight);
                    if (r <= 0) return n;
                }
            }
            for (int k = 1; k <= Shots.Count; k++)
            {
                int n = (index + k) % Shots.Count;
                if (Shots[n].Active && !Shots[n].IsAuto) return n;
            }
            return -1;
        }

        /// <summary>Lo que mira una camara: su objetivo marcado o un punto 60 m adelante.</summary>
        static Vector3 ShotLook(CameraShot s)
        {
            if (s.HasTarget) return s.Target;
            return s.Pos + MathX.Forward(s.Rot) * 60f;
        }

        void ChoosePending()
        {
            // "seguir NPC": el mismo mientras viva; si murio, el proximo; si no queda ninguno, camaras normales
            if (followLock)
            {
                int cur = Current != null && Current.IsFollow ? Current.FollowPed : 0;
                int ped = 0;
                Vector3 fp;
                bool fic, fdead;
                if (LiveCameraPed(cur) && Npcs != null && Npcs.FollowInfo(cur, out fp, out fic, out fdead) && !fdead) ped = cur;
                else if (Npcs != null) ped = Npcs.NextForCamera(cur);
                if (LiveCameraPed(ped))
                {
                    pendingShot = MakeFollowShot(ped);
                    pendingChosen = true;
                    pendingForced = false;
                    return;
                }
                followLock = false;
            }
            // NPC interruptions have their own clock and cooldown; city selection stays intact.
            int i = ChooseNextIndex();
            pendingShot = i >= 0 ? Shots[i] : null;
            pendingChosen = true;
            pendingForced = false;
        }

        void ClearPending()
        {
            pendingChosen = false;
            pendingShot = null;
            pendingForced = false;
        }

        CameraShot PickNext()
        {
            if (pendingChosen && pendingShot != null)
            {
                if (pendingShot.IsFollow)
                {
                    // el NPC puede haber muerto mientras la pantalla iba a negro
                    Vector3 p;
                    bool ic, dead;
                    if (!LiveCameraPed(pendingShot.FollowPed) || Npcs == null || !Npcs.FollowInfo(pendingShot.FollowPed, out p, out ic, out dead) || dead) ClearPending();
                }
                else if (pendingShot.IsBattle)
                {
                    Vector3 bc;
                    if (Npcs == null || !Npcs.BattleInfo(null, out bc)) ClearPending();
                    else pendingShot.Target = bc;
                }
                else
                {
                    // la camara elegida puede haberse borrado o apagado desde el menu mientras tanto
                    int i = Shots.IndexOf(pendingShot);
                    if (i < 0 || (!pendingShot.Active && !pendingForced)) ClearPending();
                }
            }
            if (!pendingChosen) ChoosePending();
            CameraShot chosen = pendingShot;
            ClearPending();
            if (chosen != null)
            {
                if (!chosen.IsFollow && !chosen.IsBattle) index = Shots.IndexOf(chosen);
                return chosen;
            }
            CameraShot a = BuildAutoShot();
            if (a != null) return a;
            if (Shots.Count == 0) return null;
            index = (index + 1) % Shots.Count;
            return Shots[index];
        }

        /// <summary>Saltar a una camara de la lista (para verla desde el menu).</summary>
        public void GoTo(int shotIndex)
        {
            if (shotIndex < 0 || shotIndex >= Shots.Count) return;
            ResetObservations();
            followLock = false;
            pendingShot = Shots[shotIndex];
            pendingChosen = true;
            pendingForced = true;
            if (!Active) Start(); else requestNext = true;
        }

        public int CurrentIndex { get { return Current != null && !Current.IsAuto ? Shots.IndexOf(Current) : -1; } }

        /// <summary>
        /// 1.9 carreras: la largada en lo que mira una de tus camaras y la llegada en lo que mira otra
        /// (a 350-1600 m). 'finish' = cero si no hay otra camara a esa distancia. False si no hay camaras.
        /// </summary>
        public bool RacePoints(out int startShot, out Vector3 start, out Vector3 finish)
        {
            startShot = -1; start = finish = Vector3.Zero;
            var ok = new List<int>();
            for (int n = 0; n < Shots.Count; n++) if (Shots[n].Active && !Shots[n].IsAuto) ok.Add(n);
            if (ok.Count == 0) return false;
            G.Shuffle(ok);
            foreach (int a in ok)
            {
                Vector3 sa = ShotLook(Shots[a]);
                var ends = new List<Vector3>();
                foreach (int b in ok)
                {
                    if (b == a) continue;
                    Vector3 sb = ShotLook(Shots[b]);
                    float dx = sa.X - sb.X, dy = sa.Y - sb.Y;
                    float d = (float)Math.Sqrt(dx * dx + dy * dy);
                    if (d > 350f && d < 1600f) ends.Add(sb);
                }
                if (ends.Count == 0) continue;
                startShot = a; start = sa; finish = ends[G.Rng.Next(ends.Count)];
                return true;
            }
            startShot = ok[0];
            start = ShotLook(Shots[ok[0]]);
            return true;
        }

        /// <summary>
        /// Una camara de la lista que mire lejos (minDist, en el plano) de todos los puntos de 'avoid'.
        /// -1 si no hay ninguna. 'pos' = lo que mira esa camara.
        /// </summary>
        public int FarShot(List<Vector3> avoid, float minDist, out Vector3 pos)
        {
            pos = Vector3.Zero;
            var ok = new List<int>();
            for (int n = 0; n < Shots.Count; n++)
            {
                CameraShot s = Shots[n];
                if (!s.Active || s.IsAuto) continue;
                Vector3 look = ShotLook(s);
                bool far = true;
                if (avoid != null)
                    foreach (Vector3 a in avoid)
                    {
                        float dx = a.X - look.X, dy = a.Y - look.Y;
                        if (dx * dx + dy * dy < minDist * minDist) { far = false; break; }
                    }
                if (far) ok.Add(n);
            }
            if (ok.Count == 0) return -1;
            int pick = ok[G.Rng.Next(ok.Count)];
            pos = ShotLook(Shots[pick]);
            return pick;
        }

        // ------------------------------------------------------------------
        // Camara que sigue al NPC de un suscriptor
        // ------------------------------------------------------------------
        CameraShot MakeFollowShot(int ped)
        {
            return new CameraShot { Name = "NPC", IsAuto = true, IsFollow = true, FollowPed = ped, Fov = followFov, Duration = followLock ? 3600f : followDuration };
        }

        /// <summary>Ir ya a la camara del NPC (cuando aparece). Con "seguir NPC" puesto, solo la tecla cambia de NPC.</summary>
        public void FollowNow(int ped, bool manual = false)
        {
            if (manual && followLock) { PinNpc(ped); return; }
            ObserveNpc(ped, spawnDuration, "spawn", 40, manual);
        }

        /// <summary>Temporary view; preserves the exact previous shot and its remaining time.</summary>
        public bool ObserveNpc(int ped, double seconds, string reason, int priority, bool manual = false)
        {
            if (EditorActive || ped == 0 || !LiveCameraPed(ped)) return false;
            if (!Active)
            {
                if (!manual && priority < 40) return false;
                Start();
            }
            if (!interruption.CanAccept(priority, manual, followLock)) return false;
            if (observation != null && observation.Priority > priority) return false;
            observation = new Observation { Ped = ped, Seconds = Math.Max(1, Math.Min(600, seconds)),
                Reason = reason ?? "NPC", Priority = priority, Manual = manual, Expires = G.Now + 45 };
            return true;
        }

        public bool PinNpc(int ped)
        {
            if (EditorActive || !LiveCameraPed(ped)) return false;
            if (!Active) Start();
            observation = new Observation { Ped = ped, Seconds = 3600, Reason = "manual", Priority = 100,
                Manual = true, Pin = true, Expires = G.Now + 45 };
            return true;
        }

        static bool LiveCameraPed(int ped)
        {
            return ped != 0 && DOES_CHAR_EXIST(ped) && !IS_CHAR_DEAD(ped) && !IS_CHAR_FATALLY_INJURED(ped);
        }

        void SaveInterruptedView(double now)
        {
            if (Current == null) return;
            if (resume != null)
            {
                if (followLock && !interruption.Active && temporaryResume == null) temporaryResume = CaptureView(now);
                return;
            }
            resume = CaptureView(now);
        }

        CameraResume CaptureView(double now)
        {
            return new CameraResume { Shot = Current, Target = CurrentTarget, Anchor = anchor,
                Elapsed = CityCameraClock.Elapsed(now, shotStart), HoldRemaining = Math.Max(0, holdUntil - now),
                Duration = shotDuration, Index = index, SwitchCount = switchCount, Pinned = followLock };
        }

        void ProcessObservation(double now)
        {
            Observation wanted = observation;
            if (wanted == null) return;
            if (wanted.Expires < now || !LiveCameraPed(wanted.Ped)) { observation = null; return; }
            if (!wanted.Pin && !interruption.CanAccept(wanted.Priority, wanted.Manual, followLock)) { observation = null; return; }
            SaveInterruptedView(now);
            if (wanted.Pin)
            {
                interruption.Finish(now);
                temporaryResume = null;
                followLock = true;
            }
            else if (!interruption.Begin(now, wanted.Seconds, wanted.Priority, wanted.Manual, followLock)) return;
            observation = null;
            returnToCity = false;
            pendingShot = MakeFollowShot(wanted.Ped);
            pendingShot.Duration = (float)wanted.Seconds;
            pendingShot.Transition = 1;
            pendingChosen = pendingForced = true;
            // Loading has to complete before follow duration starts. Avoid requestNext lost during Loading.
            switchFade = false;
            BeginLoading();
            log("[Camara] " + wanted.Reason + ": NPC " + wanted.Ped + (wanted.Pin ? " fijado" : " por " + wanted.Seconds.ToString("0") + " s"));
        }

        void RestoreInterruptedView(double now)
        {
            returnToCity = false;
            observation = null;
            interruption.Finish(now);
            ClearPending();
            requestNext = false;
            CameraResume saved = temporaryResume ?? resume;
            if (temporaryResume == null) resume = null;
            temporaryResume = null;
            if (saved == null) { followLock = false; ChoosePending(); switchFade = false; BeginLoading(); return; }
            if (saved.Shot.IsFollow && !LiveCameraPed(saved.Shot.FollowPed))
            {
                if (resume != null) { RestoreInterruptedView(now); return; }
                followLock = false;
                ChoosePending(); switchFade = false; BeginLoading(); return;
            }
            Current = saved.Shot;
            CurrentTarget = saved.Target;
            shotDuration = saved.Duration;
            index = saved.Index;
            switchCount = saved.SwitchCount;
            followLock = saved.Pinned;
            CLEAR_ROOM_FOR_CHAR(G.PlayerPed);
            MovePlayer(saved.Anchor);
            REQUEST_COLLISION_AT_POSN(saved.Target);
            // Return does not clear traffic or reroll the city camera.
            LOAD_SCENE(saved.Target);
            holdUntil = G.Now + saved.HoldRemaining;
            if (Current.IsFollow)
            {
                Vector3 p; bool inCar, dead;
                if (Npcs != null && Npcs.FollowInfo(Current.FollowPed, out p, out inCar, out dead)) InitFollow(Current, p, inCar);
            }
            resumeElapsed = saved.Elapsed;
            shotStart = CityCameraClock.ResumeStart(G.Now, saved.Elapsed);
            resumingShot = true;
            phase = Phase.Loading;
            phaseStart = G.Now;
            switchFade = false;
            ApplyCamera(G.Now);
            ReapplyWeather();
            log("[Camara] Vuelve a " + Current.Name + ": " + CityCameraClock.Remaining(saved.Duration, saved.Elapsed).ToString("0") + " s restantes");
        }

        void EvaluateStreamCamera(double now)
        {
            if (!streamInterruptions || !interruption.Evaluate(now, G.Rng.NextDouble(),
                !followLock && observation == null && Current != null && !Current.IsFollow && !Current.IsBattle && Npcs != null)) return;
            var rows = Npcs.SnapshotRows();
            double total = 0;
            foreach (var row in rows)
            {
                double until;
                if (cameraSkipUntil.TryGetValue(row.Ped, out until) && until > now) continue;
                total += StreamCameraCandidates.Weight(row, now, CamPos.X, CamPos.Y);
            }
            double roll = G.Rng.NextDouble() * total;
            if (total <= 0) return;
            foreach (var row in rows)
            {
                double until;
                if (cameraSkipUntil.TryGetValue(row.Ped, out until) && until > now) continue;
                double weight = StreamCameraCandidates.Weight(row, now, CamPos.X, CamPos.Y);
                if (weight <= 0) continue;
                roll -= weight;
                if (roll <= 0) { ObserveNpc(row.Ped, streamDuration, "ambiental", 10); break; }
            }
        }

        public bool FollowingNpc { get { return Active && Current != null && Current.IsFollow; } }

        public void ReplaceNpcPed(int previous, int replacement)
        {
            if (previous == 0 || replacement == 0 || previous == replacement) return;
            ReplaceFollowPed(Current, previous, replacement);
            ReplaceFollowPed(pendingShot, previous, replacement);
            if (resume != null) ReplaceFollowPed(resume.Shot, previous, replacement);
            if (temporaryResume != null) ReplaceFollowPed(temporaryResume.Shot, previous, replacement);
            if (observation != null && observation.Ped == previous) observation.Ped = replacement;
            double skippedUntil;
            if (cameraSkipUntil.TryGetValue(previous, out skippedUntil))
            {
                cameraSkipUntil.Remove(previous);
                cameraSkipUntil[replacement] = skippedUntil;
            }
            if (collisionPed == previous) collisionPed = 0;
        }

        static void ReplaceFollowPed(CameraShot shot, int previous, int replacement)
        {
            if (shot != null && shot.IsFollow && shot.FollowPed == previous) shot.FollowPed = replacement;
        }

        /// <summary>Tecla "seguir NPC": va al proximo NPC vivo (uno detras del otro). False si no hay ninguno.</summary>
        public bool FollowNext()
        {
            if (Npcs == null) return false;
            int ped = Npcs.NextForCamera(FollowedPed);
            if (ped == 0) return false;
            return PinNpc(ped);
        }

        /// <summary>Dejar de seguir NPC (vuelven las camaras normales).</summary>
        public void ReleaseFollow()
        {
            followLock = false;
            observation = null;
            temporaryResume = null;
            if (resume != null || interruption.Active) returnToCity = true;
        }

        public void ReturnToCity()
        {
            ReleaseFollow();
            if (!returnToCity && Current != null && (Current.IsFollow || Current.IsBattle)) requestNext = true;
        }

        public bool FollowLocked { get { return followLock; } }

        /// <summary>El NPC que esta siguiendo la camara (0 = ninguno).</summary>
        public int FollowedPed { get { return FollowingNpc ? Current.FollowPed : 0; } }

        static Vector3 Flat(float az) { return MathX.Forward(new Vector3(0, 0, az)); }

        Vector3 FollowPose(float az, float h, float dist, Vector3 look)
        {
            // A ground-height query can return a bridge roof. It cannot detect camera obstruction.
            // Native camera collision handles nearby geometry without lifting the shot above a tunnel.
            return look + Flat(az) * dist + new Vector3(0, 0, h);
        }

        static bool FollowClear(Vector3 look, Vector3 camPos)
        {
            return FollowCameraGeometry.Usable(camPos, look);
        }

        /// <summary>Pose inicial moderada; la colision nativa ajusta el espacio entre NPC y camara.</summary>
        bool FindFollowSpot(Vector3 look, float dist, float preferAz, float baseH, out float az, out float h)
        {
            float[] offs = { 0f, 35f, -35f, 70f, -70f, 110f, -110f, 180f, 145f, -145f };
            float[] hs = { baseH, Math.Min(baseH, 2f), 1f };
            foreach (float hh in hs)
                foreach (float o in offs)
                {
                    float a = preferAz + o;
                    if (FollowClear(look, FollowPose(a, hh, dist, look))) { az = a; h = hh; return true; }
                }
            az = preferAz;
            h = hs[hs.Length - 1];
            return false;
        }

        void InitFollow(CameraShot s, Vector3 target, bool inCar)
        {
            Vector3 look = target + new Vector3(0, 0, 0.5f);
            float heading = 0f;
            try { GET_CHAR_HEADING(s.FollowPed, out heading); } catch { }
            // a pie: tres cuartos de frente (se ve la cara y el nombre). En auto: de atras.
            float pref = inCar ? heading + 180f + G.Rand(-20f, 20f) : heading + (G.Rng.Next(2) == 0 ? 35f : -35f);
            // primera toma: la normal (de tres cuartos / de atras); despues va cambiando
            fBeat = -1;
            fDuelW = 0f; // no mirar al enemigo del NPC anterior
            fUnder = false; fRoomKey = 0; fUnderCheck = -100;
            SetBeat(0, inCar, G.Now, true);
            float dist = FollowDistNow(inCar);
            float az, h;
            FindFollowSpot(look, dist, pref, followHeight * fHMul, out az, out h);
            fAz = az;
            fH = h;
            fCam = FollowPose(az, h, dist, look);
            fLook = look;
            fPrevTarget = target;
            fVel = Vector3.Zero;
            fLastLos = fLastRelax = G.Now;
            fBlockedSince = -1;
            fLastAnchor = G.Now;
            fLastGoodCam = fCam;
            fHaveGoodCam = FollowCameraGeometry.Usable(fCam, look);
            fUnsafeSince = -1;
            s.Pos = fCam;
            s.Rot = MathX.LookRotation(fCam, fLook);
            s.Fov = followFov + fFovAdd;
        }

        float FollowDistNow(bool inCar) { return Math.Max(3.5f, followDist * (inCar ? 1.6f : 1f) * fDistMul); }

        // tomas: distancia (x), altura (x), FOV (+), giro quieto (grados/s), angulo respecto de "atras" (grados)
        static readonly float[][] FootBeats =
        {
            new[] { 1.00f, 1.00f, 0f, 2f, 0f },      // 0 normal
            new[] { 1.05f, 1.10f, 0f, 7f, 0f },      // 1 orbita
            new[] { 0.50f, 0.40f, -3f, 3f, 35f },    // 2 cerca y bajo
            new[] { 0.80f, 0.55f, 0f, 0f, 90f },     // 3 de costado
            new[] { 1.60f, 3.20f, 6f, 4f, 20f },     // 4 alta y abierta
            new[] { 0.75f, 0.55f, 0f, 1f, 170f },    // 5 de frente
        };
        static readonly float[][] CarBeats =
        {
            new[] { 1.00f, 1.00f, 0f, 2f, 0f },      // 0 de atras
            new[] { 0.80f, 0.60f, 0f, 3f, 75f },     // 1 de costado
            new[] { 1.45f, 3.20f, 5f, 3f, 10f },     // 2 alta
            new[] { 0.90f, 0.45f, -2f, 1f, 165f },   // 3 de frente, baja
            new[] { 0.65f, 0.45f, 0f, 2f, 25f },     // 4 pegada atras
        };

        /// <summary>Cambia de toma. 'snap' = sin transicion (al empezar o en un corte).</summary>
        void SetBeat(int beat, bool inCar, double now, bool snap)
        {
            float[][] set = inCar ? CarBeats : FootBeats;
            beat = Math.Max(0, Math.Min(set.Length - 1, beat));
            float[] b = set[beat];
            float sign = G.Rng.Next(2) == 0 ? 1f : -1f;
            fBeat = beat;
            fBeatUntil = now + G.Rand(5f, 9f);
            tDistMul = b[0]; tHMul = b[1]; tFovAdd = b[2]; tOrbit = b[3] * sign; tSide = b[4] * sign;
            if (snap) { fDistMul = tDistMul; fHMul = tHMul; fFovAdd = tFovAdd; fOrbit = tOrbit; fSide = tSide; }
        }

        /// <summary>Elige la proxima toma (si esta a los tiros: mas tomas cerca / de costado).</summary>
        int PickBeat(bool inCar, bool action)
        {
            int count = inCar ? CarBeats.Length : FootBeats.Length;
            int[] close = inCar ? new[] { 1, 3, 4 } : new[] { 2, 3, 5 };
            for (int tries = 0; tries < 6; tries++)
            {
                int b = action && G.Rng.NextDouble() < 0.7 ? close[G.Rng.Next(close.Length)] : 1 + G.Rng.Next(count - 1);
                if (b != fBeat) return b;
            }
            return 0;
        }

        /// <summary>
        /// Interiores: usar la sala real del NPC, una toma cercana y colision nativa.
        /// Mantener al espectador oculto en la misma sala para que se dibuje el tunel.
        /// </summary>
        bool UnderFollow(CameraShot s, double now, float dt, Vector3 target, Vector3 look, bool inCar, float flatSpeed)
        {
            if (now - fUnderCheck > 0.4)
            {
                fUnderCheck = now;
                uint key = 0;
                try { GET_KEY_FOR_CHAR_IN_ROOM(s.FollowPed, out key); } catch { key = 0; }
                // Room membership is native information; a roof height is not a visibility test.
                bool under = key != 0;
                if (under != fUnder || key != fRoomKey)
                {
                    if (under != fUnder)
                        log("[Camara] " + (under ? "NPC bajo tierra / tapado" + (key != 0 ? " (sala " + key + ")" : "") + ": camara pegada"
                                                 : "NPC otra vez al aire libre: camara normal"));
                    bool wasUnder = fUnder;
                    fUnder = under;
                    fRoomKey = key;
                    if (under) { fCam = look + Flat(fAz) * (inCar ? 6.5f : 4f) + new Vector3(0, 0, inCar ? 1.6f : 1.0f); fLastAnchor = -100; }
                    else if (wasUnder)
                    {
                        // vuelve a la camara normal desde un lugar que se vea
                        float az, h;
                        FindFollowSpot(look, FollowDistNow(inCar), fAz, followHeight * fHMul, out az, out h);
                        fAz = az; fH = h;
                        fCam = FollowPose(az, h, FollowDistNow(inCar), look);
                        fBlockedSince = -1;
                        MovePlayer(target + new Vector3(0, 0, anchorHeight));
                    }
                }
            }
            if (!fUnder) return false;
            if (flatSpeed > 2f)
            {
                float moveAz = (float)(Math.Atan2(-fVel.X, fVel.Y) * 180.0 / Math.PI);
                float diff = MathX.AngleDiff(fAz, moveAz + 180f);
                float rate = (inCar ? 90f : 50f) * dt;
                fAz += Math.Max(-rate, Math.Min(rate, diff));
            }
            float dist = inCar ? 6.5f : 4f;
            Vector3 desired = look + Flat(fAz) * dist + new Vector3(0, 0, inCar ? 1.6f : 1.0f);
            fCam = Vector3.Lerp(fCam, desired, 1f - (float)Math.Exp(-dt * (inCar ? 5.0 : 3.0)));
            fLook = Vector3.Lerp(fLook, look, 1f - (float)Math.Exp(-dt * 6.0));
            if (now - fLastAnchor > 1.0)
            {
                // jugador oculto en la misma sala del NPC (asi el juego dibuja el tunel)
                fLastAnchor = now;
                try
                {
                    MovePlayer(target + new Vector3(0, 0, 0.4f));
                    if (fRoomKey != 0) SET_ROOM_FOR_CHAR_BY_KEY(G.PlayerPed, fRoomKey);
                }
                catch { }
            }
            s.Pos = fCam;
            s.Rot = MathX.LookRotation(fCam, fLook);
            s.Fov = Math.Max(8f, Math.Min(80f, followFov + 8f));
            return true;
        }

        void UpdateFollow(CameraShot s, double now, float dt)
        {
            Vector3 target;
            bool inCar, dead;
            if (Npcs == null || !Npcs.FollowInfo(s.FollowPed, out target, out inCar, out dead)) { target = fPrevTarget; inCar = false; }
            if (dt > 0.0005f)
            {
                Vector3 v = (target - fPrevTarget) / dt;
                if (v.Length() > 90f) v = Vector3.Zero; // salto (teletransporte)
                fVel = Vector3.Lerp(fVel, v, 1f - (float)Math.Exp(-dt * 2.0));
            }
            fPrevTarget = target;
            CurrentTarget = target;
            Vector3 look = target + new Vector3(0, 0, 0.5f);
            float flatSpeed = (float)Math.Sqrt(fVel.X * fVel.X + fVel.Y * fVel.Y);
            if (underCam && UnderFollow(s, now, dt, target, look, inCar, flatSpeed)) return;

            // a los tiros: la camara mira tambien a quien le esta tirando (atras de el, de tres cuartos)
            Vector3 aimT = Vector3.Zero;
            bool aiming = aimCam && Npcs != null && Npcs.AimInfo(s.FollowPed, out aimT);
            if (aiming)
            {
                if (fDuelW < 0.05f) { fDuelTarget = aimT; fDuelSide = G.Rng.Next(2) == 0 ? 28f : -28f; }
                fDuelTarget = Vector3.Lerp(fDuelTarget, aimT, 1f - (float)Math.Exp(-dt * 4.0));
            }
            fDuelW += ((aiming ? 1f : 0f) - fDuelW) * (1f - (float)Math.Exp(-dt * (aiming ? 2.5 : 0.8)));
            if (fDuelW < 0.01f) fDuelW = 0f;

            // camara dinamica: cada 5-9 s otra toma (se acerca, se va de costado, sube, se pone de frente...)
            bool cut = false;
            if (fDuelW > 0.5f && now >= fBeatUntil) fBeatUntil = now + 2.0; // apuntando: no cambia de toma
            if (followDynamic && now >= fBeatUntil)
            {
                bool action = Npcs != null && Npcs.InAction(s.FollowPed);
                float oldSide = fSide;
                SetBeat(PickBeat(inCar, action), inCar, now, false);
                // si cambia mucho el angulo, corte (pasar por encima del NPC queda feo)
                float jump = Math.Abs(((tSide - oldSide) % 360f + 540f) % 360f - 180f);
                if (jump > 100f && G.Rng.NextDouble() < 0.8) cut = true;
                else if (G.Rng.NextDouble() < 0.25) cut = true;
            }
            float kb = cut ? 1f : 1f - (float)Math.Exp(-dt * 0.7);
            fDistMul += (tDistMul - fDistMul) * kb;
            fHMul += (tHMul - fHMul) * kb;
            fFovAdd += (tFovAdd - fFovAdd) * kb;
            fOrbit += (tOrbit - fOrbit) * kb;
            float sd = ((tSide - fSide) % 360f + 540f) % 360f - 180f;
            float dSide = cut ? sd : Math.Max(-45f * dt, Math.Min(45f * dt, sd * kb));
            fSide += dSide;
            float baseH = followHeight * fHMul;
            float dist = FollowDistNow(inCar);
            float duelAz = fAz;
            if (fDuelW > 0f)
            {
                Vector3 v = fDuelTarget - target;
                v.Z = 0f;
                float sep = v.Length();
                if (sep > 1f && sep < 90f)
                {
                    duelAz = MathX.AzOf(-v) + fDuelSide;
                    Vector3 duelLook = Vector3.Lerp(target, fDuelTarget, 0.35f) + new Vector3(0, 0, 0.8f);
                    float duelDist = Math.Max(6f, Math.Min(40f, Math.Max(dist, followDist * 0.8f + sep * 0.35f)));
                    look = Vector3.Lerp(look, duelLook, fDuelW);
                    dist += (duelDist - dist) * fDuelW;
                    baseH = Math.Max(baseH, followHeight * 0.8f);
                }
            }

            if (fDuelW > 0.4f)
            {
                // apuntando: atras del NPC, mirando hacia el otro
                float rate = 90f * fDuelW * dt;
                float dd = MathX.AngleDiff(fAz, duelAz);
                fAz += Math.Max(-rate, Math.Min(rate, dd));
            }
            else if (flatSpeed > 4f)
            {
                // en movimiento: la camara se va quedando detras (o al costado / de frente, segun la toma)
                float moveAz = (float)(Math.Atan2(-fVel.X, fVel.Y) * 180.0 / Math.PI);
                float behind = moveAz + 180f + fSide;
                float diff = ((behind - fAz) % 360f + 540f) % 360f - 180f;
                float rate = (inCar ? 40f : 20f) * dt;
                if (cut) fAz = behind;
                else fAz += Math.Max(-rate, Math.Min(rate, diff));
            }
            else
            {
                // quieto: la toma nueva lo rodea (o corta) y despues gira alrededor, mas o menos rapido
                fAz += dSide + fOrbit * dt;
            }
            if (cut)
            {
                // corte a la toma nueva: desde un lugar que se vea
                float az, h;
                FindFollowSpot(look, dist, fAz, baseH, out az, out h);
                fAz = az;
                fH = h;
                fCam = FollowPose(az, h, dist, look);
                fLook = look;
                fBlockedSince = -1;
                fLastRelax = now;
            }
            else if (fH < baseH - 0.3f || (fH > baseH + 0.3f && fH < baseH * 1.2f + 1f)) fH += (baseH - fH) * (1f - (float)Math.Exp(-dt * 0.8));

            // Si la pose pierde una composicion util, bajar a una toma moderada.
            // La deteccion de paredes corresponde a la colision nativa, nunca a alturas de techos.
            if (now - fLastLos > 0.2)
            {
                fLastLos = now;
                bool inside = !FollowCameraGeometry.Usable(fCam, look);
                if (!inside && FollowClear(look, fCam)) fBlockedSince = -1;
                else if (fBlockedSince < 0) fBlockedSince = inside ? now - 1.0 : now;
                if (fBlockedSince > 0 && now - fBlockedSince > 0.35)
                {
                    float az, h;
                    FindFollowSpot(look, dist, fAz, baseH, out az, out h);
                    fAz = az;
                    fH = h;
                    fCam = FollowPose(az, h, dist, look);
                    fLook = look;
                    fBlockedSince = -1;
                }
            }
            if (fH > baseH * 1.2f + 1f && now - fLastRelax > 3.0)
            {
                fLastRelax = now;
                if (FollowClear(look, FollowPose(fAz, baseH, dist, look))) fH = baseH;
            }

            Vector3 desired = FollowPose(fAz, fH, dist, look);
            if (!FollowCameraGeometry.Usable(desired, look))
            {
                // girando se iba a meter en un edificio: para el otro lado
                fOrbit = -fOrbit; tOrbit = -tOrbit;
                fBlockedSince = fBlockedSince > 0 ? fBlockedSince : now;
            }
            float kPos = inCar ? 2.5f : 1.2f;
            Vector3 nextCam = Vector3.Lerp(fCam, desired, 1f - (float)Math.Exp(-dt * kPos));
            if (!FollowCameraGeometry.Usable(nextCam, target + new Vector3(0, 0, 0.5f)))
            {
                if (fUnsafeSince < 0) fUnsafeSince = now;
                nextCam = fHaveGoodCam ? fLastGoodCam : target + Flat(fAz) * 4 + new Vector3(0, 0, 1.5f);
                if (interruption.Active && now - fUnsafeSince > 2)
                {
                    cameraSkipUntil[s.FollowPed] = now + 60;
                    returnToCity = true;
                }
            }
            else
            {
                fUnsafeSince = -1;
                fHaveGoodCam = true;
                fLastGoodCam = nextCam;
            }
            fCam = nextCam;
            fLook = Vector3.Lerp(fLook, look, 1f - (float)Math.Exp(-dt * 6.0));

            // Niko (invisible) va con el NPC para que la ciudad cargue alrededor
            Vector3 a = anchor;
            if (now - fLastAnchor > 1.5 && Math.Sqrt((a.X - target.X) * (a.X - target.X) + (a.Y - target.Y) * (a.Y - target.Y)) > 20.0)
            {
                fLastAnchor = now;
                MovePlayer(target + new Vector3(0, 0, anchorHeight));
            }
            s.Pos = fCam;
            s.Rot = MathX.LookRotation(fCam, fLook);
            s.Fov = Math.Max(8f, Math.Min(80f, followFov + fFovAdd));
        }

        // ------------------------------------------------------------------
        // Camara del tiroteo: desde donde se vean todos los que pelean (sin edificios en el medio)
        // ------------------------------------------------------------------
        /// <summary>Empezo un tiroteo: ir a la camara que encuadra a todos. False si no corresponde ahora.</summary>
        public bool BattleNow(Vector3 center)
        {
            // The stream scheduler weights combat without allowing every fight to cut the city loop.
            if (streamInterruptions) return false;
            if (!Active || EditorActive || followLock || !battleCam || phase != Phase.Showing) return false;
            if (Current != null && Current.IsBattle) return false;
            if (Npcs == null || G.Now < interruption.AutomaticReadyAt) return false;
            int ped = Npcs.PickForCamera(0);
            return ped != 0 && ObserveNpc(ped, streamDuration, "combate", 20);
        }

        public bool ShowingBattle { get { return Active && Current != null && Current.IsBattle; } }

        static bool InsideSolid(Vector3 p)
        {
            float tz;
            return G.TopZ(p.X, p.Y, out tz) && tz > p.Z + 0.4f;
        }

        static float Spread(List<Vector3> pts, Vector3 c)
        {
            float r = 4f;
            foreach (var p in pts) r = Math.Max(r, (float)Math.Sqrt((p.X - c.X) * (p.X - c.X) + (p.Y - c.Y) * (p.Y - c.Y)));
            return r;
        }

        /// <summary>Cuantos de los que pelean se ven desde ahi (hasta 6, para no tardar).</summary>
        int VisibleCount(Vector3 cam, List<Vector3> pts)
        {
            int n = 0, k = 0;
            foreach (var p in pts)
            {
                if (k++ >= 4) break;
                if (ClearCoarse(p + new Vector3(0, 0, 1f), cam)) n++;
            }
            return n;
        }

        /// <summary>Como FollowClear pero mirando cada 5 m (para no trabar el juego probando muchos lugares).</summary>
        static bool ClearCoarse(Vector3 look, Vector3 cam)
        {
            Vector3 d = cam - look;
            float len = d.Length();
            if (len < 3f) return true;
            Vector3 from = look + d / len * 2.5f;
            int steps = Math.Max(2, Math.Min(40, (int)(len / 5f)));
            for (int i = 1; i <= steps; i++)
            {
                Vector3 p = Vector3.Lerp(from, cam, i / (float)steps);
                float tz;
                if (G.TopZ(p.X, p.Y, out tz) && tz > p.Z + 0.3f) return false;
            }
            return true;
        }

        /// <summary>El mejor angulo y altura para ver a todos. Devuelve cuantos se ven.</summary>
        int BestBattleView(Vector3 look, float dist, float baseH, float preferAz, out float az, out float h)
        {
            az = preferAz;
            h = baseH;
            int best = -1;
            float[] hs = { baseH, baseH * 1.6f + 6f };
            foreach (float hh in hs)
                for (int i = 0; i < 8; i++)
                {
                    float a = preferAz + (i % 2 == 0 ? 1 : -1) * ((i + 1) / 2) * 45f;
                    Vector3 cam = FollowPose(a, hh, dist, look);
                    if (InsideSolid(cam)) continue;
                    int v = VisibleCount(cam, bPts);
                    if (v > best) { best = v; az = a; h = hh; }
                    if (best >= Math.Min(4, bPts.Count)) return best;
                }
            return Math.Max(0, best);
        }

        void InitBattle(CameraShot s)
        {
            Vector3 c;
            if (Npcs == null || !Npcs.BattleInfo(bPts, out c)) { c = s.Target; bPts.Clear(); bPts.Add(c); }
            bFov = Math.Max(35f, Math.Min(55f, followFov));
            bLook = c + new Vector3(0, 0, 1f);
            bDist = Math.Max(12f, Math.Min(140f, MathX.FitDistance(Spread(bPts, c), bFov, 16f / 9f)));
            float az, h;
            BestBattleView(bLook, bDist, Math.Max(6f, Math.Min(45f, bDist * 0.4f)), G.Rand(0f, 360f), out az, out h);
            bAz = az;
            bH = h;
            bCam = FollowPose(bAz, bH, bDist, bLook);
            bOrbit = G.Rng.Next(2) == 0 ? 3f : -3f;
            bLastEval = bLastAnchor = G.Now;
            bGoneAt = -1;
            CurrentTarget = c;
            s.Pos = bCam;
            s.Rot = MathX.LookRotation(bCam, bLook);
            s.Fov = bFov;
        }

        void UpdateBattle(CameraShot s, double now, float dt)
        {
            Vector3 c = Vector3.Zero;
            bool on = Npcs != null && Npcs.BattleInfo(bPts, out c);
            if (!on) { c = bLook - new Vector3(0, 0, 1f); if (bPts.Count == 0) bPts.Add(c); }
            CurrentTarget = c;
            bLook = Vector3.Lerp(bLook, c + new Vector3(0, 0, 1f), 1f - (float)Math.Exp(-dt * 1.5));
            float want = Math.Max(12f, Math.Min(140f, MathX.FitDistance(on ? Spread(bPts, c) : 8f, bFov, 16f / 9f)));
            bDist += (want - bDist) * (1f - (float)Math.Exp(-dt * 0.8));
            float wantH = Math.Max(6f, Math.Min(45f, bDist * 0.4f));
            bAz += bOrbit * dt;
            if (now - bLastEval > 1.2)
            {
                bLastEval = now;
                Vector3 pose = FollowPose(bAz, bH, bDist, bLook);
                int tot = Math.Min(4, bPts.Count), vis = VisibleCount(pose, bPts);
                bool inside = InsideSolid(pose);
                if (inside || (tot > 0 && vis * 2 < tot))
                {
                    float az, h;
                    int bv = BestBattleView(bLook, bDist, wantH, bAz, out az, out h);
                    if (bv > vis || inside)
                    {
                        bAz = az; bH = h;
                        bCam = FollowPose(bAz, bH, bDist, bLook); // corte a un lugar desde donde se ven
                    }
                }
                else if (bH > wantH + 1f && VisibleCount(FollowPose(bAz, wantH, bDist, bLook), bPts) >= vis) bH = wantH;
                else if (bH < wantH) bH = wantH;
            }
            Vector3 desired = FollowPose(bAz, bH, bDist, bLook);
            if (InsideSolid(desired)) { bOrbit = -bOrbit; bAz += 2f * bOrbit * dt; desired = FollowPose(bAz, bH, bDist, bLook); }
            Vector3 next = Vector3.Lerp(bCam, desired, 1f - (float)Math.Exp(-dt * 1.5));
            if (!InsideSolid(next)) bCam = next;
            else if (!InsideSolid(desired)) bCam = desired;
            Vector3 a = anchor;
            if (now - bLastAnchor > 1.5 && Math.Sqrt((a.X - c.X) * (a.X - c.X) + (a.Y - c.Y) * (a.Y - c.Y)) > 25.0)
            {
                bLastAnchor = now;
                MovePlayer(c + new Vector3(0, 0, anchorHeight));
            }
            s.Pos = bCam;
            s.Rot = MathX.LookRotation(bCam, bLook);
            s.Fov = bFov;
        }

        /// <summary>True si lo que se ve es la escena (no un fundido): para dibujar los nombres.</summary>
        public bool SceneVisible()
        {
            if (EditorActive) return true;
            if (!Active) return !IS_SCREEN_FADED_OUT();
            return phase == Phase.Showing && G.Now - shotStart >= (switchFade ? fadeMs / 1000.0 * 0.6 : 0.0);
        }

        /// <summary>Desde donde se esta mirando ahora (camara del director, del editor o la del juego).</summary>
        public void ViewPose(out Vector3 pos, out Vector3 rot, out float fov)
        {
            if (Active && Current != null) { pos = CamPos; rot = CamRot; fov = CamFov; return; }
            if (EditorActive) { pos = edPos; rot = edRot; fov = edFov; return; }
            int gc;
            GET_GAME_CAM(out gc);
            GET_CAM_POS(gc, out pos);
            GET_CAM_ROT(gc, out rot);
            float f = 0f;
            try { GET_CAM_FOV(gc, out f); } catch { }
            fov = f > 1f && f < 170f ? f : 45f;
        }

        /// <summary>
        /// Arma un plano automatico: busca una calle cerca del centro, la carga y prueba posiciones
        /// de camara (sobre todo a lo largo de la calle) que vean el punto sin obstaculos.
        /// Siempre devuelve algo: si no encuentra un buen angulo usa una toma alta mirando hacia abajo.
        /// </summary>
        CameraShot BuildAutoShot()
        {
            if (!haveHub) { hub = G.CharPos(G.PlayerPed); haveHub = true; }
            // con lio en la calle (bandas, tiros), la camara automatica se arma cerca de ahi
            Vector3 center = hub;
            float radius = autoRadius;
            Vector3 hot;
            if (Npcs != null && Npcs.Hotspot(out hot) && G.Rng.NextDouble() < 0.75) { center = hot; radius = Math.Min(autoRadius, 70f); }
            Vector3 node = center;
            float heading = G.Rand(0f, 360f);
            bool gotNode = false;

            for (int attempt = 0; attempt < 3; attempt++)
            {
                double a = G.Rng.NextDouble() * Math.PI * 2;
                float r = G.Rand(0f, radius);
                var p = new Vector3(center.X + (float)Math.Cos(a) * r, center.Y + (float)Math.Sin(a) * r, center.Z);
                Vector3 n;
                float h;
                if (!GET_NTH_CLOSEST_CAR_NODE_WITH_HEADING(p, (uint)G.Rng.Next(1, 4), out n, out h) || n == Vector3.Zero) continue;
                node = n;
                heading = h;
                gotNode = true;

                MovePlayer(node + new Vector3(0, 0, anchorHeight));
                REQUEST_COLLISION_AT_POSN(node);
                LOAD_SCENE(node);
                float gz;
                if (G.GroundZ(node, out gz) && Math.Abs(gz - node.Z) < 6f) node.Z = gz;
                float tz;
                if (G.TopZ(node.X, node.Y, out tz) && tz - node.Z > 3f) continue; // calle tapada (vias elevadas, puente, autopista)
                Vector3 look = node + new Vector3(0, 0, 1.2f);

                for (int k = 0; k < 16; k++)
                {
                    Vector3 dirFlat;
                    if (k < 11)
                    {
                        // a lo largo de la calle (en una ciudad en cuadricula es lo que mejor funciona)
                        float hd = heading + G.Rand(-25f, 25f) + (G.Rng.Next(2) == 0 ? 0f : 180f);
                        dirFlat = MathX.Forward(new Vector3(0, 0, hd));
                    }
                    else
                    {
                        double az = G.Rng.NextDouble() * Math.PI * 2;
                        dirFlat = new Vector3((float)Math.Cos(az), (float)Math.Sin(az), 0f);
                    }
                    float d = G.Rand(autoDistMin, autoDistMax);
                    float hgt = G.Rand(autoHMin, autoHMax);
                    Vector3 camPos = node + dirFlat * d + new Vector3(0, 0, hgt);
                    Vector3 dir = Vector3.Normalize(camPos - look);
                    Vector3 hit;
                    if (G.Raycast(look + dir * 4f, camPos, out hit)) continue;          // algo tapa la vista
                    float cgz;
                    if (G.GroundZ(camPos, out cgz) && cgz > camPos.Z - 1.5f) continue;  // camara metida en el suelo
                    return MakeAutoShot(camPos, look, node, true);
                }
            }

            // Plano de seguridad: alto y mirando hacia abajo, probando varias direcciones y alturas.
            if (!gotNode)
            {
                MovePlayer(node + new Vector3(0, 0, anchorHeight));
                LOAD_SCENE(node);
            }
            Vector3 lookAt = node + new Vector3(0, 0, 1.2f);
            float[] heights = { 45f, 90f, 150f };
            foreach (float hgt in heights)
            {
                for (int i = 0; i < 6; i++)
                {
                    Vector3 back = MathX.Forward(new Vector3(0, 0, heading + 180f + i * 60f));
                    Vector3 safePos = node + back * hgt + new Vector3(0, 0, hgt);
                    Vector3 hit;
                    if (!G.Raycast(lookAt + Vector3.Normalize(safePos - lookAt) * 4f, safePos, out hit))
                        return MakeAutoShot(safePos, lookAt, node, false);
                }
            }
            return MakeAutoShot(node + new Vector3(0, 0, 150f) + MathX.Forward(new Vector3(0, 0, heading)) * 60f, lookAt, node, false);
        }

        CameraShot MakeAutoShot(Vector3 camPos, Vector3 look, Vector3 node, bool allowDrift)
        {
            var shot = new CameraShot
            {
                Name = "Auto",
                IsAuto = true,
                Pos = camPos,
                Rot = MathX.LookRotation(camPos, look),
                Fov = G.Rand(autoFovMin, autoFovMax),
                Duration = defDuration,
                HasTarget = true,
                Target = node
            };
            if (allowDrift && autoDrift > 0)
            {
                Vector3 side = MathX.Right(shot.Rot) * (G.Rng.Next(2) == 0 ? -autoDrift : autoDrift);
                Vector3 endPos = camPos + side;
                Vector3 hit2;
                if (!G.Raycast(camPos, endPos, out hit2))
                {
                    shot.HasEnd = true;
                    shot.EndPos = endPos;
                    shot.EndRot = MathX.LookRotation(endPos, look);
                }
            }
            return shot;
        }

        // ------------------------------------------------------------------
        // Jugador escondido
        // ------------------------------------------------------------------
        void RememberReturn()
        {
            int ped = G.PlayerPed;
            returnPos = G.CharPos(ped);
            GET_CHAR_HEADING(ped, out returnHeading);
            returnVehicle = 0;
            returnWasHeli = false;
            if (IS_CHAR_IN_ANY_CAR(ped))
            {
                GET_CAR_CHAR_IS_USING(ped, out returnVehicle);
                uint model;
                GET_CAR_MODEL(returnVehicle, out model);
                returnWasHeli = IS_THIS_MODEL_A_HELI(model);
            }
            returnRoomKey = 0;
            try { GET_KEY_FOR_CHAR_IN_ROOM(ped, out returnRoomKey); } catch { }
        }

        /// <summary>
        /// Marca en el AppDomain que Niko esta escondido (y donde volver). Si ScriptHookDotNet recarga
        /// los scripts sin avisar, la nueva instancia lo encuentra y deja todo como estaba.
        /// </summary>
        void SetHiddenMarker(bool hidden)
        {
            try
            {
                AppDomain.CurrentDomain.SetData("KickChaos.Hidden", hidden
                    ? new float[] { returnPos.X, returnPos.Y, returnPos.Z, anchor.X, anchor.Y, anchor.Z, returnHeading }
                    : null);
            }
            catch { }
        }

        void MovePlayer(Vector3 p)
        {
            int ped = G.PlayerPed;
            if (IS_CHAR_IN_ANY_CAR(ped)) WARP_CHAR_FROM_CAR_TO_COORD(ped, p);
            FREEZE_CHAR_POSITION(ped, false);
            SET_CHAR_COORDINATES(ped, p);
            FREEZE_CHAR_POSITION(ped, true);
            CLEAR_ROOM_FOR_CHAR(ped); // estamos afuera: que el juego no crea que seguimos en un interior
            anchor = p;
            lastAnchorMove = G.Now;
            SetHiddenMarker(true);
        }

        void HidePlayer(Vector3 p)
        {
            int ped = G.PlayerPed;
            int pl = G.PlayerIndex;
            SET_PLAYER_CONTROL(pl, false);
            SET_CURRENT_CHAR_WEAPON(ped, 0, true);
            SET_CHAR_VISIBLE(ped, false);
            SET_CHAR_INVINCIBLE(ped, true);
            SET_PLAYER_INVINCIBLE(pl, true);
            SET_CHAR_PROOFS(ped, true, true, true, true, true);
            SET_CHAR_COLLISION(ped, false);
            SET_EVERYONE_IGNORE_PLAYER(pl, true);
            SET_POLICE_IGNORE_PLAYER(pl, true);
            MovePlayer(p);
        }

        /// <summary>Devuelve a Niko a donde estaba. 'loadScene' = esperar a que cargue el lugar (bloquea un momento).</summary>
        void RestorePlayer(bool loadScene)
        {
            int ped = G.PlayerPed;
            int pl = G.PlayerIndex;
            PoliceMode = false;
            CLEAR_WANTED_LEVEL(pl);
            SET_MAX_WANTED_LEVEL(6);
            FREEZE_CHAR_POSITION(ped, false);
            SET_CHAR_COLLISION(ped, true);

            Vector3 p = returnPos;
            REQUEST_COLLISION_AT_POSN(p);
            if (loadScene) LOAD_SCENE(p);

            bool inCar = false;
            if (returnVehicle != 0 && DOES_VEHICLE_EXIST(returnVehicle) && !IS_CAR_DEAD(returnVehicle) &&
                Vector3.Distance(G.CarPos(returnVehicle), p) < 50f)
            {
                WARP_CHAR_INTO_CAR(ped, returnVehicle);
                inCar = true;
            }
            if (!inCar)
            {
                // Si estaba volando en helicoptero (y el helicoptero ya no esta) lo dejamos en el suelo.
                float gz, wz;
                if (loadScene && returnWasHeli && returnRoomKey == 0 && G.GroundZ(p, out gz) && p.Z - gz > 3f)
                {
                    bool water = GET_WATER_HEIGHT(new Vector3(p.X, p.Y, p.Z), out wz) && wz > gz;
                    if (!water) p.Z = gz + 1f;
                }
                SET_CHAR_COORDINATES(ped, p);
                SET_CHAR_HEADING(ped, returnHeading);
                if (returnRoomKey != 0) { try { SET_ROOM_FOR_CHAR_BY_KEY(ped, returnRoomKey); } catch { } }
            }
            SET_CHAR_VISIBLE(ped, true);
            SET_CHAR_INVINCIBLE(ped, false);
            SET_PLAYER_INVINCIBLE(pl, false);
            SET_CHAR_PROOFS(ped, false, false, false, false, false);
            SET_EVERYONE_IGNORE_PLAYER(pl, false);
            SET_POLICE_IGNORE_PLAYER(pl, false);
            SET_PLAYER_CONTROL(pl, true);
            DISPLAY_HUD(true);
            DISPLAY_RADAR(true);
            SetHiddenMarker(false);
        }

        /// <summary>
        /// Al arrancar una instancia nueva del script: si una instancia anterior dejo a Niko escondido
        /// (recarga de scripts) o cambio cosas globales, lo deshace.
        /// </summary>
        public void RecoverFromPreviousInstance()
        {
            SET_TIME_SCALE(1f);
            SET_TEXT_INPUT_ACTIVE(false);
            DISABLE_PAUSE_MENU(false);
            SET_PED_DENSITY_MULTIPLIER(1f);
            SET_CAR_DENSITY_MULTIPLIER(1f);
            float[] m = null;
            try { m = AppDomain.CurrentDomain.GetData("KickChaos.Hidden") as float[]; } catch { }
            if (m == null || m.Length < 7) return;
            log("Recuperando el estado que dejo una instancia anterior del mod");
            ACTIVATE_SCRIPTED_CAMS(false, false);
            int ped = G.PlayerPed;
            var oldAnchor = new Vector3(m[3], m[4], m[5]);
            returnPos = new Vector3(m[0], m[1], m[2]);
            returnHeading = m[6];
            returnVehicle = 0;
            returnRoomKey = 0;
            returnWasHeli = false;
            if (Vector3.Distance(G.CharPos(ped), oldAnchor) < 10f)
                RestorePlayer(true); // sigue flotando donde lo dejamos: volver a donde estaba
            else
            {
                // el juego ya lo movio (partida cargada): solo devolver visibilidad y control
                int pl = G.PlayerIndex;
                FREEZE_CHAR_POSITION(ped, false);
                SET_CHAR_COLLISION(ped, true);
                SET_CHAR_VISIBLE(ped, true);
                SET_CHAR_INVINCIBLE(ped, false);
                SET_PLAYER_INVINCIBLE(pl, false);
                SET_CHAR_PROOFS(ped, false, false, false, false, false);
                SET_EVERYONE_IGNORE_PLAYER(pl, false);
                SET_POLICE_IGNORE_PLAYER(pl, false);
                SET_PLAYER_CONTROL(pl, true);
                SET_MAX_WANTED_LEVEL(6);
                DISPLAY_HUD(true);
                DISPLAY_RADAR(true);
                SetHiddenMarker(false);
            }
            DO_SCREEN_FADE_IN(0);
        }

        void ForceCamActive()
        {
            if (cam == 0 || !DOES_CAM_EXIST(cam)) { CreateCam(); return; }
            SET_CAM_ACTIVE(cam, true);
            SET_CAM_PROPAGATE(cam, true);
            ACTIVATE_SCRIPTED_CAMS(true, true);
            lastCamForce = G.Now;
        }

        void CreateCam()
        {
            if (cam != 0 && DOES_CAM_EXIST(cam)) return;
            CREATE_CAM(14, out cam);
            collisionPed = -1;
            SET_CAM_FOV(cam, defFov);
            SET_CAM_ACTIVE(cam, true);
            SET_CAM_PROPAGATE(cam, true);
            ACTIVATE_SCRIPTED_CAMS(true, true);
            lastCamForce = G.Now;
        }

        void DestroyCam()
        {
            if (cam != 0 && DOES_CAM_EXIST(cam))
            {
                SET_CAM_ACTIVE(cam, false);
                SET_CAM_PROPAGATE(cam, false);
                ACTIVATE_SCRIPTED_CAMS(false, false);
                DESTROY_CAM(cam);
            }
            else ACTIVATE_SCRIPTED_CAMS(false, false);
            cam = 0;
            collisionPed = 0;
        }

        // ------------------------------------------------------------------
        // Editor (camara libre para guardar planos)
        // ------------------------------------------------------------------
        public void ToggleEditor()
        {
            if (EditorActive) ExitEditor(); else EnterEditor();
        }

        void EnterEditor()
        {
            if (Active) Stop();
            RememberReturn();
            int gc;
            GET_GAME_CAM(out gc);
            GET_CAM_POS(gc, out edPos);
            GET_CAM_ROT(gc, out edRot);
            edRot.Y = 0f;
            edFov = defFov;
            edAim = edPos;
            edAimHit = false;
            CreateCam();
            HidePlayer(edPos);
            lastEditorAnchor = edPos;
            DISPLAY_RADAR(false);
            EditorActive = true;
            log("Editor de camaras abierto");
        }

        void ExitEditor()
        {
            EditorActive = false;
            DestroyCam();
            RestorePlayer(true);
            subtitle(" ", 1);
            log("Editor de camaras cerrado");
        }

        void UpdateEditor(double now, float dt)
        {
            if (!DOES_CAM_EXIST(cam)) CreateCam();
            HIDE_HUD_AND_RADAR_THIS_FRAME();
            if (G.IsGameFocused())
            {
                float speed = edSpeed;
                if (IsDown(Keys.ShiftKey)) speed *= 5f;
                if (IsDown(Keys.ControlKey)) speed *= 0.2f;

                Vector3 fwd = MathX.Forward(edRot);
                Vector3 right = MathX.Right(edRot);
                Vector3 move = Vector3.Zero;
                if (IsDown(Keys.W)) move += fwd;
                if (IsDown(Keys.S)) move -= fwd;
                if (IsDown(Keys.D)) move += right;
                if (IsDown(Keys.A)) move -= right;
                if (IsDown(Keys.E)) move += Vector3.UnitZ;
                if (IsDown(Keys.Q)) move -= Vector3.UnitZ;
                if (move != Vector3.Zero) edPos += Vector3.Normalize(move) * speed * dt;

                float rotSpeed = 60f * dt * (IsDown(Keys.ControlKey) ? 0.25f : 1f) * (edFov / 50f + 0.2f);
                if (IsDown(Keys.J)) edRot.Z += rotSpeed;
                if (IsDown(Keys.L)) edRot.Z -= rotSpeed;
                if (IsDown(Keys.I)) edRot.X += rotSpeed;
                if (IsDown(Keys.K)) edRot.X -= rotSpeed;

                try
                {
                    Point mouse;
                    GET_MOUSE_INPUT(out mouse);
                    float zoomFactor = edFov / 50f; // con FOV bajo el mouse va mas lento
                    edRot.Z -= mouse.X * mouseSens * zoomFactor;
                    edRot.X += (invertMouse ? mouse.Y : -mouse.Y) * mouseSens * zoomFactor;
                }
                catch { }

                if (IsDown(Keys.Z)) edFov -= 15f * dt;
                if (IsDown(Keys.X)) edFov += 15f * dt;
                edFov = Math.Max(3f, Math.Min(120f, edFov));
                edRot.X = Math.Max(-89f, Math.Min(89f, edRot.X));
                edRot.Z = ((edRot.Z % 360f) + 360f) % 360f;
            }

            SET_CAM_POS(cam, edPos);
            SET_CAM_ROT(cam, edRot);
            SET_CAM_FOV(cam, edFov);

            // El jugador (invisible) sigue a la camara para que cargue la ciudad alrededor.
            if (now - lastAnchorMove > 0.5 && Vector3.Distance(lastEditorAnchor, edPos) > 15f)
            {
                MovePlayer(edPos);
                lastEditorAnchor = edPos;
            }
            SET_PLAYER_CONTROL(G.PlayerIndex, false);

            // Donde ocurririan las acciones con este encuadre (marcador rojo)
            if (now - lastAim > 0.5)
            {
                lastAim = now;
                edAimHit = G.TryFindTarget(edPos, edRot, 1500f, false, out edAim);
            }
            if (edAimHit) DRAW_CHECKPOINT(edAim, 1.5f, Color.FromArgb(255, 255, 60, 30));

            if (now - lastInfo > 0.25)
            {
                lastInfo = now;
                int saved = 0;
                foreach (var s in Shots) if (!s.IsAuto) saved++;
                string aim = edAimHit ? "objetivo a " + Vector3.Distance(edPos, edAim).ToString("0") + " m" : "SIN OBJETIVO (muy lejos)";
                subtitle(string.Format("EDITOR | FOV {0:0} | {1} | camaras: {2} | ENTER guardar - SHIFT+ENTER punto final - T fijar objetivo - RETROCESO borrar - {3} salir",
                    edFov, aim, saved, cfg.KeyEditor), 400);
            }
        }

        bool IsDown(Keys k)
        {
            return G.KeyDown(k);
        }

        public void EditorSave(bool asEndPoint)
        {
            if (!EditorActive) return;
            if (asEndPoint)
            {
                CameraShot last = LastManualShot();
                if (last == null) { subtitle("Primero guarda una camara con ENTER", 2500); return; }
                if (Vector3.Distance(last.Pos, edPos) > 300f)
                {
                    subtitle("'" + last.Name + "' esta a mas de 300 m: el punto final tiene que estar cerca del inicial", 3500);
                    return;
                }
                last.HasEnd = true;
                last.EndPos = edPos;
                last.EndRot = edRot;
                last.EndFov = Math.Abs(edFov - last.Fov) > 0.5f ? edFov : -1f;
                Persist();
                subtitle("Punto final guardado para '" + last.Name + "' (se movera durante el plano)", 3000);
                return;
            }
            Vector3 target;
            bool hit = G.TryFindTarget(edPos, edRot, 1500f, true, out target);
            var shot = new CameraShot
            {
                Name = CameraStore.NextName(Shots),
                Pos = edPos,
                Rot = edRot,
                Fov = (float)Math.Round(edFov, 1),
                Duration = -1f,
                HasTarget = true,
                Target = target
            };
            Shots.Add(shot);
            Persist();
            if (hit) subtitle("Guardada '" + shot.Name + "' (FOV " + shot.Fov.ToString("0") + ")", 2500);
            else subtitle("Guardada '" + shot.Name + "' pero SIN OBJETIVO: acercate al lugar, apuntalo y apreta T", 5000);
            log("Camara guardada: " + shot.Name + " en " + IniFile.V3(edPos.X, edPos.Y, edPos.Z) + (hit ? "" : " (objetivo estimado)"));
        }

        /// <summary>Fija el objetivo de la ultima camara en lo que se esta mirando ahora.</summary>
        public void EditorSetTarget()
        {
            if (!EditorActive) return;
            CameraShot last = LastManualShot();
            if (last == null) { subtitle("Primero guarda una camara con ENTER", 2500); return; }
            Vector3 target;
            if (!G.TryFindTarget(edPos, edRot, 1500f, true, out target))
            {
                subtitle("No encuentro el suelo ahi: acercate mas al lugar", 2500);
                return;
            }
            last.Target = target;
            last.HasTarget = true;
            Persist();
            subtitle("Objetivo de '" + last.Name + "' fijado (a " + Vector3.Distance(last.Pos, target).ToString("0") + " m de la camara)", 3000);
        }

        public void EditorDeleteLast()
        {
            if (!EditorActive) return;
            CameraShot last = LastManualShot();
            if (last == null) { subtitle("No hay camaras para borrar", 2000); return; }
            Shots.Remove(last);
            Persist();
            subtitle("Borrada '" + last.Name + "'", 2500);
        }

        CameraShot LastManualShot()
        {
            for (int i = Shots.Count - 1; i >= 0; i--) if (!Shots[i].IsAuto) return Shots[i];
            return null;
        }

        public void Persist()
        {
            try { CameraStore.Save(cfg.Folder, Shots); }
            catch (Exception ex) { log("No se pudo guardar camaras.ini: " + ex.Message); subtitle("ERROR guardando camaras.ini", 3000); }
        }

        public string StatusText()
        {
            if (EditorActive) return "editor de camaras";
            if (!Active) return "apagado";
            string name = Current != null ? (Current.IsFollow ? "siguiendo a un NPC" : Current.IsAuto ? "automatica" : Current.Name) : "-";
            return "encendido, plano: " + name + ", fase " + phase;
        }
    }
}
