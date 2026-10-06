using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Numerics;
using static KickChaos.N;

namespace KickChaos
{
    /// <summary>Un cartel para dibujar (se arma en el Tick, se dibuja en PerFrameDrawing).</summary>
    public class NpcTag
    {
        public float X, Y, Scale = 1f, Health = 1f, Armor = -1f;
        public string Name = "", Sub = "", Timer = "", Cmds = "", Perks = "", Ammo = "";
        public int NameColor;       // 1.9: color del nombre en el chat de Kick (0 = blanco)
        public string[] Bubble;
        public int SubColor;        // 0 verde Kick, 1 rojo, 2 naranja, 3 amarillo
        public bool Dead, Bar, CmdsVote;
    }

    /// <summary>
    /// Accion "Npc": aparece una persona con el nombre del que se suscribio arriba de la cabeza.
    /// Dos modos: Paseo (anda por la ciudad y nadie lo ataca) o Batalla (se buscan entre bandas para
    /// matarse y subir de nivel). Cada vez que mata se suma otro a su banda (hasta 4), elige una mejora
    /// (roguelike) y el chat le puede dar ordenes ("!atacar", "!huir", "!bailar"...).
    /// Se cubren detras de los autos y las paredes, se persiguen en auto y se curan manejando.
    /// </summary>
    public partial class SubNpcManager
    {
        // lo que esta haciendo cada uno (para no repetir ordenes: repetirlas hace que frenen y arranquen)
        const int M_NONE = 0, M_WANDER = 1, M_COMBAT = 2, M_GOTO = 3, M_DRIVE_TO = 4, M_CRUISE = 5,
                  M_ENTER_CAR = 6, M_LEAVE_CAR = 7, M_CHASE = 8, M_DRIVEBY = 9, M_SHOOT = 10, M_GOTO2 = 11,
                  M_COVER = 12, M_DUCK = 13, M_EAT = 14, M_RIDE = 15, M_FLEE = 16, M_FOLLOW = 17, M_SEEK_COVER = 18,
                  M_STRAFE = 19, M_WAIT = 20, M_ARREST = 21, M_HANDS = 22, M_AIMMOVE = 23, M_DANCE = 24, M_SPECIAL = 25,
                  M_CHAT = 26, M_RACE = 27;
        static readonly string[] ModeNames = { "nada", "camina", "combate", "corre", "maneja hacia", "maneja", "sube al auto",
                                               "baja del auto", "persigue", "tira desde el auto", "dispara", "corre derecho",
                                               "se cubre", "agachado", "come", "acompanante", "huye", "sigue al jefe",
                                               "cobertura", "al costado", "espera", "arresta", "manos arriba", "se mueve apuntando",
                                               "baila", "tira un explosivo", "charla", "corre una carrera" };
        static string ModeName(int m) { return ModeNames[Math.Max(0, Math.Min(ModeNames.Length - 1, m))]; }

        // tiroteo a pie: correr hacia el, tirar, cubrirse detras de un auto, agacharse, tirar...
        const int P_NONE = 0, P_APPROACH = 1, P_SHOOT = 2, P_COVER = 3, P_DUCK = 4, P_MOVE = 5, P_SPECIAL = 6;

        /// <summary>Grupos de relacion "Mission_1".."Mission_8": uno por banda (los de la misma banda no se pelean).</summary>
        const int GROUP_BASE = 23, MAX_SLOTS = 8;
        /// <summary>Personajes de bandas a la vez (sumando todas).</summary>
        const int TOTAL_MAX = 96;

        struct Threat
        {
            public int Ped;
            public float Dist;
            public bool Cop;
            public Gang Rival;
            public Vector3 Pos;
        }

        class Cop
        {
            public int Ped, Car, Mode, Target, Fails, Block = -1, Bursts, Shots, Gun = 7;
            public bool Driver, Seen, Street, Disguised;
            public bool WasInCar;
            public double LastExit = -100, EntryRetryAt = -1, LastProgress = -100;
            public float LastDistance = float.MaxValue;
            public Vector3 MoveTo;
            public double Spawned, LastTask = -100, ModeAt, StillSince = -1, LastShot = -100, LastSeen = -100, DeadAt = -1, PhaseLen = 4;
            public Vector3 Pos;
            public bool Alive { get { return Ped != 0 && DeadAt < 0; } }
        }

        class Member
        {
            public Gang G;
            public int RowId, ParentRowId;
            public string OwnerName = "", Objective = "viajar por la ciudad";
            public RogueBuild Build = RogueBuild.Superviviente;
            public int VehicleSidearmAmmoLeft = -1;
            public string[] OwnerBubble;
            public double OwnerBubbleUntil = -1, RecentEventUntil = -1, FollowerBattleUntil = -1;
            public double LastBuildChange = -100, NextFollowerBattle = -1;
            public int Ped, Index, Mode, Target, StealCar, Phase, Weapon, Kills, Seat = -1, Fails, Car, Block = -1;
            public string Status = "", LastLogged = "", Flash = "";
            public double Created, DeadAt = -1, NextThink, LastTask = -100, ModeAt = -100, StillSince = -1, LastHealth = -100,
                          LastLos = -100, ProtectUntil = -1, StealSince = -1, LastShot = -100, LastDiag = -100, PhaseAt = -100,
                          PhaseLen = 4, LastHit = -100, FleeSince = -1, EatUntil = -1, CalmSince = -1, LastSeen = -100,
                          DriveSince = -1, FlashUntil = -1, LastCarCheck = -100;
            public Vector3 Pos, Head, MoveTo;
            public bool Dead, InCar, Driving, Armed, Occluded, FakeName, Fleeing, Eating, EatStarted, Seen, CoverIsCar, Arrested, Surrender, SurrenderRolled;
            public double HandsUpAt = -1;
            public bool CruiseFast;
            public double HopAt = -1;
            public int MonthsWeapon;
            public double NoSeeSince = -1, ShieldUntil = -1, SpecialUntil = -1, DanceAt = -100, HealAt = -100, FleeEnded = -100;
            public int SpecialWeapon;
            // 1.9: explosivo en curso (para ver si salio y, si no, simularlo)
            public double SpecialAt = -100;
            public int SpecialTarget, SpecialAmmo;
            public bool SpecialResolved = true, SpecialBoom;
            public Vector3 SpecialPos;
            // 1.9: municion limitada
            public int AmmoCapped = -1, Ammo = -1, AmmoMax;
            public bool PistolFallback;
            public int Primary;   // el arma que tenia antes de quedarse sin balas
            public uint RoomKey;  // 1.9: != 0 = adentro de un interior / tunel
            public double LastRoom = -100;
            public uint Armor;
            // 1.9: eventos (1 = charla, 2 = carrera)
            public int EventKind, EventPed;
            public double EventUntil = -1, EventAt = -100;
            public bool EventStarted;
            public double FunUntil = -1;   // 1.9: haciendo un comando divertido (saltar, desmayo...)
            public double LastAmmoCheck = -100;
            public string DanceSet = "", DanceAnim = "";
            public bool Safe;            // modo paseo: nadie lo puede lastimar
            public bool NoAimMove;       // "moverse apuntando" no le funciona: corre
            public bool MapClear;        // no hay edificios entre el y su objetivo
            public int MoveFails;
            public uint StartHealth = 200, LastHp;
            public float Health01 = 1f;
        }

        class Gang
        {
            public int Id, Slot, State, CarModel, CopsPending, PatrolsMade, CopWaits, Kills, Order = -1, NextIndex = 1, Stars, CopKills;
            public string User = "", Sub = "", Flash = "";
            public NpcBehavior Behavior;
            public bool Test, Wanted, Control, Fighter, FullLogged, Lethal, HostileSet, HostileNow;
            public bool IsFollower;
            public int MainRowId;
            public uint PrincipalHealth;
            public double EncounterUntil = -1, NextEncounterCheck = -1, LastEncounterCheck = -100;
            public int PendingStars, Months = 1, Level = 1, NpcKills, WaitShot = -1, TripsMade;
            public double NextTrip = -1, TripUntil = -1, WaitUntil = -1, LeftCarAt = -100, FarSince = -1, FollowAt = -1;
            public int SpawnTries, FollowTries;
            public int LeftCar;   // el auto que dejaron para robar otro (no volver a subirse a ese)
            // armas especiales (las usa cualquiera de la banda) y mejoras roguelike
            public int Grenades, Molotovs, Rockets, AccBonus, LevelGiven = 1, OffersQueued;
            public float SpeedMul = 1.08f;
            // 1.9: mejoras nuevas
            public float AmmoMul = 1f;
            public int FireBonus;
            public bool Vampire, Armored;
            public bool Truce;   // 1.9: en una carrera no se tirotean
            public bool Regen;
            public readonly List<int> Perks = new List<int>();
            public int[] Offer;
            public double OfferUntil = -1, LastSpecial = -100, LastModeSwitch = -100, LastOffer = -100;
            public readonly GangRules.VoteBox PerkVotes = new GangRules.VoteBox(3);
            // persecucion en auto con otra banda: 1 = persigue, 2 = escapa
            public Gang ChaseWith;
            public int ChaseRole;
            public double ChaseUntil = -1, ChaseSince = -1, ChaseStill = -1, NoChaseUntil = -1;
            public bool EverFighter;
            public Vector3 TripTo;
            public bool Tripping;
            public double WantedSince = -1, LastContact = -100, LastStarAt = -100;
            public double Created, LoadDeadline, EndAt, NextCops = -1, PoliceAt = -1, LastReinforce = -100, LastScan = -100,
                          LastDirty = -100, LastKillScan = -100, LastCivScan = -100, OrderUntil = -1, OrderAt = -100,
                          BubbleUntil = -1, FlashUntil = -1, LastWarp = -100, LastCmd = -100, AllDeadAt = -1, LastCopThink = -100,
                          LastStoleNotice = -100, LastEatNotice = -100, LastCleanup = -100, LastCopLog = -100;
            public Vector3 Center;
            public string[] Bubble;
            public readonly List<Member> Members = new List<Member>();
            public readonly List<Cop> Cops = new List<Cop>();
            public readonly HashSet<int> Tasked = new HashSet<int>();
            public readonly List<Vector3> UsedSpots = new List<Vector3>();
            public readonly HashSet<int> Counted = new HashSet<int>();
            public readonly Dictionary<int, double> Cars = new Dictionary<int, double>(); // autos robados -> ultima vez que se usaron
            public readonly GangRules.VoteBox Votes = new GangRules.VoteBox(3);
            public int Group { get { return GROUP_BASE + Slot; } }
            /// <summary>El primero vivo (el que lleva el cartel de la banda).</summary>
            public Member Leader { get { foreach (var m in Members) if (!m.Dead) return m; return null; } }
            public int Alive { get { int k = 0; foreach (var m in Members) if (!m.Dead) k++; return k; } }
        }

        Config cfg;
        readonly Director dir;
        readonly Action<string> log;
        readonly Action<string> notify;
        readonly List<Gang> gangs = new List<Gang>();
        volatile List<NpcTag> tags;
        volatile float tagAspect = 16f / 9f;
        HashSet<uint> copModels;
        int nextId = 1;
        bool loggedGameCover;
        // kill feed y ranking
        KillRanking ranking;
        readonly List<FeedLine> feed = new List<FeedLine>();
        volatile FeedLine[] feedSnap;
        volatile KillStat[] rankSnap;
        public bool ShowFeed = true, ShowRanking;
        double lastRankSave = -100;
        bool spreadSpawns = true, gangTrips = true, carHop = true;
        /// <summary>
        /// Tipo de personaje "policia" para CREATE_CHAR_INSIDE_CAR. No es el mismo numero que el grupo de
        /// relacion (3): se averigua con un policia del juego (o con un civil) y si no, 6.
        /// </summary>
        uint copType = 2; // en este juego: civiles 0-1, policia 2 (se confirma con un policia del juego)
        bool copTypeFromCop, civProbed;
        /// <summary>Policias que creamos nosotros (para no averiguar el tipo con uno de ellos).</summary>
        readonly HashSet<int> madeCops = new HashSet<int>();
        const int MAX_COPS = 24;

        // ajustes [Suscriptor]
        NpcBehavior modeSub = NpcBehavior.Batalla, modeGift = NpcBehavior.Batalla, modeFollow = NpcBehavior.Pasear;
        readonly int[] tierFrom = { 1, 2, 6, 12, 24 };
        readonly int[] tierWeapons = { 7, 12, 11, 14, 15 };
        readonly int[] levelWeapons = { 4, 11, 5, 14, 4, 15, 18, 5, 18 };
        bool policeOn = true, attackCivs, paseoSafe = true, paseoCar = true, modeByChat = true, weaponsByLevel = true, useSpecials = true,
             healInCar = true, buyFood, chaseCars = true, randomChase = true, perksOn = true, tactics = true, alwaysRadio = true;
        float healRate = 4f, chaseEvery = 150f, perkSeconds = 20f;
        int perkRule = GangRules.GrowAnyone;
        double nextRandomChase = -1;
        int maxNpcs = 32, maxCharacters = 48, patrols = 2, gangMax = 4, growRule, unlockRule, controlMode;
        float maxLife = 180f, walkLife = 70f, lifeMult = 1f, fleeSpeed = 30f, killBonus = 20f, fleeAt = 0.35f,
              voteSeconds = 15f, orderSeconds = 30f, cmdCooldown = 4f, eatSeconds = 7f;
        bool duel = true, diag = true, bringGangs = true, gameCover = true, nativeAi = true;
        // 1.9: policia que pelea de verdad
        bool copAggro = true, copDisguised = true, streetCops = true;
        bool doorOpen = true, engineOn = true, radioPositional;
        bool aimMove, fakeExplosives = true, useGrenades = true, useMolotovs = true, useRockets = true, limitedAmmo = true, showAmmo = true, armorBar = true;
        int mags = 6;
        float speedBase = 1.08f, speedStep = 0.05f, speedMax = 1.22f;
        readonly bool[] perkOn = new bool[PerkRules.Count];
        int copGunPref = 7;
        readonly int[] slots = { GangRules.CmdRobarAuto, GangRules.CmdAtacar, GangRules.CmdHuir };
        public bool ShowName = true, HealthBar = true, GameName, FollowOnSpawn = true, ShowChat = true, ShowTimer = true;
        public float NameScale = 1f;
        int spawnCam = 2;
        float spawnCamSeconds = 5f;

        static readonly string DefaultCars = "SULTAN,BANSHEE,INFERNUS,COMET,FEROCI,PMP600,BUFFALO,TURISMO";

        public SubNpcManager(Config cfg, Director dir, Action<string> log, Action<string> notify)
        {
            this.dir = dir;
            this.log = log;
            this.notify = notify;
            ApplyConfig(cfg);
        }

        static float Clamp(float v, float lo, float hi) { return Math.Max(lo, Math.Min(hi, v)); }

        public void ApplyConfig(Config c)
        {
            cfg = c;
            IniFile ini = c.Ini;
            const string S = "Suscriptor";
            // modos: Batalla o Paseo (los nombres viejos de 1.7: Nivel1 / Regalo / Otros)
            NpcBehavior oldSub = NpcRules.Resolve(ini.Get(S, "Nivel1", "Batalla"), NpcBehavior.Batalla);
            modeSub = NpcRules.Resolve(ini.Get(S, "ModoSub", ""), oldSub);
            modeGift = NpcRules.Resolve(ini.Get(S, "ModoRegalo", ""), NpcRules.Resolve(ini.Get(S, "Regalo", "Batalla"), NpcBehavior.Batalla));
            modeFollow = NpcRules.Resolve(ini.Get(S, "ModoFollow", ""), NpcRules.Resolve(ini.Get(S, "Otros", "Pasear"), NpcBehavior.Pasear));
            tierFrom[1] = Math.Max(2, ini.GetInt(S, "Nivel2Desde", 2));
            tierFrom[2] = Math.Max(tierFrom[1] + 1, ini.GetInt(S, "Nivel3Desde", 6));
            tierFrom[3] = Math.Max(tierFrom[2] + 1, ini.GetInt(S, "Nivel4Desde", 12));
            tierFrom[4] = Math.Max(tierFrom[3] + 1, ini.GetInt(S, "Nivel5Desde", 24));
            for (int i = 0; i < GangLevels.Tiers; i++)
            {
                int w = GangLevels.ParseWeapon(ini.Get(S, "Arma" + (i + 1), GangLevels.DefaultTierWeapons[i]));
                tierWeapons[i] = w >= 0 ? w : GangLevels.ParseWeapon(GangLevels.DefaultTierWeapons[i]);
            }
            for (int i = 0; i < levelWeapons.Length; i++)
            {
                int w = GangLevels.ParseWeapon(ini.Get(S, "ArmaNivel" + (i + 2), GangLevels.DefaultLevelWeapons[i]));
                levelWeapons[i] = w >= 0 ? w : GangLevels.ParseWeapon(GangLevels.DefaultLevelWeapons[i]);
            }
            policeOn = ini.GetBool(S, "Policia", true);
            attackCivs = ini.GetBool(S, "AtacanALaGente", false);
            paseoSafe = ini.GetBool(S, "PaseoIntocable", true);
            paseoCar = ini.GetBool(S, "PaseoEnAuto", true);
            modeByChat = ini.GetBool(S, "CambiarModoPorChat", true);
            weaponsByLevel = ini.GetBool(S, "ArmasPorNivel", true);
            useSpecials = ini.GetBool(S, "UsanExplosivos", true);
            healInCar = ini.GetBool(S, "CuraEnAuto", true);
            healRate = Clamp(ini.GetFloat(S, "CuraPorSegundo", 4f), 0.5f, 25f);
            buyFood = ini.GetBool(S, "ComprarComida", false);
            chaseCars = ini.GetBool(S, "PersecucionesEnAuto", true);
            randomChase = ini.GetBool(S, "PersecucionAleatoria", true);
            chaseEvery = Clamp(ini.GetFloat(S, "PersecucionCada", 150f), 30f, 1200f);
            perksOn = ini.GetBool(S, "Mejoras", true);
            perkRule = GangRules.ParseGrow(ini.Get(S, "MejoraCon", "Cualquiera"));
            perkSeconds = Clamp(ini.GetFloat(S, "TiempoMejora", 20f), 5f, 120f);
            tactics = ini.GetBool(S, "PeleaDinamica", true);
            alwaysRadio = ini.GetBool(S, "RadioSiempre", true);
            kickColors = ini.GetBool(S, "ColorDeKick", true);
            {
                string fm = Config.Normalize(ini.Get(S, "ComandosDivertidos", "Chat"));
                funMode = fm.StartsWith("no") || fm.StartsWith("apag") ? 0 : fm.StartsWith("sus") || fm.StartsWith("sub") ? 1 : 2;
                funCooldown = Clamp(ini.GetFloat(S, "EsperaDivertidos", 8f), 0f, 120f);
                for (int i = 0; i < GangRules.FunKeys.Length; i++) funOn[i] = ini.GetBool("Divertidos", GangRules.FunKeys[i], true);
            }
            eventChat = ini.GetBool(S, "EventoCharlar", true);
            chatEvery = Clamp(ini.GetFloat(S, "CharlaCada", 90f), 20f, 1800f);
            chatSeconds = Clamp(ini.GetFloat(S, "DuracionCharla", 12f), 4f, 60f);
            eventRace = ini.GetBool(S, "EventoCarrera", true);
            raceEvery = Clamp(ini.GetFloat(S, "CarreraCada", 120f), 60f, 3600f);
            raceMax = Math.Max(2, Math.Min(4, ini.GetInt(S, "CorredoresCarrera", 3)));
            raceSeconds = Clamp(ini.GetFloat(S, "DuracionCarrera", 150f), 40f, 600f);
            raceSpeed = Clamp(ini.GetFloat(S, "VelocidadCarrera", 32f), 20f, 80f);
            ReadAmbientConfig(ini);
            copAggro = ini.GetBool(S, "PoliciaAgresiva", true);
            doorOpen = ini.GetBool(S, "PuertaAbierta", true);
            aimMove = ini.GetBool(S, "MoverseApuntando", false);
            fakeExplosives = ini.GetBool(S, "ExplosivoSimulado", true);
            useGrenades = ini.GetBool(S, "UsanGranadas", true);
            useMolotovs = ini.GetBool(S, "UsanMolotov", true);
            useRockets = ini.GetBool(S, "UsanRpg", true);
            limitedAmmo = ini.GetBool(S, "MunicionLimitada", true);
            mags = Math.Max(1, Math.Min(50, ini.GetInt(S, "Cargadores", 6)));
            showAmmo = ini.GetBool(S, "MostrarMunicion", true);
            armorBar = ini.GetBool(S, "BarraDeChaleco", true);
            speedBase = Clamp(ini.GetFloat(S, "VelocidadBase", 1.08f), 0.8f, 1.5f);
            speedStep = Clamp(ini.GetFloat(S, "VelocidadMejora", 0.05f), 0f, 0.3f);
            speedMax = Clamp(ini.GetFloat(S, "VelocidadMaxima", 1.22f), speedBase, 1.6f);
            for (int i = 0; i < PerkRules.Count; i++) perkOn[i] = ini.GetBool("Mejoras", PerkRules.Keys[i], true);
            engineOn = ini.GetBool(S, "MotorPrendido", true);
            radioPositional = !Config.Normalize(ini.Get(S, "RadioModo", "Auto")).StartsWith("cel");
            copDisguised = ini.GetBool(S, "PoliciaDisfrazada", true);
            streetCops = ini.GetBool(S, "PoliciasDeLaCalle", true);
            { int w = GangLevels.ParseWeapon(ini.Get(S, "ArmaPolicia", "Pistola")); copGunPref = w > 0 && !GangLevels.IsSpecial(w) ? w : 7; }
            maxNpcs = Math.Max(1, Math.Min(64, ini.GetInt(S, "Maximo", 32)));
            maxCharacters = Math.Max(1, Math.Min(TOTAL_MAX, ini.GetInt(S, "MaximoPersonajes", 48)));
            maxLife = Math.Max(20f, ini.GetFloat(S, "TiempoMaximo", 180f));
            walkLife = Math.Max(10f, ini.GetFloat(S, "DuracionPasear", 70f));
            patrols = Math.Max(0, Math.Min(6, ini.GetInt(S, "Patrulleros", 2)));
            lifeMult = Clamp(ini.GetFloat(S, "Vida", 1f), 0.3f, 5f);
            fleeSpeed = Clamp(ini.GetFloat(S, "VelocidadAuto", 30f), 10f, 60f);
            duel = ini.GetBool(S, "PelearEntreEllos", true);
            diag = ini.GetBool(S, "Diagnostico", true);
            ShowName = ini.GetBool(S, "MostrarNombre", true);
            HealthBar = ini.GetBool(S, "BarraDeVida", true);
            NameScale = Clamp(ini.GetFloat(S, "TamanoNombre", 1f), 0.5f, 2.5f);
            GameName = ini.GetBool(S, "NombreDelJuego", false);
            FollowOnSpawn = ini.GetBool(S, "IrAlAparecer", true);
            {
                // 1.9: que hace la camara cuando aparece uno: Seguir (directo) / Camara (aparece delante de la
                // camara y no lo sigue) / CamaraYSeguir (aparece delante y a los segundos lo sigue)
                string am = Config.Normalize(ini.Get(S, "AlAparecer", FollowOnSpawn ? "CamaraYSeguir" : "Camara")).Replace(" ", "");
                spawnCam = am.StartsWith("seguir") ? 0 : am.Contains("seguir") ? 2 : 1;
                FollowOnSpawn = spawnCam != 1;
                spawnCamSeconds = Clamp(ini.GetFloat(S, "SegundosEnCamara", 5f), 0f, 30f);
            }
            // bandas y control por chat (1.5)
            gangMax = Math.Max(1, Math.Min(6, ini.GetInt(S, "BandaMaximo", 4)));
            growRule = GangRules.ParseGrow(ini.Get(S, "SeDuplicaCon", "Policias"));
            killBonus = Clamp(ini.GetFloat(S, "SegundosPorMuerte", 20f), 0f, 300f);
            fleeAt = Clamp(ini.GetFloat(S, "VidaParaHuir", 35f), 0f, 90f) / 100f;
            eatSeconds = Clamp(ini.GetFloat(S, "SegundosComiendo", 7f), 2f, 60f);
            bringGangs = ini.GetBool(S, "TraerBandas", true);
            gameCover = ini.GetBool(S, "CoberturaDelJuego", true);
            nativeAi = !Config.Normalize(ini.Get(S, "IA", "Juego")).StartsWith("mod");
            unlockRule = GangRules.ParseUnlock(ini.Get(S, "ControlDelChat", "AlDuplicarse"));
            controlMode = GangRules.ParseMode(ini.Get(S, "QuienDecide", "Suscriptor"));
            voteSeconds = Clamp(ini.GetFloat(S, "TiempoVotacion", 15f), 5f, 120f);
            orderSeconds = Clamp(ini.GetFloat(S, "DuracionOrden", 30f), 10f, 180f);
            cmdCooldown = Clamp(ini.GetFloat(S, "EsperaEntreOrdenes", 4f), 0f, 60f);
            for (int i = 0; i < 3; i++)
            {
                int k = GangRules.ParseKey(ini.Get(S, "Opcion" + (i + 1), GangRules.DefaultSlots[i]));
                slots[i] = k >= 0 ? k : GangRules.ParseKey(GangRules.DefaultSlots[i]);
            }
            GangRules.Dedupe(slots);
            ShowChat = ini.GetBool(S, "MostrarMensajes", true);
            spreadSpawns = ini.GetBool(S, "ApareceLejos", true);
            gangTrips = ini.GetBool(S, "BandasRecorren", true);
            carHop = ini.GetBool(S, "CambianDeAuto", true);
            if (ranking == null)
            {
                ShowFeed = ini.GetBool(S, "KillFeed", !ini.GetBool("HUDStream", "Mostrar", true));
                ShowRanking = ini.GetBool(S, "Ranking", false);
                LoadRanking();
            }
            ShowTimer = ini.GetBool(S, "MostrarTiempo", true);
            ApplyStreamingNpcConfig(ini);
        }

        public int[] TierFrom { get { return tierFrom; } }
        public int[] Choices { get { return slots; } }

        // ------------------------------------------------------------------
        // Consultas (las usa el director y el menu)
        // ------------------------------------------------------------------
        public int Count { get { return gangs.Count; } }
        public bool Busy { get { return gangs.Count > 0; } }

        public int AliveCount
        {
            get { int k = 0; foreach (var g in gangs) if (g.State == 1) k += g.Alive; else k++; return k; }
        }

        int LiveGangs()
        {
            int k = 0;
            foreach (var g in gangs) if (g.State != 1 || g.Alive > 0) k++;
            return k;
        }

        int TotalMembers()
        {
            int k = 0;
            foreach (var g in gangs) k += g.Alive;
            return k;
        }

        public bool AnyNear(Vector3 p, float radius)
        {
            foreach (var g in gangs)
                if (g.State == 1)
                    foreach (var m in g.Members) if (!m.Dead && Vector3.Distance(m.Pos, p) < radius) return true;
            return false;
        }

        /// <summary>Datos para la camara que sigue al NPC. False si ya no esta.</summary>
        public bool FollowInfo(int ped, out Vector3 pos, out bool inCar, out bool dead)
        {
            foreach (var g in gangs)
                if (g.State == 1)
                    foreach (var m in g.Members)
                        if (m.Ped == ped)
                        {
                            pos = m.Pos; inCar = m.InCar; dead = m.Dead;
                            return true;
                        }
            pos = Vector3.Zero; inCar = false; dead = true;
            return false;
        }

        /// <summary>
        /// El proximo NPC para "seguir NPC": de banda en banda (el jefe, o el primero vivo). Con una sola banda,
        /// pasa de un integrante al otro. 0 = ninguno.
        /// </summary>
        public int NextForCamera(int current)
        {
            var live = new List<Gang>();
            foreach (var g in gangs) if (g.State == 1 && g.Alive > 0) live.Add(g);
            if (live.Count == 0) return 0;
            Gang cg = null;
            if (current != 0 && FindMember(current, out cg) == null) cg = null;
            if (live.Count == 1 && cg == live[0])
            {
                var peds = new List<int>();
                foreach (var m in live[0].Members) if (!m.Dead && m.Ped != 0) peds.Add(m.Ped);
                if (peds.Count == 0) return 0;
                int i = peds.IndexOf(current);
                return peds[(i + 1) % peds.Count];
            }
            int gi = cg != null ? live.IndexOf(cg) : -1;
            Gang next = live[(gi + 1) % live.Count];
            Member l = next.Leader;
            if (l != null && !l.Dead && l.Ped != 0) return l.Ped;
            foreach (var m in next.Members) if (!m.Dead && m.Ped != 0) return m.Ped;
            return 0;
        }

        /// <summary>
        /// Para la camara que lo sigue: a quien le esta tirando (o quien le tira a el). False si no esta a los tiros.
        /// </summary>
        public bool AimInfo(int ped, out Vector3 targetPos)
        {
            targetPos = Vector3.Zero;
            Gang g;
            Member m = FindMember(ped, out g);
            if (m == null || m.Dead || g == null || !g.Fighter) return false;
            double now = G.Now;
            bool shooting = now - m.LastShot < 3.5;
            bool hit = now - m.LastHit < 2.0;
            if (!shooting && !hit) return false;
            Threat t;
            if (m.Target != 0 && ThreatOf(g, m, m.Target, out t) && t.Dist < 90f) { targetPos = t.Pos; return true; }
            if (m.Target != 0 && DOES_CHAR_EXIST(m.Target) && !IS_CHAR_DEAD(m.Target))
            {
                Vector3 p = G.CharPos(m.Target);
                if (Vector3.Distance(p, m.Pos) < 90f) { targetPos = p; return true; }
            }
            t = NearestThreat(g, m, 70f);
            if (t.Ped != 0) { targetPos = t.Pos; return true; }
            return false;
        }

        // ------------------------------------------------------------------
        // La radio del auto del NPC que se esta siguiendo: se escucha siempre (como la radio del jugador),
        // aunque la camara este lejos. Mientras tanto se callan las radios "de la calle" para que no se mezclen.
        // ------------------------------------------------------------------
        bool radioOn, radioLogged;
        int radioCar;
        double radioCheck = -100;

        void UpdateRadio(double now)
        {
            if (now - radioCheck < 0.5) return;
            radioCheck = now;
            int car = 0;
            Member observed = null;
            if (alwaysRadio && dir.Active && gangs.Count > 0)
            {
                int fp = dir.FollowedPed;
                Gang g;
                Member m = fp != 0 ? FindMember(fp, out g) : null;
                if (m != null && !m.Dead && m.InCar && m.Car != 0 && CarOk(m.Car)) { car = m.Car; observed = m; }
            }
            if (car != 0 && radioPositional)
            {
                // radio del auto de verdad (con su efecto), solo se escucha con la camara cerca
                StopRadio();
                if (car != radioCar) { radioCar = car; try { SET_LOUD_VEHICLE_RADIO(car, true); } catch { } }
                if (observed != null) ApplyRadioPreference(observed);
                return;
            }
            if (car != 0)
            {
                if (!radioOn)
                {
                    SET_MOBILE_RADIO_ENABLED_DURING_GAMEPLAY(true);
                    SET_MOBILE_PHONE_RADIO_STATE(true);
                    MUTE_POSITIONED_RADIO(true);
                    radioOn = true;
                    if (!radioLogged) { radioLogged = true; log("[NPC] radio del auto: se escucha siempre mientras la camara sigue a un NPC en auto"); }
                }
                if (car != radioCar)
                {
                    if (radioCar != 0) RETUNE_RADIO_UP(); // otro auto, otra radio
                    radioCar = car;
                }
                if (observed != null) ApplyRadioPreference(observed);
            }
            else StopRadio();
        }

        /// <summary>
        /// 1.9: al bajarse deja la puerta abierta y el motor prendido, asi la radio del auto se sigue
        /// escuchando (con el sonido "de afuera del auto" del juego).
        /// </summary>
        void LeftCar(Gang g, Member m, bool wasDriving, double now)
        {
            int car = m.Car;
            if (car == 0 || (!doorOpen && !engineOn)) return;
            try
            {
                if (!DOES_VEHICLE_EXIST(car) || IS_CAR_DEAD(car)) return;
                if (engineOn) SET_CAR_ENGINE_ON(car, true, true);
                if (doorOpen) OPEN_CAR_DOOR(car, wasDriving ? 0u : 1u);
                if (diag && now - lastDoorLog > 20) { lastDoorLog = now; log("[NPC] " + Name(g, m) + " se bajo y dejo " + (doorOpen ? "la puerta abierta" : "el motor prendido") + " (radio sonando)"); }
            }
            catch { }
        }
        double lastDoorLog = -100;

        public void StopRadio()
        {
            if (!radioOn) return;
            radioOn = false;
            radioCar = 0;
            try
            {
                SET_MOBILE_PHONE_RADIO_STATE(false);
                SET_MOBILE_RADIO_ENABLED_DURING_GAMEPLAY(false);
                MUTE_POSITIONED_RADIO(false);
            }
            catch { }
        }

        readonly List<Vector3> battlePts = new List<Vector3>();
        bool battleOn;
        double battleSince = -1, lastBattleShot = -100, battleCheck = -100, lastBattleCam = -100;

        /// <summary>
        /// Hay un tiroteo? Devuelve los que participan (bandas y policias cerca de donde se tira) y el centro.
        /// Hace falta que haya al menos dos bandos (dos bandas, o una banda y la policia).
        /// </summary>
        public bool BattleInfo(List<Vector3> pts, out Vector3 center)
        {
            center = Vector3.Zero;
            if (pts != null) pts.Clear();
            double now = G.Now;
            Member hot = null;
            double best = 4.0;
            foreach (var g in gangs)
            {
                if (g.State != 1 || !g.Fighter) continue;
                foreach (var m in g.Members)
                {
                    if (m.Dead) continue;
                    double ago = Math.Min(now - m.LastShot, now - m.LastHit + 1.0);
                    if (ago < best) { best = ago; hot = m; }
                }
            }
            if (hot == null) return false;
            Vector3 c = hot.Pos;
            var sides = new HashSet<int>();
            int n = 0;
            Vector3 sum = Vector3.Zero;
            foreach (var g in gangs)
            {
                if (g.State != 1) continue;
                foreach (var m in g.Members)
                {
                    if (m.Dead || FlatDist(m.Pos, c) > 70f) continue;
                    if (pts != null) pts.Add(m.Pos);
                    sum += m.Pos; n++;
                    if (g.Fighter) sides.Add(g.Id);
                }
                foreach (var cop in g.Cops)
                {
                    if (!cop.Alive || FlatDist(cop.Pos, c) > 70f) continue;
                    if (pts != null) pts.Add(cop.Pos);
                    sum += cop.Pos; n++;
                    sides.Add(-1);
                }
            }
            if (sides.Count < 2 || n < 2) return false;
            center = sum / n;
            return true;
        }

        /// <summary>Arranco un tiroteo: la camara va para ahi (una vez; despues de un rato puede volver a pasar).</summary>
        void CheckBattle(double now)
        {
            if (now - battleCheck < 0.5) return;
            battleCheck = now;
            Vector3 c;
            bool b = BattleInfo(null, out c);
            if (b) lastBattleShot = now;
            if (!battleOn && b)
            {
                battleOn = true;
                battleSince = now;
                if (now - lastBattleCam > 35 && dir.Active && dir.BattleNow(c)) { lastBattleCam = now; log("[NPC] arranco un tiroteo: camara al tiroteo"); }
            }
            else if (battleOn && !b && now - lastBattleShot > 8) battleOn = false;
        }

        /// <summary>Esta en el lio (tiro hace poco)? Para la camara que lo sigue.</summary>
        public bool InAction(int ped)
        {
            Gang g;
            Member m = FindMember(ped, out g);
            return m != null && !m.Dead && G.Now - m.LastShot < 4;
        }

        /// <summary>Que tan "caliente" esta una banda: integrantes, tiros, policias, otra banda cerca.</summary>
        float Heat(Gang g, double now)
        {
            if (g.State != 1 || g.Alive == 0) return 0f;
            float h = g.Alive;
            foreach (var m in g.Members) if (!m.Dead && now - m.LastShot < 6) h += 2f;
            int cops = 0;
            foreach (var c in g.Cops) if (c.Alive) cops++;
            h += Math.Min(4, cops) * 0.5f;
            if (g.Fighter && duel)
            {
                Gang r = NearestRivalGang(g);
                if (r != null && GangDistance(g, r) < 120f) h += 4f;
            }
            return h;
        }

        /// <summary>Un NPC vivo para la proxima camara (0 = ninguno). Prefiere los que estan en el lio.</summary>
        public int PickForCamera(int exclude)
        {
            double now = G.Now;
            var peds = new List<int>();
            var w = new List<double>();
            double total = 0;
            foreach (var g in gangs)
            {
                if (g.State != 1) continue;
                double heat = 1.0 + Heat(g, now);
                foreach (var m in g.Members)
                {
                    if (m.Dead || m.Ped == exclude) continue;
                    double x = heat * (m == g.Leader ? 1.5 : 1.0);
                    peds.Add(m.Ped); w.Add(x); total += x;
                }
            }
            if (peds.Count == 0) return 0;
            double r = G.Rng.NextDouble() * total;
            for (int i = 0; i < peds.Count; i++) { r -= w[i]; if (r <= 0) return peds[i]; }
            return peds[peds.Count - 1];
        }

        /// <summary>Donde esta el lio (la banda mas caliente). Para elegir camaras cerca.</summary>
        public bool Hotspot(out Vector3 p)
        {
            p = Vector3.Zero;
            double now = G.Now;
            float best = 0f;
            foreach (var g in gangs)
            {
                float h = Heat(g, now);
                if (h <= best) continue;
                Member l = g.Leader;
                if (l == null) continue;
                best = h;
                p = l.Pos;
            }
            return best > 0f;
        }

        public string Describe()
        {
            if (gangs.Count == 0) return "Ninguno en la calle";
            double now = G.Now;
            var parts = new List<string>();
            foreach (var g in gangs)
            {
                if (g.State != 1) { parts.Add(g.User + " (llegando)"); continue; }
                Member l = g.Leader;
                if (l == null) { parts.Add(g.User + " (cayo)"); continue; }
                int cops = 0;
                foreach (var c in g.Cops) if (c.Alive) cops++;
                string s = g.User + (g.Alive > 1 ? " +" + (g.Alive - 1) : "") + " (" + (l.Status.Length > 0 ? l.Status : NpcRules.Key(g.Behavior)) +
                           ", " + cops + " poli" + (g.Stars > 0 ? " " + g.Stars + "*" : "") + ", permanente" + (g.Control ? ", control" : "") + ")";
                parts.Add(s);
            }
            return string.Join(", ", parts.ToArray());
        }

        /// <summary>Usuario de la banda mas nueva (para las pruebas del menu).</summary>
        public string NewestUser()
        {
            Gang b = Newest(false);
            return b != null ? b.User : null;
        }

        Gang Newest(bool fighterOnly)
        {
            Gang best = null;
            foreach (var g in gangs)
                if (g.State == 1 && g.Alive > 0 && (!fighterOnly || g.Fighter) && (best == null || g.Created > best.Created)) best = g;
            return best;
        }

        /// <summary>Prueba desde el menu: suma uno a la banda mas nueva (como si hubiera matado a un policia).</summary>
        public bool TestGrow()
        {
            Gang g = Newest(true);
            if (g == null) return false;
            Member l = g.Leader;
            return l != null && Grow(g, l, G.Now, true);
        }

        // ------------------------------------------------------------------
        // Aparecer
        // ------------------------------------------------------------------
        /// <summary>'forced' = modo fijo (acciones NpcBatalla / NpcPasear). null = el modo del evento (sub, regalo, follow).</summary>
        public bool Spawn(QueuedAction qa, Vector3 center, NpcBehavior? forced, out string label)
        {
            label = null;
            string user = (qa.User ?? "").Trim();
            if (user.Length == 0) user = "Anonimo";
            if (user.Length > 25) user = user.Substring(0, 25);
            NpcBehavior b = forced.HasValue ? forced.Value : NpcRules.ChooseMode(qa.Source, modeSub, modeGift, modeFollow);
            bool isFollower = NpcRoguePolicy.IsFollowSource(qa.Source);
            // A repeat event belongs to the same living character. Never reset its life, kills or upgrades.
            foreach (var existing in gangs)
                if (string.Equals(existing.User, user, StringComparison.OrdinalIgnoreCase) && existing.IsFollower == isFollower &&
                    (existing.State != 1 || existing.Alive > 0))
                {
                    if (!isFollower) existing.Months = Math.Max(existing.Months, GangLevels.MonthsFor(qa.Source, qa.Count));
                    MarkRecentEvent(user, G.Now + 45);
                    Member current = existing.Leader;
                    if (FollowOnSpawn && (!isFollower || followFollowersOnSpawn) && current != null && dir.Active)
                        dir.ObserveNpc(current.Ped, spawnCamSeconds > 0 ? spawnCamSeconds : 12, "suscripcion repetida", 50);
                    label = "ya tiene un personaje vivo";
                    return true;
                }
            if (SpawnCapacityFull(qa))
            { log("[NPC] capacidad alcanzada: se conserva a los personajes vivos; subir Maximo/MaximoPersonajes si hace falta"); return false; }
            int slot = isFollower ? MAX_SLOTS - 1 : FreeSlot();
            if (slot < 0) { log("[NPC] las 7 bandas de suscriptores estan ocupadas; se conserva a los personajes vivos"); return false; }

            double now = G.Now;
            var g = new Gang
            {
                Id = nextId++, Slot = slot, User = user, Behavior = b, Sub = NpcRules.Subtitle(qa.Source, qa.Count),
                Created = now, Center = center, Fighter = b != NpcBehavior.Pasear,
                Test = string.Equals(user, "prueba", StringComparison.OrdinalIgnoreCase), SpeedMul = speedBase,
                Months = GangLevels.MonthsFor(qa.Source, qa.Count), IsFollower = isFollower
            };
            // si ya hay otra banda cerca, aparece en otra parte de la ciudad (la camara va para alla): se cruzan despues
            if (spreadSpawns && !ambientPacing && !dir.FollowLocked && !dir.TemporaryCameraActive &&
                g.Fighter && dir.Active && GangNear(center, 400f))
            {
                var avoid = new List<Vector3>();
                foreach (var o in gangs) foreach (var om in o.Members) if (!om.Dead) avoid.Add(om.Pos);
                Vector3 far;
                int shot = dir.FarShot(avoid, 450f, out far);
                if (shot >= 0)
                {
                    g.Center = far;
                    g.WaitShot = shot;
                    g.WaitUntil = now + 9.0;
                    g.State = 2;
                    gangs.Add(g);
                    dir.GoTo(shot);
                    label = NpcRules.Label(b);
                    log("[NPC] " + user + " aparece en otra parte de la ciudad (camara " + (shot + 1) + ", lejos de las otras bandas)");
                    return true;
                }
            }
            bool ok = SpawnAt(g, now, out label);
            if (!ok) gangs.Remove(g);
            return ok;
        }

        bool GangNear(Vector3 p, float r)
        {
            foreach (var o in gangs)
                if (o.Fighter)
                    foreach (var om in o.Members) if (!om.Dead && FlatDist(om.Pos, p) < r) return true;
            return false;
        }

        /// <summary>Crea el primer integrante donde corresponde (o empieza a cargar el auto del que se escapa).</summary>
        bool SpawnAt(Gang g, double now, out string label)
        {
            label = null;
            NpcBehavior b = g.Behavior;
            string user = g.User;
            Vector3 center = g.Center;
            if (!gangs.Contains(g)) gangs.Add(g);
            if (b == NpcBehavior.Pasear && paseoCar)
            {
                // pasea en su propio auto (con la radio prendida)
                g.CarModel = PickCarModel();
                if (g.CarModel != 0)
                {
                    REQUEST_MODEL(g.CarModel);
                    g.LoadDeadline = now + 5.0;
                    g.State = 0;
                    label = NpcRules.Label(b);
                    log("[NPC] " + user + ": " + label + " (cargando el auto)");
                    return true;
                }
                // no hay autos para crear: pasea a pie
            }
            bool inFrame = spawnCam != 0 && dir.Active;
            Member m = NewMember(g, FindSpawnPoint(center, inFrame), now);
            if (m == null)
            {
                log("[NPC] no se pudo crear el personaje de " + user + (g.SpawnTries > 0 ? " (intento " + (g.SpawnTries + 1) + ")" : ""));
                return false;
            }
            ActivateGang(g, m, now);
            label = NpcRules.Label(g.Behavior);
            return true;
        }

        int FreeSlot()
        {
            for (int s = 0; s < MAX_SLOTS - 1; s++)
            {
                bool used = false;
                foreach (var g in gangs) if (g.Slot == s) { used = true; break; }
                if (!used) return s;
            }
            return -1;
        }

        int PickCarModel()
        {
            var ok = new List<int>();
            foreach (string s in cfg.Ini.Get("Suscriptor", "Autos", DefaultCars).Split(','))
            {
                string m = s.Trim();
                if (m.Length == 0) continue;
                int h = GET_HASH_KEY(m);
                if (IS_MODEL_IN_CDIMAGE(h)) ok.Add(h);
            }
            return ok.Count == 0 ? 0 : ok[G.Rng.Next(ok.Count)];
        }

        /// <summary>Al aire libre: no hay nada arriba (techo, puente) en esa posicion.</summary>
        static bool OpenSky(Vector3 p, float groundZ)
        {
            float top;
            return !G.TopZ(p.X, p.Y, out top) || top - groundZ < 1.5f;
        }

        static float FlatDist(Vector3 a, Vector3 b) { return GangRules.Flat(a, b); }

        /// <summary>El suelo en (x, y) cerca de la altura 'refZ' (si no lo encuentra, 'refZ').</summary>
        static Vector3 Ground(Vector3 p, float refZ)
        {
            float gz;
            if (G.GroundZ(new Vector3(p.X, p.Y, refZ + 1.5f), out gz) && Math.Abs(gz - refZ) < 3f) p.Z = gz;
            else p.Z = refZ;
            return p;
        }

        /// <summary>
        /// Lugar para que aparezca: la vereda de una calle cercana, a nivel de la calle (nunca en un techo
        /// ni adentro de un edificio) y, si se puede, fuera del cuadro que se esta viendo.
        /// </summary>
        Vector3 FindSpawnPoint(Vector3 center) { return FindSpawnPoint(center, false); }

        /// <summary>'inFrame': mejor donde lo vea la camara (aparece "en vivo" delante de ella).</summary>
        Vector3 FindSpawnPoint(Vector3 center, bool inFrame)
        {
            if (inFrame)
            {
                Vector3 seen;
                if (FindInFrameSpawn(center, out seen)) return seen;
                log("[Camara] no encontre un lugar en cuadro para aparecer: aparece cerca, fuera de cuadro");
            }
            Vector3 firstOk = Vector3.Zero, firstNode = Vector3.Zero;
            bool haveOk = false;
            float[] sides = { 6f, -6f, 4.5f, -4.5f, 0f };
            for (uint k = 1; k <= 14; k++)
            {
                Vector3 node;
                float h;
                if (!GET_NTH_CLOSEST_CAR_NODE_WITH_HEADING(center, k, out node, out h) || node == Vector3.Zero) continue;
                if (FlatDist(node, center) > 70f) continue;           // la altura no importa: el centro puede ser un techo
                if (firstNode == Vector3.Zero) firstNode = node;
                float nodeTop;
                if (G.TopZ(node.X, node.Y, out nodeTop) && nodeTop - node.Z > 3f) continue; // calle tapada (tunel, puente)
                Vector3 right = MathX.Right(new Vector3(0, 0, h));
                foreach (float side in sides)
                {
                    Vector3 p = node + right * side;
                    float gz;
                    if (!G.GroundZ(new Vector3(p.X, p.Y, node.Z + 1.5f), out gz)) continue;
                    if (Math.Abs(gz - node.Z) > 1.5f) continue;       // escalon, pared o techo: no es la vereda
                    if (!OpenSky(p, gz)) continue;                     // adentro de algo
                    p.Z = gz;
                    if (!haveOk) { firstOk = p; haveOk = true; }
                    if (!(dir.Active && dir.InFrame(p + new Vector3(0, 0, 1f), 1f))) return p;
                    break; // probar con la proxima calle
                }
            }
            if (haveOk) return firstOk;
            if (firstNode != Vector3.Zero) return firstNode; // en la calle misma
            return center;
        }

        bool FindInFrameSpawn(Vector3 center, out Vector3 spot)
        {
            spot = Vector3.Zero;
            Vector3 cam = dir.CamPos;
            float[] sides = { 4.5f, -4.5f, 6f, -6f, 0f };
            for (uint k = 1; k <= 16; k++)
            {
                Vector3 node;
                float h;
                if (!GET_NTH_CLOSEST_CAR_NODE_WITH_HEADING(center, k, out node, out h) || node == Vector3.Zero) continue;
                if (FlatDist(node, center) > 60f) continue;
                float nodeTop;
                if (G.TopZ(node.X, node.Y, out nodeTop) && nodeTop - node.Z > 3f) continue;
                Vector3 right = MathX.Right(new Vector3(0, 0, h));
                foreach (float side in sides)
                {
                    Vector3 p = node + right * side;
                    float gz;
                    if (!G.GroundZ(new Vector3(p.X, p.Y, node.Z + 1.5f), out gz) || Math.Abs(gz - node.Z) > 1.5f || !OpenSky(p, gz)) continue;
                    p.Z = gz;
                    float d = Vector3.Distance(cam, p);
                    if (d < 8f || d > 70f) continue;                                    // ni pegado a la camara ni un punto lejos
                    if (!dir.InFrameCenter(p + new Vector3(0, 0, 1f), 0.75f)) continue;  // bien adentro del cuadro
                    Vector3 hit;
                    if (G.Raycast(cam, p + new Vector3(0, 0, 1.2f), out hit)) continue;  // algo lo tapa
                    spot = p;
                    return true;
                }
            }
            return false;
        }

        Member NewMember(Gang g, Vector3 p, double now)
        {
            int ped = 0;
            for (int tries = 0; tries < 3 && ped == 0; tries++)
            {
                CREATE_RANDOM_CHAR(p + new Vector3(0, 0, 1f), out ped);
                if (ped != 0 && !DOES_CHAR_EXIST(ped)) ped = 0;
                if (ped != 0 && IsCop(ped)) { try { DELETE_CHAR(ped); } catch { } ped = 0; }
            }
            if (ped == 0) return null;
            var m = new Member { G = g, Ped = ped, Index = g.Members.Count == 0 ? 0 : g.NextIndex++, Created = now };
            InitializeStreamMember(g, m, now);
            SET_CHAR_HEADING(ped, G.Rand(0f, 360f));
            ProbeCivType(ped);
            g.Members.Add(m);
            return m;
        }

        Member SpawnInCar(Gang g, double now)
        {
            Vector3 node = g.Center;
            float heading = G.Rand(0f, 360f);
            bool got = false;
            for (uint k = 1; k <= 8; k++)
            {
                Vector3 r;
                float h;
                if (!GET_NTH_CLOSEST_CAR_NODE_WITH_HEADING(g.Center, k, out r, out h) || r == Vector3.Zero) continue;
                if (FlatDist(r, g.Center) > 90f) continue;
                if (!OpenSky(r, r.Z)) continue; // tunel, debajo de las vias...
                if (G.VehiclesNear(r, 4f, 1, true).Count > 0) continue; // ya hay un auto ahi
                if (!got) { node = r; heading = h; got = true; }
                if (!(dir.Active && dir.InFrame(r, 3f))) { node = r; heading = h; break; }
            }
            if (!got) return null;
            int car;
            CREATE_CAR(g.CarModel, node + new Vector3(0, 0, 0.5f), out car, true);
            if (car == 0 || !DOES_VEHICLE_EXIST(car)) return null;
            SET_CAR_HEADING(car, heading);
            SET_LOUD_VEHICLE_RADIO(car, true);
            int ped = 0;
            try { CREATE_RANDOM_CHAR_AS_DRIVER(car, out ped); } catch { ped = 0; }
            if (ped == 0 || !DOES_CHAR_EXIST(ped))
            {
                CREATE_RANDOM_CHAR(node + new Vector3(3f, 0, 1f), out ped);
                if (ped != 0 && DOES_CHAR_EXIST(ped)) WARP_CHAR_INTO_CAR(ped, car);
                else { MARK_CAR_AS_NO_LONGER_NEEDED(car); return null; }
            }
            var m = new Member { G = g, Ped = ped, Index = 0, Created = now, Car = car, InCar = true, Driving = true };
            InitializeStreamMember(g, m, now);
            g.Members.Add(m);
            g.Cars[car] = now;
            return m;
        }

        /// <summary>Con un civil: si su tipo es 4/5 la lista empieza con los jugadores (policia = 6); si es 0/1, policia = 2.</summary>
        void ProbeCivType(int ped)
        {
            // Ped creation type COP is 2 in GTA IV. Civilian types never identify
            // alternate enum layouts; types3/6 belong to other populations.
            if (civProbed) return;
            civProbed = true;
            copType = 2;
        }

        /// <summary>Con un policia del juego cerca se sabe el tipo exacto (una vez).</summary>
        uint CopType(Vector3 near)
        {
            if (copTypeFromCop || N.IsMissing("GET_PED_TYPE")) return copType;
            try
            {
                foreach (int p in G.PedsNear(near, 250f, 30))
                {
                    if (madeCops.Contains(p) || !IsCop(p)) continue;
                    uint t;
                    GET_PED_TYPE(p, out t);
                    if (t != 2) continue;
                    copTypeFromCop = true;
                    if (t != copType) log("[NPC] tipo de los policias del juego: " + t + " (antes " + copType + ")");
                    else log("[NPC] tipo de los policias del juego: " + t);
                    copType = t;
                    break;
                }
            }
            catch { }
            return copType;
        }

        static uint BaseHealth(NpcBehavior b)
        {
            return b == NpcBehavior.Pasear ? 200u : 350u;
        }

        static void GiveGun(Member m, int weapon, int ammo)
        {
            GIVE_WEAPON_TO_CHAR(m.Ped, weapon, ammo, false);
            int use = weapon;
            // si por sus meses tiene una mejor, se queda con esa en la mano
            if (m.MonthsWeapon != 0 && GangLevels.Rank(m.MonthsWeapon) > GangLevels.Rank(weapon)) use = m.MonthsWeapon;
            if (GangLevels.IsSpecial(use)) use = m.MonthsWeapon != 0 && !GangLevels.IsSpecial(m.MonthsWeapon) ? m.MonthsWeapon : 7;
            SET_CURRENT_CHAR_WEAPON(m.Ped, use, true);
            m.Weapon = use;
            m.Armed = use >= 7; // 1-3 bate, taco, cuchillo (pinas)
            m.AmmoCapped = -1; // arma nueva (o premio): se recarga
            m.VehicleSidearmAmmoLeft = -1;
            m.PistolFallback = false;
        }

        /// <summary>Configura la banda recien creada (con su primer integrante) segun lo que tiene que hacer.</summary>
        void ActivateGang(Gang g, Member leader, double now)
        {
            g.State = 1;
            g.EndAt = double.PositiveInfinity;
            SetupMember(g, leader, now);
            int ped = leader.Ped;
            if (g.Behavior == NpcBehavior.Pasear)
            {
                if (leader.InCar && CarOk(leader.Car)) { CruiseCalm(leader, leader.Car, now); Say(leader, "pasea en auto"); }
                else { Wander(leader, now); Say(leader, "paseando"); }
            }
            else
            {
                Say(leader, "sale a la batalla: busca un auto y a las otras bandas");
                if (policeOn && !ambientPacing && !g.IsFollower) g.PoliceAt = now + 30;
            }
            if (unlockRule == GangRules.UnlockAlways && g.Fighter) g.Control = true;
            if (GameName)
            {
                try { GIVE_PED_FAKE_NETWORK_NAME(ped, g.User, 255, 255, 255, 255); leader.FakeName = true; } catch { }
            }
            Changed();
            log("[NPC] aparecio " + g.User + " -> " + NpcRules.Key(g.Behavior) + (g.Sub.Length > 0 ? " (" + g.Sub + ")" : "") +
                (g.Fighter ? ", arma: " + GangLevels.WeaponName(leader.Weapon) : "") +
                " a " + FlatDist(leader.Pos, g.Center).ToString("0") + " m del centro" + (g.Control ? " (control por chat activo)" : ""));
            if (!g.IsFollower) StreamRuntime.AddFeed("spawnsub", g.User, "NPC listo", now);
            if ((!g.IsFollower || followFollowersOnSpawn) && dir.Active && spawnCam == 0) dir.FollowNow(ped);
            else if ((!g.IsFollower || followFollowersOnSpawn) && dir.Active && spawnCam == 2) { g.FollowAt = now + spawnCamSeconds; g.FollowTries = 0; }
            if (diag && dir.Active)
                log("[Camara] " + g.User + " aparecio " + (dir.InFrame(leader.Head, 0f) ? "en cuadro" : "fuera de cuadro") +
                    (spawnCam == 0 ? ", la camara lo sigue" : spawnCam == 2 ? ", la camara lo sigue en " + spawnCamSeconds.ToString("0") + " s" : ", queda la camara"));
        }

        /// <summary>1.9: "aparece delante de la camara y despues lo sigue" (reintenta si otra toma le gano).</summary>
        void StepSpawnFollow(Gang g, double now)
        {
            if (g.FollowAt < 0 || now < g.FollowAt) return;
            Member l = g.Leader;
            if (l == null || !dir.Active) { g.FollowAt = -1; return; }
            if (dir.FollowedPed != 0 && FindMember(dir.FollowedPed, out Gang fg) != null && fg == g) { g.FollowAt = -1; return; }
            if (dir.FollowLocked || g.FollowTries >= 3) { g.FollowAt = -1; if (g.FollowTries >= 3) log("[Camara] no se pudo ir a seguir a " + g.User); return; }
            g.FollowTries++;
            dir.FollowNow(l.Ped);
            g.FollowAt = now + 2.5; // chequear que haya ido
        }

        /// <summary>Vida, armas y bando de un integrante (el primero o uno que se sumo).</summary>
        void SetupMember(Gang g, Member m, double now)
        {
            int ped = m.Ped;
            m.Pos = G.CharPos(ped);
            m.Head = m.Pos + new Vector3(0, 0, 1f);
            m.NextThink = now + 0.4 + 0.13 * m.Index;
            uint hp = NpcRoguePolicy.InitialHealth(g.IsFollower, m.Index > 0,
                (uint)Math.Max(120, BaseHealth(NpcBehavior.Batalla) * lifeMult), principalHealthScale, cloneHealthScale, followerHealth);
            if (m.Index > 0) hp = NpcRoguePolicy.CloneHealth(g.PrincipalHealth, hp);
            SET_CHAR_MAX_HEALTH(ped, hp);
            SET_CHAR_HEALTH(ped, hp);
            m.StartHealth = hp;
            if (m.Index == 0) g.PrincipalHealth = hp;
            m.LastHp = hp;
            SET_CHAR_KEEP_TASK(ped, true);
            SET_CHAR_DROPS_WEAPONS_WHEN_DEAD(ped, false);
            // unos segundos a prueba de fuego y explosiones: las otras acciones del evento pasan cerca
            SET_CHAR_PROOFS(ped, false, true, true, false, false);
            m.ProtectUntil = now + (m.Index == 0 ? 10.0 : 4.0);
            if (!g.Fighter)
            {
                MakeSafe(g, m, !g.IsFollower && paseoSafe);
                if (g.IsFollower) { SET_CHAR_RELATIONSHIP_GROUP(ped, g.Group); Block(m, false); }
                ApplyRogueStats(g, m);
                return;
            }
            g.EverFighter = true;
            SetupFighter(g, m, true);
        }

        /// <summary>Lo que necesita uno de una banda en modo batalla: bando, armas, punteria.</summary>
        void SetupFighter(Gang g, Member m, bool first)
        {
            int ped = m.Ped;
            MakeSafe(g, m, false);
            MakeFighter(g, m);
            SET_CHAR_WILL_USE_COVER(ped, true);
            SET_CHAR_WILL_DO_DRIVEBYS(ped, true);
            SET_CHAR_CANT_BE_DRAGGED_OUT(ped, true);
            SET_CHAR_STAY_IN_CAR_WHEN_JACKED(ped, true);
            SET_CHAR_WILL_LEAVE_CAR_IN_COMBAT(ped, false); // las bajadas del auto las decidimos nosotros
            SET_CHAR_MOVE_ANIM_SPEED_MULTIPLIER(ped, Math.Min(speedMax, g.SpeedMul)); // corren un poco mas (y con la mejora, mas)
            if (g.FireBonus > 0) SET_CHAR_SHOOT_RATE(ped, 100 + g.FireBonus);
            if (!first)
            {
                // vuelve a la batalla (despues de !pasear): sus armas de antes, sin regalar chaleco ni explosivos otra vez
                if (m.Weapon > 0) SET_CURRENT_CHAR_WEAPON(ped, m.Weapon, true);
                else if (m.Index == 0 && m.Primary == 0 && !m.PistolFallback) ApplyMonthsWeapon(g, m); // (sin balas: no se le regala otra)
                SET_CHAR_ACCURACY(ped, (uint)Math.Min(90, 40 + g.AccBonus + (g.Level - 1) * 5));
            }
            else if (m.Index == 0)
            {
                ApplyMonthsWeapon(g, m);
                SET_CHAR_ACCURACY(ped, (uint)Math.Min(90, 45 + g.AccBonus + (g.Level - 1) * 5));
            }
            else
            {
                // el que se suma: con el arma del jefe (o la mejor que tenga la banda)
                Member l = null;
                foreach (var o in g.Members) if (o != m && !o.Dead && o.Armed) { l = o; break; }
                if (l != null && l.MonthsWeapon != 0) m.MonthsWeapon = l.MonthsWeapon;
                int baseW = tierWeapons[0] > 0 && !GangLevels.IsSpecial(tierWeapons[0]) ? tierWeapons[0] : 7;
                GiveGun(m, l != null && l.Weapon > 0 ? l.Weapon : baseW, 800);
                SET_CHAR_ACCURACY(ped, (uint)Math.Min(90, 40 + g.AccBonus + (g.Level - 1) * 5));
                ADD_ARMOUR_TO_CHAR(ped, 50);
            }
            if (g.Wanted) OutlawPed(g, m);
            ApplyRogueStats(g, m);
        }

        /// <summary>Modo paseo: nadie lo puede lastimar ni apuntar, y no reacciona a los tiros.</summary>
        void MakeSafe(Gang g, Member m, bool on)
        {
            int ped = m.Ped;
            if (ped == 0 || !DOES_CHAR_EXIST(ped)) return;
            if (!on && !m.Safe) return;
            m.Safe = on;
            SET_CHAR_INVINCIBLE(ped, on);
            SET_CHAR_NEVER_TARGETTED(ped, on);
            if (on)
            {
                // con la gente de la calle (no en el grupo de la banda, que la policia y las otras bandas odian)
                bool male = true;
                try { male = IS_CHAR_MALE(ped); } catch { }
                SET_CHAR_RELATIONSHIP_GROUP(ped, male ? 1 : 2);
                SET_BLOCKING_OF_NON_TEMPORARY_EVENTS(ped, true);
                m.Block = 1;
            }
        }

        /// <summary>El arma que eligio el streamer para esos meses de sub (y mas chaleco con mas meses).</summary>
        void ApplyMonthsWeapon(Gang g, Member m)
        {
            if (!g.Fighter) return;
            int w = tierWeapons[GangLevels.TierFor(g.Months, tierFrom)];
            if (GangLevels.IsSpecial(w))
            {
                // si eligio granadas / molotov / RPG: las usa de vez en cuando, y en la mano una pistola
                AddSpecial(g, w, GangLevels.AmmoFor(w));
                w = 7;
            }
            if (w > 0) GIVE_WEAPON_TO_CHAR(m.Ped, w, 900, false);
            m.MonthsWeapon = w;
            GiveGun(m, w > 0 ? w : 0, 1);
            if (w == 0) { m.Armed = false; m.Weapon = 0; }
            ADD_ARMOUR_TO_CHAR(m.Ped, Math.Min(200, 20 + g.Months * 6));
        }

        /// <summary>Granadas, molotov o cohetes para la banda (los usa cualquiera, de a uno).</summary>
        void AddSpecial(Gang g, int w, int n)
        {
            if (w == GangLevels.Grenade) g.Grenades += n;
            else if (w == GangLevels.Molotov) g.Molotovs += n;
            else if (w == GangLevels.Rpg) g.Rockets += n;
        }

        /// <summary>
        /// Lo pone en el grupo de su banda: odia a la policia y a las otras bandas (y la policia nuestra lo
        /// odia a el). Los de la misma banda no se pelean entre ellos.
        /// </summary>
        void MakeFighter(Gang g, Member m)
        {
            int ped = m.Ped;
            SET_CHAR_RELATIONSHIP_GROUP(ped, g.Group);
            SET_CHAR_RELATIONSHIP(ped, 5, 3); // odia a la policia
            if (duel)
                foreach (var o in gangs)
                {
                    if (o == g || !o.Fighter || o.State != 1) continue;
                    if (PeacefulAlly(g, o)) { SET_CHAR_RELATIONSHIP(ped, 3, o.Group); continue; }
                    uint rel = RivalRelationship(g, o);
                    SET_CHAR_RELATIONSHIP(ped, rel, o.Group);
                    foreach (var om in o.Members)
                        if (!om.Dead && om.Ped != 0 && DOES_CHAR_EXIST(om.Ped)) SET_CHAR_RELATIONSHIP(om.Ped, rel, g.Group);
                }
            if (attackCivs)
                for (int grp = 1; grp <= 22; grp++) if (grp != 3) SET_CHAR_RELATIONSHIP(ped, 5, grp);
        }

        /// <summary>La policia lo busca (a toda la banda).</summary>
        void MakeOutlaw(Gang g, bool everyone)
        {
            if (!g.Wanted) { g.Stars = Math.Max(g.Stars, 2); g.WantedSince = G.Now; g.LastContact = G.Now; }
            g.Wanted = true;
            foreach (var m in g.Members) if (!m.Dead && m.Ped != 0) OutlawPed(g, m);
        }

        void OutlawPed(Gang g, Member m)
        {
            int ped = m.Ped;
            if (!DOES_CHAR_EXIST(ped)) return;
            // No lo marcamos "buscado por la policia": con eso los policias intentan ARRESTARLO (caminan
            // apuntando y no tiran). Nuestros policias lo odian por su grupo y van a matarlo.
            SET_CHAR_IS_TARGET_PRIORITY(ped, true);
            SET_CHAR_WILL_USE_COVER(ped, true);
            if (!m.Armed) GiveGun(m, 7, 300); // cuando llega la policia, saca un arma
        }

        // ------------------------------------------------------------------
        // Cada frame
        // ------------------------------------------------------------------
        public void Update()
        {
            double now = G.Now;
            UpdateHudData(now);
            try { UpdateRadio(now); } catch { }
            if (gangs.Count == 0) { EndAmbientEvents("sin NPC activos"); return; }
            try { RandomChase(now); } catch (Exception ex) { log("[NPC] error en la persecucion: " + ex.Message); }
            try { StepEvents(now); } catch (Exception ex) { log("[NPC] error en un evento: " + ex.Message); }
            try { CheckBattle(now); } catch (Exception ex) { log("[NPC] error con la camara del tiroteo: " + ex.Message); }
            bool changed = false;
            for (int i = 0; i < gangs.Count; i++)
            {
                Gang g = gangs[i];
                bool keep;
                try { keep = StepGang(g, now, ref changed); }
                catch (Exception ex) { log("[NPC] error con la banda de " + g.User + ": " + ex.Message); keep = false; }
                if (keep) continue;
                ReleaseGang(g);
                gangs.RemoveAt(i--);
                changed = true;
            }
            if (changed) Changed();
        }

        /// <summary>Devuelve false cuando hay que soltar a toda la banda.</summary>
        bool StepGang(Gang g, double now, ref bool changed)
        {
            if (g.State == 2)
            {
                // esperando que la camara llegue (para que esa parte de la ciudad este cargada)
                bool there = g.WaitShot == -2 || dir.CurrentIndex == g.WaitShot && dir.ReadyForAction();
                if (g.WaitShot == -2 && now < g.WaitUntil) return true;
                if (!there && now < g.WaitUntil) return true;
                // ya llego: el objetivo real de esa camara. Si no llego: donde este la camara (esta cargado)
                g.Center = dir.ActionCenter();
                g.State = 1;
                string lbl;
                if (SpawnAt(g, now, out lbl)) return true;
                // 1.9: la ciudad todavia no termino de cargar: probar de nuevo en un rato (antes se perdia)
                if (++g.SpawnTries >= 4) return false;
                g.State = 2;
                g.WaitUntil = now + 1.5;
                g.WaitShot = -2; // ya no esperar a la camara
                return true;
            }
            if (g.State == 0)
            {
                // esperando el modelo del auto
                bool loaded = HAS_MODEL_LOADED(g.CarModel);
                if (!loaded && now < g.LoadDeadline) return true;
                Member first = loaded ? SpawnInCar(g, now) : null;
                MARK_MODEL_AS_NO_LONGER_NEEDED(g.CarModel);
                if (first == null)
                {
                    // sin auto propio: a pie
                    first = NewMember(g, FindSpawnPoint(g.Center), now);
                    if (first == null) { log("[NPC] no se pudo crear el personaje de " + g.User); return false; }
                }
                ActivateGang(g, first, now);
                return true;
            }

            for (int j = 0; j < g.Members.Count; j++)
            {
                Member m = g.Members[j];
                bool keep;
                try { keep = StepMember(g, m, now); }
                catch (Exception ex) { log("[NPC] error con " + Name(g, m) + ": " + ex.Message); keep = false; }
                if (keep) continue;
                ReleaseMember(m);
                g.Members.RemoveAt(j--);
                changed = true;
            }
            if (g.Members.Count == 0) return false;

            // policias: donde estan y si tiran (cada frame: los tiros duran un instante)
            foreach (var c in g.Cops)
            {
                if (!c.Alive) continue;
                if (!DOES_CHAR_EXIST(c.Ped)) { c.DeadAt = now; continue; }
                if (IS_CHAR_DEAD(c.Ped)) { c.DeadAt = now; continue; }
                c.Pos = G.CharPos(c.Ped);
                if (IS_CHAR_SHOOTING(c.Ped)) { if (now - c.LastShot > 0.4) c.Shots++; c.LastShot = now; }
            }
            if (diag && g.Cops.Count > 0 && now - g.LastCopLog > 10) { g.LastCopLog = now; LogCops(g, now); }

            if (g.Alive == 0)
            {
                if (g.AllDeadAt < 0)
                {
                    g.AllDeadAt = now;
                    log("[NPC] " + (g.Members.Count > 1 || g.Kills > 0 ? "la banda de " + g.User + " cayo entera" : g.User + " cayo") +
                        " despues de " + (now - g.Created).ToString("0") + " s (" + g.Kills + " muertes)");
                    notify(g.User + " -> FUERA DE COMBATE");
                    if (g.Fighter) dir.Repair.MarkDirty(g.Members[0].Pos);
                }
                return now - g.AllDeadAt < 6.0;
            }
            Member lead = g.Leader;
            StepAmbientEncounter(g, now);
            if (g.Wanted && now - g.LastDirty > 10) { g.LastDirty = now; dir.Repair.MarkDirty(lead.Pos); }
            if (!g.Wanted && g.PoliceAt > 0 && now >= g.PoliceAt && policeOn && g.Fighter)
            {
                g.PoliceAt = -1;
                MakeOutlaw(g, attackCivs);
                g.CopsPending = Math.Max(1, patrols / 2);
                g.NextCops = now;
                Say(lead, "llega la policia");
            }
            if (g.CopsPending > 0 && g.NextCops > 0 && now >= g.NextCops) { if (policeOn && g.Fighter) SpawnPatrol(g, now); else g.CopsPending = 0; }
            if (now - g.LastKillScan > 0.5) { g.LastKillScan = now; ScanKills(g, now); }
            if (g.Votes.Due(now))
            {
                int w = g.Votes.Winner();
                int total = g.Votes.Total;
                g.Votes.Reset();
                if (w >= 0 && g.Control) ApplyOrder(g, slots[w], total == 1 ? "1 voto" : total + " votos", now);
            }
            if (g.Order >= 0 && now >= g.OrderUntil) { g.Order = -1; log("[NPC] la banda de " + g.User + " termino la orden del chat"); }
            if (g.Fighter) StepOffer(g, now);
            StepSpawnFollow(g, now);
            if (now - g.LastCopThink > 0.5)
            {
                g.LastCopThink = now;
                if (g.Cops.Count > 0 || g.Wanted) ManageCops(g, now);
                else if (streetCops && policeOn && g.Fighter && now - g.LastScan > 2.0 && GangShotRecently(g, now, 8))
                {
                    // 1.9: tiroteo entre bandas sin estrellas: los policias que estan cerca igual se meten
                    g.LastScan = now;
                    ManageStreetCops(g, now);
                }
                Reinforce(g, now);
                BringRival(g, now);
            }
            if (now - g.LastCleanup > 2.0) { g.LastCleanup = now; if (Cleanup(g, now)) changed = true; }
            return true;
        }

        /// <summary>Devuelve false cuando hay que soltar a ese integrante.</summary>
        bool StepMember(Gang g, Member m, double now)
        {
            if (m.Ped == 0 || !DOES_CHAR_EXIST(m.Ped))
            {
                if (!m.Dead) log("[NPC] " + Name(g, m) + " desaparecio");
                return false;
            }
            bool wasInCar = m.InCar, wasDriving = m.Driving;
            m.InCar = IS_CHAR_IN_ANY_CAR(m.Ped);
            if (wasInCar && !m.InCar && !m.Dead) LeftCar(g, m, wasDriving, now);
            m.Pos = G.CharPos(m.Ped);
            m.Head = m.Pos + new Vector3(0, 0, 1.0f);
            if (m.Dead) return now - m.DeadAt < 8.0 || g.Alive == 0;
            if (IS_CHAR_DEAD(m.Ped) || IS_CHAR_FATALLY_INJURED(m.Ped)) { OnMemberDeath(g, m, now); return true; }

            if (m.InCar && now - m.LastCarCheck > 0.25)
            {
                m.LastCarCheck = now;
                int car;
                GET_CAR_CHAR_IS_USING(m.Ped, out car);
                if (car != 0)
                {
                    int drv;
                    GET_DRIVER_OF_CAR(car, out drv);
                    m.Driving = drv == m.Ped;
                    if (car != m.Car) OnGotCar(g, m, car, now);
                    g.Cars[car] = now;
                }
            }
            else if (!m.InCar) m.Driving = false;

            if (now - m.LastRoom > 0.5) { m.LastRoom = now; try { uint rk; GET_KEY_FOR_CHAR_IN_ROOM(m.Ped, out rk); m.RoomKey = rk; } catch { m.RoomKey = 0; } }
            if (!m.SpecialResolved) CheckSpecial(g, m, now);
            if (limitedAmmo && g.Fighter && now - m.LastAmmoCheck > 1.0) { m.LastAmmoCheck = now; CheckAmmo(g, m, now); }
            if (m.SpecialUntil > 0 && now > m.SpecialUntil)
            {
                // termino de tirar el explosivo (aunque otra cosa le haya cambiado la orden): vuelve a su arma
                m.SpecialUntil = -1;
                if (m.Weapon > 0) SET_CURRENT_CHAR_WEAPON(m.Ped, m.Weapon, true);
                if (m.Phase == P_SPECIAL) { m.Phase = P_NONE; m.Mode = M_NONE; }
            }
            if (m.ShieldUntil > 0 && now > m.ShieldUntil)
            {
                m.ShieldUntil = -1;
                if (!m.Safe) SET_CHAR_INVINCIBLE(m.Ped, false);
            }
            if (m.ProtectUntil > 0 && now > m.ProtectUntil)
            {
                m.ProtectUntil = -1;
                SET_CHAR_PROOFS(m.Ped, false, false, false, false, false);
            }
            if (now - m.LastHealth > 0.2)
            {
                m.LastHealth = now;
                uint hp;
                GET_CHAR_HEALTH(m.Ped, out hp);
                if (armorBar) { try { uint ar; GET_CHAR_ARMOUR(m.Ped, out ar); m.Armor = ar; } catch { } }
                if (hp + 3 < m.LastHp) m.LastHit = now; // le pegaron
                // se curan de a poco manejando (o siempre, con la mejora "regeneracion")
                bool carHeal = healInCar && m.InCar && g.Fighter && !g.IsFollower;
                if ((carHeal || g.Regen) && hp > 0 && hp < m.StartHealth && now - m.LastHit > 3)
                {
                    float rate = (carHeal ? healRate : 1.5f) * (m.Index > 0 ? 0.5f : 1f);
                    uint add = (uint)Math.Max(1f, m.StartHealth * rate / 100f * (float)(now - Math.Max(m.HealAt, now - 0.5)));
                    hp = Math.Min(m.StartHealth, hp + add);
                    SET_CHAR_HEALTH(m.Ped, hp);
                }
                m.HealAt = now;
                m.LastHp = hp;
                m.Health01 = Math.Max(0f, Math.Min(1f, hp / (float)Math.Max(1u, m.StartHealth)));
            }
            if (IS_CHAR_SHOOTING(m.Ped))
            {
                m.LastShot = now;
            }
            if (diag && now - m.LastDiag > 3 && now - m.Created < 150) { m.LastDiag = now; Diagnose(g, m, now); }
            if (now >= m.NextThink)
            {
                m.NextThink = now + 0.5;
                Think(g, m, now);
            }
            return true;
        }

        void OnGotCar(Gang g, Member m, int car, double now)
        {
            bool stolen = m.Mode == M_ENTER_CAR && m.StealCar == car && m.Driving;
            m.Car = car;
            m.StealCar = 0;
            m.Seat = -1;
            m.HopAt = -1;
            if (g.Armored) ArmorCar(car);
            if (m.Driving)
            {
                try { SET_CAR_AS_MISSION_CAR(car); } catch { }
                SET_LOUD_VEHICLE_RADIO(car, true); // siempre con la radio prendida (y fuerte)
                m.DriveSince = now;
            }
            Changed();
            if (!stolen) return;
            m.Mode = M_NONE;
            Say(m, "se robo un auto");
            if (now - g.LastStoleNotice > 20)
            {
                g.LastStoleNotice = now;
                notify(DisplayName(g, m) + " -> SE ROBO UN AUTO");
            }
            if (!g.Wanted && g.Fighter && !g.IsFollower && policeOn && (g.PoliceAt < 0 || g.PoliceAt > now + 15) &&
                (!ambientPacing || (now >= nextTheftPolice && G.Rng.NextDouble() < theftPoliceChance)))
            { g.PoliceAt = now + 15; nextTheftPolice = now + theftPoliceCooldown; }
        }

        void OnMemberDeath(Gang g, Member m, double now)
        {
            m.Dead = true;
            m.DeadAt = now;
            m.Health01 = 0f;
            ranking.Death(g.User);
            // si lo mato otra banda, el kill lo anota esa banda; si no, la policia o "cayo"
            bool byGang = false, byCop = false;
            try
            {
                foreach (var og in gangs)
                {
                    if (og != g) foreach (var om in og.Members) if (!om.Dead && om.Ped != 0 && HAS_CHAR_BEEN_DAMAGED_BY_CHAR(m.Ped, om.Ped, false)) { byGang = true; break; }
                    if (byGang) break;
                    foreach (var c in og.Cops) if (c.Alive && HAS_CHAR_BEEN_DAMAGED_BY_CHAR(m.Ped, c.Ped, false)) { byCop = true; break; }
                }
            }
            catch { }
            if (!byGang) Feed(byCop ? "POLICIA" : DisplayName(g, m), byCop ? 2 : 4, byCop ? DisplayName(g, m) : "CAYO", byCop ? 1 : 4, now);
            int left = g.Alive;
            if (left > 0)
            {
                log("[NPC] cayo " + Name(g, m) + " despues de " + (now - m.Created).ToString("0") + " s (quedan " + left + ")");
                if (m.Index == 0) notify(g.User + " -> CAYO (SU BANDA SIGUE)");
            }
            if (g.Fighter && left > 0) dir.Repair.MarkDirty(m.Pos);
        }

        // ------------------------------------------------------------------
        // Ordenes basicas. Cada una se da UNA vez y se repite solo si quedo trabado:
        // repetirlas a cada rato hace que el personaje (o el auto) frene y arranque todo el tiempo.
        // ------------------------------------------------------------------
        static void Task(Member m, int mode, double now)
        {
            if (m.Mode != mode) m.ModeAt = now;
            m.Mode = mode;
            m.LastTask = now;
            // Mientras hace algo nuestro (correr, cubrirse, robar un auto, manejar, tirar) el juego no lo
            // interrumpe con sus reacciones (si no, cuando ve a la policia se queda quieto "en combate").
            // Solo el combate del juego (pelea, tiros desde el auto) usa las reacciones normales.
            if (m.G != null && m.G.Fighter) Block(m, mode != M_COMBAT && mode != M_DRIVEBY);
        }

        static void Block(Member m, bool on)
        {
            int v = on ? 1 : 0;
            if (m.Block == v || m.Ped == 0) return;
            SET_BLOCKING_OF_NON_TEMPORARY_EVENTS(m.Ped, on);
            m.Block = v;
        }

        static void Block(Cop c, bool on)
        {
            int v = on ? 1 : 0;
            if (c.Block == v || c.Ped == 0) return;
            SET_BLOCKING_OF_NON_TEMPORARY_EVENTS(c.Ped, on);
            c.Block = v;
        }

        void Wander(Member m, double now)
        {
            _TASK_WANDER_STANDARD(m.Ped);
            Task(m, M_WANDER, now);
            m.Target = 0;
            m.StillSince = -1;
        }

        /// <summary>Correr hasta un punto: por el camino de la vereda o derecho.</summary>
        void RunTo(Member m, Vector3 p, double now, bool straight)
        {
            if (straight) _TASK_GO_STRAIGHT_TO_COORD(m.Ped, p, 4);
            else _TASK_FOLLOW_NAV_MESH_TO_COORD(m.Ped, p, 4);
            Task(m, straight ? M_GOTO2 : M_GOTO, now);
            m.MoveTo = p;
            m.StillSince = -1;
        }

        void Shoot(Member m, int target, double now) { Shoot(m, target, now, G.Rand(3f, 5.5f)); }

        void Shoot(Member m, int target, double now, float seconds)
        {
            int ms = (int)(seconds * 1000f);
            _TASK_SHOOT_AT_CHAR(m.Ped, target, ms, 4);
            Task(m, M_SHOOT, now);
            m.Target = target;
            m.Phase = P_SHOOT;
            m.PhaseAt = now;
            m.PhaseLen = ms / 1000.0;
        }

        void Duck(Member m, double now)
        {
            int ms = (int)(G.Rand(1.2f, 2.4f) * 1000f);
            _TASK_DUCK(m.Ped, ms);
            Task(m, M_DUCK, now);
            m.Phase = P_DUCK;
            m.PhaseAt = now;
            m.PhaseLen = ms / 1000.0;
        }

        void Cruise(Member m, int car, float speed, double now, int mode)
        {
            _TASK_CAR_DRIVE_WANDER(m.Ped, car, speed, AiTaskPolicy.RoadDrivingStyle);
            bool again = m.Mode == mode; // destrabar: no reiniciar cuanto lleva trabado
            Task(m, mode, now);
            if (!again) m.StillSince = -1;
        }

        void EnterAsDriver(Member m, int car, double now)
        {
            _TASK_ENTER_CAR_AS_DRIVER(m.Ped, car, 0);
            Task(m, M_ENTER_CAR, now);
            m.StealCar = car;
            m.Seat = -1;
            m.StealSince = now;
        }

        void EnterAsPassenger(Member m, int car, int seat, double now)
        {
            _TASK_ENTER_CAR_AS_PASSENGER(m.Ped, car, 0, (uint)seat);
            Task(m, M_ENTER_CAR, now);
            m.StealCar = car;
            m.Seat = seat;
            m.StealSince = now;
        }

        void LeaveCar(Member m, double now)
        {
            _TASK_LEAVE_ANY_CAR(m.Ped);
            Task(m, M_LEAVE_CAR, now);
            m.Phase = P_NONE;
        }

        /// <summary>El que maneja se baja, y los de la banda que van con el tambien.</summary>
        void BailOut(Gang g, Member m, double now, string why)
        {
            int car = m.Car;
            LeaveCar(m, now);
            foreach (var o in g.Members)
                if (o != m && !o.Dead && o.InCar && o.Car == car && !o.Eating) LeaveCar(o, now);
            Say(m, why);
        }

        bool Moving(Member m, double now, float minSpeed)
        {
            float sp;
            if (m.InCar && m.Car != 0 && DOES_VEHICLE_EXIST(m.Car)) GET_CAR_SPEED(m.Car, out sp);
            else GET_CHAR_SPEED(m.Ped, out sp);
            if (sp >= minSpeed) { m.StillSince = -1; return true; }
            if (m.StillSince < 0) m.StillSince = now;
            return false;
        }

        static double StillFor(Member m, double now) { return m.StillSince < 0 ? 0 : now - m.StillSince; }

        static bool CarOk(int car)
        {
            return car != 0 && DOES_VEHICLE_EXIST(car) && !IS_CAR_DEAD(car) && !IS_CAR_ON_FIRE(car) &&
                   !IS_CAR_UPSIDEDOWN(car) && !IS_CAR_STUCK_ON_ROOF(car);
        }

        // ------------------------------------------------------------------
        // Que hace cada uno
        // ------------------------------------------------------------------
        void Think(Gang g, Member m, double now)
        {
            Moving(m, now, m.InCar ? 1.5f : 0.4f);
            RestoreVehicleWeapon(m);
            if (!m.InCar && m.Mode == M_ENTER_CAR)
            {
                bool usable = CarOk(m.StealCar);
                float distance = usable ? Vector3.Distance(G.CarPos(m.StealCar), m.Pos) : float.MaxValue;
                if (AiTaskPolicy.KeepVehicleEntry(usable, distance, now - m.StealSince, IS_CHAR_GETTING_IN_TO_A_CAR(m.Ped))) return;
            }
            if (m.Phase == P_SPECIAL && SpecialBusy(m, now)) return; // tirando una granada / un cohete
            if (m.EventKind != 0 && ThinkEvent(g, m, now)) return;   // 1.9: charla / carrera
            if (m.FunUntil > now && now - m.LastHit > 2) return;     // 1.9: comando divertido en curso
            if (m.Eating) { ThinkEat(g, m, now); return; }
            StepFollowerEncounter(g, m, now);
            if (m.InCar && m.Mode == M_LEAVE_CAR)
            {
                if (now - m.ModeAt > 4) LeaveCar(m, now); // no se bajo: otra vez
                return;
            }
            bool order = g.Order >= 0 && now < g.OrderUntil;
            if (m.Fleeing && g.Fighter) { ThinkFlee(g, m, now); return; }
            if (!g.Fighter)
            {
                m.Fleeing = false;
                if (!order && ThinkConvoy(g, m, now)) return;
                ThinkWalker(g, m, now); return;
            }
            if (g.Fighter && m.Health01 < NpcRoguePolicy.FleeThreshold(m.Build, fleeAt) && now - m.Created > 4 && now - m.FleeEnded > 30 &&
                !(order && (g.Order == GangRules.CmdAtacar || g.Order == GangRules.CmdBailar) && m.Health01 > 0.12f))
            {
                StartFlee(g, m, now);
                ThinkFlee(g, m, now);
                return;
            }
            if (m.InCar && !m.Driving) { ThinkPassenger(g, m, now); return; }
            if (order && DoOrder(g, m, now)) return;
            if (!order && ThinkConvoy(g, m, now)) return;
            if (!g.Fighter) { ThinkWalker(g, m, now); return; }

            float radius = m.InCar ? 45f : NpcRoguePolicy.SearchRadius(m.Build);
            Threat t = Sticky(g, m, NearestThreat(g, m, radius), radius);
            if (m.InCar)
            {
                if (g.IsFollower && ThinkFollowerDriver(g, m, t, now)) return;
                ThinkDriver(g, m, t, now); return;
            }
            if (m.Mode == M_LEAVE_CAR && now - m.ModeAt < 1.0) return; // terminando de bajarse
            if (t.Ped != 0) { Fight(g, m, t, now); return; }
            // la otra banda no esta tan lejos: ir corriendo (de auto en auto)
            Gang rival = Hunting(g, now) ? NearestRivalGang(g) : null;
            if (rival != null)
            {
                Member rm = NearestMemberOf(rival, m.Pos);
                float rd = rm != null ? Vector3.Distance(rm.Pos, m.Pos) : float.MaxValue;
                if (rd < 200f) { Fight(g, m, new Threat { Ped = rm.Ped, Dist = rd, Rival = rival, Pos = rm.Pos }, now); return; }
            }
            if (attackCivs)
            {
                float cd;
                int civ = NearestCivilian(m, 35f, out cd);
                if (civ != 0) { Fight(g, m, new Threat { Ped = civ, Dist = cd, Pos = G.CharPos(civ) }, now); return; }
            }
            // nadie cerca: se roba un auto (con la banda) y sale a dar vueltas o a buscar a la otra banda
            Roam(g, m, now);
        }

        /// <summary>Modo paseo: camina, o maneja tranquilo (frena en los semaforos) con la radio prendida.</summary>
        void ThinkWalker(Gang g, Member m, double now)
        {
            if (m.InCar)
            {
                if (!m.Driving) { if (m.Mode != M_RIDE) Task(m, M_RIDE, now); Say(m, "pasea en auto (acompanante)"); return; }
                int car = m.Car;
                if (!CarOk(car) || !paseoCar) { if (m.Mode != M_LEAVE_CAR) LeaveCar(m, now); return; }
                if (m.Mode != M_CRUISE) CruiseCalm(m, car, now);
                else if (!Moving(m, now, 1.5f))
                {
                    double still = StillFor(m, now);
                    if (still > 45) { LeaveCar(m, now); Say(m, "el auto quedo trabado: sigue a pie"); return; }
                    if (still > 12 && now - m.LastTask > 8) CruiseCalm(m, car, now); // no es un semaforo: destrabar
                }
                Say(m, "pasea en auto");
                return;
            }
            if (m.Mode == M_LEAVE_CAR && now - m.ModeAt < 1.0) return;
            if (m.Mode != M_WANDER || (!Moving(m, now, 0.3f) && StillFor(m, now) > 4)) Wander(m, now);
            Say(m, "paseando");
        }

        /// <summary>Manejar tranquilo, respetando el transito (para el que pasea).</summary>
        void CruiseCalm(Member m, int car, double now)
        {
            _TASK_CAR_DRIVE_WANDER(m.Ped, car, 15f, 0);
            bool again = m.Mode == M_CRUISE;
            Task(m, M_CRUISE, now);
            if (!again) m.StillSince = -1;
            SET_LOUD_VEHICLE_RADIO(car, true);
        }

        /// <summary>Maneja: da vueltas, sigue al jefe, va a buscar a la otra banda o se baja a pelear.</summary>
        void ThinkDriver(Gang g, Member m, Threat t, double now)
        {
            int car = m.Car;
            if (!CarOk(car)) { BailOut(g, m, now, "se bajo del auto"); return; }
            if (nativeAi && m.Mode == M_COMBAT)
            {
                Threat previous;
                if (ThreatOf(g, m, m.Target, out previous) && previous.Dist < 120)
                { NativeVehicleCombat(g, m, previous, now); return; }
            }
            if (nativeAi && t.Ped != 0 && t.Dist < 40)
            { NativeVehicleCombat(g, m, t, now); return; }
            if (chaseCars && ThinkChase(g, m, car, t, now)) return;
            bool runner = false;
            if (t.Ped != 0 && t.Dist < 38f && !runner)
            {
                BailOut(g, m, now, t.Cop ? "se baja a enfrentar a la policia" : "se baja a pelear con la banda de " + t.Rival.User);
                return;
            }
            Gang rival = Hunting(g, now) ? NearestRivalGang(g) : null;
            if (rival != null)
            {
                Member rm = NearestMemberOf(rival, m.Pos);
                if (rm != null)
                {
                    float d = Vector3.Distance(rm.Pos, m.Pos);
                    if (d < 45f)
                    {
                        if (nativeAi) NativeVehicleCombat(g, m, new Threat { Ped = rm.Ped, Dist = d, Pos = rm.Pos, Rival = rival }, now);
                        else BailOut(g, m, now, "llego a donde esta la banda de " + rival.User);
                        return;
                    }
                    if (WaitForCrew(g, m, car, now)) return;
                    bool redo = m.Mode != M_DRIVE_TO || m.Target != rm.Ped || (!Moving(m, now, 1.5f) && StillFor(m, now) > 6) || now - m.LastTask > 30;
                    if (redo)
                    {
                        _TASK_CAR_MISSION_PED_TARGET(m.Ped, car, rm.Ped, 4, Math.Max(fleeSpeed, 28f), AiTaskPolicy.RoadDrivingStyle, 15, 10);
                        Task(m, M_DRIVE_TO, now);
                        m.Target = rm.Ped;
                        m.StillSince = -1;
                    }
                    Say(m, "va en auto a buscar a la banda de " + rival.User, true);
                    return;
                }
            }
            if (WaitForCrew(g, m, car, now)) return;
            Member lead = g.Leader;
            if (lead != null && lead != m && lead.InCar && lead.Driving && lead.Car != car && CarOk(lead.Car))
            {
                // otro auto de la banda: seguir al jefe
                bool redo = m.Mode != M_FOLLOW || (!Moving(m, now, 1.5f) && StillFor(m, now) > 6);
                if (redo)
                {
                    _TASK_CAR_MISSION_PED_TARGET(m.Ped, car, lead.Ped, 2, fleeSpeed + 5f, AiTaskPolicy.RoadDrivingStyle, 10, 10);
                    Task(m, M_FOLLOW, now);
                    m.StillSince = -1;
                }
                Say(m, "sigue al auto de " + g.User);
                return;
            }
            if (!g.Wanted && !(g.Order >= 0 && now < g.OrderUntil))
            {
                if (gangTrips && m == lead && g.Fighter && g.Alive >= Math.Min(4, gangMax) && Trip(g, m, car, now)) return;
                if (carHop && TryHop(g, m, car, now)) return;
            }
            if (ambientPacing && !g.HostileNow && !g.Wanted && !runner)
            {
                if (m.Mode != M_CRUISE || (!Moving(m, now, 1.5f) && StillFor(m, now) > 20 && now - m.LastTask > 12))
                    CruiseCalm(m, car, now);
                Say(m, "recorre la ciudad con calma");
                return;
            }
            DriveAround(g, m, car, now, runner ? "se escapa en auto" : "da vueltas en un auto robado");
        }

        // ------------------------------------------------------------------
        // Persecuciones en auto entre bandas: una persigue, la otra escapa, y los de atras se tiran
        // ------------------------------------------------------------------
        /// <summary>El que maneja un auto sano en esa banda (null si van a pie).</summary>
        static Member DriverOf(Gang g)
        {
            foreach (var m in g.Members) if (!m.Dead && m.InCar && m.Driving && CarOk(m.Car)) return m;
            return null;
        }

        void StartChase(Gang chaser, Gang fleer, double now, bool random)
        {
            chaser.ChaseWith = fleer; chaser.ChaseRole = 1;
            fleer.ChaseWith = chaser; fleer.ChaseRole = 2;
            chaser.ChaseSince = fleer.ChaseSince = now;
            chaser.ChaseUntil = fleer.ChaseUntil = now + (random ? 75 : 90);
            chaser.ChaseStill = fleer.ChaseStill = -1;
            foreach (var m in chaser.Members) if (m.Driving) m.Mode = M_NONE;
            foreach (var m in fleer.Members) if (m.Driving) m.Mode = M_NONE;
            log("[NPC] persecucion en auto" + (random ? " (evento)" : "") + ": la banda de " + chaser.User + " persigue a la de " + fleer.User);
            Feed("PERSECUCION: " + chaser.User.ToUpperInvariant(), 3, fleer.User.ToUpperInvariant(), 1, now);
            chaser.Flash = "PERSIGUE A " + fleer.User.ToUpperInvariant(); chaser.FlashUntil = now + 4;
            fleer.Flash = "LO PERSIGUE " + chaser.User.ToUpperInvariant(); fleer.FlashUntil = now + 4;
        }

        void EndChase(Gang g, string why)
        {
            Gang o = g.ChaseWith;
            double cool = G.Now + 50; // un rato sin otra persecucion (que se bajen y se tiroteen)
            g.NoChaseUntil = cool;
            if (o != null) o.NoChaseUntil = cool;
            g.ChaseWith = null; g.ChaseRole = 0; g.ChaseUntil = -1;
            foreach (var m in g.Members) if (m.Driving && (m.Mode == M_CHASE || m.Mode == M_FLEE)) m.Mode = M_NONE;
            if (o != null && o.ChaseWith == g)
            {
                o.ChaseWith = null; o.ChaseRole = 0; o.ChaseUntil = -1;
                foreach (var m in o.Members) if (m.Driving && (m.Mode == M_CHASE || m.Mode == M_FLEE)) m.Mode = M_NONE;
            }
            if (why.Length > 0) log("[NPC] termina la persecucion de la banda de " + g.User + (o != null ? " y la de " + o.User : "") + ": " + why);
        }

        /// <summary>El que maneja, en una persecucion (o arrancando una: el rival va en auto y esta cerca). True = ya hizo algo.</summary>
        bool ThinkChase(Gang g, Member m, int car, Threat t, double now)
        {
            if (!g.Fighter) return false;
            Gang other = g.ChaseWith;
            if (other != null && (now > g.ChaseUntil || other.State != 1 || other.Alive == 0 || other.ChaseWith != g))
            {
                EndChase(g, now > g.ChaseUntil ? "se termino el tiempo" : "ya no esta");
                other = null;
            }
            if (other == null)
            {
                if (!Hunting(g, now) || (g.Wanted && t.Cop) || now < g.NoChaseUntil) return false;
                Gang rival = t.Rival ?? NearestRivalGang(g);
                if (rival == null || rival.ChaseWith != null || now < rival.NoChaseUntil || !rival.Fighter) return false;
                Member rd = DriverOf(rival);
                if (rd == null || FlatDist(rd.Pos, m.Pos) > 90f) return false;
                if (m.DriveSince > 0 && now - m.DriveSince < 3) return false; // recien subido
                StartChase(g, rival, now, false);
                other = rival;
            }
            Member od = DriverOf(other);
            if (od == null) { EndChase(g, "se bajaron del auto"); return false; }
            float dist = FlatDist(od.Pos, m.Pos);
            if (dist > 300f) { EndChase(g, "los perdio"); return false; }
            float mySp, otSp;
            GET_CAR_SPEED(car, out mySp);
            GET_CAR_SPEED(od.Car, out otSp);
            if (dist < 32f && mySp < 2f && otSp < 2f)
            {
                if (g.ChaseStill < 0) g.ChaseStill = now;
                else if (now - g.ChaseStill > 3)
                {
                    EndChase(g, "frenaron: se bajan a los tiros");
                    BailOut(g, m, now, "se baja a los tiros con la banda de " + other.User);
                    return true;
                }
            }
            else g.ChaseStill = -1;
            if (g.ChaseRole == 1)
            {
                // solo en el auto: de vez en cuando le tira desde la ventanilla
                bool alone = true;
                foreach (var o in g.Members) if (o != m && !o.Dead && o.InCar && o.Car == car) { alone = false; break; }
                if (alone && m.Armed && (dist < 35f || (m.Mode == M_DRIVEBY && m.Target == od.Ped && dist < 65f)))
                {
                    VehicleWeapon(m);
                    if (m.Mode != M_DRIVEBY || m.Target != od.Ped || (!IS_PED_IN_COMBAT(m.Ped) && now - m.LastTask >= 14))
                    {
                        SET_CHAR_WILL_USE_CARS_IN_COMBAT(m.Ped, true);
                        SET_CHAR_WILL_DO_DRIVEBYS(m.Ped, true);
                        SET_CHAR_WILL_LEAVE_CAR_IN_COMBAT(m.Ped, false);
                        _TASK_COMBAT(m.Ped, od.Ped); Task(m, M_DRIVEBY, now); m.Target = od.Ped;
                    }
                    Say(m, "le tira desde el auto a la banda de " + other.User, true);
                    return true;
                }
                bool redo = m.Mode != M_CHASE || m.Target != od.Ped ||
                    (!Moving(m, now, 1.5f) && StillFor(m, now) > 12 && now - m.LastTask > 12);
                if (redo)
                {
                    _TASK_CAR_MISSION_PED_TARGET(m.Ped, car, od.Ped, 2, fleeSpeed + 10f, AiTaskPolicy.RoadDrivingStyle, 5, 10);
                    Task(m, M_CHASE, now);
                    m.Target = od.Ped;
                    m.StillSince = -1;
                }
                Say(m, "persigue en auto a la banda de " + other.User, true);
            }
            else
            {
                bool redo = m.Mode != M_FLEE || (!Moving(m, now, 2f) && StillFor(m, now) > 4 && now - m.LastTask > 4);
                if (redo) Cruise(m, car, fleeSpeed + 12f, now, M_FLEE);
                Say(m, "escapa en auto de la banda de " + other.User, true);
            }
            return true;
        }

        /// <summary>Evento aleatorio: dos bandas que van en auto, una persigue a la otra (si estan lejos, aparece atras).</summary>
        void RandomChase(double now)
        {
            if (nextRandomChase < 0) { nextRandomChase = now + chaseEvery * G.Rand(0.6f, 1.0f); return; }
            if (now < nextRandomChase) return;
            nextRandomChase = now + 20; // si no se puede ahora, se prueba en un rato
            if (!randomChase || !chaseCars || !duel) return;
            if (ambientPacing)
            {
                nextRandomChase = now + chaseEvery;
                if (G.Rng.NextDouble() >= ambientChaseChance) return;
            }
            var ready = new List<Gang>();
            foreach (var g in gangs)
                if (g.State == 1 && g.Fighter && !g.IsFollower && !g.Truce && !IsInConvoy(g) && g.Alive > 0 &&
                    g.ChaseWith == null && now >= g.NoChaseUntil && DriverOf(g) != null && (!ambientPacing || !g.Wanted)) ready.Add(g);
            if (ready.Count < 2) return;
            G.Shuffle(ready);
            Gang fleer = ready[0], chaser = ready[1];
            if (PeacefulAlly(fleer, chaser)) return;
            Member fd = DriverOf(fleer), cd = DriverOf(chaser);
            if (ambientPacing && FlatDist(fd.Pos, cd.Pos) > 140) return;
            if (FlatDist(fd.Pos, cd.Pos) > 200f)
            {
                // el que persigue aparece unos metros atras (fuera de camara)
                int followed = dir.FollowedPed;
                foreach (var m in chaser.Members)
                    if (!m.Dead && (m.Ped == followed || (dir.Active && dir.InFrame(m.Head, 2f)))) return;
                Vector3 spot;
                float heading;
                if (!FindStreetNear(fd.Pos, 55f, 110f, new List<Vector3>(), out spot, out heading)) return;
                if (dir.Active && dir.InFrame(spot, 4f)) return;
                SET_CAR_COORDINATES(cd.Car, spot + new Vector3(0, 0, 0.5f));
                SET_CAR_HEADING(cd.Car, heading);
                chaser.LastWarp = now;
            }
            StartChase(chaser, fleer, now, true);
            nextRandomChase = now + chaseEvery * G.Rand(0.8f, 1.3f);
            if (FollowOnSpawn && dir.Active) dir.ObserveNpc(fd.Ped, 10, "persecucion", 35);
        }

        /// <summary>
        /// La banda completa (de a cuatro) cada tanto sale a recorrer la ciudad: maneja hasta otra zona
        /// (una de tus camaras, lejos) buscando a otros para tirotearse.
        /// </summary>
        bool Trip(Gang g, Member m, int car, double now)
        {
            if (!g.Tripping)
            {
                if (g.NextTrip < 0) g.NextTrip = now + G.Rand(20f, 45f);
                if (now < g.NextTrip) return false;
                Vector3 to;
                int shot = dir.FarShot(new List<Vector3> { m.Pos }, 300f, out to);
                if (shot < 0) { g.NextTrip = now + 60; return false; }
                g.Tripping = true;
                g.TripTo = to;
                g.TripUntil = now + 120;
                g.TripsMade++;
                m.Mode = M_NONE;
                log("[NPC] la banda de " + g.User + " sale a recorrer la ciudad buscando a otros (a " + FlatDist(m.Pos, to).ToString("0") + " m)");
            }
            float d = FlatDist(m.Pos, g.TripTo);
            if (d < 40f || now > g.TripUntil)
            {
                g.Tripping = false;
                g.NextTrip = now + G.Rand(60f, 120f);
                m.Mode = M_NONE;
                return false;
            }
            if (m.Mode != M_DRIVE_TO || (!Moving(m, now, 1.5f) && StillFor(m, now) > 6) || now - m.LastTask > 40)
            {
                _TASK_CAR_MISSION_COORS_TARGET(m.Ped, car, g.TripTo, 4, fleeSpeed, AiTaskPolicy.RoadDrivingStyle, 5, 10);
                Task(m, M_DRIVE_TO, now);
                m.Target = 0;
                m.StillSince = -1;
            }
            Say(m, "recorre la ciudad buscando a otros (" + d.ToString("0") + " m)", true);
            return true;
        }

        /// <summary>Despues de un rato manejando (sin policia), se bajan a robar otro auto.</summary>
        bool TryHop(Gang g, Member m, int car, double now)
        {
            if (m.HopAt < 0) { m.HopAt = now + G.Rand(45f, 80f); return false; }
            if (now < m.HopAt) return false;
            float sp;
            GET_CAR_SPEED(car, out sp);
            if (sp > 9f) return false; // que afloje primero (en una esquina, un semaforo)
            m.HopAt = -1;
            g.LeftCar = car;
            g.LeftCarAt = now;
            BailOut(g, m, now, "se baja a robar otro auto");
            return true;
        }

        void DriveAround(Gang g, Member m, int car, double now, string what)
        {
            Say(m, what);
            if (m.Mode != M_CRUISE) { Cruise(m, car, fleeSpeed, now, M_CRUISE); return; }
            if (Moving(m, now, 2f)) return;
            double still = StillFor(m, now);
            if (still > 10) { BailOut(g, m, now, "el auto quedo trabado: se baja"); return; }
            if (still > 4 && now - m.LastTask > 4) Cruise(m, car, fleeSpeed, now, M_CRUISE);
        }

        /// <summary>
        /// Recien robado el auto: el que maneja espera (hasta 9 s) a los de la banda que estan a pie cerca,
        /// para que se suban atras.
        /// </summary>
        bool WaitForCrew(Gang g, Member m, int car, double now)
        {
            if (m.DriveSince < 0 || now - m.DriveSince > 9) return false;
            bool waiting = false;
            foreach (var o in g.Members)
            {
                if (o == m || o.Dead || o.InCar || o.Fleeing || o.Eating) continue;
                if (Vector3.Distance(o.Pos, m.Pos) < 40f) { waiting = true; break; }
            }
            if (!waiting) return false;
            if (m.Mode != M_WAIT) { Task(m, M_WAIT, now); Say(m, "espera a la banda"); }
            return true;
        }

        /// <summary>Acompanante: tira desde el auto; si el que maneja se baja (o no es de la banda), se baja.</summary>
        void ThinkPassenger(Gang g, Member m, double now)
        {
            int car = m.Car;
            int drv = 0;
            bool carOk = CarOk(car);
            if (carOk) GET_DRIVER_OF_CAR(car, out drv);
            if (drv == m.Ped) { m.Driving = true; return; }
            Member mate = MemberByPed(g, drv);
            if (!carOk || drv == 0 || IS_CHAR_DEAD(drv) || (mate != null && (mate.Dead || mate.Eating || mate.Mode == M_LEAVE_CAR)))
            {
                LeaveCar(m, now);
                Say(m, "se baja del auto");
                return;
            }
            Threat t = Sticky(g, m, NearestThreat(g, m, 45f), 45f);
            if (t.Ped != 0 && m.Armed)
            {
                if (m.Mode != M_DRIVEBY || m.Target != t.Ped || (!IS_PED_IN_COMBAT(m.Ped) && now - m.LastTask > 14))
                {
                    VehicleWeapon(m);
                    SET_CHAR_WILL_LEAVE_CAR_IN_COMBAT(m.Ped, false);
                    SET_CHAR_WILL_DO_DRIVEBYS(m.Ped, true);
                    _TASK_COMBAT(m.Ped, t.Ped);
                    Task(m, M_DRIVEBY, now);
                    m.Target = t.Ped;
                }
                Say(m, "tira desde el auto", true);
                return;
            }
            if (m.Mode != M_RIDE && m.Mode != M_DRIVEBY) Task(m, M_RIDE, now);
            Say(m, "va en el auto de la banda");
        }

        /// <summary>
        /// Sin nadie cerca: subirse al auto de la banda si hay uno cerca, o robar uno (el primero que
        /// lo roba maneja y los demas se suben atras).
        /// </summary>
        void Roam(Gang g, Member m, double now)
        {
            if (m.Mode == M_ENTER_CAR)
            {
                bool ok = CarOk(m.StealCar);
                bool tooLong = now - m.StealSince > (m.Seat >= 0 ? 12 : 15);
                bool away = ok && Vector3.Distance(G.CarPos(m.StealCar), m.Pos) > 45f;
                bool seatGone = ok && m.Seat >= 0 && now - m.StealSince > 3 && !IS_CHAR_GETTING_IN_TO_A_CAR(m.Ped) &&
                                !IS_CAR_PASSENGER_SEAT_FREE(m.StealCar, (uint)m.Seat);
                if (ok && !tooLong && !away && !seatGone) return; // yendo / subiendose
                m.Mode = M_NONE;
                m.StealCar = 0;
                m.Seat = -1;
            }
            if (now - m.LastTask < 1.5) return;
            float gd;
            int gcar = GangCarFor(g, m, out gd);
            if (gcar == 0)
            {
                // otro de la banda esta robando un auto: ir con el y subirse cuando lo tenga
                foreach (var o in g.Members)
                {
                    if (o == m || o.Dead || o.Mode != M_ENTER_CAR || o.Seat >= 0 || !CarOk(o.StealCar)) continue;
                    Vector3 cp = G.CarPos(o.StealCar);
                    float cd = Vector3.Distance(cp, m.Pos);
                    if (cd > 50f) continue;
                    if (cd > 7f && (m.Mode != M_GOTO || now - m.LastTask > 3)) RunTo(m, cp, now, false);
                    Say(m, "acompana a la banda a robar un auto");
                    return;
                }
            }
            if (gcar != 0 && gd < 40f)
            {
                int seat = FreeSeat(g, gcar, m);
                if (seat >= 0)
                {
                    EnterAsPassenger(m, gcar, seat, now);
                    Say(m, "se sube al auto de la banda");
                    return;
                }
            }
            int car = NearestStealable(g, m, 50f);
            if (car != 0)
            {
                EnterAsDriver(m, car, now);
                Say(m, g.Alive > 1 ? "va a robar un auto para la banda" : "va a robar un auto");
                return;
            }
            if (m.Mode != M_WANDER || (!Moving(m, now, 0.3f) && StillFor(m, now) > 5)) Wander(m, now);
            Say(m, "busca un auto para robar");
        }

        /// <summary>Auto que maneja (o esta por robar) otro de la banda, para subirse.</summary>
        int GangCarFor(Gang g, Member who, out float dist)
        {
            dist = float.MaxValue;
            int best = 0;
            foreach (var o in g.Members)
            {
                if (o == who || o.Dead || o.Fleeing || o.Eating) continue;
                if (!o.InCar || !o.Driving) continue;
                int car = o.Car;
                if (!CarOk(car)) continue;
                float d = Vector3.Distance(G.CarPos(car), who.Pos);
                if (o.InCar)
                {
                    float sp;
                    GET_CAR_SPEED(car, out sp);
                    if (sp > 4f && d > 12f) continue; // va andando: no lo alcanza
                }
                if (d < dist) { dist = d; best = car; }
            }
            return best;
        }

        int FreeSeat(Gang g, int car, Member who)
        {
            int max;
            GET_MAXIMUM_NUMBER_OF_PASSENGERS(car, out max);
            max = Math.Max(0, Math.Min(3, max));
            for (int s = 0; s < max; s++)
            {
                if (!IS_CAR_PASSENGER_SEAT_FREE(car, (uint)s)) continue;
                bool taken = false;
                foreach (var o in g.Members)
                    if (o != who && !o.Dead && o.Mode == M_ENTER_CAR && o.StealCar == car && o.Seat == s) { taken = true; break; }
                if (!taken) return s;
            }
            return -1;
        }

        /// <summary>Un auto cerca para robar: sano, que no sea de la policia ni de otra banda ni ya elegido.</summary>
        int NearestStealable(Gang g, Member m, float radius)
        {
            int best = 0;
            float bd = radius;
            foreach (int car in G.VehiclesNear(m.Pos, radius, 12))
            {
                if (G.ProtectedCars.Contains(car) || IS_EMERGENCY_SERVICES_VEHICLE(car) || IS_CAR_ON_FIRE(car) || IS_CAR_UPSIDEDOWN(car)) continue;
                if (car == g.LeftCar && G.Now - g.LeftCarAt < 90) continue; // el que acaban de dejar: otro
                uint model;
                GET_CAR_MODEL(car, out model);
                if (IS_THIS_MODEL_A_HELI(model)) continue;
                bool taken = false;
                foreach (var gg in gangs)
                    foreach (var o in gg.Members) if (o != m && o.Mode == M_ENTER_CAR && o.StealCar == car) { taken = true; break; }
                if (taken) continue;
                float d = Vector3.Distance(G.CarPos(car), m.Pos);
                if (d < bd) { bd = d; best = car; }
            }
            return best;
        }

        Member MemberByPed(Gang g, int ped)
        {
            if (ped == 0) return null;
            foreach (var m in g.Members) if (m.Ped == ped) return m;
            return null;
        }

        // ------------------------------------------------------------------
        // Tiroteo a pie: correr hacia el (de auto en auto), tirar, cubrirse, agacharse...
        // ------------------------------------------------------------------
        /// <summary>Pelear: con la IA del juego (por defecto) o con la del mod.</summary>
        void Fight(Gang g, Member m, Threat t, double now)
        {
            if (t.Ped == 0 || !DOES_CHAR_EXIST(t.Ped) || IS_CHAR_DEAD(t.Ped))
            { m.Target = 0; m.Mode = M_NONE; return; }
            if (IS_PED_RAGDOLL(m.Ped) || IS_CHAR_GETTING_UP(m.Ped)) return;
            bool changed = m.Target != t.Ped;
            bool approaching = m.Mode == M_GOTO || m.Mode == M_GOTO2;
            if (t.Dist > CombatPolicy.SightRange)
            {
                if (changed || !approaching || AiTaskPolicy.RefreshPath(now - m.LastTask,
                    FlatDist(m.MoveTo, t.Pos), StillFor(m, now), FlatDist(m.Pos, m.MoveTo) < 2))
                    RunTo(m, t.Pos, now, approaching && StillFor(m, now) >= 4);
                m.Target = t.Ped;
                Say(m, "se acerca al rival", true); return;
            }
            if (SpecialBusy(m, now)) return;
            if (TrySpecial(g, m, t, now, false)) return;
            if (!nativeAi)
            {
                if (tactics && m.Armed) SkirmishDynamic(g, m, t, now);
                else Skirmish(g, m, t, now);
                return;
            }
            string who = t.Cop ? "la policia" : t.Rival != null ? "la banda de " + t.Rival.User : "la gente";
            if (!changed && m.Mode == M_SHOOT && now - m.LastTask < m.PhaseLen) return;
            if (changed) { m.Seen = false; m.LastSeen = -100; }
            if (now - m.LastSeen >= 1) { m.LastSeen = now; m.Seen = CanSee(m.Ped, m.Pos, t.Ped, t.Pos); }
            float speed; GET_CHAR_SPEED(m.Ped, out speed);
            var recovery = CombatPolicy.Decide(m.Armed, changed || m.Mode != M_COMBAT, IS_PED_IN_COMBAT(m.Ped),
                m.Seen, t.Dist, now - m.LastTask, now - m.LastShot, speed,
                IS_PED_IN_COVER(m.Ped) || IS_CHAR_DUCKING(m.Ped), true);
            if (recovery == CombatPolicy.Recovery.Advance)
            { Approach(m, t, now, false); m.Target = t.Ped; return; }
            if (recovery == CombatPolicy.Recovery.Engage)
            {
                SET_CHAR_WILL_ONLY_FIRE_WITH_CLEAR_LOS(m.Ped, true);
                _TASK_COMBAT(m.Ped, t.Ped);
                Task(m, M_COMBAT, now); m.Target = t.Ped; m.Phase = P_NONE;
            }
            Say(m, "a los tiros con " + who);
        }

        void Skirmish(Gang g, Member m, Threat t, double now)
        {
            int target = t.Ped;
            float d = t.Dist;
            string who = t.Cop ? "la policia" : t.Rival != null ? "la banda de " + t.Rival.User : "la gente";
            if (m.Target != target) { m.Target = target; m.Phase = P_NONE; m.Fails = 0; }
            if (!m.Armed)
            {
                // a las pinas: correr hasta el y pelear
                if (d > 3.5f)
                {
                    bool redo = (m.Mode != M_GOTO && m.Mode != M_GOTO2) || now - m.LastTask > 2.5;
                    if (redo) RunTo(m, t.Pos, now, m.Mode == M_GOTO && !Moving(m, now, 0.5f) && StillFor(m, now) > 1.5);
                    Say(m, "va a las pinas contra " + who);
                    return;
                }
                if (m.Mode != M_COMBAT || now - m.LastTask > 8) { _TASK_COMBAT(m.Ped, target); Task(m, M_COMBAT, now); }
                Say(m, "a las pinas con " + who);
                return;
            }
            if (now - m.LastSeen > 1.0)
            {
                m.LastSeen = now;
                m.Seen = now - m.LastShot < 1.5 || CanSee(m.Ped, m.Pos, target, t.Pos);
            }
            bool hit = now - m.LastHit < 1.5;
            // lejos, sin verlo, o "lo ve" pero no le puede tirar (algo en el medio): acercarse
            bool approach = d > 32f || (!m.Seen && now - m.LastShot > 4 && d > 7f) || (m.Fails >= 1 && now - m.LastShot > 6 && d > 10f);
            if (approach)
            {
                bool arrived = m.Phase == P_APPROACH && GangRules.Flat(m.Pos, m.MoveTo) < 1.6f;
                bool stuck = m.Phase == P_APPROACH && now - m.PhaseAt > 1.5 && !Moving(m, now, 0.5f) && StillFor(m, now) > 1.5;
                if (m.Phase != P_APPROACH || arrived || stuck || now - m.PhaseAt > 3.0) Approach(m, t, now, stuck);
                Say(m, "corre hacia " + who, true);
                return;
            }
            double inPhase = now - m.PhaseAt;
            switch (m.Phase)
            {
                case P_SHOOT:
                    if (hit && inPhase > 1.2) { TakeCover(m, t, now); break; }          // le pegaron: a cubrirse
                    if (inPhase > m.PhaseLen) { if (G.Rng.NextDouble() < 0.7) TakeCover(m, t, now); else Shoot(m, target, now); break; }
                    if (inPhase > 3.0 && now - m.LastShot > 3.0)
                    {
                        // no esta tirando: acercarse, o dejar que el juego pelee
                        m.Fails++;
                        if (m.Fails == 1 && d > 10f) Approach(m, t, now, false);
                        else
                        {
                            _TASK_COMBAT(m.Ped, target);
                            Task(m, M_COMBAT, now);
                            m.Phase = P_SHOOT;
                            m.PhaseAt = now;
                            m.PhaseLen = 5;
                        }
                    }
                    break;
                case P_COVER:
                    {
                        bool arrived = GangRules.Flat(m.Pos, m.MoveTo) < 1.4f;
                        bool stuck = inPhase > 1.5 && !Moving(m, now, 0.4f) && StillFor(m, now) > 1.0;
                        if (arrived || stuck || inPhase > 4.0)
                        {
                            if (m.CoverIsCar && arrived && G.Rng.NextDouble() < 0.6) Duck(m, now);
                            else Shoot(m, target, now);
                        }
                        break;
                    }
                case P_DUCK:
                    if (inPhase > m.PhaseLen) Shoot(m, target, now);
                    break;
                default:
                    Shoot(m, target, now);
                    break;
            }
            if (now - m.LastShot < 2) m.Fails = 0;
            switch (m.Phase)
            {
                case P_COVER: Say(m, m.CoverIsCar ? "se cubre detras de un auto" : "se mueve para todos lados", true); break;
                case P_DUCK: Say(m, "agachado detras de un auto", true); break;
                case P_APPROACH: Say(m, "corre hacia " + who, true); break;
                default: Say(m, "a los tiros con " + who); break;
            }
        }

        /// <summary>Lo ve: el juego dice que lo vio y no hay un edificio en el medio (mapa de alturas).</summary>
        static bool CanSee(int ped, Vector3 from, int target, Vector3 to)
        {
            // (de la 1.6.3 de GPT) muy lejos o muy arriba/abajo no cuenta como "lo ve" aunque el juego diga que si
            if (Vector3.Distance(from, to) > 80f || Math.Abs(from.Z - to.Z) > 8f) return false;
            return HAS_CHAR_SPOTTED_CHAR(ped, target);
        }

        // ------------------------------------------------------------------
        // Pelea dinamica (IA del mod): rafagas cortas, se mueven apuntando, se cubren detras de autos y
        // paredes, flanquean si no lo ven (no tiran a traves de las paredes) y son mas agresivos o mas
        // cuidadosos segun como vienen (vida, cuantos son, si les estan pegando).
        // ------------------------------------------------------------------
        void SkirmishDynamic(Gang g, Member m, Threat t, double now)
        {
            int target = t.Ped;
            float d = t.Dist;
            string who = t.Cop ? "la policia" : t.Rival != null ? "la banda de " + t.Rival.User : "la gente";
            if (m.Target != target) { m.Target = target; m.Phase = P_NONE; m.Fails = 0; m.NoSeeSince = -1; }
            if (now - m.LastSeen > 0.5)
            {
                m.LastSeen = now;
                // no tirarle a una pared: tiene que haber vista libre (sin edificios en el medio) y el juego
                // tiene que "verlo" (o ya le estaba tirando, o esta muy cerca)
                m.MapClear = ClearShot(m.Pos, t.Pos);
                bool spotted = false;
                try { spotted = HAS_CHAR_SPOTTED_CHAR(m.Ped, target); } catch { }
                m.Seen = m.MapClear && (spotted || now - m.LastShot < 1.2 || d < 12f);
                if (m.Seen) m.NoSeeSince = -1;
                else if (m.NoSeeSince < 0) m.NoSeeSince = now;
            }
            bool seen = m.Seen || (m.NoSeeSince > 0 && now - m.NoSeeSince < 0.8) ||
                        (m.MapClear && m.NoSeeSince > 0 && now - m.NoSeeSince > 2.5); // vista libre: que se de vuelta y tire
            bool hit = now - m.LastHit < 1.2;
            int stance = Stance(g, m, t, now);
            double inPhase = now - m.PhaseAt;

            // lejos: acercarse (de auto en auto)
            if (d > 34f || (!seen && d > 40f))
            {
                bool arrived = m.Phase == P_APPROACH && GangRules.Flat(m.Pos, m.MoveTo) < 1.6f;
                bool stuck = m.Phase == P_APPROACH && inPhase > 1.5 && !Moving(m, now, 0.5f) && StillFor(m, now) > 1.5;
                if (m.Phase != P_APPROACH || arrived || stuck || inPhase > 3.0) Approach(m, t, now, stuck);
                Say(m, "corre hacia " + who, true);
                return;
            }
            switch (m.Phase)
            {
                case P_SHOOT:
                    if (!seen) { Flank(m, t, now); break; }                                 // no lo ve: no tira a la pared
                    if (hit && inPhase > 0.7) { if (stance == 2) Strafe(m, t, now, false); else TakeCover(m, t, now, stance == 0); break; }
                    if (inPhase > m.PhaseLen) { AfterBurst(g, m, t, now, stance); break; }
                    if (inPhase > 2.5 && now - m.LastShot > 2.5)
                    {
                        // apunta pero no tira (algo en el medio): moverse
                        if (++m.Fails >= 2 && d < 12f) { _TASK_COMBAT(m.Ped, target); Task(m, M_COMBAT, now); m.Phase = P_SHOOT; m.PhaseAt = now; m.PhaseLen = 4; }
                        else Flank(m, t, now);
                    }
                    break;
                case P_MOVE:
                    {
                        bool arrived = GangRules.Flat(m.Pos, m.MoveTo) < 1.5f;
                        bool stuck = inPhase > 0.9 && !Moving(m, now, 0.4f) && StillFor(m, now) > 0.9;
                        if (stuck && !arrived && m.Mode == M_AIMMOVE && ++m.MoveFails >= 3 && !m.NoAimMove)
                        {
                            m.NoAimMove = true; // este juego no lo mueve apuntando: que corra
                            log("[NPC] " + Name(g, m) + ": moverse apuntando no anda, corre al costado");
                        }
                        if (arrived) m.MoveFails = 0;
                        if (arrived || stuck || inPhase > m.PhaseLen)
                        {
                            if (seen) Shoot(m, target, now, stance == 2 ? G.Rand(1.2f, 2.2f) : G.Rand(1.5f, 2.6f));
                            else Flank(m, t, now);
                        }
                        break;
                    }
                case P_COVER:
                    {
                        bool arrived = GangRules.Flat(m.Pos, m.MoveTo) < 1.4f;
                        bool stuck = inPhase > 1.5 && !Moving(m, now, 0.4f) && StillFor(m, now) > 1.0;
                        if (arrived || stuck || inPhase > 4.0)
                        {
                            if (m.CoverIsCar && arrived && G.Rng.NextDouble() < (stance == 0 ? 0.7 : 0.45)) Duck(m, now);
                            else if (seen) Shoot(m, target, now, G.Rand(1.4f, 2.4f));
                            else Flank(m, t, now);
                        }
                        break;
                    }
                case P_DUCK:
                    if (inPhase > m.PhaseLen) { if (seen) Shoot(m, target, now, G.Rand(1.4f, 2.4f)); else Flank(m, t, now); }
                    break;
                default:
                    if (seen) Shoot(m, target, now, G.Rand(1.3f, 2.3f));
                    else Flank(m, t, now);
                    break;
            }
            if (now - m.LastShot < 2) m.Fails = 0;
            switch (m.Phase)
            {
                case P_COVER: Say(m, m.CoverIsCar ? "se cubre detras de un auto" : "se cubre en una pared", true); break;
                case P_DUCK: Say(m, "agachado detras de un auto", true); break;
                case P_MOVE: Say(m, stance == 2 ? "avanza apuntando hacia " + who : "se mueve apuntando a " + who, true); break;
                case P_APPROACH: Say(m, "corre hacia " + who, true); break;
                default: Say(m, "a los tiros con " + who + (stance == 2 ? " (agresivo)" : stance == 0 ? " (con cuidado)" : "")); break;
            }
        }

        /// <summary>0 = con cuidado (poca vida, son menos, le estan pegando), 1 = normal, 2 = agresivo.</summary>
        int Stance(Gang g, Member m, Threat t, double now)
        {
            if (g.Order == GangRules.CmdAtacar && now < g.OrderUntil) return 2;
            int allies = 0, enemies = 0;
            foreach (var o in g.Members) if (!o.Dead && Vector3.Distance(o.Pos, m.Pos) < 40f) allies++;
            foreach (var og in gangs)
            {
                foreach (var c in og.Cops) if (c.Alive && Vector3.Distance(c.Pos, m.Pos) < 40f) enemies++;
                if (og == g || !og.Fighter || og.State != 1) continue;
                foreach (var om in og.Members) if (!om.Dead && Vector3.Distance(om.Pos, m.Pos) < 40f) enemies++;
            }
            return NpcRoguePolicy.Stance(m.Build, m.Health01, allies, enemies, now - m.LastHit < 1.5, g.Level);
        }

        /// <summary>Termino la rafaga: avanzar, correrse al costado, cubrirse o seguir tirando, segun como viene.</summary>
        void AfterBurst(Gang g, Member m, Threat t, double now, int stance)
        {
            double r = G.Rng.NextDouble();
            if (stance == 2)
            {
                if (r < 0.55 && t.Dist > 7f) Advance(m, t, now);
                else if (r < 0.8) Strafe(m, t, now, false);
                else Shoot(m, t.Ped, now, G.Rand(1.2f, 2.0f));
            }
            else if (stance == 1)
            {
                if (r < 0.4) Strafe(m, t, now, false);
                else if (r < 0.75) TakeCover(m, t, now, false);
                else Shoot(m, t.Ped, now, G.Rand(1.4f, 2.4f));
            }
            else
            {
                if (r < 0.75) TakeCover(m, t, now, true);
                else Strafe(m, t, now, true);
            }
        }

        /// <summary>Moverse a un punto apuntando (o corriendo, si en este juego no anda).</summary>
        void MoveAiming(Member m, Vector3 dest, Threat t, double now, bool run, double maxTime)
        {
            dest = Ground(dest, m.Pos.Z);
            if (m.NoAimMove || !aimMove) RunTo(m, dest, now, true); // 1.9: apagado por defecto (a veces apuntaban al cielo)
            else
            {
                _TASK_GO_TO_COORD_WHILE_AIMING(m.Ped, dest, run ? 4 : 2, t.Pos + new Vector3(0, 0, 0.6f), t.Ped);
                Task(m, M_AIMMOVE, now);
                m.MoveTo = dest;
                m.StillSince = -1;
            }
            m.Phase = P_MOVE;
            m.PhaseAt = now;
            m.PhaseLen = maxTime;
        }

        /// <summary>Un lugar al que se puede ir: no adentro de un edificio y sin paredes en el medio.</summary>
        /// <summary>Abajo de algo (vias elevadas, autopista, puente): ahi el mapa de alturas no sirve.</summary>
        static bool UnderStructure(Vector3 p)
        {
            float tz;
            return G.TopZ(p.X, p.Y, out tz) && tz > p.Z + 3f;
        }

        static bool Walkable(Vector3 from, Vector3 to)
        {
            if (UnderStructure(from)) return true;
            float tz;
            if (G.TopZ(to.X, to.Y, out tz) && tz > to.Z + 2.5f) return false; // adentro de un edificio (o abajo de un puente)
            Vector3 hit;
            return !G.Raycast(from + new Vector3(0, 0, 0.8f), to + new Vector3(0, 0, 0.8f), out hit);
        }

        static bool ClearShot(Vector3 from, Vector3 target)
        {
            if (UnderStructure(from) && UnderStructure(target)) return true; // los dos abajo de las vias: decide el juego
            Vector3 a = from + new Vector3(0, 0, 0.8f), b = target + new Vector3(0, 0, 0.8f);
            float len = Vector3.Distance(a, b);
            if (len < 4f) return true;
            Vector3 hit;
            return !G.Raycast(Vector3.Lerp(a, b, 1.5f / len), Vector3.Lerp(a, b, 1f - 1.5f / len), out hit);
        }

        /// <summary>Correrse al costado apuntando ('back' = un poco para atras).</summary>
        void Strafe(Member m, Threat t, double now, bool back)
        {
            float sign = G.Rng.Next(2) == 0 ? -1f : 1f;
            for (int i = 0; i < 2; i++, sign = -sign)
            {
                Vector3 p = Ground(GangRules.Strafe(m.Pos, t.Pos, sign * G.Rand(4f, 7f), back ? -G.Rand(2f, 4f) : G.Rand(-1f, 2f)), m.Pos.Z);
                if (!Walkable(m.Pos, p)) continue;
                MoveAiming(m, p, t, now, true, 2.6);
                return;
            }
            Shoot(m, t.Ped, now, G.Rand(1.2f, 2.0f));
        }

        /// <summary>Avanzar apuntando hacia el (agresivo), un poco en zigzag.</summary>
        void Advance(Member m, Threat t, double now)
        {
            float step = Math.Min(t.Dist - 4f, G.Rand(4f, 7f));
            Vector3 p = Ground(GangRules.Strafe(m.Pos, t.Pos, G.Rand(-3f, 3f), step), m.Pos.Z);
            if (Walkable(m.Pos, p)) { MoveAiming(m, p, t, now, true, 2.8); return; }
            Strafe(m, t, now, false);
        }

        /// <summary>No lo ve (hay una pared): buscar al costado un lugar desde donde si lo vea; si no hay, acercarse.</summary>
        void Flank(Member m, Threat t, double now)
        {
            float[,] offs = { { 5f, 1f }, { -5f, 1f }, { 9f, 2f }, { -9f, 2f }, { 6f, 5f }, { -6f, 5f }, { 4f, -3f }, { -4f, -3f } };
            Vector3 best = Vector3.Zero;
            float bd = float.MaxValue;
            for (int i = 0; i < offs.GetLength(0); i++)
            {
                Vector3 p = Ground(GangRules.Strafe(m.Pos, t.Pos, offs[i, 0], offs[i, 1]), m.Pos.Z);
                if (!Walkable(m.Pos, p) || !ClearShot(p, t.Pos)) continue;
                float dd = GangRules.Flat(p, m.Pos);
                if (dd < bd) { bd = dd; best = p; }
            }
            if (bd < float.MaxValue) { MoveAiming(m, best, t, now, true, 3.0); return; }
            Approach(m, t, now, false);
        }

        // ------------------------------------------------------------------
        // Granadas, molotov y RPG (de la banda): de vez en cuando, si lo ve y esta a buena distancia
        // ------------------------------------------------------------------
        bool SpecialBusy(Member m, double now)
        {
            if (m.Phase != P_SPECIAL) return false;
            if (now - m.PhaseAt < m.PhaseLen) return true;
            // termino: vuelve a su arma
            if (m.Weapon > 0) SET_CURRENT_CHAR_WEAPON(m.Ped, m.Weapon, true);
            m.Phase = P_NONE;
            m.Mode = M_NONE;
            return false;
        }

        /// <summary>
        /// 1.9: municion limitada. Al recibir un arma se le dejan N cargadores; cuando se le acaban saca la
        /// pistola (una vez) y despues pelea a las pinas. Las mejoras / niveles lo recargan.
        /// </summary>
        void CheckAmmo(Gang g, Member m, double now)
        {
            if (m.Phase == P_SPECIAL || m.SpecialUntil > 0) return;
            if (CheckVehicleAmmo(g, m, now)) return;
            int w = m.Weapon;
            int clip = GangLevels.ClipSize(w);
            if (!m.Armed || clip <= 0) { m.Ammo = -1; return; }
            if (m.AmmoCapped != w)
            {
                m.AmmoCapped = w;
                float ammoShare = m.Index > 0 ? 0.65f : g.IsFollower ? 0.45f : 1f;
                m.AmmoMax = (int)(clip * mags * (w == 7 && m.PistolFallback ? 0.5f : g.AmmoMul) * ammoShare);
                try { SET_CHAR_AMMO(m.Ped, w, m.AmmoMax); } catch { }
                m.Ammo = m.AmmoMax;
                return;
            }
            int ammo = m.Ammo;
            try { GET_AMMO_IN_CHAR_WEAPON(m.Ped, w, out ammo); } catch { return; }
            m.Ammo = ammo;
            if (ammo > 0) return;
            if (w != 7 && !m.PistolFallback)
            {
                m.PistolFallback = true;
                m.Primary = w;
                GIVE_WEAPON_TO_CHAR(m.Ped, 7, 1, false);
                SET_CURRENT_CHAR_WEAPON(m.Ped, 7, true);
                m.Weapon = 7;
                m.AmmoCapped = -1;
                m.Flash = "SIN BALAS: PISTOLA";
                m.FlashUntil = now + 2.5;
                Say(m, "se quedo sin balas: saca la pistola");
                log("[NPC] " + Name(g, m) + " se quedo sin balas de " + GangLevels.WeaponName(w) + ": pistola");
                return;
            }
            if (m.Primary == 0 && w > 0) m.Primary = w;
            SET_CURRENT_CHAR_WEAPON(m.Ped, 0, true);
            m.Weapon = 0;
            m.Armed = false;
            m.Ammo = 0;
            m.Flash = "SIN BALAS";
            m.FlashUntil = now + 2.5;
            Say(m, "sin balas: a las pinas");
            log("[NPC] " + Name(g, m) + " se quedo sin balas: pelea a las pinas");
        }

        void ArmorCar(int car)
        {
            try { if (car != 0 && DOES_VEHICLE_EXIST(car)) { SET_CAR_PROOFS(car, true, false, false, false, false); SET_CAR_CAN_BE_VISIBLY_DAMAGED(car, false); } } catch { }
        }

        /// <summary>Por que no conviene tirar el explosivo (null = se puede).</summary>
        string FriendlyRisk(Gang g, Member m, Vector3 target, int w)
        {
            float blast = w == GangLevels.Rpg ? 10f : w == GangLevels.Grenade ? 8f : 7f;
            foreach (var o in g.Members)
            {
                if (o == m || o.Dead) continue;
                if (Vector3.Distance(o.Pos, target) < blast) return "hay uno de la banda cerca del objetivo";
                if (Vector3.Distance(o.Pos, m.Pos) < 3.5f) return "tiene uno de la banda pegado";
                if (w == GangLevels.Rpg && MathX.SegmentDistance(o.Pos, m.Pos, target) < 3f) return "hay uno de la banda en el camino";
            }
            return null;
        }

        static bool ExplosionNear(Vector3 p, float r)
        {
            try
            {
                for (int type = 0; type <= 3; type++) if (IS_EXPLOSION_IN_SPHERE(type, p, r)) return true;
            }
            catch { }
            return false;
        }

        /// <summary>
        /// 1.9: a veces el NPC no tira la granada / molotov (se queda quieto) o el cohete del RPG desaparece.
        /// Despues de un rato vemos si gasto el explosivo y si exploto algo; si no, lo simulamos en el objetivo.
        /// </summary>
        void CheckSpecial(Gang g, Member m, double now)
        {
            if (m.SpecialResolved) return;
            int w = m.SpecialWeapon;
            Vector3 tp = m.SpecialPos;
            try { if (m.SpecialTarget != 0 && DOES_CHAR_EXIST(m.SpecialTarget)) tp = G.CharPos(m.SpecialTarget); } catch { }
            float dist = Vector3.Distance(m.Pos, tp);
            // mientras tanto, cualquier explosion cerca del objetivo (o en el camino del cohete) cuenta
            if (!m.SpecialBoom && now - m.SpecialAt > 0.3)
                m.SpecialBoom = ExplosionNear(tp, 12f) || ExplosionNear(m.SpecialPos, 12f) ||
                                (w == GangLevels.Rpg && ExplosionNear(Vector3.Lerp(m.Pos, tp, 0.5f), dist * 0.5f + 4f));
            if (m.SpecialBoom) { m.SpecialResolved = true; log("[NPC] " + Name(g, m) + ": " + GangLevels.WeaponName(w) + " exploto bien (" + dist.ToString("0") + " m)"); return; }
            double wait = w == GangLevels.Rpg ? 2.2 : 1.6;
            if (now - m.SpecialAt < wait) return;
            int ammo = m.SpecialAmmo;
            try { GET_AMMO_IN_CHAR_WEAPON(m.Ped, w, out ammo); } catch { }
            bool used = ammo < m.SpecialAmmo;
            // una granada / molotov que si salio puede tardar en explotar: esperar mas antes de simularla
            if (used && w != GangLevels.Rpg && now - m.SpecialAt < 4.5) return;
            m.SpecialResolved = true;
            bool boom = false;
            string wn = GangLevels.WeaponName(w);
            if (boom) { log("[NPC] " + Name(g, m) + ": " + wn + " exploto bien (" + dist.ToString("0") + " m)"); return; }
            if (!fakeExplosives) { log("[NPC] " + Name(g, m) + ": " + wn + (used ? " se perdio (no exploto)" : " no salio (se quedo quieto)")); return; }
            string why = FriendlyRisk(g, m, tp, w);
            if (why == null && dist < (w == GangLevels.Rpg ? 10f : 8f)) why = "el objetivo esta pegado al que lo tiro";
            if (why != null) { log("[NPC] " + Name(g, m) + ": " + wn + " no salio y no se simula: " + why); return; }
            // un poco de error, apoyado en el suelo
            Vector3 at = tp + new Vector3(G.Rand(-1.5f, 1.5f), G.Rand(-1.5f, 1.5f), 0f);
            float gz;
            if (G.GroundZ(at, out gz) && Math.Abs(gz - tp.Z) < 3f) at.Z = gz;
            int type = w == GangLevels.Rpg ? 2 : w == GangLevels.Grenade ? 0 : 1;
            try
            {
                if (used || ammo > 0) SET_CHAR_AMMO(m.Ped, w, Math.Max(0, ammo - 1));
                ADD_EXPLOSION(at, type, w == GangLevels.Molotov ? 4f : 6f, true, false, 0.4f);
            }
            catch { }
            log("[NPC] " + Name(g, m) + ": " + wn + (used ? " se perdio" : " no salio") + " -> explosion simulada en el objetivo (" + dist.ToString("0") + " m)");
        }

        bool TrySpecial(Gang g, Member m, Threat t, double now, bool fromCar)
        {
            if (!useSpecials || !g.Fighter || m.InCar || t.Ped == 0 || now - g.LastSpecial < 8 || g.Grenades + g.Molotovs + g.Rockets <= 0) return false;
            if (!m.SpecialResolved) return false;
            if (G.Rng.NextDouble() > 0.3) return false;
            bool targetInCar = false;
            try { targetInCar = IS_CHAR_IN_ANY_CAR(t.Ped); } catch { }
            int w = 0;
            int rockets = useRockets ? g.Rockets : 0, grenades = useGrenades ? g.Grenades : 0, molotovs = useMolotovs ? g.Molotovs : 0;
            if (rockets > 0 && t.Dist > 24f && t.Dist < 85f && (targetInCar || grenades + molotovs == 0 || G.Rng.NextDouble() < 0.5)) w = GangLevels.Rpg;
            else if (grenades > 0 && t.Dist > 10f && t.Dist < 30f) w = GangLevels.Grenade;
            else if (molotovs > 0 && t.Dist > 9f && t.Dist < 26f) w = GangLevels.Molotov;
            if (w == 0) return false;
            if (!CanSee(m.Ped, m.Pos, t.Ped, t.Pos)) return false;
            // que no le caiga a uno de la banda: ni cerca del objetivo, ni en el camino, ni pegado al que tira
            string why = FriendlyRisk(g, m, t.Pos, w);
            if (why != null)
            {
                if (diag && now - g.LastSpecial > 8) { g.LastSpecial = now - 4; log("[NPC] " + Name(g, m) + " no tira " + GangLevels.WeaponName(w) + ": " + why); }
                return false;
            }
            GIVE_WEAPON_TO_CHAR(m.Ped, w, 1, false);
            SET_CURRENT_CHAR_WEAPON(m.Ped, w, true);
            _TASK_SHOOT_AT_CHAR(m.Ped, t.Ped, 1700, 4);
            int ammo0 = 1;
            try { GET_AMMO_IN_CHAR_WEAPON(m.Ped, w, out ammo0); } catch { ammo0 = 1; }
            m.SpecialAt = now;
            m.SpecialTarget = t.Ped;
            m.SpecialPos = t.Pos;
            m.SpecialAmmo = ammo0;
            m.SpecialResolved = false;
            m.SpecialBoom = false;
            Task(m, M_SPECIAL, now);
            m.Phase = P_SPECIAL;
            m.PhaseAt = now;
            m.PhaseLen = 2.3;
            m.SpecialWeapon = w;
            m.SpecialUntil = now + 2.4;
            g.LastSpecial = now;
            if (w == GangLevels.Rpg) g.Rockets--; else if (w == GangLevels.Grenade) g.Grenades--; else g.Molotovs--;
            string name = w == GangLevels.Rpg ? "RPG" : w == GangLevels.Grenade ? "GRANADA" : "MOLOTOV";
            m.Flash = name + "!";
            m.FlashUntil = now + 2.5;
            Say(m, "tira " + (w == GangLevels.Rpg ? "un cohete" : w == GangLevels.Grenade ? "una granada" : "una molotov"));
            return true;
        }

        /// <summary>Acercarse: de auto en auto si hay (moverse por donde hay autos), si no, corriendo derecho.</summary>
        void Approach(Member m, Threat t, double now, bool stuck)
        {
            float myDist = GangRules.Flat(m.Pos, t.Pos);
            m.Phase = P_APPROACH;
            m.PhaseAt = now;
            if (myDist < 70f && !stuck)
            {
                float bs = float.MaxValue;
                Vector3 bp = Vector3.Zero;
                foreach (int car in G.VehiclesNear(m.Pos, 24f, 10, true))
                {
                    Vector3 cp = G.CarPos(car);
                    float s = GangRules.CoverScore(m.Pos, cp, t.Pos, 24f, true);
                    if (s == float.MaxValue) continue;
                    Vector3 spot = GangRules.CoverBehind(cp, t.Pos, 2.3f);
                    if (GangRules.Flat(spot, t.Pos) > myDist - 4f) continue; // tiene que acercarlo
                    if (s < bs) { bs = s; bp = spot; }
                }
                if (bs < float.MaxValue)
                {
                    bp = Ground(bp, m.Pos.Z);
                    RunTo(m, bp, now, GangRules.Flat(m.Pos, bp) < 14f);
                    m.CoverIsCar = true;
                    return;
                }
            }
            RunTo(m, t.Pos, now, stuck && m.Mode == M_GOTO);
            m.CoverIsCar = false;
        }

        /// <summary>Cubrirse: detras del auto mas conveniente (del lado contrario a la amenaza) o moverse al costado.</summary>
        void TakeCover(Member m, Threat t, double now) { TakeCover(m, t, now, false); }

        void TakeCover(Member m, Threat t, double now, bool careful)
        {
            float bs = float.MaxValue;
            Vector3 bp = Vector3.Zero;
            foreach (int car in G.VehiclesNear(m.Pos, 18f, 10, true))
            {
                Vector3 cp = G.CarPos(car);
                float s = GangRules.CoverScore(m.Pos, cp, t.Pos, 16f, t.Cop);
                if (s < bs) { bs = s; bp = GangRules.CoverBehind(cp, t.Pos, 2.3f); }
            }
            m.Phase = P_COVER;
            m.PhaseAt = now;
            if (bs < float.MaxValue)
            {
                RunTo(m, Ground(bp, m.Pos.Z), now, true);
                Task(m, M_COVER, now);
                m.CoverIsCar = true;
                return;
            }
            m.CoverIsCar = false;
            if (gameCover && G.Rng.NextDouble() < (tactics ? (careful ? 1.0 : 0.75) : 0.5))
            {
                _TASK_SEEK_COVER_FROM_PED(m.Ped, t.Ped, 5000);
                Task(m, M_SEEK_COVER, now);
                m.MoveTo = new Vector3(1e6f, 1e6f, 0f); // termina por tiempo
                if (!loggedGameCover) { loggedGameCover = true; log("[NPC] sin autos cerca: usa la cobertura del juego"); }
                return;
            }
            float side = G.Rand(4f, 7f) * (G.Rng.Next(2) == 0 ? -1f : 1f);
            float fwd = t.Cop ? G.Rand(1f, 4f) : G.Rand(-2f, 2f);
            RunTo(m, Ground(GangRules.Strafe(m.Pos, t.Pos, side, fwd), m.Pos.Z), now, true);
            Task(m, M_STRAFE, now);
        }

        // ------------------------------------------------------------------
        // Poca vida: buscar un auto, escaparse y comprarse comida
        // ------------------------------------------------------------------
        void StartFlee(Gang g, Member m, double now)
        {
            m.Fleeing = true;
            m.FleeSince = now;
            m.CalmSince = -1;
            m.Phase = P_NONE;
            m.DriveSince = -1;
            log("[NPC] " + Name(g, m) + " tiene poca vida (" + (int)(m.Health01 * 100) + "%): busca un auto para escapar");
        }

        void ThinkFlee(Gang g, Member m, double now)
        {
            Threat t = NearestThreat(g, m, 70f);
            if (m.InCar)
            {
                if (!m.Driving) { ThinkPassenger(g, m, now); return; } // va con otro de la banda
                int car = m.Car;
                if (!CarOk(car)) { BailOut(g, m, now, "se le rompio el auto"); return; }
                if (m.Mode != M_FLEE)
                {
                    Cruise(m, car, fleeSpeed + 8f, now, M_FLEE);
                    m.DriveSince = now;
                }
                else if (!Moving(m, now, 2f))
                {
                    double still = StillFor(m, now);
                    if (still > 9) { BailOut(g, m, now, "quedo trabado: escapa a pie"); return; }
                    if (still > 4 && now - m.LastTask > 4) Cruise(m, car, fleeSpeed + 8f, now, M_FLEE);
                }
                if (t.Ped == 0) { if (m.CalmSince < 0) m.CalmSince = now; }
                else m.CalmSince = -1;
                if (m.DriveSince < 0) m.DriveSince = now;
                double driving = now - m.DriveSince;
                if (buyFood)
                {
                    if ((m.CalmSince > 0 && now - m.CalmSince > 6 && driving > 12) || driving > 45) { StartEat(g, m, now); return; }
                }
                else if ((m.Health01 >= 0.9f && driving > 6) || driving > 70) { EndFlee(g, m, now, healInCar ? "SE CURO MANEJANDO" : ""); return; }
                Say(m, healInCar ? "escapa en auto y se cura manejando" : "escapa en auto con poca vida");
                return;
            }
            if (m.Mode == M_LEAVE_CAR && now - m.ModeAt < 1.0) return;
            // a pie: conseguir un auto
            if (m.Mode == M_ENTER_CAR)
            {
                bool ok = CarOk(m.StealCar) && now - m.StealSince < 14 && Vector3.Distance(G.CarPos(m.StealCar), m.Pos) < 45f;
                if (ok) return;
                m.Mode = M_NONE;
                m.StealCar = 0;
                m.Seat = -1;
            }
            if (now - m.LastTask < 1.5) return;
            int steal = NearestStealable(g, m, 60f);
            if (steal != 0) { EnterAsDriver(m, steal, now); Say(m, "busca un auto para escapar"); return; }
            if (t.Ped == 0)
            {
                // nadie cerca y sin autos: come aca nomas (o sigue, y se cura cuando consiga un auto)
                if (m.CalmSince < 0) m.CalmSince = now;
                if (now - m.CalmSince > 4) { if (buyFood) StartEat(g, m, now); else EndFlee(g, m, now, ""); return; }
            }
            else m.CalmSince = -1;
            if (t.Ped != 0 && (m.Mode != M_FLEE || now - m.LastTask > 5))
            {
                _TASK_SMART_FLEE_CHAR(m.Ped, t.Ped, 80f, 5000);
                Task(m, M_FLEE, now);
            }
            Say(m, "escapa a pie con poca vida");
        }

        void EndFlee(Gang g, Member m, double now, string flash)
        {
            m.Fleeing = false;
            m.FleeEnded = now;
            m.Phase = P_NONE;
            if (m.Mode != M_ENTER_CAR && m.Mode != M_LEAVE_CAR) m.Mode = M_NONE;
            m.CalmSince = -1;
            if (flash.Length > 0) { m.Flash = flash; m.FlashUntil = now + 3.5; }
            log("[NPC] " + Name(g, m) + " deja de escapar (" + (int)(m.Health01 * 100) + "% de vida)");
        }

        /// <summary>Se baja (con los de la banda que van en el auto) y se va a comprar comida: vuelve con toda la vida.</summary>
        void StartEat(Gang g, Member m, double now)
        {
            var who = new List<Member> { m };
            if (m.InCar && m.Driving)
                foreach (var o in g.Members) if (o != m && !o.Dead && !o.Eating && o.InCar && o.Car == m.Car) who.Add(o);
            foreach (var x in who)
            {
                x.Eating = true;
                x.EatStarted = false;
                x.EatUntil = now + eatSeconds + 6;
                x.Phase = P_NONE;
                if (x.InCar) LeaveCar(x, now);
                Say(x, "se fue a comprar comida");
            }
            if (now - g.LastEatNotice > 20)
            {
                g.LastEatNotice = now;
                notify(DisplayName(g, m) + " -> SE FUE A COMPRAR COMIDA");
            }
        }

        void ThinkEat(Gang g, Member m, double now)
        {
            if (m.InCar)
            {
                if (now - m.LastTask > 4) LeaveCar(m, now);
                return;
            }
            if (!m.EatStarted)
            {
                m.EatStarted = true;
                m.EatUntil = now + eatSeconds;
                _TASK_WANDER_STANDARD(m.Ped); // va caminando hasta el local
                Task(m, M_EAT, now);
            }
            // si le pegan mientras come y la policia esta encima, sigue escapando
            if (now - m.LastHit < 0.6 && now - m.ModeAt > 1)
            {
                Threat t = NearestThreat(g, m, 40f);
                if (t.Ped != 0)
                {
                    m.Eating = false;
                    if (m.Health01 < fleeAt) { m.Fleeing = true; m.FleeSince = now; }
                    Say(m, "lo interrumpieron mientras comia");
                    return;
                }
            }
            if (now < m.EatUntil) { Say(m, "comprando comida", true); return; }
            SET_CHAR_HEALTH(m.Ped, m.StartHealth);
            ADD_ARMOUR_TO_CHAR(m.Ped, 50);
            m.LastHp = m.StartHealth;
            m.Health01 = 1f;
            m.Eating = false;
            m.Fleeing = false;
            m.Phase = P_NONE;
            m.Mode = M_NONE;
            m.CalmSince = -1;
            m.Flash = "COMIO: VIDA LLENA";
            m.FlashUntil = now + 4;
            log("[NPC] " + Name(g, m) + " comio y vuelve con toda la vida");
            Say(m, "comio y vuelve con toda la vida");
        }

        // ------------------------------------------------------------------
        // A quien atacar
        // ------------------------------------------------------------------
        Threat NearestThreat(Gang g, Member m, float radius)
        {
            var t = new Threat { Dist = radius };
            float bestScore = radius + 100;
            foreach (var og in gangs)
                foreach (var c in og.Cops)
                {
                    if (!c.Alive) continue;
                    if (ambientPacing && !g.Wanted && !g.HostileNow && G.Now - c.LastShot > 6 && G.Now - m.LastHit > 6) continue;
                    float d = Vector3.Distance(c.Pos, m.Pos);
                    if (d >= radius) continue;
                    float score = RogueThreatScore(m, c.Ped, d, 1);
                    if (score < bestScore) { bestScore = score; t.Dist = d; t.Ped = c.Ped; t.Cop = true; t.Rival = null; t.Pos = c.Pos; }
                }
            foreach (int p in g.Tasked)
            {
                if (!DOES_CHAR_EXIST(p) || IS_CHAR_DEAD(p)) continue;
                Vector3 pp = G.CharPos(p);
                float d = Vector3.Distance(pp, m.Pos);
                if (d >= radius) continue;
                float score = RogueThreatScore(m, p, d, 1);
                if (score < bestScore) { bestScore = score; t.Dist = d; t.Ped = p; t.Cop = true; t.Rival = null; t.Pos = pp; }
            }
            if (Hunting(g, G.Now) && g.Fighter)
                foreach (var og in gangs)
                {
                    if (og == g || !og.Fighter || og.State != 1 || PeacefulAlly(g, og)) continue;
                    foreach (var om in og.Members)
                    {
                        if (om.Dead) continue;
                        float d = Vector3.Distance(om.Pos, m.Pos);
                        if (d >= radius) continue;
                        int nearby = 0;
                        foreach (var ally in og.Members) if (!ally.Dead && Vector3.Distance(ally.Pos, om.Pos) < 20f) nearby++;
                        float score = RogueThreatScore(m, om.Ped, d, nearby);
                        if (score < bestScore) { bestScore = score; t.Dist = d; t.Ped = om.Ped; t.Cop = false; t.Rival = og; t.Pos = om.Pos; }
                    }
                }
            return t;
        }

        /// <summary>
        /// Seguir con el mismo objetivo mientras no haya otro mucho mas cerca: cambiar a cada rato hace que
        /// vuelva a empezar la orden (y se quede trabado apuntando).
        /// </summary>
        Threat Sticky(Gang g, Member m, Threat t, float radius)
        {
            if (m.Target == 0 || t.Ped == 0 || t.Ped == m.Target) return t;
            Threat cur;
            if (!ThreatOf(g, m, m.Target, out cur)) return t;
            if (m.Build == RogueBuild.Cazador && cur.Dist < radius)
                return RogueThreatScore(m, cur.Ped, cur.Dist, NearbyThreatAllies(cur)) <
                    RogueThreatScore(m, t.Ped, t.Dist, NearbyThreatAllies(t)) + 10 ? cur : t;
            return cur.Dist < radius && cur.Dist < t.Dist + 10f ? cur : t;
        }

        bool ThreatOf(Gang g, Member m, int ped, out Threat t)
        {
            t = new Threat();
            foreach (var og in gangs)
            {
                foreach (var c in og.Cops)
                    if (c.Ped == ped)
                    {
                        if (!c.Alive) return false;
                        t.Ped = ped; t.Cop = true; t.Pos = c.Pos; t.Dist = Vector3.Distance(c.Pos, m.Pos);
                        return true;
                    }
                if (og == g || !Hunting(g, G.Now) || !og.Fighter || og.State != 1 || PeacefulAlly(g, og)) continue;
                foreach (var om in og.Members)
                    if (om.Ped == ped)
                    {
                        if (om.Dead) return false;
                        t.Ped = ped; t.Rival = og; t.Pos = om.Pos; t.Dist = Vector3.Distance(om.Pos, m.Pos);
                        return true;
                    }
            }
            if (g.Tasked.Contains(ped) && DOES_CHAR_EXIST(ped) && !IS_CHAR_DEAD(ped))
            {
                t.Ped = ped; t.Cop = true; t.Pos = G.CharPos(ped); t.Dist = Vector3.Distance(t.Pos, m.Pos);
                return true;
            }
            return false;
        }

        Threat NearestCop(Gang g, Member m, float radius)
        {
            var t = new Threat { Dist = radius };
            foreach (var og in gangs)
                foreach (var c in og.Cops)
                {
                    if (!c.Alive) continue;
                    float d = Vector3.Distance(c.Pos, m.Pos);
                    if (d < t.Dist) { t.Dist = d; t.Ped = c.Ped; t.Cop = true; t.Pos = c.Pos; }
                }
            return t;
        }

        /// <summary>Se buscan con otras bandas? (si estan peleados entre ellos o el chat eligio "!banda")</summary>
        bool Hunting(Gang g, double now)
        {
            if (g.Truce) return false; // 1.9: tregua (carrera)
            if (g.IsFollower) return g.Fighter;
            if (ambientPacing && !(g.Order == GangRules.CmdBanda && now < g.OrderUntil))
            {
                if (!g.HostileNow) return false;
                Member lead = g.Leader;
                if (lead == null) return false;
                // A fight is an encounter. Avoid crossing the entire map for a constant deathmatch.
                bool nearby = false;
                foreach (var other in gangs)
                    if (other != g && other.Fighter && !PeacefulAlly(g, other) && other.State == 1 && GangDistance(g, other) < encounterRadius)
                    { nearby = true; break; }
                if (!nearby) return false;
                if (lead.Build == RogueBuild.Superviviente && lead.Health01 < 0.65f) return false;
            }
            return duel || (g.Order == GangRules.CmdBanda && now < g.OrderUntil);
        }

        Gang NearestRivalGang(Gang g)
        {
            Member l = g.Leader;
            if (l == null) return null;
            Gang best = null;
            float bd = float.MaxValue;
            foreach (var o in gangs)
            {
                if (o == g || !o.Fighter || o.State != 1 || o.Alive == 0 || PeacefulAlly(g, o)) continue;
                Member ol = NearestMemberOf(o, l.Pos);
                if (ol == null) continue;
                float d = Vector3.Distance(ol.Pos, l.Pos);
                if (d < bd) { bd = d; best = o; }
            }
            return best;
        }

        static Member NearestMemberOf(Gang g, Vector3 p)
        {
            Member best = null;
            float bd = float.MaxValue;
            foreach (var m in g.Members)
            {
                if (m.Dead) continue;
                float d = Vector3.Distance(m.Pos, p);
                if (d < bd) { bd = d; best = m; }
            }
            return best;
        }

        static float GangDistance(Gang a, Gang b)
        {
            float bd = float.MaxValue;
            foreach (var m in a.Members)
            {
                if (m.Dead) continue;
                foreach (var o in b.Members) if (!o.Dead) bd = Math.Min(bd, FlatDist(m.Pos, o.Pos));
            }
            return bd;
        }

        int PolicePresent(Gang g)
        {
            int k = 0;
            try
            {
                var street = new HashSet<int>();
                foreach (var c in g.Cops) { if (c.Alive) k++; if (c.Street) street.Add(c.Ped); }
                foreach (int p in g.Tasked) if (!street.Contains(p) && DOES_CHAR_EXIST(p) && !IS_CHAR_DEAD(p)) k++;
            }
            catch { }
            return k;
        }

        /// <summary>Una persona cerca para pelearse (no policias, no gente en autos, no de las bandas).</summary>
        int NearestCivilian(Member m, float radius, out float dist)
        {
            int best = 0;
            dist = radius;
            foreach (int p in G.PedsNear(m.Pos, radius, 12))
            {
                if (IS_CHAR_IN_ANY_CAR(p) || IsCop(p)) continue;
                float d = Vector3.Distance(G.CharPos(p), m.Pos);
                if (d < dist) { dist = d; best = p; }
            }
            return best;
        }

        string Name(Gang g, Member m) { return !string.IsNullOrEmpty(m.OwnerName) ? m.OwnerName : g.User + " #" + (m.Index + 1); }

        string DisplayName(Gang g, Member m) { return !string.IsNullOrEmpty(m.OwnerName) ? m.OwnerName : "Duplicado #" + m.RowId; }

        void Say(Member m, string status) { Say(m, status, false); }

        /// <summary>Lo que esta haciendo (menu, log). 'quiet' = no se anota en log.txt (cambia cada 2 segundos).</summary>
        void Say(Member m, string status, bool quiet)
        {
            m.Status = status;
            if (quiet || status == m.LastLogged) return;
            m.LastLogged = status;
            log("[NPC] " + Name(m.G, m) + ": " + status);
        }

        // ------------------------------------------------------------------
        // Muertes: suman tiempo y hacen crecer la banda
        // ------------------------------------------------------------------
        void ScanKills(Gang g, double now)
        {
            var dead = new List<KeyValuePair<int, int>>(); // ped -> 0 civil, 1 policia nuestro, 2 policia, 3 otra banda
            foreach (var og in gangs)
                foreach (var c in og.Cops)
                    if (c.Ped != 0 && c.DeadAt > 0 && !g.Counted.Contains(c.Ped)) dead.Add(new KeyValuePair<int, int>(c.Ped, og == g ? 1 : 2));
            foreach (int p in g.Tasked)
                if (!g.Counted.Contains(p) && !dead.Exists(kv => kv.Key == p) && DOES_CHAR_EXIST(p) && IS_CHAR_DEAD(p)) dead.Add(new KeyValuePair<int, int>(p, 2));
            foreach (var og in gangs)
            {
                if (og == g) continue;
                foreach (var om in og.Members)
                    if (om.Dead && !g.Counted.Contains(om.Ped)) dead.Add(new KeyValuePair<int, int>(om.Ped, 3));
            }
            if (now - g.LastCivScan > 1.0)
            {
                g.LastCivScan = now;
                Member l = g.Leader;
                if (l != null)
                    foreach (int p in G.DeadPedsNear(l.Pos, 50f, 10))
                        if (!g.Counted.Contains(p)) dead.Add(new KeyValuePair<int, int>(p, IsCop(p) ? 2 : 0));
            }
            foreach (var kv in dead)
            {
                int p = kv.Key;
                if (!g.Counted.Add(p)) continue;
                if (!DOES_CHAR_EXIST(p)) continue;
                Member killer = null;
                foreach (var m in g.Members)
                    if (!m.Dead && HAS_CHAR_BEEN_DAMAGED_BY_CHAR(p, m.Ped, false)) { killer = m; break; }
                if (killer == null && kv.Value == 1)
                {
                    // policia nuestro que cayo al lado de uno que estaba tirando
                    Vector3 pp = G.CharPos(p);
                    float bd = 35f;
                    foreach (var m in g.Members)
                    {
                        if (m.Dead || now - m.LastShot > 3) continue;
                        float d = Vector3.Distance(m.Pos, pp);
                        if (d < bd) { bd = d; killer = m; }
                    }
                }
                if (killer != null) OnKill(g, killer, p, kv.Value == 1 || kv.Value == 2, kv.Value == 3, now);
            }
            if (g.Counted.Count > 400) g.Counted.Clear();
        }

        void OnKill(Gang g, Member killer, int victim, bool cop, bool gang, double now)
        {
            g.Kills++;
            killer.Kills++;
            if (g.Vampire)
                foreach (var vamp in g.Members)
                {
                    if (vamp.Dead || vamp.Ped == 0) continue;
                    try
                    {
                        uint hp; GET_CHAR_HEALTH(vamp.Ped, out hp);
                        uint nh = (uint)Math.Min(vamp.StartHealth, hp + vamp.StartHealth / 4);
                        SET_CHAR_HEALTH(vamp.Ped, nh);
                    }
                    catch { }
                }
            // kill feed y ranking (los kills de la banda cuentan para el suscriptor)
            Gang vg;
            Member vm = FindMember(victim, out vg);
            string vname = cop ? "POLICIA" : vm != null ? DisplayName(vg, vm) : "CIVIL";
            Feed(DisplayName(g, killer), 1, vname, cop ? 2 : vm != null ? 1 : 0, now);
            StreamRuntime.AddFeed("KILL", Name(g, killer), "elimino a " + vname, now, killer.Kills);
            ranking.Kill(g.User, cop ? KillRanking.Cop : gang ? KillRanking.Npc : KillRanking.Civilian);
            if (gang)
            {
                g.NpcKills++;
                int lvl = GangLevels.FromNpcKills(g.NpcKills);
                if (lvl > g.Level) LevelUp(g, lvl, now);
            }
            string what = cop ? "un policia" : gang ? "uno de otra banda" : "a alguien";
            killer.Flash = "KILL " + killer.Kills; killer.FlashUntil = now + 2.5;
            log("[NPC] " + Name(g, killer) + " mato a " + what);
            if (cop)
            {
                g.CopKills++;
                if (g.CopKills % 2 == 0 && g.Stars < 5)
                {
                    g.Stars++;
                    log("[NPC] la banda de " + g.User + " sube a " + g.Stars + " estrellas: llegan mas policias" + (g.Stars >= 4 ? " y NOOSE" : ""));
                }
            }
            if (!g.IsFollower && g.Fighter && GangRules.Grows(growRule, cop, gang)) Grow(g, killer, now, false);
            if (!g.IsFollower && perksOn && g.Fighter && GangRules.Grows(perkRule, cop, gang)) QueueOffer(g, now);
        }

        // ------------------------------------------------------------------
        // Mejoras roguelike: al matar aparecen 3 arriba del NPC y el sub (o el chat) elige una
        // ------------------------------------------------------------------
        void QueueOffer(Gang g, double now)
        {
            if (g.Offer == null && now - g.LastOffer > 3) OpenOffer(g, now);
            else g.OffersQueued = Math.Min(3, g.OffersQueued + 1);
        }

        void OpenOffer(Gang g, double now)
        {
            var skip = new HashSet<int>();
            if (g.IsFollower || g.Alive >= gangMax || TotalMembers() >= maxCharacters) skip.Add(PerkRules.Refuerzo);
            skip.Add(PerkRules.Tiempo); // Character lifetime is unlimited; a time perk would be misleading.
            if (g.Regen) skip.Add(PerkRules.Regeneracion);
            if (g.SpeedMul >= speedMax - 0.001f || speedStep <= 0f) skip.Add(PerkRules.Velocidad);
            for (int i = 0; i < PerkRules.Count; i++) if (!perkOn[i]) skip.Add(i);
            if (!limitedAmmo) skip.Add(PerkRules.Municion);
            if (g.Vampire) skip.Add(PerkRules.Vampiro);
            if (g.Armored) skip.Add(PerkRules.AutoBlindado);
            if (g.FireBonus >= 100) skip.Add(PerkRules.Cadencia);
            if (!useGrenades) skip.Add(PerkRules.Granadas);
            if (!useRockets) skip.Add(PerkRules.Rpg);
            if (!useMolotovs) skip.Add(PerkRules.Molotov);
            if (g.AccBonus >= 40) skip.Add(PerkRules.Punteria);
            if (!useSpecials) { skip.Add(PerkRules.Granadas); skip.Add(PerkRules.Rpg); skip.Add(PerkRules.Molotov); }
            g.Offer = PerkRules.Offer(G.Rng, 3, skip);
            if (g.Offer.Length == 0) { g.Offer = null; return; }
            g.OfferUntil = now + perkSeconds;
            g.PerkVotes.Reset();
            log("[NPC] mejora para la banda de " + g.User + ": " + PerkRules.Line(g.Offer));
            notify(g.User + " -> ELEGI UNA MEJORA: " + PerkRules.Line(g.Offer));
        }

        /// <summary>La banda que esta eligiendo una mejora para ese mensaje del chat.</summary>
        Gang PerkGangFor(string user, int level)
        {
            if (controlMode == GangRules.ModeSub)
            {
                Gang own = OwnGang(user, level);
                return own != null && own.Offer != null ? own : null;
            }
            int fp = dir.FollowedPed;
            Gang best = null;
            foreach (var g in gangs)
            {
                if (g.Offer == null || g.State != 1 || g.Alive == 0) continue;
                if (fp != 0) foreach (var m in g.Members) if (m.Ped == fp) return g;
                if (best == null || g.OfferUntil < best.OfferUntil) best = g;
            }
            return best;
        }

        void StepOffer(Gang g, double now)
        {
            if (g.Offer == null)
            {
                if (g.OffersQueued > 0 && now - g.LastOffer > 3) { g.OffersQueued--; OpenOffer(g, now); }
                return;
            }
            if (now < g.OfferUntil) return;
            int w = g.PerkVotes.Winner();
            int total = g.PerkVotes.Total;
            if (w < 0) ApplyPerk(g, g.Offer[G.Rng.Next(g.Offer.Length)], "al azar (nadie eligio)", now);
            else ApplyPerk(g, g.Offer[w], total == 1 ? "1 voto" : total + " votos", now);
        }

        void ApplyPerk(Gang g, int perk, string who, double now)
        {
            g.Offer = null;
            g.LastOffer = now;
            g.PerkVotes.Reset();
            Member lead = g.Leader;
            foreach (var m in g.Members)
            {
                if (m.Dead || m.Ped == 0 || !DOES_CHAR_EXIST(m.Ped)) continue;
                switch (perk)
                {
                    case PerkRules.Chaleco: ADD_ARMOUR_TO_CHAR(m.Ped, m.Index > 0 ? 50 : 100); break;
                    case PerkRules.Vida:
                        m.StartHealth = (uint)Math.Min(m.Index > 0 ? Math.Max(m.StartHealth, g.PrincipalHealth * 0.85f) : 2500,
                            m.StartHealth * (m.Index > 0 ? 1.15f : 1.35f));
                        if (m.Index == 0) g.PrincipalHealth = m.StartHealth;
                        SET_CHAR_MAX_HEALTH(m.Ped, m.StartHealth);
                        SET_CHAR_HEALTH(m.Ped, m.StartHealth);
                        m.LastHp = m.StartHealth;
                        m.Health01 = 1f;
                        break;
                    case PerkRules.Arma:
                        {
                            int w = GangLevels.Upgrade(m.Weapon);
                            GIVE_WEAPON_TO_CHAR(m.Ped, w, 600, false);
                            m.MonthsWeapon = w;
                            GiveGun(m, w, 1);
                            break;
                        }
                    case PerkRules.Punteria: SET_CHAR_ACCURACY(m.Ped, (uint)Math.Min(90, 40 + g.AccBonus + 15 + g.Level * 5)); break;
                    case PerkRules.Velocidad: SET_CHAR_MOVE_ANIM_SPEED_MULTIPLIER(m.Ped, Math.Min(speedMax, g.SpeedMul + speedStep)); break;
                    case PerkRules.Municion:
                        // se recarga con mas balas (el tope sube abajo, con AmmoMul) y vuelve a su arma de antes
                        if (m.Primary > 0) { GIVE_WEAPON_TO_CHAR(m.Ped, m.Primary, 1, false); GiveGun(m, m.Primary, 1); m.Primary = 0; }
                        m.AmmoCapped = -1; m.PistolFallback = false;
                        break;
                    case PerkRules.Cadencia: SET_CHAR_SHOOT_RATE(m.Ped, Math.Min(200, 100 + g.FireBonus + 35)); break;
                    case PerkRules.Botiquin:
                        SET_CHAR_HEALTH(m.Ped, m.StartHealth);
                        m.LastHp = m.StartHealth;
                        m.Health01 = 1f;
                        ADD_ARMOUR_TO_CHAR(m.Ped, m.Index > 0 ? 50 : 100);
                        break;
                    case PerkRules.AutoBlindado:
                        if (m.Car != 0) ArmorCar(m.Car);
                        break;
                    case PerkRules.Escudo:
                        SET_CHAR_INVINCIBLE(m.Ped, true);
                        m.ShieldUntil = now + (m.Index > 0 ? 10 : 20);
                        break;
                }
            }
            switch (perk)
            {
                case PerkRules.Granadas: g.Grenades += 4; break;
                case PerkRules.Rpg: g.Rockets += 2; break;
                case PerkRules.Molotov: g.Molotovs += 4; break;
                case PerkRules.Punteria: g.AccBonus += 15; break;
                case PerkRules.Velocidad: g.SpeedMul = Math.Min(speedMax, g.SpeedMul + speedStep); break;
                case PerkRules.Municion: g.AmmoMul += 0.5f; break;
                case PerkRules.Cadencia: g.FireBonus = Math.Min(100, g.FireBonus + 35); break;
                case PerkRules.Vampiro: g.Vampire = true; break;
                case PerkRules.AutoBlindado: g.Armored = true; break;
                case PerkRules.Refuerzo: if (lead != null) Grow(g, lead, now, false); break;
                case PerkRules.Regeneracion: g.Regen = true; break;
                case PerkRules.Tiempo: break;
            }
            g.Perks.Add(perk);
            foreach (var m in g.Members) if (!m.Dead) ApplyRogueStats(g, m);
            string label = PerkRules.Labels[perk];
            g.Flash = "MEJORA: " + label;
            g.FlashUntil = now + 4;
            Feed(GangRules.GangTitle(g.User, 0) + " ELIGE " + label, 3, "", 0, now);
            log("[NPC] la banda de " + g.User + " eligio la mejora " + label + " (" + who + ")");
            notify(g.User + " -> MEJORA: " + label);
        }

        /// <summary>La policia de esta banda se va (y sus patrulleros), y los de la calle dejan de atacarlo.</summary>
        void ReleaseCops(Gang g)
        {
            var cars = new HashSet<int>();
            foreach (var c in g.Cops)
            {
                if (c.Street) { ReleaseStreetCop(g, c.Ped); continue; }
                try
                {
                    if (c.Ped != 0 && DOES_CHAR_EXIST(c.Ped))
                    {
                        if (!IS_CHAR_DEAD(c.Ped)) CLEAR_CHAR_TASKS(c.Ped);
                        if (c.Disguised) SET_BLOCKING_OF_NON_TEMPORARY_EVENTS(c.Ped, false);
                        MARK_CHAR_AS_NO_LONGER_NEEDED(c.Ped);
                    }
                    if (!c.Disguised) madeCops.Remove(c.Ped); // los disfrazados no sirven para saber el tipo de policia
                    if (c.Car != 0) cars.Add(c.Car);
                }
                catch { }
            }
            g.Cops.Clear();
            foreach (int car in cars)
                try
                {
                    if (CarUsedByNpc(car)) continue;
                    G.ProtectedCars.Remove(car);
                    if (DOES_VEHICLE_EXIST(car)) MARK_CAR_AS_NO_LONGER_NEEDED(car);
                }
                catch { }
            foreach (int p in g.Tasked)
                try { if (DOES_CHAR_EXIST(p) && !IS_CHAR_DEAD(p)) CLEAR_CHAR_TASKS(p); } catch { }
            g.Tasked.Clear();
        }

        // ------------------------------------------------------------------
        // El sub cambia de modo con "!pasear" / "!batalla"
        // ------------------------------------------------------------------
        void SetMode(Gang g, NpcBehavior mode, string who, double now)
        {
            if (g.Behavior == mode) { g.Flash = mode == NpcBehavior.Pasear ? "YA ESTA PASEANDO" : "YA ESTA EN BATALLA"; g.FlashUntil = now + 3; return; }
            if (now - g.LastModeSwitch < 15) return;
            g.LastModeSwitch = now;
            g.Behavior = mode;
            g.Fighter = mode == NpcBehavior.Batalla;
            g.Order = -1;
            g.Votes.Reset();
            if (!g.Fighter)
            {
                if (g.ChaseWith != null) EndChase(g, "se puso a pasear");
                g.Wanted = false; g.Stars = 0; g.PoliceAt = -1; g.CopsPending = 0; g.Offer = null; g.OffersQueued = 0; g.Control = false;
                ReleaseCops(g);
                foreach (var og in gangs)
                    if (og != g) foreach (var om in og.Members) if (!om.Dead && om.Ped != 0 && DOES_CHAR_EXIST(om.Ped)) SET_CHAR_RELATIONSHIP(om.Ped, 3, g.Group);
            }
            else
            {
                if (unlockRule == GangRules.UnlockAlways) g.Control = true;
                if (policeOn && !g.IsFollower) g.PoliceAt = now + 25;
                g.EndAt = double.PositiveInfinity;
            }
            foreach (var m in g.Members)
            {
                if (m.Dead || m.Ped == 0 || !DOES_CHAR_EXIST(m.Ped)) continue;
                m.Mode = M_NONE; m.Phase = P_NONE; m.Fleeing = false; m.Eating = false; m.Target = 0;
                if (g.Fighter) SetupFighter(g, m, !g.EverFighter);
                else { MakeSafe(g, m, paseoSafe); SET_BLOCKING_OF_NON_TEMPORARY_EVENTS(m.Ped, true); }
            }
            if (g.Fighter) g.EverFighter = true;
            g.Flash = g.Fighter ? "MODO BATALLA" : "MODO PASEO";
            g.FlashUntil = now + 4;
            Changed();
            log("[NPC] " + g.User + " cambia a " + (g.Fighter ? "modo batalla" : "modo paseo") + " (" + who + ")");
            notify(g.User + " -> " + (g.Fighter ? "MODO BATALLA" : "MODO PASEO"));
        }

        /// <summary>La banda sube de nivel (mata NPC de otras bandas): se anuncia y se ponen mas duros.</summary>
        void LevelUp(Gang g, int lvl, double now)
        {
            g.Level = lvl;
            ranking.Level(g.User, lvl);
            log("[NPC] la banda de " + g.User + " sube a nivel " + lvl);
            Feed(GangRules.GangTitle(g.User, 0) + " SUBE A NIVEL " + lvl, 3, "", 0, now);
            g.Flash = "NIVEL " + lvl;
            g.FlashUntil = now + 4;
            foreach (var m in g.Members)
            {
                if (m.Dead || m.Ped == 0 || !DOES_CHAR_EXIST(m.Ped)) continue;
                ADD_ARMOUR_TO_CHAR(m.Ped, 25);
                SET_CHAR_ACCURACY(m.Ped, (uint)Math.Min(90, 40 + g.AccBonus + lvl * 5));
            }
            if (!weaponsByLevel) { g.LevelGiven = Math.Max(g.LevelGiven, lvl); return; }
            var got = new List<string>();
            for (int l = Math.Max(2, g.LevelGiven + 1); l <= lvl; l++)
            {
                int w = levelWeapons[Math.Min(levelWeapons.Length - 1, l - 2)];
                if (GiveGangWeapon(g, w)) got.Add(GangLevels.WeaponName(w).ToUpperInvariant());
            }
            g.LevelGiven = Math.Max(g.LevelGiven, lvl);
            if (got.Count > 0)
            {
                string list = string.Join(" + ", got.ToArray());
                g.Flash = "NIVEL " + lvl + ": " + list;
                Feed(GangRules.GangTitle(g.User, 0) + " GANA " + list, 3, "", 0, now);
                log("[NPC] la banda de " + g.User + " gana por nivel: " + list);
            }
        }

        /// <summary>Un arma para toda la banda: las especiales van a la reserva; las de mano, si es mejor, a la mano.</summary>
        bool GiveGangWeapon(Gang g, int w)
        {
            if (w <= 0) return false;
            if (GangLevels.IsSpecial(w)) { AddSpecial(g, w, GangLevels.AmmoFor(w)); return true; }
            foreach (var m in g.Members)
            {
                if (m.Dead || m.Ped == 0 || !DOES_CHAR_EXIST(m.Ped)) continue;
                GIVE_WEAPON_TO_CHAR(m.Ped, w, GangLevels.AmmoFor(w), false);
                if (GangLevels.Rank(w) > GangLevels.Rank(m.Weapon)) { m.MonthsWeapon = w; GiveGun(m, w, 1); }
            }
            return true;
        }

        Member FindMember(int ped, out Gang gang)
        {
            gang = null;
            if (ped == 0) return null;
            foreach (var g in gangs) foreach (var m in g.Members) if (m.Ped == ped) { gang = g; return m; }
            return null;
        }

        /// <summary>Agrega una linea al kill feed.</summary>
        void Feed(string a, int colorA, string b, int colorB, double now)
        {
            feed.Add(new FeedLine { A = a, B = b, ColorA = colorA, ColorB = colorB, At = now });
            while (feed.Count > 12) feed.RemoveAt(0);
            feedSnap = feed.ToArray();
        }

        /// <summary>Se suma uno a la banda, al lado del que mato. La primera vez se desbloquea el control por chat.</summary>
        bool Grow(Gang g, Member killer, double now, bool test)
        {
            if (!NpcRoguePolicy.CanDuplicate(g.IsFollower, g.Alive, gangMax, TotalMembers(), maxCharacters)) return false;
            int alive = g.Alive;
            if (alive >= gangMax)
            {
                if (!g.FullLogged) { g.FullLogged = true; log("[NPC] la banda de " + g.User + " ya esta completa (" + alive + ")"); }
                return false;
            }
            if (TotalMembers() >= maxCharacters) return false;
            Vector3 p;
            if (killer.InCar) p = FindSpawnPoint(killer.Pos);
            else
            {
                double a = G.Rng.NextDouble() * Math.PI * 2;
                p = killer.Pos + new Vector3((float)Math.Cos(a) * 2.5f, (float)Math.Sin(a) * 2.5f, 0f);
                p = Ground(p, killer.Pos.Z);
            }
            Member m = NewMember(g, p, now);
            if (m == null) { log("[NPC] no se pudo sumar a la banda de " + g.User); return false; }
            SetupMember(g, m, now);
            g.FullLogged = false;
            Changed();
            int n = g.Alive;
            log("[NPC] la banda de " + g.User + " suma uno" + (test ? " (prueba)" : "") + ": ahora son " + n);
            bool unlock = !g.Control && unlockRule == GangRules.UnlockOnGrow;
            if (unlock)
            {
                g.Control = true;
                log("[NPC] control por chat desbloqueado para la banda de " + g.User + ": " + GangRules.ChoicesLine(slots));
                notify(GangRules.GangTitle(g.User, n) + ": EL CHAT ELIGE " + GangRules.ChoicesLine(slots).ToUpperInvariant());
            }
            else notify(GangRules.GangTitle(g.User, n));
            g.Flash = unlock ? "CONTROL DESBLOQUEADO" : "SE SUMA UNO A LA BANDA";
            g.FlashUntil = now + 4;
            return true;
        }

        // ------------------------------------------------------------------
        // Chat: mensajes arriba de la cabeza y ordenes con "!"
        // ------------------------------------------------------------------
        /// <summary>
        /// Un mensaje del chat. Si es del suscriptor y no empieza con "!", aparece arriba de su personaje.
        /// Si es una orden ("!robarauto") y su banda tiene el control desbloqueado, la cumple (o la vota el chat).
        /// </summary>
        // 1.9: el color de cada uno en el chat de Kick (para pintar su nombre)
        readonly Dictionary<string, int> userColors = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        bool kickColors = true;

        public void SetUserColor(string user, string hex)
        {
            if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(hex)) return;
            int c = NpcRules.ParseColor(hex);
            if (c == 0) return;
            if (userColors.Count > 800 && !userColors.ContainsKey(user)) userColors.Clear();
            int old;
            if (!userColors.TryGetValue(user, out old) || old != c)
            {
                userColors[user] = c;
                if (diag && ColorLogs++ < 30) log("[Kick] color de " + user + ": " + hex);
            }
        }
        int ColorLogs;

        int ColorFor(string user)
        {
            int c;
            return user != null && userColors.TryGetValue(user, out c) ? c : 0;
        }

        public void OnChat(string user, string text, int level)
        {
            if (gangs.Count == 0 || string.IsNullOrEmpty(text)) return;
            double now = G.Now;
            string msg = text.Trim();
            bool bang = msg.StartsWith("!");
            Member ownedMember = OwnedMember(user, level);
            if (HandleStreamingChat(user, msg, level, ownedMember, now)) return;
            if (HandleRadioCommand(user, msg, now)) return;
            Gang own = OwnGang(user, level);
            if (own != null && !bang)
            {
                if (ShowChat)
                {
                    string[] lines = GangRules.Bubble(msg, 30, 3);
                    if (lines.Length > 0) { own.Bubble = lines; own.BubbleUntil = now + GangRules.BubbleSeconds(msg); }
                }
                return;
            }
            if (!bang) return;
            // 1.9: comandos divertidos (!skin, !saltar, !desmayo...)
            if (funMode != 0)
            {
                int fc = GangRules.FunCommand(msg);
                if (fc >= 0)
                {
                    Gang fg = ownedMember != null ? ownedMember.G : own ?? (funMode == 2 ? FocusGang() : null);
                    if (fg != null) DoFun(fg, fc, user, now, ownedMember);
                    return;
                }
            }
            // el sub cambia su modo: "!pasear" (nadie lo ataca) / "!batalla"
            int ms = GangRules.ModeSwitch(msg);
            if (ms >= 0)
            {
                if (own != null && modeByChat) SetMode(own, ms == 0 ? NpcBehavior.Pasear : NpcBehavior.Batalla, user, now);
                return;
            }
            // mejoras: "!1" "!2" "!3" (o el nombre de la mejora) mientras hay una para elegir
            Gang pg = PerkGangFor(user, level);
            if (pg != null)
            {
                int k = PerkRules.ParsePick(msg, pg.Offer);
                if (k >= 0)
                {
                    if (controlMode == GangRules.ModeVote) pg.PerkVotes.Add(user, k, now, perkSeconds);
                    else ApplyPerk(pg, pg.Offer[k], user, now);
                    return;
                }
            }
            if (controlMode == GangRules.ModeSub)
            {
                if (own == null || !own.Control) return;
                int c = GangRules.ParseChoice(msg, slots);
                if (c < 0 || now - own.LastCmd < cmdCooldown) return;
                ApplyOrder(own, slots[c], user, now);
                return;
            }
            Gang f = FocusGang();
            if (f == null) return;
            int ch = GangRules.ParseChoice(msg, slots);
            if (ch < 0) return;
            if (controlMode == GangRules.ModeVote)
            {
                bool first = !f.Votes.Open;
                f.Votes.Add(user, ch, now, voteSeconds);
                if (first) log("[NPC] votacion abierta para la banda de " + f.User + " (" + voteSeconds.ToString("0") + " s)");
            }
            else if (now - f.LastCmd >= cmdCooldown) ApplyOrder(f, slots[ch], user, now);
        }

        /// <summary>La banda de ese usuario (las de prueba responden al streamer y a "prueba").</summary>
        Gang OwnGang(string user, int level)
        {
            Gang best = null;
            string u = (user ?? "").Trim();
            foreach (var g in gangs)
            {
                if (g.State != 1 || g.Alive == 0) continue;
                bool mine = string.Equals(g.User, u, StringComparison.OrdinalIgnoreCase) ||
                            (g.Test && (level >= 4 || string.Equals(u, "prueba", StringComparison.OrdinalIgnoreCase)));
                if (mine && (best == null || g.Created > best.Created)) best = g;
            }
            return best;
        }

        /// <summary>La banda que maneja el chat: la que esta en camara (si tiene control), si no la mas nueva.</summary>
        Gang FocusGang()
        {
            int fp = dir.FollowedPed;
            if (fp != 0)
                foreach (var g in gangs)
                    if (g.Control && g.State == 1 && g.Alive > 0)
                        foreach (var m in g.Members) if (m.Ped == fp) return g;
            Gang best = null;
            foreach (var g in gangs)
                if (g.Control && g.State == 1 && g.Alive > 0 && (best == null || g.Created > best.Created)) best = g;
            return best;
        }

        void ApplyOrder(Gang g, int cmd, string who, double now)
        {
            g.LastCmd = now;
            string key = "!" + GangRules.CmdKeys[cmd];
            if (cmd == GangRules.CmdBanda && NearestRivalGang(g) == null)
            {
                g.Flash = key.ToUpperInvariant() + ": NO HAY OTRA BANDA";
                g.FlashUntil = now + 4;
                log("[NPC] orden " + key + " para la banda de " + g.User + ": no hay otra banda");
                return;
            }
            g.Order = cmd;
            g.OrderAt = now;
            g.OrderUntil = now + (cmd == GangRules.CmdBanda ? Math.Max(orderSeconds, 60f) : orderSeconds);
            g.Flash = (controlMode == GangRules.ModeSub ? "" : "EL CHAT ELIGIO ") + key.ToUpperInvariant();
            g.FlashUntil = now + 4;
            foreach (var m in g.Members)
            {
                if (m.Dead || m.Eating) continue;
                m.Phase = P_NONE;
                if (m.Mode != M_ENTER_CAR && m.Mode != M_LEAVE_CAR) m.Mode = M_NONE;
                if (cmd == GangRules.CmdHuir || cmd == GangRules.CmdAtacar) m.Fleeing = false;
            }
            switch (cmd)
            {
                case GangRules.CmdAtacar:
                    if (!g.Wanted && policeOn) MakeOutlaw(g, attackCivs);
                    if (PolicePresent(g) < 2 && policeOn && g.CopsPending == 0) { g.CopsPending = 1; g.NextCops = now; }
                    break;
                case GangRules.CmdBailar:
                    foreach (var m in g.Members)
                        if (!m.Dead) { m.Eating = false; m.Fleeing = false; m.DanceAt = -100; }
                    break;
            }
            log("[NPC] orden " + key + " para la banda de " + g.User + " (" + who + ")");
            notify(g.User + " -> " + key.ToUpperInvariant() + (controlMode == GangRules.ModeSub ? "" : " (lo eligio el chat)"));
        }

        /// <summary>Cumplir la orden del chat. False = que haga lo de siempre.</summary>
        bool DoOrder(Gang g, Member m, double now)
        {
            switch (g.Order)
            {
                case GangRules.CmdRobarAuto:
                    if (m.InCar)
                    {
                        if (!CarOk(m.Car)) { BailOut(g, m, now, "se le rompio el auto"); return true; }
                        if (WaitForCrew(g, m, m.Car, now)) return true;
                        DriveAround(g, m, m.Car, now, "da vueltas en un auto robado (lo eligio el chat)");
                        return true;
                    }
                    if (m.Mode == M_LEAVE_CAR && now - m.ModeAt < 1.0) return true;
                    Roam(g, m, now);
                    return true;
                case GangRules.CmdHuir:
                    if (m.InCar)
                    {
                        if (!CarOk(m.Car)) { BailOut(g, m, now, "se le rompio el auto"); return true; }
                        if (WaitForCrew(g, m, m.Car, now)) return true;
                        if (m.Mode != M_FLEE) Cruise(m, m.Car, fleeSpeed + 8f, now, M_FLEE);
                        else if (!Moving(m, now, 2f) && StillFor(m, now) > 5 && now - m.LastTask > 4) Cruise(m, m.Car, fleeSpeed + 8f, now, M_FLEE);
                        Say(m, "escapa en auto (lo eligio el chat)");
                        return true;
                    }
                    if (m.Mode == M_LEAVE_CAR && now - m.ModeAt < 1.0) return true;
                    Roam(g, m, now); // conseguir un auto
                    return true;
                case GangRules.CmdAtacar:
                    {
                        Threat t = NearestCop(g, m, 400f);
                        if (t.Ped == 0)
                        {
                            if (!m.InCar && m.Mode != M_WANDER) Wander(m, now);
                            Say(m, "espera a la policia (lo eligio el chat)");
                            return true;
                        }
                        if (m.InCar)
                        {
                            if (t.Dist < 40f || !CarOk(m.Car)) { BailOut(g, m, now, "se baja a atacar a la policia"); return true; }
                            if (m.Mode != M_DRIVE_TO || m.Target != t.Ped || now - m.LastTask > 20)
                            {
                                _TASK_CAR_MISSION_PED_TARGET(m.Ped, m.Car, t.Ped, 4, 30f, AiTaskPolicy.RoadDrivingStyle, 15, 10);
                                Task(m, M_DRIVE_TO, now);
                                m.Target = t.Ped;
                            }
                            Say(m, "va en auto a atacar a la policia");
                            return true;
                        }
                        Fight(g, m, t, now);
                        return true;
                    }
                case GangRules.CmdPinas:
                    {
                        if (m.InCar) { BailOut(g, m, now, "se baja a pelear"); return true; }
                        float cd;
                        int civ = NearestCivilian(m, 40f, out cd);
                        if (civ == 0)
                        {
                            if (m.Mode != M_WANDER) Wander(m, now);
                            Say(m, "busca con quien pelear");
                            return true;
                        }
                        Fight(g, m, new Threat { Ped = civ, Dist = cd, Pos = G.CharPos(civ) }, now);
                        return true;
                    }
                case GangRules.CmdBailar:
                    return Dance(g, m, now);
                default:
                    return false; // !banda: lo de siempre ya busca a la otra banda
            }
        }

        bool danceWarned;
        static readonly string[] MaleDances = { "amb@dance_maleidl_a", "loop_a", "amb@dance_maleidl_b", "loop_b", "amb@dance_maleidl_c", "loop_c", "amb@dance_maleidl_d", "loop_d" };
        static readonly string[] FemaleDances = { "amb@dance_femidl_a", "loop_a", "amb@dance_femidl_b", "loop_b", "amb@dance_femidl_c", "loop_c" };

        /// <summary>!bailar: se bajan del auto y se ponen a bailar (si les pegan, primero se defienden).</summary>
        bool Dance(Gang g, Member m, double now)
        {
            if (m.InCar)
            {
                if (m.Mode != M_LEAVE_CAR) { if (m.Driving) BailOut(g, m, now, "se baja a bailar"); else LeaveCar(m, now); }
                return true;
            }
            if (m.Mode == M_LEAVE_CAR && now - m.ModeAt < 1.0) return true;
            if (now - m.LastHit < 4 && g.Fighter) return false; // le estan tirando: pelea (y despues sigue bailando)
            if (m.DanceSet.Length == 0)
            {
                bool male = true;
                try { male = IS_CHAR_MALE(m.Ped); } catch { }
                string[] list = male ? MaleDances : FemaleDances;
                int k = G.Rng.Next(list.Length / 2);
                m.DanceSet = list[k * 2];
                m.DanceAnim = list[k * 2 + 1];
            }
            REQUEST_ANIMS(m.DanceSet);
            if (!HAVE_ANIMS_LOADED(m.DanceSet))
            {
                if (now - g.OrderAt > 6)
                {
                    if (!danceWarned) { danceWarned = true; log("[NPC] no cargo la animacion de baile (" + m.DanceSet + "): sigue con lo suyo"); }
                    return false;
                }
                if (m.Mode != M_WAIT) { _TASK_STAND_STILL(m.Ped, 1500); Task(m, M_WAIT, now); }
                return true;
            }
            bool playing = false;
            try { playing = IS_CHAR_PLAYING_ANIM(m.Ped, m.DanceSet, m.DanceAnim); } catch { playing = true; }
            if (m.Mode != M_DANCE || (!playing && now - m.DanceAt > 6))
            {
                int ms = (int)Math.Max(3000, (g.OrderUntil - now) * 1000);
                _TASK_PLAY_ANIM(m.Ped, m.DanceAnim, m.DanceSet, 8f, true, ms);
                Task(m, M_DANCE, now);
                m.DanceAt = now;
            }
            Say(m, "baila (lo eligio el chat)");
            return true;
        }

        // ------------------------------------------------------------------
        // Otra banda lejos: aparecen cerca de la otra (fuera de camara), "ya sabiendo donde estan"
        // ------------------------------------------------------------------
        void BringRival(Gang g, double now)
        {
            if (ambientPacing || !bringGangs || !g.Fighter || !Hunting(g, now) || now - g.LastWarp < 25 || now - g.Created < 8) return;
            Gang r = NearestRivalGang(g);
            if (r == null) return;
            Member l = g.Leader, rl = r.Leader;
            if (l == null || rl == null) return;
            float d = GangDistance(g, r);
            if (d < 350f) { g.FarSince = -1; return; }
            // con "aparecen lejos" primero se buscan manejando (se cruzan en cualquier parte);
            // si en 100 s no se encontraron, recien ahi se acerca una
            if (g.FarSince < 0) g.FarSince = now;
            if (spreadSpawns && now - g.FarSince < 100) return;
            // se mueve la que esta mas lejos de la camara (la otra es la que se esta viendo)
            Vector3 cam = G.CharPos(G.PlayerPed);
            if (FlatDist(l.Pos, cam) < FlatDist(rl.Pos, cam)) return;
            int followed = dir.FollowedPed;
            foreach (var m in g.Members)
            {
                if (m.Dead) continue;
                if (m.Ped == followed || (dir.Active && dir.InFrame(m.Head, 2f))) return;
                if (m.Eating || m.Fleeing) return;
            }
            Vector3 spot;
            float heading;
            if (!FindStreetNear(rl.Pos, 110f, 190f, new List<Vector3>(), out spot, out heading)) return;
            g.LastWarp = now;
            var moved = new HashSet<int>();
            int k = 0;
            foreach (var m in g.Members)
            {
                if (m.Dead) continue;
                if (m.InCar && CarOk(m.Car))
                {
                    if (moved.Add(m.Car))
                    {
                        Vector3 cp = spot + MathX.Forward(new Vector3(0, 0, heading)) * (-7f * (moved.Count - 1));
                        SET_CAR_COORDINATES(m.Car, cp + new Vector3(0, 0, 0.5f));
                        SET_CAR_HEADING(m.Car, heading);
                    }
                }
                else
                {
                    Vector3 side = MathX.Right(new Vector3(0, 0, heading));
                    Vector3 pp = Ground(spot + side * 5.5f + MathX.Forward(new Vector3(0, 0, heading)) * (1.5f * k), spot.Z);
                    SET_CHAR_COORDINATES(m.Ped, pp);
                    k++;
                }
                m.Mode = M_NONE;
                m.Phase = P_NONE;
                m.StillSince = -1;
            }
            log("[NPC] la banda de " + g.User + " sabe donde esta la de " + r.User + ": llega a su barrio (estaba a " + d.ToString("0") + " m)");
        }

        /// <summary>Una calle a cierta distancia de un punto, libre, mirando hacia el y (si se puede) fuera de cuadro.</summary>
        bool FindStreetNear(Vector3 target, float minD, float maxD, List<Vector3> used, out Vector3 spot, out float heading)
        {
            spot = Vector3.Zero;
            heading = 0f;
            Vector3 fallback = Vector3.Zero;
            float fbHeading = 0f;
            for (int pass = 0; pass < 2; pass++)
            {
                float usedR = pass == 0 ? 15f : 7f;
                float lo = pass == 0 ? minD : minD * 0.7f, hi = pass == 0 ? maxD : maxD * 1.4f;
                for (uint k = 4; k <= 60; k += 2)
                {
                    Vector3 r;
                    float h;
                    if (!GET_NTH_CLOSEST_CAR_NODE_WITH_HEADING(target, k, out r, out h) || r == Vector3.Zero) continue;
                    float d = FlatDist(r, target);
                    if (d < lo || d > hi || Math.Abs(r.Z - target.Z) > 15f) continue;
                    bool taken = false;
                    foreach (var u in used) if (Vector3.Distance(u, r) < usedR) { taken = true; break; }
                    if (taken) continue;
                    if (G.VehiclesNear(r, 5f, 1, true).Count > 0) continue; // algo estacionado/trabado ahi
                    Vector3 to = target - r;
                    if (Vector3.Dot(MathX.Forward(new Vector3(0, 0, h)), to) < 0) h += 180f; // mirando hacia el
                    if (fallback == Vector3.Zero) { fallback = r; fbHeading = h; }
                    if (dir.Active && dir.InFrame(r + new Vector3(0, 0, 1f), 4f)) continue;
                    spot = r;
                    heading = h;
                    return true;
                }
            }
            if (fallback == Vector3.Zero) return false;
            spot = fallback;
            heading = fbHeading;
            return true;
        }

        // ------------------------------------------------------------------
        // Policia: los patrulleros los creamos nosotros (con policias adentro) en una calle
        // cercana fuera de cuadro, cada uno en un lugar distinto, y les damos ordenes simples.
        // ------------------------------------------------------------------
        void SpawnPatrol(Gang g, double now)
        {
            Member target = g.Leader;
            if (target == null) { g.CopsPending = 0; return; }
            uint carModel;
            GET_CURRENT_BASIC_POLICE_CAR_MODEL(out carModel);
            if (carModel == 0 || !IS_MODEL_IN_CDIMAGE((int)carModel)) carModel = (uint)GET_HASH_KEY("POLICE");
            uint copModel;
            GET_CURRENT_BASIC_COP_MODEL(out copModel);
            if (copModel == 0 || !IS_MODEL_IN_CDIMAGE((int)copModel)) copModel = (uint)GET_HASH_KEY("M_Y_COP");
            int copsAlive = 0;
            foreach (var og in gangs) foreach (var c in og.Cops) if (c.Alive) copsAlive++;
            if (copsAlive >= MAX_COPS) { g.NextCops = now + 4; return; } // ya hay demasiados (el juego no da para mas personajes)
            if ((g.Stars >= 4 || g.Alive >= 3) && g.PatrolsMade % 2 == 1)
            {
                // NOOSE: SWAT en su patrullero
                int noose = GET_HASH_KEY("NOOSE"), swat = GET_HASH_KEY("M_Y_SWAT");
                if (IS_MODEL_IN_CDIMAGE(noose) && IS_MODEL_IN_CDIMAGE(swat)) { carModel = (uint)noose; copModel = (uint)swat; }
            }
            REQUEST_MODEL((int)carModel);
            REQUEST_MODEL((int)copModel);
            if (!HAS_MODEL_LOADED((int)carModel) || !HAS_MODEL_LOADED((int)copModel))
            {
                g.NextCops = now + 0.25;
                if (++g.CopWaits > 40) { g.CopsPending = 0; log("[NPC] no cargaron los modelos de la policia"); }
                return;
            }
            g.CopWaits = 0;
            g.CopsPending--;
            g.NextCops = now + 2.5;

            // los lugares usados se olvidan de a poco (si no, despues de varios no queda calle libre)
            while (g.UsedSpots.Count > 5) g.UsedSpots.RemoveAt(0);
            Vector3 spot;
            float heading;
            if (!FindStreetNear(target.Pos, 35f, 150f, g.UsedSpots, out spot, out heading))
            {
                log("[NPC] no encontre una calle libre para el patrullero de " + g.User);
                return;
            }
            int car;
            CREATE_CAR((int)carModel, spot + new Vector3(0, 0, 0.6f), out car, true);
            if (car == 0 || !DOES_VEHICLE_EXIST(car)) { log("[NPC] no se pudo crear el patrullero"); return; }
            SET_CAR_HEADING(car, heading);
            SWITCH_CAR_SIREN(car, true);
            g.UsedSpots.Add(spot);
            int driver = 0, pass = 0;
            uint ct = CopType(target.Pos);
            // 1.9 "policia disfrazada": personas comunes con la ropa de policia. La IA de policia del juego
            // no les tira a los NPC (solo apunta y retrocede); una persona comun con nuestras ordenes si.
            bool disguised = copDisguised;
            uint pt = disguised ? (ct >= 2 ? ct - 2 : 0) : ct;
            try { CREATE_CHAR_INSIDE_CAR(car, pt, copModel, out driver); } catch { driver = 0; }
            try { CREATE_CHAR_AS_PASSENGER(car, pt, copModel, 0, out pass); } catch { pass = 0; }
            int[] guns = disguised ? (g.Stars >= 4 ? new[] { copGunPref, 15, 14, 10 } : new[] { copGunPref, copGunPref, 10, 13 })
                                   : g.Stars >= 4 ? new[] { 15, 14, 10 } : new[] { 7, 7, 10, 13 };
            int made = 0;
            foreach (int cop in new[] { driver, pass })
            {
                if (cop == 0 || !DOES_CHAR_EXIST(cop)) continue;
                madeCops.Add(cop);
                // el arma EN LA MANO: si no, el combate del juego los deja apuntando sin tirar
                int gun = guns[G.Rng.Next(guns.Length)];
                GIVE_WEAPON_TO_CHAR(cop, 7, 300, false);
                if (gun != 7) GIVE_WEAPON_TO_CHAR(cop, gun, 400, false);
                SET_CURRENT_CHAR_WEAPON(cop, gun, true);
                SET_CHAR_KEEP_TASK(cop, true);
                SET_CHAR_ACCURACY(cop, g.Stars >= 4 ? 50u : 40u);
                SET_CHAR_SHOOT_RATE(cop, 100);
                SET_CHAR_WILL_MOVE_WHEN_INJURED(cop, true);
                SET_CHAR_WILL_ONLY_FIRE_WITH_CLEAR_LOS(cop, true);
                SET_CHAR_WILL_USE_CARS_IN_COMBAT(cop, true);
                if (g.Stars >= 4) ADD_ARMOUR_TO_CHAR(cop, 100);
                SET_CHAR_WILL_USE_COVER(cop, true);
                SET_SENSE_RANGE(cop, 80f);
                if (disguised)
                {
                    SET_CHAR_RELATIONSHIP_GROUP(cop, 3); // del lado de la policia: los policias del juego no lo atacan
                    SET_BLOCKING_OF_NON_TEMPORARY_EVENTS(cop, true); // no sale corriendo con los tiros
                }
                for (int s = 0; s < MAX_SLOTS; s++) SET_CHAR_RELATIONSHIP(cop, s == g.Slot ? 5u : 3u, GROUP_BASE + s);
                g.Cops.Add(new Cop { Ped = cop, Car = car, Driver = cop == driver, Spawned = now, Pos = spot, Gun = gun, Disguised = disguised, Block = disguised ? 1 : -1 });
                made++;
            }
            if (made == 0) g.Cops.Add(new Cop { Car = car, Spawned = now, DeadAt = now });
            g.PatrolsMade++;
            g.LastReinforce = now;
            Changed();
            Say(target, "patrullero " + g.PatrolsMade + " en camino");
            log("[NPC] patrullero " + g.PatrolsMade + " para " + g.User + " a " + FlatDist(spot, target.Pos).ToString("0") + " m, " + made + " policias" +
                (g.PatrolsMade == 1 ? " (tipo " + pt + (disguised ? ", disfrazados" : "") + ", " + g.Stars + " estrellas" + (copAggro ? ", agresivos" : "") + ")" : ""));
            ManageCops(g, now);
        }

        /// <summary>Que la policia no se termine: si quedan pocos (segun cuantos son en la banda), llegan mas.</summary>
        void Reinforce(Gang g, double now)
        {
            if (!policeOn || !g.Wanted || g.CopsPending > 0 || patrols == 0 || now - g.Created < 20 || g.PatrolsMade >= 20) return;
            if (now - g.LastReinforce < (g.Stars >= 4 ? 15 : 25)) return;
            int want = Math.Min(10, Math.Max(1 + g.Alive, g.Stars * 2));
            if (PolicePresent(g) >= want) return;
            g.CopsPending = 1;
            g.NextCops = now;
            g.LastReinforce = now;
        }

        /// <summary>
        /// Ordenes de la policia, contra el integrante mas cercano. En auto: si el NPC maneja, el chofer lo
        /// SIGUE (una sola vez) y el acompanante le tira; si esta a pie, van hasta el y se bajan.
        /// A pie: corren hacia el si no lo ven, le tiran y se mueven al costado. Si el NPC se va en auto
        /// y tienen el patrullero cerca, se suben y lo persiguen.
        /// </summary>
        void ManageCops(Gang g, double now)
        {
            if (g.Leader == null) return;
            var exiting = new HashSet<int>();
            var reserved = new HashSet<int>();
            foreach (var c in g.Cops)
                if (c.Alive && c.Driver && c.Mode == M_ENTER_CAR && StreamPolicePolicy.FinishEntry(
                    now - c.LastTask, IS_CHAR_GETTING_IN_TO_A_CAR(c.Ped))) reserved.Add(c.Car);
            foreach (var c in g.Cops)
            {
                if (!c.Alive || !IS_CHAR_IN_ANY_CAR(c.Ped)) continue;
                int currentCar, driver;
                GET_CAR_CHAR_IS_USING(c.Ped, out currentCar);
                if (currentCar != 0 && currentCar != c.Car)
                {
                    if (c.Car != 0) g.Cars[c.Car] = now;
                    c.Car = currentCar;
                    c.Mode = M_NONE;
                    c.StillSince = -1;
                    Changed();
                }
                if (!CarOk(c.Car)) { exiting.Add(c.Car); continue; }
                GET_DRIVER_OF_CAR(c.Car, out driver);
                c.Driver = driver == c.Ped;
                if (!c.Driver) continue;
                ObserveCopMovement(c, now, true);
                var target = CopTarget(g, c);
                if (target == null) continue;
                double still = c.StillSince < 0 ? 0 : now - c.StillSince;
                if (AiTaskPolicy.ExitPoliceCar(true, target.InCar, Vector3.Distance(c.Pos, target.Pos), still)) exiting.Add(c.Car);
            }
            foreach (var c in g.Cops)
            {
                if (!c.Alive) continue;
                var target = CopTarget(g, c);
                if (target == null) continue;
                bool inCar = IS_CHAR_IN_ANY_CAR(c.Ped);
                if (!inCar && c.WasInCar)
                {
                    c.LastExit = now; c.Mode = M_NONE; c.StillSince = -1;
                    if (c.Gun > 0) SET_CURRENT_CHAR_WEAPON(c.Ped, c.Gun, true);
                }
                c.WasInCar = inCar;
                float distance = Vector3.Distance(c.Pos, target.Pos);
                bool changed = c.Target != target.Ped;
                if (changed) { c.Seen = false; c.LastSeen = -100; c.LastProgress = now; c.LastDistance = distance; }
                if (distance < c.LastDistance - 2 || now - c.LastShot < 2)
                { c.LastProgress = now; c.LastDistance = distance; }
                else if (distance > c.LastDistance + 10) c.LastDistance = distance;
                if (now - c.LastSeen >= 1) { c.LastSeen = now; c.Seen = CanSee(c.Ped, c.Pos, target.Ped, target.Pos); }
                ObserveCopMovement(c, now, inCar);
                if (inCar)
                {
                    int driver = 0;
                    bool usable = CarOk(c.Car);
                    if (usable) GET_DRIVER_OF_CAR(c.Car, out driver);
                    c.Driver = driver == c.Ped;
                    if (driver == 0 && reserved.Contains(c.Car) && usable) continue;
                    if (!usable || driver == 0 || exiting.Contains(c.Car) || c.Mode == M_LEAVE_CAR)
                    {
                        if (c.Mode != M_LEAVE_CAR || now - c.LastTask >= 8)
                        { _TASK_LEAVE_ANY_CAR(c.Ped); CopTask(c, M_LEAVE_CAR, target.Ped, now); }
                        continue;
                    }
                    if (c.Driver)
                    {
                        int desired = target.InCar ? M_CHASE : M_DRIVE_TO;
                        double still = c.StillSince < 0 ? 0 : now - c.StillSince;
                        bool driving = c.Mode == M_CHASE || c.Mode == M_DRIVE_TO;
                        if (AiTaskPolicy.RefreshVehiclePursuit(driving, changed, c.Mode != desired, now - c.LastTask, still))
                        {
                            _TASK_CAR_MISSION_PED_TARGET(c.Ped, c.Car, target.Ped, target.InCar ? 2u : 4u,
                                target.InCar ? 30f : 18f, AiTaskPolicy.RoadDrivingStyle, 10, 10);
                            CopTask(c, desired, target.Ped, now);
                        }
                    }
                    else if (distance <= CombatPolicy.SightRange)
                    {
                        if (changed || c.Mode != M_DRIVEBY || (!IS_PED_IN_COMBAT(c.Ped) && now - c.LastTask > 14))
                        {
                            SET_CURRENT_CHAR_WEAPON(c.Ped, 7, true);
                            SET_CHAR_WILL_LEAVE_CAR_IN_COMBAT(c.Ped, false);
                            SET_CHAR_WILL_DO_DRIVEBYS(c.Ped, true);
                            _TASK_COMBAT(c.Ped, target.Ped);
                            CopTask(c, M_DRIVEBY, target.Ped, now);
                        }
                    }
                    continue;
                }
                if (IS_PED_RAGDOLL(c.Ped) || IS_CHAR_GETTING_UP(c.Ped)) continue;
                if (c.Mode == M_ENTER_CAR)
                {
                    if (CarOk(c.Car) && StreamPolicePolicy.FinishEntry(now - c.LastTask, IS_CHAR_GETTING_IN_TO_A_CAR(c.Ped))) continue;
                    c.Mode = M_NONE; c.EntryRetryAt = now + 25; CLEAR_CHAR_TASKS(c.Ped);
                }
                if (target.InCar && distance > 45 && now - c.LastExit > 10 && now >= c.EntryRetryAt
                    && CarOk(c.Car) && !reserved.Contains(c.Car) && FlatDist(c.Pos, G.CarPos(c.Car)) < 30)
                {
                    int driver; GET_DRIVER_OF_CAR(c.Car, out driver);
                    if (driver == 0)
                    { _TASK_ENTER_CAR_AS_DRIVER(c.Ped, c.Car, 0); c.Driver = true; reserved.Add(c.Car); CopTask(c, M_ENTER_CAR, target.Ped, now); continue; }
                }
                if (distance > CombatPolicy.SightRange || (!c.Seen && distance > 12 && now - c.LastShot > 5))
                { CopRunTo(c, target, changed, now); continue; }
                if (!changed && c.Mode == M_SHOOT && now - c.LastTask < c.PhaseLen) continue;
                bool cover = IS_PED_IN_COVER(c.Ped) || IS_CHAR_DUCKING(c.Ped);
                bool direct = StreamPolicePolicy.DirectFire(nativeAi, copAggro, now - c.ModeAt,
                    now - c.LastShot, now - c.LastProgress, cover);
                if (direct && c.Seen && distance <= CombatPolicy.ShootingRange)
                {
                    if (c.Gun > 0) SET_CURRENT_CHAR_WEAPON(c.Ped, c.Gun, true);
                    int ammo;
                    GET_AMMO_IN_CHAR_WEAPON(c.Ped, c.Gun > 0 ? c.Gun : 7, out ammo);
                    if (ammo <= 0) GIVE_WEAPON_TO_CHAR(c.Ped, c.Gun > 0 ? c.Gun : 7, 150, false);
                    c.PhaseLen = 3.5;
                    // Keep the game's background police decisions from cancelling a scripted burst.
                    _TASK_SHOOT_AT_CHAR(c.Ped, target.Ped, 3500, 4);
                    CopTask(c, M_SHOOT, target.Ped, now); c.Bursts++;
                    continue;
                }
                float speed; GET_CHAR_SPEED(c.Ped, out speed);
                var recovery = CombatPolicy.Decide(true, changed || c.Mode != M_COMBAT,
                    IS_PED_IN_COMBAT(c.Ped), c.Seen, distance, now - c.LastTask, now - c.LastShot, speed, cover, nativeAi);
                if (recovery == CombatPolicy.Recovery.Keep) continue;
                if (recovery == CombatPolicy.Recovery.Advance) { CopRunTo(c, target, changed, now); continue; }
                SET_CHAR_WILL_LEAVE_CAR_IN_COMBAT(c.Ped, true);
                _TASK_COMBAT(c.Ped, target.Ped); CopTask(c, M_COMBAT, target.Ped, now);
            }
            if (now - g.LastScan > 2) { g.LastScan = now; ManageStreetCops(g, now); }
        }

        void CopTask(Cop c, int mode, int target, double now)
        {
            if (c.Mode != mode) { c.ModeAt = now; c.StillSince = -1; }
            c.Mode = mode; c.Target = target; c.LastTask = now;
            Block(c, c.Disguised || !(mode == M_COMBAT || mode == M_DRIVEBY));
        }

        static void ObserveCopMovement(Cop c, double now, bool inCar)
        {
            float speed;
            if (inCar && CarOk(c.Car)) GET_CAR_SPEED(c.Car, out speed);
            else GET_CHAR_SPEED(c.Ped, out speed);
            if (speed >= (inCar ? 1.5f : 0.4f)) c.StillSince = -1;
            else if (c.StillSince < 0) c.StillSince = now;
        }

        void CopRunTo(Cop c, Member target, bool changed, double now)
        {
            double still = c.StillSince < 0 ? 0 : now - c.StillSince;
            if (!changed && c.Mode == M_GOTO && !AiTaskPolicy.RefreshPath(now - c.LastTask,
                FlatDist(c.MoveTo, target.Pos), still, FlatDist(c.Pos, c.MoveTo) < 2)) return;
            if (still >= 4) _TASK_GO_STRAIGHT_TO_COORD(c.Ped, target.Pos, 4);
            else _TASK_FOLLOW_NAV_MESH_TO_COORD(c.Ped, target.Pos, 4);
            CopTask(c, M_GOTO, target.Ped, now); c.MoveTo = target.Pos;
        }

        const int MAX_STREET_COPS = 12;

        void ManageStreetCops(Gang g, double now)
        {
            Member l = g.Leader;
            if (l == null) return;
            // los que ya tiene: soltar a los que murieron o quedaron lejos; re-mandar a los que se quedaron sin pelear
            var drop = new List<int>();
            var recruited = new HashSet<int>();
            foreach (var c in g.Cops) if (c.Street) recruited.Add(c.Ped);
            foreach (int p in g.Tasked)
            {
                try
                {
                    if (recruited.Contains(p)) continue; // esos los maneja ManageCops (y los suelta Cleanup)
                    if (!DOES_CHAR_EXIST(p) || IS_CHAR_DEAD(p)) { drop.Add(p); continue; }
                    Vector3 pp = G.CharPos(p);
                    Member near = NearestMemberOf(g, pp);
                    float d = near != null ? FlatDist(near.Pos, pp) : float.MaxValue;
                    if (near == null || d > 240f || (d > 140f && !IS_CHAR_ON_SCREEN(p)))
                    {
                        CLEAR_CHAR_TASKS(p);
                        MARK_CHAR_AS_NO_LONGER_NEEDED(p);
                        drop.Add(p);
                        continue;
                    }
                    if (d < 70f && !IS_PED_IN_COMBAT(p) && !IS_CHAR_IN_ANY_CAR(p)) _TASK_COMBAT(p, near.Ped);
                }
                catch { drop.Add(p); }
            }
            foreach (int p in drop) g.Tasked.Remove(p);
            if (!policeOn || !g.Fighter) return;
            int total = 0, copsAlive = 0;
            bool recruitedNow = false;
            foreach (var og in gangs) { total += og.Tasked.Count; foreach (var c in og.Cops) if (c.Alive) copsAlive++; }
            foreach (int p in G.PedsNear(l.Pos, 70f, 14))
            {
                if (total >= MAX_STREET_COPS) break;
                if (g.Tasked.Contains(p) || madeCops.Contains(p) || !IsCop(p)) continue;
                bool other = false;
                foreach (var og in gangs) if (og != g && og.Tasked.Contains(p)) { other = true; break; }
                if (other) continue;
                bool inVeh = true;
                try { inVeh = IS_CHAR_IN_ANY_CAR(p); } catch { }
                // Natural patrols in traffic keep their driver; recruit cops only on foot.
                if (inVeh) continue;
                if (streetCops && copsAlive < MAX_COPS && !inVeh)
                {
                    // 1.9: el policia de la calle se suma de verdad: lo hacemos "nuestro" (si no, el juego le
                    // cambia la orden enseguida), odia a las bandas y lo manejamos como a los del patrullero
                    try
                    {
                        SET_CHAR_AS_MISSION_CHAR(p);
                        SET_CHAR_KEEP_TASK(p, true);
                        for (int s = 0; s < MAX_SLOTS; s++) SET_CHAR_RELATIONSHIP(p, s == g.Slot ? 5u : 3u, GROUP_BASE + s);
                        SET_CHAR_ACCURACY(p, 40u);
                        SET_CHAR_SHOOT_RATE(p, 100);
                        SET_CHAR_WILL_USE_COVER(p, true);
                        SET_CHAR_WILL_ONLY_FIRE_WITH_CLEAR_LOS(p, true);
                        GIVE_WEAPON_TO_CHAR(p, 7, 200, false);
                        SET_CURRENT_CHAR_WEAPON(p, 7, true);
                    }
                    catch { }
                    g.Cops.Add(new Cop { Ped = p, Street = true, Spawned = now, Pos = G.CharPos(p), Gun = 7 });
                    copsAlive++;
                    recruitedNow = true;
                    log("[poli] un policia de la calle se suma contra " + g.User + " (a " + FlatDist(G.CharPos(p), l.Pos).ToString("0") + " m)");
                }
                else _TASK_COMBAT(p, l.Ped);
                g.Tasked.Add(p);
                total++;
            }
            if (recruitedNow) Changed();
        }

        static bool GangShotRecently(Gang g, double now, double secs)
        {
            foreach (var m in g.Members) if (!m.Dead && now - m.LastShot < secs) return true;
            return false;
        }

        /// <summary>Para log.txt: si la policia de verdad esta tirando (cada 10 s).</summary>
        void LogCops(Gang g, double now)
        {
            int alive = 0, street = 0, disg = 0, fired = 0, shots = 0, bursts = 0, foot = 0;
            float near = float.MaxValue;
            foreach (var c in g.Cops)
            {
                if (!c.Alive) continue;
                alive++;
                if (c.Street) street++;
                if (c.Disguised) disg++;
                if (now - c.LastShot < 10) fired++;
                shots += c.Shots; bursts += c.Bursts;
                c.Shots = 0; c.Bursts = 0;
                try { if (!IS_CHAR_IN_ANY_CAR(c.Ped)) foot++; } catch { }
                Member tm = NearestMemberOf(g, c.Pos);
                if (tm != null) near = Math.Min(near, Vector3.Distance(tm.Pos, c.Pos));
            }
            if (alive == 0) return;
            log("[poli] " + g.User + ": " + alive + " policias (" + foot + " a pie, " + street + " de la calle, " + disg + " disfrazados), " +
                fired + " tiraron en 10 s, " + shots + " tiros, " + bursts + " rafagas ordenadas" +
                (near < 9999f ? ", el mas cerca a " + near.ToString("0") + " m" : ""));
        }

        /// <summary>Un policia de la calle que sumamos: vuelve a ser del juego.</summary>
        void ReleaseStreetCop(Gang g, int p)
        {
            g.Tasked.Remove(p);
            try
            {
                if (p == 0 || !DOES_CHAR_EXIST(p)) return;
                for (int s = 0; s < MAX_SLOTS; s++) CLEAR_CHAR_RELATIONSHIP(p, 5, GROUP_BASE + s);
                SET_CHAR_KEEP_TASK(p, false);
                SET_BLOCKING_OF_NON_TEMPORARY_EVENTS(p, false);
                if (!IS_CHAR_DEAD(p)) CLEAR_CHAR_TASKS(p);
                MARK_CHAR_AS_NO_LONGER_NEEDED(p);
            }
            catch { }
        }

        /// <summary>
        /// A quien va el policia: sigue con el mismo mientras no haya otro mucho mas cerca (cambiar de objetivo
        /// a cada rato hace que el patrullero frene y arranque).
        /// </summary>
        Member CopTarget(Gang g, Cop c)
        {
            Member nearest = NearestMemberOf(g, c.Pos);
            Member cur = MemberByPed(g, c.Target);
            if (cur == null || cur.Dead || nearest == null) return nearest;
            return AiTaskPolicy.KeepPoliceTarget(IS_CHAR_IN_ANY_CAR(c.Ped), Vector3.Distance(cur.Pos, c.Pos),
                Vector3.Distance(nearest.Pos, c.Pos), G.Now - c.LastTask) ? cur : nearest;
        }

        bool CarUsedByNpc(int car, Gang except = null)
        {
            if (car == 0) return false;
            foreach (var band in gangs)
            {
                if (band == except) continue;
                foreach (var m in band.Members)
                    if (!m.Dead && ((m.InCar && m.Car == car) || m.StealCar == car)) return true;
                foreach (var cop in band.Cops) if (cop.Alive && cop.Car == car) return true;
            }
            return false;
        }

        /// <summary>Suelta policias muertos o que quedaron muy lejos, y autos robados abandonados.</summary>
        bool Cleanup(Gang g, double now)
        {
            bool changed = false;
            for (int i = 0; i < g.Cops.Count; i++)
            {
                Cop c = g.Cops[i];
                bool gone = c.DeadAt > 0 && now - c.DeadAt > 25;
                if (!gone && c.Alive)
                {
                    Member tm = NearestMemberOf(g, c.Pos);
                    float fd = tm != null ? FlatDist(tm.Pos, c.Pos) : 0f;
                    bool onScreen = IS_CHAR_ON_SCREEN(c.Ped);
                    gone = tm != null && (c.Street ? fd > 240f || (fd > 140f && !onScreen) : fd > 320f && !onScreen);
                }
                if (!gone) continue;
                if (c.Street) { ReleaseStreetCop(g, c.Ped); g.Cops.RemoveAt(i--); changed = true; continue; }
                try
                {
                    if (c.Ped != 0 && DOES_CHAR_EXIST(c.Ped)) MARK_CHAR_AS_NO_LONGER_NEEDED(c.Ped);
                    bool carUsed = false;
                    foreach (var o in g.Cops) if (o != c && o.Car == c.Car && o.Alive) { carUsed = true; break; }
                    if (!carUsed && c.Car != 0 && DOES_VEHICLE_EXIST(c.Car))
                    {
                        bool otherRef = false;
                        foreach (var o in g.Cops) if (o != c && o.Car == c.Car) { otherRef = true; break; }
                        if (!otherRef && !CarUsedByNpc(c.Car)) MARK_CAR_AS_NO_LONGER_NEEDED(c.Car);
                    }
                }
                catch { }
                g.Cops.RemoveAt(i--);
                changed = true;
            }
            var drop = new List<int>();
            foreach (var kv in g.Cars)
            {
                int car = kv.Key;
                if (CarUsedByNpc(car)) continue;
                bool near = false;
                if (DOES_VEHICLE_EXIST(car))
                {
                    Vector3 cp = G.CarPos(car);
                    foreach (var m in g.Members) if (!m.Dead && FlatDist(m.Pos, cp) < 60f) { near = true; break; }
                }
                if (near && now - kv.Value < 90) continue;
                if (now - kv.Value < 20) continue;
                drop.Add(car);
            }
            foreach (int car in drop)
            {
                try { if (DOES_VEHICLE_EXIST(car)) MARK_CAR_AS_NO_LONGER_NEEDED(car); } catch { }
                g.Cars.Remove(car);
                foreach (var m in g.Members) if (m.Car == car && !m.InCar) m.Car = 0;
                changed = true;
            }
            return changed;
        }

        bool IsCop(int ped)
        {
            if (copModels == null)
            {
                copModels = new HashSet<uint>();
                foreach (string m in new[] { "M_Y_COP", "M_M_FATCOP_01", "M_Y_COP_TRAFFIC", "M_Y_STROOPER", "M_Y_SWAT", "M_M_FBI", "M_Y_NHELIPILOT" })
                    copModels.Add((uint)GET_HASH_KEY(m));
                try { uint b; GET_CURRENT_BASIC_COP_MODEL(out b); if (b != 0) copModels.Add(b); } catch { }
            }
            uint model;
            GET_CHAR_MODEL(ped, out model);
            return copModels.Contains(model);
        }

        /// <summary>Para log.txt: que esta haciendo cada integrante y el policia mas cercano (los primeros minutos).</summary>
        void Diagnose(Gang g, Member m, double now)
        {
            try
            {
                float sp;
                GET_CHAR_SPEED(m.Ped, out sp);
                string t = "";
                if (m.Target != 0 && DOES_CHAR_EXIST(m.Target))
                    t = " objetivo a " + Vector3.Distance(G.CharPos(m.Target), m.Pos).ToString("0") + " m" + (IS_CHAR_DEAD(m.Target) ? " (muerto)" : "") + (m.Seen ? " (lo ve)" : "");
                string line = "[diag] " + Name(g, m) + ": " + ModeName(m.Mode) + t + ", vida " + (int)(m.Health01 * 100) + "%" +
                    ", tiro hace " + (now - m.LastShot > 99 ? "-" : (now - m.LastShot).ToString("0") + " s") +
                    ", vel " + sp.ToString("0.0") + (m.InCar ? (m.Driving ? ", maneja" : ", en auto") : "") +
                    (m.Fleeing ? ", escapando" : "") + (m.Eating ? ", comiendo" : "") + (g.Order >= 0 ? ", orden !" + GangRules.CmdKeys[g.Order] : "") +
                    (IS_PED_IN_COMBAT(m.Ped) ? ", combate del juego" : "");
                Cop best = null;
                float bd = float.MaxValue;
                foreach (var og in gangs)
                    foreach (var c in og.Cops)
                    {
                        if (!c.Alive) continue;
                        float d = Vector3.Distance(c.Pos, m.Pos);
                        if (d < bd) { bd = d; best = c; }
                    }
                if (best != null)
                {
                    float cs;
                    GET_CHAR_SPEED(best.Ped, out cs);
                    line += " | poli a " + bd.ToString("0") + " m: " + ModeName(best.Mode) + (best.Seen ? " (lo ve)" : "") + (IS_PED_IN_COMBAT(best.Ped) ? " (combate)" : "") +
                        ", tiro hace " + (now - best.LastShot > 99 ? "-" : (now - best.LastShot).ToString("0") + " s") +
                        ", vel " + cs.ToString("0.0") + (IS_CHAR_IN_ANY_CAR(best.Ped) ? ", en auto" : "");
                }
                log(line);
            }
            catch (Exception ex) { log("[diag] error: " + ex.Message); }
        }

        // ------------------------------------------------------------------
        // Soltar / limpiar
        // ------------------------------------------------------------------
        bool ReleaseOldest()
        {
            if (gangs.Count == 0) return false;
            int idx = -1;
            for (int i = 0; i < gangs.Count; i++) if (gangs[i].State == 1 && gangs[i].Alive == 0) { idx = i; break; }
            if (idx < 0) return false;
            Gang g = gangs[idx];
            log("[NPC] " + g.User + " deja lugar para otro");
            ReleaseGang(g);
            gangs.RemoveAt(idx);
            Changed();
            return true;
        }

        void ReleaseMember(Member m)
        {
            vehicleWeapons.Remove(m.Ped);
            try
            {
                if (m.Ped != 0 && DOES_CHAR_EXIST(m.Ped))
                {
                    if (m.FakeName) REMOVE_FAKE_NETWORK_NAME_FROM_PED(m.Ped);
                    MARK_CHAR_AS_NO_LONGER_NEEDED(m.Ped);
                }
            }
            catch { }
        }

        void ReleaseGang(Gang g, bool preserveSharedCars = true)
        {
            try { if (g.ChaseWith != null) EndChase(g, "se fue"); } catch { }
            // los policias de la calle que iban contra esta banda dejan de hacerlo
            foreach (int p in g.Tasked)
                try { if (DOES_CHAR_EXIST(p) && !IS_CHAR_DEAD(p)) CLEAR_CHAR_TASKS(p); } catch { }
            g.Tasked.Clear();
            try
            {
                if (g.State == 0 && g.CarModel != 0) MARK_MODEL_AS_NO_LONGER_NEEDED(g.CarModel);
                var cars = new HashSet<int>();
                foreach (var m in g.Members)
                {
                    if (m.EventKind == 1) EndChat(m, null);
                    ReleaseMember(m);
                    if (m.Car != 0) cars.Add(m.Car);
                }
                foreach (var kv in g.Cars) cars.Add(kv.Key);
                foreach (var c in g.Cops)
                {
                    if (c.Street) { ReleaseStreetCop(g, c.Ped); continue; }
                    if (c.Ped != 0 && DOES_CHAR_EXIST(c.Ped)) MARK_CHAR_AS_NO_LONGER_NEEDED(c.Ped);
                    if (c.Car != 0) cars.Add(c.Car);
                }
                foreach (int car in cars)
                    if (car != 0 && (!preserveSharedCars || !CarUsedByNpc(car, g)) && DOES_VEHICLE_EXIST(car))
                        MARK_CAR_AS_NO_LONGER_NEEDED(car);
            }
            catch { }
        }

        /// <summary>Saca a todos (desde el menu, o cuando el jugador muere / carga partida).</summary>
        public void ReleaseAll()
        {
            EndAmbientEvents("reinicio");
            foreach (var g in gangs) ReleaseGang(g, false);
            gangs.Clear();
            Changed();
        }

        /// <summary>Olvidar todo sin llamar al juego.</summary>
        public void ForgetState()
        {
            vehicleWeapons.Clear();
            ForgetAmbientState();
            gangs.Clear();
            G.ProtectedPeds.Clear();
            G.ProtectedCars.Clear();
            tags = null;
            SaveHandles();
        }

        void Changed()
        {
            G.ProtectedPeds.Clear();
            G.ProtectedCars.Clear();
            foreach (var g in gangs)
            {
                foreach (var m in g.Members)
                {
                    if (m.Ped != 0) G.ProtectedPeds.Add(m.Ped);
                    if (m.Car != 0) G.ProtectedCars.Add(m.Car);
                }
                foreach (var kv in g.Cars) G.ProtectedCars.Add(kv.Key);
                foreach (var c in g.Cops)
                {
                    if (c.Ped != 0) G.ProtectedPeds.Add(c.Ped);
                    if (c.Car != 0) G.ProtectedCars.Add(c.Car);
                }
            }
            SaveHandles();
        }

        /// <summary>
        /// Guarda los handles en el AppDomain: si ScriptHookDotNet recarga el script, la instancia
        /// nueva los suelta (si no, quedarian para siempre como personajes de mision).
        /// </summary>
        void SaveHandles()
        {
            try
            {
                var peds = new List<int>(G.ProtectedPeds);
                var cars = new List<int>(G.ProtectedCars);
                AppDomain.CurrentDomain.SetData("KickChaos.NpcPeds", peds.Count > 0 ? peds.ToArray() : null);
                AppDomain.CurrentDomain.SetData("KickChaos.NpcCars", cars.Count > 0 ? cars.ToArray() : null);
            }
            catch { }
        }

        public void RecoverLeftovers()
        {
            try
            {
                var peds = AppDomain.CurrentDomain.GetData("KickChaos.NpcPeds") as int[];
                var cars = AppDomain.CurrentDomain.GetData("KickChaos.NpcCars") as int[];
                int k = 0;
                if (peds != null) foreach (int p in peds) if (p != 0 && DOES_CHAR_EXIST(p)) { madeCops.Add(p); MARK_CHAR_AS_NO_LONGER_NEEDED(p); k++; }
                if (cars != null) foreach (int c in cars) if (c != 0 && DOES_VEHICLE_EXIST(c)) MARK_CAR_AS_NO_LONGER_NEEDED(c);
                if (k > 0) log("[NPC] se soltaron " + k + " personajes que dejo una instancia anterior");
                AppDomain.CurrentDomain.SetData("KickChaos.NpcPeds", null);
                AppDomain.CurrentDomain.SetData("KickChaos.NpcCars", null);
            }
            catch { }
        }

        // ------------------------------------------------------------------
        // Carteles: nombre, banda, vida, tiempo, opciones del chat y mensajes del sub
        // ------------------------------------------------------------------
        /// <summary>Calcula donde van los carteles (en el Tick, con la camara de este frame).</summary>
        public void BuildTags(Vector3 camPos, Vector3 camRot, float fov, float aspect, bool visible)
        {
            if (!visible || !ShowName || GameName || gangs.Count == 0) { tags = null; return; }
            tagAspect = aspect;
            double now = G.Now;
            Gang focus = controlMode == GangRules.ModeSub ? null : FocusGang();
            var list = new List<NpcTag>();
            foreach (var g in gangs)
            {
                if (g.State != 1) continue;
                Member head = g.Leader;
                foreach (var m in g.Members)
                {
                    if (m.Ped == 0) continue;
                    if (m.Dead && now - m.DeadAt > 4.5) continue;
                    Vector3 hp = m.Dead ? m.Pos + new Vector3(0, 0, 0.4f) : m.Head;
                    float sx, sy, depth;
                    if (!NpcRules.Project(hp, camPos, camRot, fov, aspect, out sx, out sy, out depth)) continue;
                    if (now - m.LastLos > 0.3)
                    {
                        m.LastLos = now;
                        // 1.9: en un tunel / interior el mapa de alturas no sirve: cerca no lo tapamos
                        m.Occluded = !(m.RoomKey != 0 && Vector3.Distance(camPos, hp) < 45f) && Blocked(camPos, hp);
                    }
                    if (m.Occluded) continue;
                    bool main = m == head;
                    var t = new NpcTag
                    {
                        X = sx, Y = sy, Scale = Math.Max(0.5f, Math.Min(1f, NpcRules.ApparentHeight(depth, fov) / 0.12f)),
                        Name = DisplayName(g, m), Health = m.Health01, Dead = m.Dead,
                        Bar = HealthBar && !m.Dead
                    };
                    if (armorBar && !m.Dead && m.Armor > 0) t.Armor = Math.Min(1f, m.Armor / 100f);
                    if (showAmmo && !m.Dead && g.Fighter)
                        t.Ammo = !m.Armed ? (limitedAmmo && m.Ammo == 0 ? "SIN BALAS" : "") : limitedAmmo && m.Ammo >= 0 ? m.Ammo.ToString() : "";
                    if (kickColors && !string.IsNullOrEmpty(m.OwnerName)) t.NameColor = ColorFor(m.OwnerName);
                    int col;
                    t.Sub = SubFor(g, m, main, now, out col);
                    t.SubColor = col;
                    if (main && !m.Dead)
                    {
                        if (ShowTimer) t.Timer = "";
                        if (g.Control && (controlMode == GangRules.ModeSub || g == focus))
                        {
                            t.CmdsVote = g.Votes.Open;
                            t.Cmds = t.CmdsVote ? GangRules.VoteLine(slots, g.Votes, now) : GangRules.ChoicesLine(slots);
                        }
                        if (ShowChat && now < g.BubbleUntil && g.Bubble != null && g.Bubble.Length > 0) t.Bubble = g.Bubble;
                        if (g.Offer != null) t.Perks = PerkRules.TagLine(g.Offer, g.PerkVotes, g.OfferUntil - now, controlMode == GangRules.ModeVote);
                    }
                    if (!m.Dead)
                    {
                        if (m.Index > 0 && string.IsNullOrEmpty(m.OwnerName)) t.Cmds = "Escribe !unirme" + m.RowId + " para unirte";
                        if (m.OwnerBubble != null && now < m.OwnerBubbleUntil && ShowChat) t.Bubble = m.OwnerBubble;
                        if (t.Perks.Length == 0) t.Perks = NpcRoguePolicy.BuildName(m.Build) + " | K " + m.Kills;
                    }
                    list.Add(t);
                }
            }
            tags = list;
        }

        string SubFor(Gang g, Member m, bool main, double now, out int color)
        {
            color = 0;
            if (m.Dead) { color = 1; return "FUERA DE COMBATE"; }
            if (now < m.FlashUntil && m.Flash.Length > 0) { color = 3; return m.Flash; }
            if (m.Eating) { color = 2; return "COMPRANDO COMIDA"; }
            if (m.Fleeing) { color = 2; return "POCA VIDA: SE ESCAPA"; }
            if (!main) return "";
            if (now < g.FlashUntil && g.Flash.Length > 0) { color = 3; return g.Flash; }
            string lv = g.Level > 1 ? "  NV " + g.Level : "";
            if (g.Members.Count > 1) return GangRules.GangTitle(g.User, g.Alive) + lv;
            return g.Sub + lv;
        }

        public void ClearTags() { tags = null; }

        // ------------------------------------------------------------------
        // Kill feed y ranking
        // ------------------------------------------------------------------
        void LoadRanking()
        {
            try
            {
                string path = cfg.Folder != null ? Path.Combine(cfg.Folder, "ranking.ini") : null;
                ranking = path != null && File.Exists(path) ? KillRanking.Parse(File.ReadAllText(path)) : new KillRanking();
            }
            catch { ranking = new KillRanking(); }
            rankSnap = ranking.Top(10).ToArray();
        }

        void SaveRanking()
        {
            if (cfg.Folder == null) return;
            try
            {
                File.WriteAllText(Path.Combine(cfg.Folder, "ranking.ini"), ranking.Serialize(), new System.Text.UTF8Encoding(false));
                File.WriteAllText(Path.Combine(cfg.Folder, "ranking.txt"), ranking.ObsText(10), new System.Text.UTF8Encoding(false));
            }
            catch { }
        }

        void UpdateHudData(double now)
        {
            if (ranking == null) LoadRanking();
            if (feed.Count > 0 && now - feed[0].At > 12)
            {
                feed.RemoveAll(f => now - f.At > 12);
                feedSnap = feed.ToArray();
            }
            if (ranking.Dirty && now - lastRankSave > 2)
            {
                lastRankSave = now;
                ranking.Dirty = false;
                rankSnap = ranking.Top(10).ToArray();
                SaveRanking();
            }
        }

        public bool ToggleFeed() { ShowFeed = !ShowFeed; return ShowFeed; }
        public bool ToggleRanking() { ShowRanking = !ShowRanking; return ShowRanking; }

        public void ResetRanking()
        {
            ranking.Clear();
            ranking.Dirty = false;
            rankSnap = new KillStat[0];
            SaveRanking();
            log("[NPC] ranking borrado");
        }

        GTA.Font hudFont, hudSmall, hudTitle;

        static Color FeedColor(int c, int alpha)
        {
            switch (c)
            {
                case 1: return Color.FromArgb(alpha, 83, 252, 24);
                case 2: return Color.FromArgb(alpha, 90, 160, 255);
                case 3: return Color.FromArgb(alpha, 255, 220, 70);
                case 4: return Color.FromArgb(alpha, 235, 80, 70);
                default: return Color.FromArgb(alpha, 255, 255, 255);
            }
        }

        /// <summary>Kill feed (arriba a la derecha) y ranking (debajo). Evento PerFrameDrawing.</summary>
        public void DrawHud(GTA.Graphics g)
        {
            FeedLine[] lines = ShowFeed ? feedSnap : null;
            KillStat[] rank = ShowRanking ? rankSnap : null;
            if ((lines == null || lines.Length == 0) && rank == null) return;
            g.Scaling = GTA.FontScaling.ScreenUnits;
            if (hudFont == null)
            {
                hudFont = MakeFont(0.022f, true);
                hudSmall = MakeFont(0.017f, false);
                hudTitle = MakeFont(0.024f, true);
            }
            float aspect = Math.Max(0.5f, tagAspect);
            double now = G.Now;
            const GTA.TextAlignment left = GTA.TextAlignment.Left | GTA.TextAlignment.VerticalCenter | GTA.TextAlignment.SingleLine;
            const GTA.TextAlignment right = GTA.TextAlignment.Right | GTA.TextAlignment.VerticalCenter | GTA.TextAlignment.SingleLine;
            float rightX = 0.985f, y = 0.06f;
            if (lines != null)
            {
                float lh = 0.031f, h = 0.022f, cw = h * 0.56f / aspect;
                int shown = 0;
                for (int i = lines.Length - 1; i >= 0 && shown < 6; i--, shown++)
                {
                    FeedLine f = lines[i];
                    double age = now - f.At;
                    int alpha = age > 10.5 ? (int)Math.Max(0, 255 * (12 - age) / 1.5) : 255;
                    if (alpha <= 0) continue;
                    const string sep = "  >  ";
                    float wa = f.A.Length * cw, ws = f.B.Length > 0 ? sep.Length * cw * 0.8f : 0f, wb = f.B.Length * cw;
                    float total = wa + ws + wb, x0 = rightX - total;
                    g.DrawRectangle(new RectangleF(x0 - 0.008f, y, total + 0.014f, lh - 0.004f), Color.FromArgb(alpha * 150 / 255, 0, 0, 0));
                    float cy = y + (lh - 0.004f) / 2f - h / 2f;
                    g.DrawText(f.A, new RectangleF(x0, cy, wa + 0.05f, h), left, FeedColor(f.B.Length > 0 ? f.ColorA : 3, alpha), hudFont);
                    if (f.B.Length > 0)
                    {
                        g.DrawText(">", new RectangleF(x0 + wa, cy, ws, h), GTA.TextAlignment.Center | GTA.TextAlignment.VerticalCenter | GTA.TextAlignment.SingleLine,
                            FeedColor(0, alpha * 180 / 255), hudFont);
                        g.DrawText(f.B, new RectangleF(rightX - wb - 0.05f, cy, wb + 0.05f, h), right, FeedColor(f.ColorB, alpha), hudFont);
                    }
                    y += lh;
                }
                y += 0.02f;
            }
            if (rank != null)
            {
                y = Math.Max(y, 0.30f);
                float w = 0.28f, x = rightX - w, rh = 0.03f;
                int rows = Math.Max(1, rank.Length);
                g.DrawRectangle(new RectangleF(x - 0.006f, y - 0.004f, w + 0.012f, 0.04f + rows * rh + 0.01f), Color.FromArgb(160, 0, 0, 0));
                g.DrawRectangle(new RectangleF(x - 0.006f, y - 0.004f, w + 0.012f, 0.003f), KickGreen);
                g.DrawText("RANKING DE KILLS", new RectangleF(x, y, w, 0.034f), left, Color.White, hudTitle);
                y += 0.04f;
                if (rank.Length == 0)
                    g.DrawText("todavia nadie mato a nadie", new RectangleF(x, y, w, rh), left, Color.FromArgb(200, 200, 200, 200), hudSmall);
                for (int i = 0; i < rank.Length; i++)
                {
                    KillStat st = rank[i];
                    Color c = i == 0 ? Color.FromArgb(255, 255, 215, 60) : i < 3 ? Color.FromArgb(255, 230, 230, 230) : Color.FromArgb(255, 200, 200, 200);
                    string name = st.Name.Length > 15 ? st.Name.Substring(0, 14) + "." : st.Name;
                    g.DrawText((i + 1) + ". " + name, new RectangleF(x, y, w * 0.55f, rh), left, c, hudFont);
                    string det = KillRanking.Detail(st);
                    if (det.Length > 0) g.DrawText(det, new RectangleF(x + w * 0.42f, y + 0.002f, w * 0.42f, rh), right, Color.FromArgb(200, 180, 180, 180), hudSmall);
                    g.DrawText(st.Kills.ToString(), new RectangleF(x + w - 0.05f, y, 0.05f, rh), right, KickGreen, hudFont);
                    y += rh;
                }
            }
        }

        /// <summary>Hay un edificio entre la camara y la cabeza? (mapa de alturas; ignora los primeros metros).</summary>
        static bool Blocked(Vector3 cam, Vector3 head)
        {
            float len = Vector3.Distance(cam, head);
            if (len < 6f) return false;
            // 1.9: bajo tierra (tuneles) o debajo de algo el mapa de alturas no sirve: no lo tapamos
            Vector3 from = Vector3.Lerp(cam, head, Math.Min(0.5f, 5f / len));
            Vector3 to = Vector3.Lerp(cam, head, 1f - Math.Min(0.3f, 1.5f / len));
            Vector3 hit;
            return G.Raycast(from, to, out hit);
        }

        GTA.Font[] nameFonts, subFonts, smallFonts, bubbleFonts;
        float fontScale;
        static readonly Color KickGreen = Color.FromArgb(255, 83, 252, 24);
        static readonly Color CmdYellow = Color.FromArgb(255, 255, 214, 64);
        static readonly Color PerkCyan = Color.FromArgb(255, 80, 220, 255);
        static readonly Color FlashYellow = Color.FromArgb(255, 255, 236, 120);
        static readonly Color Orange = Color.FromArgb(255, 255, 150, 40);
        static readonly Color DeadRed = Color.FromArgb(255, 235, 70, 60);

        GTA.Font MakeFont(float h, bool bold)
        {
            var f = new GTA.Font("Arial", h, GTA.FontScaling.ScreenUnits, bold, false);
            try { f.Effect = GTA.FontEffect.Edge; f.EffectColor = Color.Black; f.EffectSize = 1; } catch { }
            return f;
        }

        static Color SubColorOf(int c)
        {
            switch (c)
            {
                case 1: return DeadRed;
                case 2: return Orange;
                case 3: return FlashYellow;
                default: return KickGreen;
            }
        }

        /// <summary>Dibuja los carteles (evento PerFrameDrawing).</summary>
        public void DrawTags(GTA.Graphics g)
        {
            List<NpcTag> list = tags;
            if (list == null || list.Count == 0) return;
            g.Scaling = GTA.FontScaling.ScreenUnits;
            if (nameFonts == null || Math.Abs(fontScale - NameScale) > 0.001f)
            {
                DisposeFonts();
                fontScale = NameScale;
                float[] kk = { 1f, 0.82f, 0.66f };
                nameFonts = new GTA.Font[3];
                subFonts = new GTA.Font[3];
                smallFonts = new GTA.Font[3];
                bubbleFonts = new GTA.Font[3];
                for (int i = 0; i < 3; i++)
                {
                    nameFonts[i] = MakeFont(0.032f * kk[i] * fontScale, true);
                    subFonts[i] = MakeFont(0.020f * kk[i] * fontScale, true);
                    smallFonts[i] = MakeFont(0.018f * kk[i] * fontScale, true);
                    bubbleFonts[i] = MakeFont(0.021f * kk[i] * fontScale, false);
                }
            }
            float aspect = Math.Max(0.5f, tagAspect);
            const GTA.TextAlignment center = GTA.TextAlignment.Center | GTA.TextAlignment.VerticalCenter | GTA.TextAlignment.SingleLine;
            const GTA.TextAlignment left = GTA.TextAlignment.Left | GTA.TextAlignment.VerticalCenter | GTA.TextAlignment.SingleLine;
            const GTA.TextAlignment right = GTA.TextAlignment.Right | GTA.TextAlignment.VerticalCenter | GTA.TextAlignment.SingleLine;
            foreach (NpcTag t in list)
            {
                int b = t.Scale >= 0.85f ? 0 : t.Scale >= 0.65f ? 1 : 2;
                float k = (b == 0 ? 1f : b == 1 ? 0.82f : 0.66f) * fontScale;
                float nameH = 0.034f * k, subH = 0.022f * k, smallH = 0.020f * k, bubH = 0.023f * k,
                      barH = 0.0055f * k, barW = 0.06f * k, gap = 0.004f * k;
                float y = t.Y - 0.008f * k;
                if (t.Bar)
                {
                    y -= barH;
                    g.DrawRectangle(new RectangleF(t.X - barW / 2f - 0.0015f, y - 0.0015f, barW + 0.003f, barH + 0.003f), Color.FromArgb(170, 0, 0, 0));
                    if (t.Health > 0.001f)
                        g.DrawRectangle(new RectangleF(t.X - barW / 2f, y, barW * t.Health, barH), HealthColor(t.Health));
                    if (t.Timer.Length > 0)
                        g.DrawText(t.Timer, new RectangleF(t.X + barW / 2f + 0.005f, y + barH / 2f - smallH / 2f, 0.1f, smallH), left, Color.White, smallFonts[b]);
                    if (t.Ammo.Length > 0)
                        g.DrawText(t.Ammo, new RectangleF(t.X - barW / 2f - 0.105f, y + barH / 2f - smallH / 2f, 0.1f, smallH), right,
                            t.Ammo == "SIN BALAS" ? Color.FromArgb(255, 255, 90, 70) : Color.FromArgb(255, 235, 220, 150), smallFonts[b]);
                    if (t.Armor > 0f)
                    {
                        // chaleco: barra azul arriba de la de vida
                        float ah = barH * 0.7f;
                        y -= ah + 0.002f;
                        g.DrawRectangle(new RectangleF(t.X - barW / 2f - 0.0015f, y - 0.0015f, barW + 0.003f, ah + 0.003f), Color.FromArgb(170, 0, 0, 0));
                        g.DrawRectangle(new RectangleF(t.X - barW / 2f, y, barW * t.Armor, ah), Color.FromArgb(230, 70, 150, 255));
                    }
                    y -= gap;
                }
                else if (t.Timer.Length > 0 && !t.Dead)
                {
                    y -= smallH;
                    g.DrawText(t.Timer, new RectangleF(t.X - 0.1f, y, 0.2f, smallH), center, Color.White, smallFonts[b]);
                }
                if (t.Sub.Length > 0)
                {
                    y -= subH;
                    g.DrawText(t.Sub, new RectangleF(t.X - 0.25f, y, 0.5f, subH), center, SubColorOf(t.SubColor), subFonts[b]);
                }
                y -= nameH;
                g.DrawText(t.Name, new RectangleF(t.X - 0.25f, y, 0.5f, nameH), center,
                    t.Dead ? Color.FromArgb(255, 170, 170, 170) : t.NameColor != 0 ? Color.FromArgb(t.NameColor) : Color.White, nameFonts[b]);
                if (t.Cmds.Length > 0)
                {
                    y -= smallH;
                    g.DrawText(t.Cmds, new RectangleF(t.X - 0.35f, y, 0.7f, smallH), center, CmdYellow, smallFonts[b]);
                }
                if (t.Perks.Length > 0)
                {
                    y -= smallH * 1.15f;
                    float pw = Math.Min(0.8f, t.Perks.Length * smallH * 0.5f / aspect + 0.02f);
                    g.DrawRectangle(new RectangleF(t.X - pw / 2f, y - 0.002f, pw, smallH + 0.004f), Color.FromArgb(170, 0, 0, 0));
                    g.DrawText(t.Perks, new RectangleF(t.X - 0.45f, y, 0.9f, smallH), center, PerkCyan, smallFonts[b]);
                }
                if (t.Bubble != null && t.Bubble.Length > 0)
                {
                    int maxLen = 0;
                    foreach (string s in t.Bubble) maxLen = Math.Max(maxLen, s.Length);
                    float lineH = bubH * 1.05f;
                    float w = Math.Min(0.5f, maxLen * bubH * 0.5f / aspect + 0.018f * k);
                    float h = t.Bubble.Length * lineH + 0.010f * k;
                    y -= gap * 1.5f + h;
                    g.DrawRectangle(new RectangleF(t.X - w / 2f, y, w, h), Color.FromArgb(190, 12, 12, 12));
                    g.DrawRectangle(new RectangleF(t.X - w / 2f, y, w, 0.0025f * k), KickGreen); // borde verde (Kick)
                    for (int i = 0; i < t.Bubble.Length; i++)
                        g.DrawText(t.Bubble[i], new RectangleF(t.X - w / 2f, y + 0.005f * k + i * lineH, w, lineH), center, Color.White, bubbleFonts[b]);
                }
            }
        }

        static Color HealthColor(float h)
        {
            if (h > 0.5f) { int r = (int)(255 * (1f - h) * 2f); return Color.FromArgb(230, Math.Min(255, r), 220, 40); }
            int g = (int)(220 * h * 2f);
            return Color.FromArgb(230, 235, Math.Max(0, g), 40);
        }

        public void DisposeFonts()
        {
            foreach (var arr in new[] { nameFonts, subFonts, smallFonts, bubbleFonts })
                if (arr != null) foreach (var f in arr) try { if (f != null) f.Dispose(); } catch { }
            nameFonts = subFonts = smallFonts = bubbleFonts = null;
            foreach (var f in new[] { hudFont, hudSmall, hudTitle }) try { if (f != null) f.Dispose(); } catch { }
            hudFont = hudSmall = hudTitle = null;
        }
    }
}
