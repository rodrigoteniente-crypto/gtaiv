using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using GTA;

namespace KickChaos;

public class SubNpcManager
{
	private struct Threat
	{
		public int Ped;

		public float Dist;

		public bool Cop;

		public Gang Rival;

		public Vector3 Pos;
	}

	private class Cop
	{
		public int Ped;

		public int Car;

		public int Mode;

		public int Target;

		public int Fails;

		public int Block = -1;

		public bool Driver;

		public bool Seen;

		public double Spawned;

		public double LastTask = -100.0;

		public double ModeAt;

		public double StillSince = -1.0;

		public double LastShot = -100.0;

		public double LastSeen = -100.0;

		public double DeadAt = -1.0;

		public double PhaseLen = 4.0;

		public Vector3 Pos;

		public bool Alive => Ped != 0 && DeadAt < 0.0;
	}

	private class Member
	{
		public Gang G;

		public int Ped;

		public int Index;

		public int Mode;

		public int Target;

		public int StealCar;

		public int Phase;

		public int Weapon;

		public int Kills;

		public int Seat = -1;

		public int Fails;

		public int Car;

		public int Block = -1;

		public string Status = string.Empty;

		public string LastLogged = string.Empty;

		public string Flash = string.Empty;

		public double Created;

		public double DeadAt = -1.0;

		public double NextThink;

		public double LastTask = -100.0;

		public double ModeAt = -100.0;

		public double StillSince = -1.0;

		public double LastHealth = -100.0;

		public double LastLos = -100.0;

		public double ProtectUntil = -1.0;

		public double StealSince = -1.0;

		public double LastShot = -100.0;

		public double LastDiag = -100.0;

		public double PhaseAt = -100.0;

		public double PhaseLen = 4.0;

		public double LastHit = -100.0;

		public double FleeSince = -1.0;

		public double EatUntil = -1.0;

		public double CalmSince = -1.0;

		public double LastSeen = -100.0;

		public double DriveSince = -1.0;

		public double FlashUntil = -1.0;

		public double LastCarCheck = -100.0;

		public Vector3 Pos;

		public Vector3 Head;

		public Vector3 MoveTo;

		public bool Dead;

		public bool InCar;

		public bool Driving;

		public bool Armed;

		public bool Occluded;

		public bool FakeName;

		public bool Fleeing;

		public bool Eating;

		public bool EatStarted;

		public bool Seen;

		public bool CoverIsCar;

		public bool Arrested;

		public bool Surrender;

		public bool SurrenderRolled;

		public double HandsUpAt = -1.0;

		public bool CruiseFast;

		public uint StartHealth = 200u;

		public uint LastHp;

		public float Health01 = 1f;
	}

	private class Gang
	{
		public int Id;

		public int Slot;

		public int State;

		public int CarModel;

		public int CopsPending;

		public int PatrolsMade;

		public int CopWaits;

		public int Kills;

		public int Order = -1;

		public int NextIndex = 1;

		public int Stars;

		public int CopKills;

		public string User = string.Empty;

		public string Sub = string.Empty;

		public string Flash = string.Empty;

		public NpcBehavior Behavior;

		public bool Test;

		public bool Wanted;

		public bool Control;

		public bool Fighter;

		public bool FullLogged;

		public bool Lethal;

		public bool HostileSet;

		public bool HostileNow;

		public int PendingStars;

		public double WantedSince = -1.0;

		public double LastContact = -100.0;

		public double LastStarAt = -100.0;

		public double Created;

		public double LoadDeadline;

		public double EndAt;

		public double NextCops = -1.0;

		public double PoliceAt = -1.0;

		public double LastReinforce = -100.0;

		public double LastScan = -100.0;

		public double LastDirty = -100.0;

		public double LastKillScan = -100.0;

		public double LastCivScan = -100.0;

		public double OrderUntil = -1.0;

		public double OrderAt = -100.0;

		public double BubbleUntil = -1.0;

		public double FlashUntil = -1.0;

		public double LastWarp = -100.0;

		public double LastCmd = -100.0;

		public double AllDeadAt = -1.0;

		public double LastCopThink = -100.0;

		public double LastStoleNotice = -100.0;

		public double LastEatNotice = -100.0;

		public double LastCleanup = -100.0;

		public Vector3 Center;

		public string[] Bubble;

		public readonly List<Member> Members = new List<Member>();

		public readonly List<Cop> Cops = new List<Cop>();

		public readonly HashSet<int> Tasked = new HashSet<int>();

		public readonly Dictionary<int, Cop> AmbientCops = new Dictionary<int, Cop>();

		public readonly List<Vector3> UsedSpots = new List<Vector3>();

		public readonly HashSet<int> Counted = new HashSet<int>();

		public readonly Dictionary<int, double> Cars = new Dictionary<int, double>();

		public readonly GangRules.VoteBox Votes = new GangRules.VoteBox(3);

		public int Group => 23 + Slot;

		public Member Leader
		{
			get
			{
				foreach (Member member in Members)
				{
					if (!member.Dead)
					{
						return member;
					}
				}
				return null;
			}
		}

		public int Alive
		{
			get
			{
				int num = 0;
				foreach (Member member in Members)
				{
					if (!member.Dead)
					{
						num++;
					}
				}
				return num;
			}
		}
	}

	private const int M_NONE = 0;

	private const int M_WANDER = 1;

	private const int M_COMBAT = 2;

	private const int M_GOTO = 3;

	private const int M_DRIVE_TO = 4;

	private const int M_CRUISE = 5;

	private const int M_ENTER_CAR = 6;

	private const int M_LEAVE_CAR = 7;

	private const int M_CHASE = 8;

	private const int M_DRIVEBY = 9;

	private const int M_SHOOT = 10;

	private const int M_GOTO2 = 11;

	private const int M_COVER = 12;

	private const int M_DUCK = 13;

	private const int M_EAT = 14;

	private const int M_RIDE = 15;

	private const int M_FLEE = 16;

	private const int M_FOLLOW = 17;

	private const int M_SEEK_COVER = 18;

	private const int M_STRAFE = 19;

	private const int M_WAIT = 20;

	private const int M_ARREST = 21;

	private const int M_HANDS = 22;

	private static readonly string[] ModeNames = new string[23]
	{
		"nada", "camina", "combate", "corre", "maneja hacia", "maneja", "sube al auto", "baja del auto", "persigue", "tira desde el auto",
		"dispara", "corre derecho", "se cubre", "agachado", "come", "acompanante", "huye", "sigue al jefe", "cobertura", "al costado",
		"espera", "arresta", "manos arriba"
	};

	private const int P_NONE = 0;

	private const int P_APPROACH = 1;

	private const int P_SHOOT = 2;

	private const int P_COVER = 3;

	private const int P_DUCK = 4;

	private const int GROUP_BASE = 23;

	private const int MAX_SLOTS = 7;

	private const int GROUP_COPS = 30;

	private const int TOTAL_MAX = 16;

	private Config cfg;

	private readonly Director dir;

	private readonly Action<string> log;

	private readonly Action<string> notify;

	private readonly List<Gang> gangs = new List<Gang>();

	private volatile List<NpcTag> tags;

	private volatile float tagAspect = 1.7777778f;

	private HashSet<uint> copModels;

	private int nextId = 1;

	private bool loggedGameCover;

	private uint copType = 2u;

	private bool copTypeFromCop;

	private readonly HashSet<int> madeCops = new HashSet<int>();

	private const int MAX_COPS = 24;

	private const int MAX_AMBIENT_COPS = 12;

	private readonly string[] tiers = new string[4] { "Pelea", "Huir", "Tiroteo", "Arrasar" };

	private readonly int[] tierFrom = new int[4] { 1, 2, 6, 12 };

	private string gift = "Arrasar";

	private string other = "Pasear";

	private int maxNpcs = 3;

	private int patrols = 2;

	private int gangMax = 4;

	private int growRule;

	private int unlockRule;

	private int controlMode;

	private float maxLife = 180f;

	private float walkLife = 70f;

	private float lifeMult = 1f;

	private float fleeSpeed = 30f;

	private float killBonus = 20f;

	private float fleeAt = 0.35f;

	private float voteSeconds = 15f;

	private float orderSeconds = 30f;

	private float cmdCooldown = 4f;

	private float eatSeconds = 7f;

	private bool duel = true;

	private bool diag = true;

	private bool bringGangs = true;

	private bool gameCover = true;

	private readonly int[] slots = new int[3] { 0, 1, 2 };

	public bool ShowName = true;

	public bool HealthBar = true;

	public bool GameName;

	public bool FollowOnSpawn = true;

	public bool ShowChat = true;

	public bool ShowTimer = true;

	public float NameScale = 1f;

	private static readonly string DefaultCars = "SULTAN,BANSHEE,INFERNUS,COMET,FEROCI,PMP600,BUFFALO,TURISMO";

	private Font[] nameFonts;

	private Font[] subFonts;

	private Font[] smallFonts;

	private Font[] bubbleFonts;

	private float fontScale;

	private static readonly Color KickGreen = Color.FromArgb(255, 83, 252, 24);

	private static readonly Color CmdYellow = Color.FromArgb(255, 255, 214, 64);

	private static readonly Color FlashYellow = Color.FromArgb(255, 255, 236, 120);

	private static readonly Color Orange = Color.FromArgb(255, 255, 150, 40);

	private static readonly Color DeadRed = Color.FromArgb(255, 235, 70, 60);

	public int[] TierFrom => tierFrom;

	public int[] Choices => slots;

	public int Count => gangs.Count;

	public bool Busy => gangs.Count > 0;

	public int AliveCount
	{
		get
		{
			int num = 0;
			foreach (Gang gang in gangs)
			{
				num = ((gang.State != 1) ? (num + 1) : (num + gang.Alive));
			}
			return num;
		}
	}

	public SubNpcManager(Config cfg, Director dir, Action<string> log, Action<string> notify)
	{
		this.dir = dir;
		this.log = log;
		this.notify = notify;
		ApplyConfig(cfg);
	}

	private static string ModeName(int m)
	{
		return ModeNames[Math.Max(0, Math.Min(ModeNames.Length - 1, m))];
	}

	private static float Clamp(float v, float lo, float hi)
	{
		return Math.Max(lo, Math.Min(hi, v));
	}

	public void ApplyConfig(Config c)
	{
		cfg = c;
		IniFile ini = c.Ini;
		tiers[0] = ini.Get("Suscriptor", "Nivel1", "Pelea");
		tiers[1] = ini.Get("Suscriptor", "Nivel2", "Huir");
		tiers[2] = ini.Get("Suscriptor", "Nivel3", "Tiroteo");
		tiers[3] = ini.Get("Suscriptor", "Nivel4", "Arrasar");
		tierFrom[1] = Math.Max(2, ini.GetInt("Suscriptor", "Nivel2Desde", 2));
		tierFrom[2] = Math.Max(tierFrom[1] + 1, ini.GetInt("Suscriptor", "Nivel3Desde", 6));
		tierFrom[3] = Math.Max(tierFrom[2] + 1, ini.GetInt("Suscriptor", "Nivel4Desde", 12));
		gift = ini.Get("Suscriptor", "Regalo", "Arrasar");
		other = ini.Get("Suscriptor", "Otros", "Pasear");
		maxNpcs = Math.Max(1, Math.Min(7, ini.GetInt("Suscriptor", "Maximo", 3)));
		maxLife = Math.Max(20f, ini.GetFloat("Suscriptor", "TiempoMaximo", 180f));
		walkLife = Math.Max(10f, ini.GetFloat("Suscriptor", "DuracionPasear", 70f));
		patrols = Math.Max(0, Math.Min(6, ini.GetInt("Suscriptor", "Patrulleros", 2)));
		lifeMult = Clamp(ini.GetFloat("Suscriptor", "Vida", 1f), 0.3f, 5f);
		fleeSpeed = Clamp(ini.GetFloat("Suscriptor", "VelocidadAuto", 30f), 10f, 60f);
		duel = ini.GetBool("Suscriptor", "PelearEntreEllos", def: true);
		diag = ini.GetBool("Suscriptor", "Diagnostico", def: true);
		ShowName = ini.GetBool("Suscriptor", "MostrarNombre", def: true);
		HealthBar = ini.GetBool("Suscriptor", "BarraDeVida", def: true);
		NameScale = Clamp(ini.GetFloat("Suscriptor", "TamanoNombre", 1f), 0.5f, 2.5f);
		GameName = ini.GetBool("Suscriptor", "NombreDelJuego", def: false);
		FollowOnSpawn = ini.GetBool("Suscriptor", "IrAlAparecer", def: true);
		gangMax = Math.Max(1, Math.Min(6, ini.GetInt("Suscriptor", "BandaMaximo", 4)));
		growRule = GangRules.ParseGrow(ini.Get("Suscriptor", "SeDuplicaCon", "Policias"));
		killBonus = Clamp(ini.GetFloat("Suscriptor", "SegundosPorMuerte", 20f), 0f, 300f);
		fleeAt = Clamp(ini.GetFloat("Suscriptor", "VidaParaHuir", 35f), 0f, 90f) / 100f;
		eatSeconds = Clamp(ini.GetFloat("Suscriptor", "SegundosComiendo", 7f), 2f, 60f);
		bringGangs = ini.GetBool("Suscriptor", "TraerBandas", def: true);
		gameCover = ini.GetBool("Suscriptor", "CoberturaDelJuego", def: true);
		unlockRule = GangRules.ParseUnlock(ini.Get("Suscriptor", "ControlDelChat", "AlDuplicarse"));
		controlMode = GangRules.ParseMode(ini.Get("Suscriptor", "QuienDecide", "Suscriptor"));
		voteSeconds = Clamp(ini.GetFloat("Suscriptor", "TiempoVotacion", 15f), 5f, 120f);
		orderSeconds = Clamp(ini.GetFloat("Suscriptor", "DuracionOrden", 30f), 10f, 180f);
		cmdCooldown = Clamp(ini.GetFloat("Suscriptor", "EsperaEntreOrdenes", 4f), 0f, 60f);
		for (int i = 0; i < 3; i++)
		{
			int num = GangRules.ParseKey(ini.Get("Suscriptor", "Opcion" + (i + 1), GangRules.DefaultSlots[i]));
			slots[i] = ((num < 0) ? GangRules.ParseKey(GangRules.DefaultSlots[i]) : num);
		}
		GangRules.Dedupe(slots);
		ShowChat = ini.GetBool("Suscriptor", "MostrarMensajes", def: true);
		ShowTimer = ini.GetBool("Suscriptor", "MostrarTiempo", def: true);
	}

	private int LiveGangs()
	{
		int num = 0;
		foreach (Gang gang in gangs)
		{
			if (gang.State == 0 || gang.Alive > 0)
			{
				num++;
			}
		}
		return num;
	}

	private int TotalMembers()
	{
		int num = 0;
		foreach (Gang gang in gangs)
		{
			num += gang.Alive;
		}
		return num;
	}

	public bool AnyNear(Vector3 p, float radius)
	{
		foreach (Gang gang in gangs)
		{
			if (gang.State != 1)
			{
				continue;
			}
			foreach (Member member in gang.Members)
			{
				if (!member.Dead && Vector3.Distance(member.Pos, p) < radius)
				{
					return true;
				}
			}
		}
		return false;
	}

	public bool FollowInfo(int ped, out Vector3 pos, out bool inCar, out bool dead)
	{
		foreach (Gang gang in gangs)
		{
			if (gang.State != 1)
			{
				continue;
			}
			foreach (Member member in gang.Members)
			{
				if (member.Ped == ped)
				{
					pos = member.Pos;
					inCar = member.InCar;
					dead = member.Dead;
					return true;
				}
			}
		}
		pos = Vector3.Zero;
		inCar = false;
		dead = true;
		return false;
	}

	private float Heat(Gang g, double now)
	{
		if (g.State != 1 || g.Alive == 0)
		{
			return 0f;
		}
		float num = g.Alive;
		foreach (Member member in g.Members)
		{
			if (!member.Dead && now - member.LastShot < 6.0)
			{
				num += 2f;
			}
		}
		int num2 = 0;
		foreach (Cop cop in g.Cops)
		{
			if (cop.Alive)
			{
				num2++;
			}
		}
		num += (float)Math.Min(4, num2) * 0.5f;
		if (g.Fighter && duel)
		{
			Gang gang = NearestRivalGang(g);
			if (gang != null && GangDistance(g, gang) < 120f)
			{
				num += 4f;
			}
		}
		return num;
	}

	public int PickForCamera(int exclude)
	{
		double now = G.Now;
		List<int> list = new List<int>();
		List<double> list2 = new List<double>();
		double num = 0.0;
		foreach (Gang gang in gangs)
		{
			if (gang.State != 1)
			{
				continue;
			}
			double num2 = 1.0 + (double)Heat(gang, now);
			foreach (Member member in gang.Members)
			{
				if (!member.Dead && member.Ped != exclude)
				{
					double num3 = num2 * ((member != gang.Leader) ? 1.0 : 1.5);
					list.Add(member.Ped);
					list2.Add(num3);
					num += num3;
				}
			}
		}
		if (list.Count == 0)
		{
			return 0;
		}
		double num4 = G.Rng.NextDouble() * num;
		for (int i = 0; i < list.Count; i++)
		{
			num4 -= list2[i];
			if (num4 <= 0.0)
			{
				return list[i];
			}
		}
		return list[list.Count - 1];
	}

	public bool Hotspot(out Vector3 p)
	{
		p = Vector3.Zero;
		double now = G.Now;
		float num = 0f;
		foreach (Gang gang in gangs)
		{
			float num2 = Heat(gang, now);
			if (!(num2 <= num))
			{
				Member leader = gang.Leader;
				if (leader != null)
				{
					num = num2;
					p = leader.Pos;
				}
			}
		}
		return num > 0f;
	}

	public string Describe()
	{
		if (gangs.Count == 0)
		{
			return "Ninguno en la calle";
		}
		double now = G.Now;
		List<string> list = new List<string>();
		foreach (Gang gang in gangs)
		{
			if (gang.State == 0)
			{
				list.Add(gang.User + " (llegando)");
				continue;
			}
			Member leader = gang.Leader;
			if (leader == null)
			{
				list.Add(gang.User + " (cayo)");
				continue;
			}
			int num = 0;
			foreach (Cop cop in gang.Cops)
			{
				if (cop.Alive)
				{
					num++;
				}
			}
			string item = gang.User + ((gang.Alive <= 1) ? string.Empty : (" +" + (gang.Alive - 1))) + " (" + ((leader.Status.Length <= 0) ? NpcRules.Key(gang.Behavior) : leader.Status) + ", " + num + " poli" + ((gang.Stars <= 0) ? string.Empty : (" " + gang.Stars + "*")) + ", " + GangRules.Clock(gang.EndAt - now) + ((!gang.Control) ? string.Empty : ", control") + ")";
			list.Add(item);
		}
		return string.Join(", ", list.ToArray());
	}

	public string NewestUser()
	{
		return Newest(fighterOnly: false)?.User;
	}

	private Gang Newest(bool fighterOnly)
	{
		Gang gang = null;
		foreach (Gang gang2 in gangs)
		{
			if (gang2.State == 1 && gang2.Alive > 0 && (!fighterOnly || gang2.Fighter) && (gang == null || gang2.Created > gang.Created))
			{
				gang = gang2;
			}
		}
		return gang;
	}

	public bool TestGrow()
	{
		Gang gang = Newest(fighterOnly: true);
		if (gang == null)
		{
			return false;
		}
		Member leader = gang.Leader;
		return leader != null && Grow(gang, leader, G.Now, test: true);
	}

	public bool Spawn(QueuedAction qa, Vector3 center, NpcBehavior? forced, out string label)
	{
		label = null;
		string text = (qa.User ?? string.Empty).Trim();
		if (text.Length == 0)
		{
			text = "Anonimo";
		}
		if (text.Length > 25)
		{
			text = text.Substring(0, 25);
		}
		NpcBehavior npcBehavior = ((!forced.HasValue) ? NpcRules.Resolve(NpcRules.ChooseName(qa.Source, qa.Count, tiers, tierFrom, gift, other), G.Rng) : forced.Value);
		while (LiveGangs() >= maxNpcs && ReleaseOldest())
		{
		}
		int num = FreeSlot();
		while (num < 0 && ReleaseOldest())
		{
			num = FreeSlot();
		}
		if (num < 0)
		{
			log("[NPC] no hay lugar para otra banda");
			return false;
		}
		double now = G.Now;
		Gang gang = new Gang();
		gang.Id = nextId++;
		gang.Slot = num;
		gang.User = text;
		gang.Behavior = npcBehavior;
		gang.Sub = NpcRules.Subtitle(qa.Source, qa.Count);
		gang.Created = now;
		gang.Center = center;
		gang.Fighter = npcBehavior != NpcBehavior.Pasear;
		gang.Test = string.Equals(text, "prueba", StringComparison.OrdinalIgnoreCase);
		Gang gang2 = gang;
		if (npcBehavior == NpcBehavior.Huir)
		{
			gang2.CarModel = PickCarModel();
			if (gang2.CarModel != 0)
			{
				N.REQUEST_MODEL(gang2.CarModel);
				gang2.LoadDeadline = now + 5.0;
				gang2.State = 0;
				gangs.Add(gang2);
				label = NpcRules.Label(npcBehavior);
				log("[NPC] " + text + ": " + label + " (cargando el auto)");
				return true;
			}
			npcBehavior = (gang2.Behavior = NpcBehavior.Robar);
		}
		Member member = NewMember(gang2, FindSpawnPoint(center), now);
		if (member == null)
		{
			log("[NPC] no se pudo crear el personaje de " + text);
			return false;
		}
		gangs.Add(gang2);
		ActivateGang(gang2, member, now);
		label = NpcRules.Label(gang2.Behavior);
		return true;
	}

	private int FreeSlot()
	{
		for (int i = 0; i < 7; i++)
		{
			bool flag = false;
			foreach (Gang gang in gangs)
			{
				if (gang.Slot == i)
				{
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				return i;
			}
		}
		return -1;
	}

	private int PickCarModel()
	{
		List<int> list = new List<int>();
		string[] array = cfg.Ini.Get("Suscriptor", "Autos", DefaultCars).Split(new char[1] { ',' });
		foreach (string text in array)
		{
			string text2 = text.Trim();
			if (text2.Length != 0)
			{
				int num = N.GET_HASH_KEY(text2);
				if (N.IS_MODEL_IN_CDIMAGE(num))
				{
					list.Add(num);
				}
			}
		}
		return (list.Count != 0) ? list[G.Rng.Next(list.Count)] : 0;
	}

	private static bool OpenSky(Vector3 p, float groundZ)
	{
		float z;
		return !G.TopZ(p.X, p.Y, out z) || z - groundZ < 1.5f;
	}

	private static float FlatDist(Vector3 a, Vector3 b)
	{
		return GangRules.Flat(a, b);
	}

	private static Vector3 Ground(Vector3 p, float refZ)
	{
		if (G.GroundZ(new Vector3(p.X, p.Y, refZ + 1.5f), out var z) && Math.Abs(z - refZ) < 3f)
		{
			p.Z = z;
		}
		else
		{
			p.Z = refZ;
		}
		return p;
	}

	private Vector3 FindSpawnPoint(Vector3 center)
	{
		Vector3 result = Vector3.Zero;
		Vector3 vector = Vector3.Zero;
		bool flag = false;
		float[] array = new float[5] { 6f, -6f, 4.5f, -4.5f, 0f };
		for (uint num = 1u; num <= 14; num++)
		{
			if (!N.GET_NTH_CLOSEST_CAR_NODE_WITH_HEADING(center, num, out var result2, out var heading) || result2 == Vector3.Zero || FlatDist(result2, center) > 70f)
			{
				continue;
			}
			if (vector == Vector3.Zero)
			{
				vector = result2;
			}
			if (G.TopZ(result2.X, result2.Y, out var z) && z - result2.Z > 3f)
			{
				continue;
			}
			Vector3 vector2 = MathX.Right(new Vector3(0f, 0f, heading));
			float[] array2 = array;
			foreach (float num2 in array2)
			{
				Vector3 vector3 = result2 + vector2 * num2;
				if (G.GroundZ(new Vector3(vector3.X, vector3.Y, result2.Z + 1.5f), out var z2) && !(Math.Abs(z2 - result2.Z) > 1.5f) && OpenSky(vector3, z2))
				{
					vector3.Z = z2;
					if (!flag)
					{
						result = vector3;
						flag = true;
					}
					if (!dir.Active || !dir.InFrame(vector3 + new Vector3(0f, 0f, 1f), 1f))
					{
						return vector3;
					}
					break;
				}
			}
		}
		if (flag)
		{
			return result;
		}
		if (vector != Vector3.Zero)
		{
			return vector;
		}
		return center;
	}

	private Member NewMember(Gang g, Vector3 p, double now)
	{
		int ped = 0;
		for (int i = 0; i < 3; i++)
		{
			if (ped != 0)
			{
				break;
			}
			N.CREATE_RANDOM_CHAR(p + new Vector3(0f, 0f, 1f), out ped);
			if (ped != 0 && !N.DOES_CHAR_EXIST(ped))
			{
				ped = 0;
			}
			if (ped != 0 && IsCop(ped))
			{
				try
				{
					N.DELETE_CHAR(ped);
				}
				catch
				{
				}
				ped = 0;
			}
		}
		if (ped == 0)
		{
			return null;
		}
		Member member = new Member();
		member.G = g;
		member.Ped = ped;
		member.Index = ((g.Members.Count != 0) ? g.NextIndex++ : 0);
		member.Created = now;
		Member member2 = member;
		N.SET_CHAR_HEADING(ped, G.Rand(0f, 360f));
		g.Members.Add(member2);
		return member2;
	}

	private Member SpawnInCar(Gang g, double now)
	{
		Vector3 vector = g.Center;
		float h = G.Rand(0f, 360f);
		bool flag = false;
		for (uint num = 1u; num <= 8; num++)
		{
			if (N.GET_NTH_CLOSEST_CAR_NODE_WITH_HEADING(g.Center, num, out var result, out var heading) && !(result == Vector3.Zero) && !(FlatDist(result, g.Center) > 90f) && OpenSky(result, result.Z) && G.VehiclesNear(result, 4f, 1, includeDead: true).Count <= 0)
			{
				if (!flag)
				{
					vector = result;
					h = heading;
					flag = true;
				}
				if (!dir.Active || !dir.InFrame(result, 3f))
				{
					vector = result;
					h = heading;
					break;
				}
			}
		}
		if (!flag)
		{
			return null;
		}
		N.CREATE_CAR(g.CarModel, vector + new Vector3(0f, 0f, 0.5f), out var veh, b: true);
		if (veh == 0 || !N.DOES_VEHICLE_EXIST(veh))
		{
			return null;
		}
		N.SET_CAR_HEADING(veh, h);
		int ped = 0;
		try
		{
			N.CREATE_RANDOM_CHAR_AS_DRIVER(veh, out ped);
		}
		catch
		{
			ped = 0;
		}
		if (ped == 0 || !N.DOES_CHAR_EXIST(ped))
		{
			N.CREATE_RANDOM_CHAR(vector + new Vector3(3f, 0f, 1f), out ped);
			if (ped == 0 || !N.DOES_CHAR_EXIST(ped))
			{
				N.MARK_CAR_AS_NO_LONGER_NEEDED(veh);
				return null;
			}
			N.WARP_CHAR_INTO_CAR(ped, veh);
		}
		Member member = new Member();
		member.G = g;
		member.Ped = ped;
		member.Index = 0;
		member.Created = now;
		member.Car = veh;
		member.InCar = true;
		member.Driving = true;
		Member member2 = member;
		g.Members.Add(member2);
		g.Cars[veh] = now;
		return member2;
	}

	private uint CopType(Vector3 near)
	{
		// GTA IV's creation type COP is 2. A civilian's gang type does not
		// identify an alternate enum layout; guessing 3/6 creates gang members.
		if (copTypeFromCop || N.IsMissing("GET_PED_TYPE"))
		{
			return copType;
		}
		try
		{
			foreach (int item in G.PedsNear(near, 250f, 30))
			{
				if (madeCops.Contains(item) || !IsCop(item))
				{
					continue;
				}
				N.GET_PED_TYPE(item, out var type);
				if (type != 2u)
				{
					continue;
				}
				copTypeFromCop = true;
				if (type != copType)
				{
					log("[NPC] tipo de los policias del juego: " + type + " (antes " + copType + ")");
				}
				else
				{
					log("[NPC] tipo de los policias del juego: " + type);
				}
				copType = type;
				break;
			}
		}
		catch
		{
		}
		return copType;
	}

	private static uint BaseHealth(NpcBehavior b)
	{
		return b switch
		{
			NpcBehavior.Pasear => 200u, 
			NpcBehavior.Pelea => 300u, 
			NpcBehavior.Huir => 300u, 
			NpcBehavior.Robar => 300u, 
			NpcBehavior.Tiroteo => 400u, 
			_ => 800u, 
		};
	}

	private static void GiveGun(Member m, int weapon, int ammo)
	{
		N.GIVE_WEAPON_TO_CHAR(m.Ped, weapon, ammo, b: false);
		N.SET_CURRENT_CHAR_WEAPON(m.Ped, weapon, b: true);
		m.Weapon = weapon;
		m.Armed = weapon >= 7;
	}

	private void ActivateGang(Gang g, Member leader, double now)
	{
		g.State = 1;
		g.EndAt = now + (double)((g.Behavior != NpcBehavior.Pasear) ? maxLife : walkLife);
		SetupMember(g, leader, now);
		int ped = leader.Ped;
		switch (g.Behavior)
		{
		case NpcBehavior.Pasear:
			Wander(leader, now);
			Say(leader, "paseando");
			break;
		case NpcBehavior.Pelea:
			Wander(leader, now);
			g.PendingStars = 1;
			g.PoliceAt = now + 20.0;
			break;
		case NpcBehavior.Huir:
			Cruise(leader, leader.Car, fleeSpeed, now, 5);
			Say(leader, "escapando en auto");
			SetStars(g, 2, "se escapa de la policia", now);
			break;
		case NpcBehavior.Robar:
			Say(leader, "buscando un auto para robar");
			break;
		case NpcBehavior.Tiroteo:
			SetStars(g, 3, "busca tiroteo con la policia", now);
			break;
		default:
			SetStars(g, 3, "arrasa con todo", now);
			break;
		}
		if (unlockRule == 1 && g.Fighter)
		{
			g.Control = true;
		}
		if (GameName)
		{
			try
			{
				N.GIVE_PED_FAKE_NETWORK_NAME(ped, g.User, 255, 255, 255, 255);
				leader.FakeName = true;
			}
			catch
			{
			}
		}
		Changed();
		log("[NPC] aparecio " + g.User + " -> " + NpcRules.Key(g.Behavior) + ((g.Sub.Length <= 0) ? string.Empty : (" (" + g.Sub + ")")) + " a " + FlatDist(leader.Pos, g.Center).ToString("0") + " m del centro" + ((!g.Control) ? string.Empty : " (control por chat activo)"));
		if (FollowOnSpawn && dir.Active)
		{
			dir.FollowNow(ped);
		}
	}

	private void SetupMember(Gang g, Member m, double now)
	{
		int ped = m.Ped;
		m.Pos = G.CharPos(ped);
		m.Head = m.Pos + new Vector3(0f, 0f, 1f);
		m.NextThink = now + 0.4 + 0.13 * (double)m.Index;
		uint num = (uint)Math.Max(120f, (float)BaseHealth(g.Behavior) * lifeMult);
		if (m.Index > 0)
		{
			num = (uint)Math.Max(120f, (float)num * 0.75f);
		}
		N.SET_CHAR_MAX_HEALTH(ped, num);
		N.SET_CHAR_HEALTH(ped, num);
		m.StartHealth = num;
		m.LastHp = num;
		N.SET_CHAR_KEEP_TASK(ped, v: true);
		N.SET_CHAR_DROPS_WEAPONS_WHEN_DEAD(ped, v: false);
		N.SET_CHAR_PROOFS(ped, a: false, b: true, c: true, d: false, e: false);
		m.ProtectUntil = now + ((m.Index != 0) ? 4.0 : 10.0);
		if (!g.Fighter)
		{
			return;
		}
		MakeFighter(g, m);
		N.SET_CHAR_SHOOT_RATE(ped, 100);
		N.SET_SENSE_RANGE(ped, CombatPolicy.SightRange);
		N.SET_CHAR_WILL_MOVE_WHEN_INJURED(ped, v: true);
		N.SET_CHAR_WILL_USE_COVER(ped, v: true);
		N.SET_CHAR_WILL_DO_DRIVEBYS(ped, v: true);
		N.SET_CHAR_CANT_BE_DRAGGED_OUT(ped, v: true);
		N.SET_CHAR_STAY_IN_CAR_WHEN_JACKED(ped, v: true);
		N.SET_CHAR_WILL_LEAVE_CAR_IN_COMBAT(ped, v: false);
		if (m.Index == 0)
		{
			switch (g.Behavior)
			{
			case NpcBehavior.Pelea:
				if (G.Rng.NextDouble() < 0.5)
				{
					GiveGun(m, 1, 1);
				}
				break;
			case NpcBehavior.Huir:
				GiveGun(m, 12, 600);
				N.ADD_ARMOUR_TO_CHAR(ped, 50);
				break;
			case NpcBehavior.Robar:
				GiveGun(m, 7, 400);
				break;
			case NpcBehavior.Tiroteo:
			{
				int[] array = new int[5] { 7, 9, 12, 13, 10 };
				GiveGun(m, array[G.Rng.Next(array.Length)], 800);
				N.SET_CHAR_ACCURACY(ped, 45u);
				N.ADD_ARMOUR_TO_CHAR(ped, 100);
				break;
			}
			default:
				GiveGun(m, (!(G.Rng.NextDouble() < 0.5)) ? 15 : 14, 2000);
				N.SET_CHAR_ACCURACY(ped, 60u);
				N.SET_CHAR_SUFFERS_CRITICAL_HITS(ped, v: false);
				N.ADD_ARMOUR_TO_CHAR(ped, 200);
				break;
			}
		}
		else
		{
			Member member = null;
			foreach (Member member2 in g.Members)
			{
				if (member2 != m && member2.Armed)
				{
					member = member2;
					break;
				}
			}
			int[] array2 = new int[4] { 7, 12, 13, 9 };
			GiveGun(m, member?.Weapon ?? array2[G.Rng.Next(array2.Length)], 800);
			N.SET_CHAR_ACCURACY(ped, 40u);
			N.ADD_ARMOUR_TO_CHAR(ped, 50);
		}
		if (g.Wanted)
		{
			OutlawPed(g, m);
		}
	}

	private void MakeFighter(Gang g, Member m)
	{
		int ped = m.Ped;
		N.SET_CHAR_RELATIONSHIP_GROUP(ped, g.Group);
		N.SET_CHAR_RELATIONSHIP(ped, 0u, g.Group);
		bool flag = Hostile(g, G.Now);
		N.SET_CHAR_RELATIONSHIP(ped, (!flag) ? 3u : 5u, 3);
		N.SET_CHAR_RELATIONSHIP(ped, (!flag) ? 3u : 5u, 30);
		if (duel)
		{
			foreach (Gang gang in gangs)
			{
				if (gang == g || !gang.Fighter || gang.State != 1)
				{
					continue;
				}
				N.SET_CHAR_RELATIONSHIP(ped, 5u, gang.Group);
				foreach (Member member in gang.Members)
				{
					if (!member.Dead && member.Ped != 0 && N.DOES_CHAR_EXIST(member.Ped))
					{
						N.SET_CHAR_RELATIONSHIP(member.Ped, 5u, g.Group);
					}
				}
			}
		}
		if (g.Behavior != NpcBehavior.Arrasar)
		{
			return;
		}
		for (int i = 1; i <= 22; i++)
		{
			if (i != 3)
			{
				N.SET_CHAR_RELATIONSHIP(ped, 5u, i);
			}
		}
	}

	private bool Hostile(Gang g, double now)
	{
		return g.Behavior == NpcBehavior.Tiroteo || g.Behavior == NpcBehavior.Arrasar || g.Stars >= 3 || (g.Order == 1 && now < g.OrderUntil);
	}

	private void UpdateHostility(Gang g, double now)
	{
		bool flag = Hostile(g, now);
		if (g.HostileSet && g.HostileNow == flag)
		{
			return;
		}
		g.HostileSet = true;
		g.HostileNow = flag;
		foreach (Member member in g.Members)
		{
			if (!member.Dead && member.Ped != 0 && N.DOES_CHAR_EXIST(member.Ped))
			{
				N.SET_CHAR_RELATIONSHIP(member.Ped, (!flag) ? 3u : 5u, 3);
				N.SET_CHAR_RELATIONSHIP(member.Ped, (!flag) ? 3u : 5u, 30);
				if (flag && !member.InCar)
				{
					member.Mode = 0;
				}
			}
		}
	}

	private void SetStars(Gang g, int n, string why, double now)
	{
		if (!g.Fighter)
		{
			return;
		}
		n = Math.Max(0, Math.Min(5, n));
		if (n > g.Stars)
		{
			int stars = g.Stars;
			g.Stars = n;
			g.LastStarAt = now;
			log("[NPC] " + ((g.Alive <= 1) ? g.User : ("la banda de " + g.User)) + ": " + n + ((n != 1) ? " estrellas" : " estrella") + " (" + why + ")" + ((n >= 3 && stars < 3) ? " - la policia tira a matar" : ((n > 2 || stars != 0) ? string.Empty : " - lo quieren arrestar")));
			if (!g.Wanted)
			{
				MakeOutlaw(g, now);
			}
			if (n >= 3)
			{
				MakeLethal(g);
			}
			UpdateHostility(g, now);
			g.Flash = new string('*', n) + ((n < 3) ? " BUSCADO" : " A MATAR");
			g.FlashUntil = now + 3.0;
		}
	}

	private void MakeOutlaw(Gang g, double now)
	{
		g.Wanted = true;
		g.WantedSince = now;
		g.LastContact = now;
		g.PoliceAt = -1.0;
		if (g.CopsPending == 0)
		{
			g.CopsPending = 1;
			g.NextCops = now + 2.0;
		}
		foreach (Member member in g.Members)
		{
			if (!member.Dead && member.Ped != 0)
			{
				OutlawPed(g, member);
			}
		}
	}

	private void LoseCops(Gang g, double now)
	{
		ReleaseAmbientCops(g);
		log("[NPC] " + ((g.Alive <= 1) ? g.User : ("la banda de " + g.User)) + " perdio a la policia (" + g.Stars + " estrellas -> 0)");
		notify(g.User + " -> PERDIO A LA POLICIA");
		g.Stars = 0;
		g.Wanted = false;
		g.Lethal = false;
		g.CopsPending = 0;
		g.PendingStars = 0;
		foreach (Member member in g.Members)
		{
			if (!member.Dead && member.Ped != 0 && N.DOES_CHAR_EXIST(member.Ped))
			{
				N.SET_CHAR_WANTED_BY_POLICE(member.Ped, v: false);
				member.SurrenderRolled = false;
			}
		}
		foreach (Cop cop in g.Cops)
		{
			try
			{
				ReleaseManagedCop(cop);
				if (cop.Car != 0 && N.DOES_VEHICLE_EXIST(cop.Car))
				{
					N.MARK_CAR_AS_NO_LONGER_NEEDED(cop.Car);
				}
			}
			catch
			{
			}
		}
		g.Cops.Clear();
		g.Tasked.Clear();
		UpdateHostility(g, now);
		Changed();
	}

	private void OutlawPed(Gang g, Member m)
	{
		int ped = m.Ped;
		if (N.DOES_CHAR_EXIST(ped))
		{
			N.SET_CHAR_WANTED_BY_POLICE(ped, v: true);
			N.SET_CHAR_IS_TARGET_PRIORITY(ped, v: true);
			N.SET_CHAR_WILL_USE_COVER(ped, v: true);
			if (!m.Armed)
			{
				GiveGun(m, 7, 300);
			}
		}
	}

	public void Update()
	{
		if (gangs.Count == 0)
		{
			return;
		}
		double now = G.Now;
		bool changed = false;
		for (int i = 0; i < gangs.Count; i++)
		{
			Gang gang = gangs[i];
			bool flag;
			try
			{
				flag = StepGang(gang, now, ref changed);
			}
			catch (Exception ex)
			{
				log("[NPC] error con la banda de " + gang.User + ": " + ex.Message);
				flag = false;
			}
			if (!flag)
			{
				ReleaseGang(gang);
				gangs.RemoveAt(i--);
				changed = true;
			}
		}
		if (changed)
		{
			Changed();
		}
	}

	private bool StepGang(Gang g, double now, ref bool changed)
	{
		if (g.State == 0)
		{
			bool flag = N.HAS_MODEL_LOADED(g.CarModel);
			if (!flag && now < g.LoadDeadline)
			{
				return true;
			}
			Member member = ((!flag) ? null : SpawnInCar(g, now));
			N.MARK_MODEL_AS_NO_LONGER_NEEDED(g.CarModel);
			if (member == null)
			{
				g.Behavior = NpcBehavior.Robar;
				member = NewMember(g, FindSpawnPoint(g.Center), now);
				if (member == null)
				{
					log("[NPC] no se pudo crear el personaje de " + g.User);
					return false;
				}
			}
			ActivateGang(g, member, now);
			return true;
		}
		for (int i = 0; i < g.Members.Count; i++)
		{
			Member m = g.Members[i];
			bool flag2;
			try
			{
				flag2 = StepMember(g, m, now);
			}
			catch (Exception ex)
			{
				log("[NPC] error con " + Name(g, m) + ": " + ex.Message);
				flag2 = false;
			}
			if (!flag2)
			{
				ReleaseMember(m);
				g.Members.RemoveAt(i--);
				changed = true;
			}
		}
		if (g.Members.Count == 0)
		{
			return false;
		}
		foreach (Cop cop in g.Cops)
		{
			if (!cop.Alive)
			{
				continue;
			}
			if (!N.DOES_CHAR_EXIST(cop.Ped))
			{
				cop.DeadAt = now;
				continue;
			}
			if (N.IS_CHAR_DEAD(cop.Ped) || N.IS_CHAR_FATALLY_INJURED(cop.Ped))
			{
				cop.DeadAt = now;
				continue;
			}
			cop.Pos = G.CharPos(cop.Ped);
			if (N.IS_CHAR_SHOOTING(cop.Ped))
			{
				cop.LastShot = now;
			}
		}
		if (g.Alive == 0)
		{
			if (g.AllDeadAt < 0.0)
			{
				g.AllDeadAt = now;
				log("[NPC] " + ((g.Members.Count <= 1 && g.Kills <= 0) ? (g.User + " cayo") : ("la banda de " + g.User + " cayo entera")) + " despues de " + (now - g.Created).ToString("0") + " s (" + g.Kills + " muertes)");
				notify(g.User + ((!g.Members.TrueForAll((Member x) => x.Arrested)) ? " -> FUERA DE COMBATE" : " -> ARRESTADO"));
				if (g.Fighter)
				{
					dir.Repair.MarkDirty(g.Members[0].Pos);
				}
			}
			return now - g.AllDeadAt < 6.0;
		}
		if (now > g.EndAt)
		{
			log("[NPC] " + ((g.Alive <= 1) ? g.User : ("la banda de " + g.User)) + " se fue (se termino su tiempo)");
			return false;
		}
		Member leader = g.Leader;
		if (g.Wanted && now - g.LastDirty > 10.0)
		{
			g.LastDirty = now;
			dir.Repair.MarkDirty(leader.Pos);
		}
		if (g.PoliceAt > 0.0 && now >= g.PoliceAt && g.Fighter)
		{
			g.PoliceAt = -1.0;
			SetStars(g, Math.Max(1, g.PendingStars), (g.Behavior != NpcBehavior.Pelea) ? "se robo un auto" : "se agarro a pinas", now);
			Say(leader, "lo busca la policia");
		}
		if (g.Wanted)
		{
			if (now - g.LastContact > 40.0 && g.CopsPending == 0 && now - g.WantedSince > 30.0 && g.PatrolsMade > 0)
			{
				LoseCops(g, now);
			}
			else if (g.Stars < 4 && now - g.LastStarAt > 75.0 && now - g.LastContact < 10.0)
			{
				SetStars(g, g.Stars + 1, "sigue escapando de la policia", now);
			}
		}
		UpdateHostility(g, now);
		if (g.CopsPending > 0 && g.NextCops > 0.0 && now >= g.NextCops)
		{
			SpawnPatrol(g, now);
		}
		if (now - g.LastKillScan > 0.5)
		{
			g.LastKillScan = now;
			ScanKills(g, now);
		}
		if (g.Votes.Due(now))
		{
			int num = g.Votes.Winner();
			int total = g.Votes.Total;
			g.Votes.Reset();
			if (num >= 0 && g.Control)
			{
				ApplyOrder(g, slots[num], (total != 1) ? (total + " votos") : "1 voto", now);
			}
		}
		if (g.Order >= 0 && now >= g.OrderUntil)
		{
			g.Order = -1;
			log("[NPC] la banda de " + g.User + " termino la orden del chat");
		}
		if (now - g.LastCopThink > 0.5)
		{
			g.LastCopThink = now;
			if (g.Cops.Count > 0 || g.Wanted)
			{
				ManageCops(g, now);
			}
			Reinforce(g, now);
			BringRival(g, now);
		}
		if (now - g.LastCleanup > 2.0)
		{
			g.LastCleanup = now;
			if (Cleanup(g, now))
			{
				changed = true;
			}
		}
		return true;
	}

	private bool StepMember(Gang g, Member m, double now)
	{
		if (m.Ped == 0 || !N.DOES_CHAR_EXIST(m.Ped))
		{
			if (!m.Dead)
			{
				log("[NPC] " + Name(g, m) + " desaparecio");
			}
			return false;
		}
		m.InCar = N.IS_CHAR_IN_ANY_CAR(m.Ped);
		m.Pos = G.CharPos(m.Ped);
		m.Head = m.Pos + new Vector3(0f, 0f, 1f);
		if (m.Dead)
		{
			return now - m.DeadAt < 8.0 || g.Alive == 0;
		}
		if (N.IS_CHAR_DEAD(m.Ped) || N.IS_CHAR_FATALLY_INJURED(m.Ped))
		{
			OnMemberDeath(g, m, now);
			return true;
		}
		bool flag = m.Mode == 22 && m.HandsUpAt > 0.0 && now - m.HandsUpAt > 8.0 && CopWithin(g, m.Pos, 6f);
		if (N.HAS_CHAR_BEEN_ARRESTED(m.Ped) || flag)
		{
			m.Arrested = true;
			log("[NPC] " + Name(g, m) + " fue ARRESTADO");
			notify(DisplayName(g, m) + " -> ARRESTADO");
			OnMemberDeath(g, m, now);
			return true;
		}
		if (m.InCar && now - m.LastCarCheck > 0.25)
		{
			m.LastCarCheck = now;
			N.GET_CAR_CHAR_IS_USING(m.Ped, out var veh);
			if (veh != 0)
			{
				N.GET_DRIVER_OF_CAR(veh, out var ped);
				m.Driving = ped == m.Ped;
				if (veh != m.Car)
				{
					OnGotCar(g, m, veh, now);
				}
				g.Cars[veh] = now;
			}
		}
		else if (!m.InCar)
		{
			m.Driving = false;
		}
		if (m.ProtectUntil > 0.0 && now > m.ProtectUntil)
		{
			m.ProtectUntil = -1.0;
			N.SET_CHAR_PROOFS(m.Ped, a: false, b: false, c: false, d: false, e: false);
		}
		if (now - m.LastHealth > 0.2)
		{
			m.LastHealth = now;
			N.GET_CHAR_HEALTH(m.Ped, out var v);
			if (v + 3 < m.LastHp)
			{
				m.LastHit = now;
			}
			m.LastHp = v;
			m.Health01 = Math.Max(0f, Math.Min(1f, (float)v / (float)Math.Max(1u, m.StartHealth)));
		}
		if (N.IS_CHAR_SHOOTING(m.Ped))
		{
			m.LastShot = now;
			if (g.Stars < 2)
			{
				SetStars(g, 2, "disparo un arma", now);
			}
		}
		if (diag && now - m.LastDiag > 3.0 && now - m.Created < 150.0)
		{
			m.LastDiag = now;
			Diagnose(g, m, now);
		}
		if (now >= m.NextThink)
		{
			m.NextThink = now + 0.5;
			Think(g, m, now);
		}
		return true;
	}

	private void OnGotCar(Gang g, Member m, int car, double now)
	{
		bool flag = m.Mode == 6 && m.StealCar == car && m.Driving;
		m.Car = car;
		m.StealCar = 0;
		m.Seat = -1;
		if (m.Driving)
		{
			try
			{
				N.SET_CAR_AS_MISSION_CAR(car);
			}
			catch
			{
			}
			m.DriveSince = now;
		}
		Changed();
		if (flag)
		{
			m.Mode = 0;
			Say(m, "se robo un auto");
			if (now - g.LastStoleNotice > 20.0)
			{
				g.LastStoleNotice = now;
				notify(DisplayName(g, m) + " -> SE ROBO UN AUTO");
			}
			if (!g.Wanted && g.Fighter && g.PoliceAt < 0.0)
			{
				g.PendingStars = Math.Max(g.PendingStars, 1);
				g.PoliceAt = now + 10.0;
			}
		}
	}

	private void OnMemberDeath(Gang g, Member m, double now)
	{
		m.Dead = true;
		m.DeadAt = now;
		m.Health01 = 0f;
		int alive = g.Alive;
		if (alive > 0)
		{
			log("[NPC] cayo " + Name(g, m) + " despues de " + (now - m.Created).ToString("0") + " s (quedan " + alive + ")");
			if (m.Index == 0)
			{
				notify(g.User + " -> CAYO (SU BANDA SIGUE)");
			}
		}
		if (g.Fighter && alive > 0)
		{
			dir.Repair.MarkDirty(m.Pos);
		}
	}

	private static void Task(Member m, int mode, double now)
	{
		if (m.Mode != mode)
		{
			m.ModeAt = now;
		}
		m.Mode = mode;
		m.LastTask = now;
		if (m.G != null && m.G.Fighter)
		{
			// These are explicit script tasks, including combat. Allowing ambient
			// fear/crime events here can replace them with civilian fleeing tasks.
			Block(m, mode != M_HANDS);
		}
	}

	private static void Block(Member m, bool on)
	{
		int num = (on ? 1 : 0);
		if (m.Block != num && m.Ped != 0)
		{
			N.SET_BLOCKING_OF_NON_TEMPORARY_EVENTS(m.Ped, on);
			m.Block = num;
		}
	}

	private static void Block(Cop c, bool on)
	{
		int num = (on ? 1 : 0);
		if (c.Block != num && c.Ped != 0)
		{
			N.SET_BLOCKING_OF_NON_TEMPORARY_EVENTS(c.Ped, on);
			c.Block = num;
		}
	}

	private void Wander(Member m, double now)
	{
		N._TASK_WANDER_STANDARD(m.Ped);
		Task(m, 1, now);
		m.Target = 0;
		m.StillSince = -1.0;
	}

	private void RunTo(Member m, Vector3 p, double now, bool straight)
	{
		if (straight)
		{
			N._TASK_GO_STRAIGHT_TO_COORD(m.Ped, p, 4);
		}
		else
		{
			N._TASK_FOLLOW_NAV_MESH_TO_COORD(m.Ped, p, 4);
		}
		Task(m, (!straight) ? 3 : 11, now);
		m.MoveTo = p;
		m.StillSince = -1.0;
	}

	private void Shoot(Member m, int target, double now)
	{
		int num = (int)(G.Rand(3f, 5.5f) * 1000f);
		N._TASK_SHOOT_AT_CHAR(m.Ped, target, num, 4);
		Task(m, 10, now);
		m.Target = target;
		m.Phase = 2;
		m.PhaseAt = now;
		m.PhaseLen = (double)num / 1000.0;
	}

	private void Duck(Member m, double now)
	{
		int num = (int)(G.Rand(1.2f, 2.4f) * 1000f);
		N._TASK_DUCK(m.Ped, num);
		Task(m, 13, now);
		m.Phase = 4;
		m.PhaseAt = now;
		m.PhaseLen = (double)num / 1000.0;
	}

	private void Cruise(Member m, int car, float speed, double now, int mode)
	{
		N._TASK_CAR_DRIVE_WANDER(m.Ped, car, speed, 2u);
		bool flag = m.Mode == mode;
		Task(m, mode, now);
		if (!flag)
		{
			m.StillSince = -1.0;
		}
	}

	private void EnterAsDriver(Member m, int car, double now)
	{
		N._TASK_ENTER_CAR_AS_DRIVER(m.Ped, car, 0u);
		Task(m, 6, now);
		m.StealCar = car;
		m.Seat = -1;
		m.StealSince = now;
	}

	private void EnterAsPassenger(Member m, int car, int seat, double now)
	{
		N._TASK_ENTER_CAR_AS_PASSENGER(m.Ped, car, 0u, (uint)seat);
		Task(m, 6, now);
		m.StealCar = car;
		m.Seat = seat;
		m.StealSince = now;
	}

	private void LeaveCar(Member m, double now)
	{
		N._TASK_LEAVE_ANY_CAR(m.Ped);
		Task(m, 7, now);
		m.Phase = 0;
	}

	private void BailOut(Gang g, Member m, double now, string why)
	{
		int car = m.Car;
		LeaveCar(m, now);
		foreach (Member member in g.Members)
		{
			if (member != m && !member.Dead && member.InCar && member.Car == car && !member.Eating)
			{
				LeaveCar(member, now);
			}
		}
		Say(m, why);
	}

	private bool Moving(Member m, double now, float minSpeed)
	{
		float v;
		if (m.InCar && m.Car != 0 && N.DOES_VEHICLE_EXIST(m.Car))
		{
			N.GET_CAR_SPEED(m.Car, out v);
		}
		else
		{
			N.GET_CHAR_SPEED(m.Ped, out v);
		}
		if (v >= minSpeed)
		{
			m.StillSince = -1.0;
			return true;
		}
		if (m.StillSince < 0.0)
		{
			m.StillSince = now;
		}
		return false;
	}

	private static double StillFor(Member m, double now)
	{
		return (!(m.StillSince < 0.0)) ? (now - m.StillSince) : 0.0;
	}

	private static bool CarOk(int car)
	{
		return car != 0 && N.DOES_VEHICLE_EXIST(car) && !N.IS_CAR_DEAD(car) && !N.IS_CAR_ON_FIRE(car) && !N.IS_CAR_UPSIDEDOWN(car) && !N.IS_CAR_STUCK_ON_ROOF(car);
	}

	private void Think(Gang g, Member m, double now)
	{
		if (m.Eating)
		{
			ThinkEat(g, m, now);
			return;
		}
		if (m.InCar && m.Mode == 7)
		{
			if (now - m.ModeAt > 4.0)
			{
				LeaveCar(m, now);
			}
			return;
		}
		bool flag = g.Order >= 0 && now < g.OrderUntil;
		if (m.Fleeing)
		{
			ThinkFlee(g, m, now);
		}
		else if (g.Fighter && m.Health01 < fleeAt && now - m.Created > 4.0 && (!flag || g.Order != 1 || !(m.Health01 > 0.12f)))
		{
			StartFlee(g, m, now);
			ThinkFlee(g, m, now);
		}
		else if (m.InCar && !m.Driving)
		{
			ThinkPassenger(g, m, now);
		}
		else
		{
			if (flag && DoOrder(g, m, now))
			{
				return;
			}
			if (!g.Fighter)
			{
				ThinkWalker(g, m, now);
				return;
			}
			float radius = ((!m.InCar) ? 75f : 45f);
			Threat t = Sticky(g, m, NearestThreat(g, m, radius), radius);
			if (m.InCar)
			{
				ThinkDriver(g, m, t, now);
			}
			else
			{
				if (m.Mode == 7 && now - m.ModeAt < 1.0)
				{
					return;
				}
				if (m.Mode == 22)
				{
					if (!(now - m.HandsUpAt > 20.0) || CopWithin(g, m.Pos, 15f))
					{
						Say(m, "manos arriba: lo arrestan");
						return;
					}
					m.Mode = 0;
					m.Surrender = false;
					m.SurrenderRolled = false;
				}
				if (t.Ped != 0 && t.Cop && !Hostile(g, now))
				{
					Escape(g, m, t, now);
					return;
				}
				if (t.Ped != 0)
				{
					Fight(g, m, t, now);
					return;
				}
				Gang gang = ((!Hunting(g, now)) ? null : NearestRivalGang(g));
				if (gang != null)
				{
					Member member = NearestMemberOf(gang, m.Pos);
					float num = ((member == null) ? float.MaxValue : Vector3.Distance(member.Pos, m.Pos));
					if (num < 200f)
					{
						Fight(g, m, new Threat
						{
							Ped = member.Ped,
							Dist = num,
							Rival = gang,
							Pos = member.Pos
						}, now);
						return;
					}
				}
				if (g.Behavior == NpcBehavior.Pelea || g.Behavior == NpcBehavior.Arrasar)
				{
					float dist;
					int num2 = NearestCivilian(m, (g.Behavior != NpcBehavior.Arrasar) ? 25f : 45f, out dist);
					if (num2 != 0)
					{
						Fight(g, m, new Threat
						{
							Ped = num2,
							Dist = dist,
							Pos = G.CharPos(num2)
						}, now);
						return;
					}
				}
				Roam(g, m, now);
			}
		}
	}

	private void ThinkWalker(Gang g, Member m, double now)
	{
		if (m.InCar)
		{
			if (m.Mode != 7)
			{
				LeaveCar(m, now);
			}
			return;
		}
		if (m.Mode != 1 || (!Moving(m, now, 0.3f) && StillFor(m, now) > 4.0))
		{
			Wander(m, now);
		}
		Say(m, "paseando");
	}

	private void ThinkDriver(Gang g, Member m, Threat t, double now)
	{
		int car = m.Car;
		if (!CarOk(car))
		{
			BailOut(g, m, now, "se bajo del auto");
			return;
		}
		bool flag = g.Behavior == NpcBehavior.Huir || (t.Cop && !Hostile(g, now));
		if (t.Ped != 0 && t.Dist < 38f && !flag)
		{
			BailOut(g, m, now, (!t.Cop) ? ("se baja a pelear con la banda de " + t.Rival.User) : "se baja a enfrentar a la policia");
			return;
		}
		Gang gang = ((!Hunting(g, now)) ? null : NearestRivalGang(g));
		if (gang != null)
		{
			Member member = NearestMemberOf(gang, m.Pos);
			if (member != null)
			{
				float num = Vector3.Distance(member.Pos, m.Pos);
				if (num < 45f)
				{
					BailOut(g, m, now, "llego a donde esta la banda de " + gang.User);
				}
				else if (!WaitForCrew(g, m, car, now))
				{
					if (m.Mode != 4 || m.Target != member.Ped || (!Moving(m, now, 1.5f) && StillFor(m, now) > 6.0) || now - m.LastTask > 30.0)
					{
						N._TASK_CAR_MISSION_PED_TARGET(m.Ped, car, member.Ped, 4u, Math.Max(fleeSpeed, 28f), 2u, 15u, 10u);
						Task(m, 4, now);
						m.Target = member.Ped;
						m.StillSince = -1.0;
					}
					Say(m, "va en auto a buscar a la banda de " + gang.User, quiet: true);
				}
				return;
			}
		}
		if (WaitForCrew(g, m, car, now))
		{
			return;
		}
		Member leader = g.Leader;
		if (leader != null && leader != m && leader.InCar && leader.Driving && leader.Car != car && CarOk(leader.Car))
		{
			if (m.Mode != 17 || (!Moving(m, now, 1.5f) && StillFor(m, now) > 6.0))
			{
				N._TASK_CAR_MISSION_PED_TARGET(m.Ped, car, leader.Ped, 2u, fleeSpeed + 5f, 2u, 10u, 10u);
				Task(m, 17, now);
				m.StillSince = -1.0;
			}
			Say(m, "sigue al auto de " + g.User);
		}
		else
		{
			DriveAround(g, m, car, now, (!flag) ? "da vueltas en un auto robado" : "se escapa en auto");
		}
	}

	private void DriveAround(Gang g, Member m, int car, double now, string what)
	{
		Say(m, (!g.Wanted) ? "maneja por ahi" : what);
		bool wanted = g.Wanted;
		if (m.Mode != 5 || m.CruiseFast != wanted)
		{
			m.CruiseFast = wanted;
			N._TASK_CAR_DRIVE_WANDER(m.Ped, car, (!wanted) ? 16f : fleeSpeed, (!wanted) ? 1u : 2u);
			bool flag = m.Mode == 5;
			Task(m, 5, now);
			if (!flag)
			{
				m.StillSince = -1.0;
			}
		}
		else if (!Moving(m, now, 2f))
		{
			double num = StillFor(m, now);
			if (num > (double)((!wanted) ? 25 : 10))
			{
				BailOut(g, m, now, "el auto quedo trabado: se baja");
			}
			else if (num > (double)((!wanted) ? 12 : 4) && now - m.LastTask > 4.0)
			{
				N._TASK_CAR_DRIVE_WANDER(m.Ped, car, (!wanted) ? 16f : fleeSpeed, (!wanted) ? 1u : 2u);
			}
		}
	}

	private bool WaitForCrew(Gang g, Member m, int car, double now)
	{
		if (m.DriveSince < 0.0 || now - m.DriveSince > 9.0)
		{
			return false;
		}
		bool flag = false;
		foreach (Member member in g.Members)
		{
			if (member == m || member.Dead || member.InCar || member.Fleeing || member.Eating || !(Vector3.Distance(member.Pos, m.Pos) < 40f))
			{
				continue;
			}
			flag = true;
			break;
		}
		if (!flag)
		{
			return false;
		}
		if (m.Mode != 20)
		{
			Task(m, 20, now);
			Say(m, "espera a la banda");
		}
		return true;
	}

	private void ThinkPassenger(Gang g, Member m, double now)
	{
		int car = m.Car;
		int ped = 0;
		bool flag = CarOk(car);
		if (flag)
		{
			N.GET_DRIVER_OF_CAR(car, out ped);
		}
		if (ped == m.Ped)
		{
			m.Driving = true;
			return;
		}
		Member member = MemberByPed(g, ped);
		if (!flag || ped == 0 || member == null || member.Dead || member.Eating || member.Mode == 7)
		{
			LeaveCar(m, now);
			Say(m, "se baja del auto");
			return;
		}
		Threat threat = Sticky(g, m, NearestThreat(g, m, 45f), 45f);
		if (threat.Ped != 0 && m.Armed && (!threat.Cop || Hostile(g, now)))
		{
			if (m.Mode != 9 || m.Target != threat.Ped || now - m.LastTask > 10.0)
			{
				N.SET_CHAR_WILL_LEAVE_CAR_IN_COMBAT(m.Ped, v: false);
				N.SET_CHAR_WILL_DO_DRIVEBYS(m.Ped, v: true);
				N._TASK_COMBAT(m.Ped, threat.Ped);
				Task(m, 9, now);
				m.Target = threat.Ped;
			}
			Say(m, "tira desde el auto", quiet: true);
		}
		else
		{
			if (m.Mode != 15 && m.Mode != 9)
			{
				Task(m, 15, now);
			}
			Say(m, "va en el auto de la banda");
		}
	}

	private void Roam(Gang g, Member m, double now)
	{
		if (m.Mode == 6)
		{
			bool flag = CarOk(m.StealCar);
			bool flag2 = now - m.StealSince > (double)((m.Seat < 0) ? 15 : 12);
			bool flag3 = flag && Vector3.Distance(G.CarPos(m.StealCar), m.Pos) > 45f;
			bool flag4 = flag && m.Seat >= 0 && now - m.StealSince > 3.0 && !N.IS_CHAR_GETTING_IN_TO_A_CAR(m.Ped) && !N.IS_CAR_PASSENGER_SEAT_FREE(m.StealCar, (uint)m.Seat);
			if (flag && !flag2 && !flag3 && !flag4)
			{
				return;
			}
			m.Mode = 0;
			m.StealCar = 0;
			m.Seat = -1;
		}
		if (now - m.LastTask < 1.5)
		{
			return;
		}
		float dist;
		int num = GangCarFor(g, m, out dist);
		if (num == 0)
		{
			foreach (Member member in g.Members)
			{
				if (member == m || member.Dead || member.Mode != 6 || member.Seat >= 0 || !CarOk(member.StealCar))
				{
					continue;
				}
				Vector3 vector = G.CarPos(member.StealCar);
				float num2 = Vector3.Distance(vector, m.Pos);
				if (num2 > 50f)
				{
					continue;
				}
				if (num2 > 7f && (m.Mode != 3 || now - m.LastTask > 3.0))
				{
					RunTo(m, vector, now, straight: false);
				}
				Say(m, "acompana a la banda a robar un auto");
				return;
			}
		}
		if (num != 0 && dist < 40f)
		{
			int num3 = FreeSeat(g, num, m);
			if (num3 >= 0)
			{
				EnterAsPassenger(m, num, num3, now);
				Say(m, "se sube al auto de la banda");
				return;
			}
		}
		int num4 = NearestStealable(g, m, 50f);
		if (num4 != 0)
		{
			EnterAsDriver(m, num4, now);
			Say(m, (g.Alive <= 1) ? "va a robar un auto" : "va a robar un auto para la banda");
			return;
		}
		if (m.Mode != 1 || (!Moving(m, now, 0.3f) && StillFor(m, now) > 5.0))
		{
			Wander(m, now);
		}
		Say(m, "busca un auto para robar");
	}

	private int GangCarFor(Gang g, Member who, out float dist)
	{
		dist = float.MaxValue;
		int result = 0;
		foreach (Member member in g.Members)
		{
			if (member == who || member.Dead || member.Fleeing || member.Eating || !member.InCar || !member.Driving)
			{
				continue;
			}
			int car = member.Car;
			if (!CarOk(car))
			{
				continue;
			}
			float num = Vector3.Distance(G.CarPos(car), who.Pos);
			if (member.InCar)
			{
				N.GET_CAR_SPEED(car, out var speed);
				if (speed > 4f && num > 12f)
				{
					continue;
				}
			}
			if (num < dist)
			{
				dist = num;
				result = car;
			}
		}
		return result;
	}

	private int FreeSeat(Gang g, int car, Member who)
	{
		N.GET_MAXIMUM_NUMBER_OF_PASSENGERS(car, out var max);
		max = Math.Max(0, Math.Min(3, max));
		for (int i = 0; i < max; i++)
		{
			if (!N.IS_CAR_PASSENGER_SEAT_FREE(car, (uint)i))
			{
				continue;
			}
			bool flag = false;
			foreach (Member member in g.Members)
			{
				if (member != who && !member.Dead && member.Mode == 6 && member.StealCar == car && member.Seat == i)
				{
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				return i;
			}
		}
		return -1;
	}

	private int NearestStealable(Gang g, Member m, float radius)
	{
		int result = 0;
		float num = radius;
		foreach (int item in G.VehiclesNear(m.Pos, radius, 12))
		{
			if (G.ProtectedCars.Contains(item) || N.IS_EMERGENCY_SERVICES_VEHICLE(item) || N.IS_CAR_ON_FIRE(item) || N.IS_CAR_UPSIDEDOWN(item))
			{
				continue;
			}
			N.GET_CAR_MODEL(item, out var model);
			if (N.IS_THIS_MODEL_A_HELI(model))
			{
				continue;
			}
			bool flag = false;
			foreach (Gang gang in gangs)
			{
				foreach (Member member in gang.Members)
				{
					if (member != m && member.Mode == 6 && member.StealCar == item)
					{
						flag = true;
						break;
					}
				}
			}
			if (!flag)
			{
				float num2 = Vector3.Distance(G.CarPos(item), m.Pos);
				if (num2 < num)
				{
					num = num2;
					result = item;
				}
			}
		}
		return result;
	}

	private Member MemberByPed(Gang g, int ped)
	{
		if (ped == 0)
		{
			return null;
		}
		foreach (Member member in g.Members)
		{
			if (member.Ped == ped)
			{
				return member;
			}
		}
		return null;
	}

	private void Fight(Gang g, Member m, Threat t, double now)
	{
		if (t.Ped == 0 || !N.DOES_CHAR_EXIST(t.Ped) || N.IS_CHAR_DEAD(t.Ped))
		{
			m.Target = 0;
			m.Mode = M_NONE;
			return;
		}
		string text = (t.Cop ? "la policia" : ((t.Rival == null) ? "la gente" : ("la banda de " + t.Rival.User)));
		bool targetChanged = m.Target != t.Ped;
		if (targetChanged)
		{
			m.Seen = false;
			m.LastSeen = -100.0;
			m.Phase = P_NONE;
			m.Fails = 0;
		}
		if (now - m.LastSeen > 1.0)
		{
			m.LastSeen = now;
			m.Seen = CanSee(m.Ped, m.Pos, t.Ped, t.Pos);
		}
		bool flag = m.Mode == 3 || m.Mode == 11;
		float approachRange = m.Armed ? 45f : 3.5f;
		if (t.Dist > approachRange || (flag && !m.Seen && t.Dist > 10f && now - m.ModeAt < 3.0))
		{
			if (targetChanged || !flag || now - m.LastTask > 3.0)
			{
				bool straight = flag && !Moving(m, now, 0.5f) && StillFor(m, now) > 2.0;
				RunTo(m, t.Pos, now, straight);
			}
			m.Target = t.Ped;
			Say(m, "va hacia " + text, quiet: true);
			return;
		}
		// Let an aimed burst finish. Replacing it on the next think tick prevents
		// the weapon animation from ever reaching a shot.
		if (!targetChanged && m.Mode == M_SHOOT && now - m.LastTask < m.PhaseLen)
		{
			Say(m, "a los tiros con " + text);
			return;
		}
		CombatPolicy.Recovery recovery = CombatPolicy.Decide(m.Armed,
			targetChanged || m.Mode != M_COMBAT, N.IS_PED_IN_COMBAT(m.Ped),
			m.Seen, t.Dist, now - m.LastTask, now - m.LastShot);
		if (recovery == CombatPolicy.Recovery.Advance)
		{
			Approach(m, t, now, stuck: false);
			m.Target = t.Ped;
			Say(m, "busca un angulo contra " + text, quiet: true);
			return;
		}
		if (recovery == CombatPolicy.Recovery.Shoot)
		{
			Shoot(m, t.Ped, now);
			m.Fails++;
			if (diag)
				log("[NPC] " + Name(g, m) + ": recupera combate sin disparos con un tiro dirigido a " + text);
		}
		else if (recovery == CombatPolicy.Recovery.Engage)
		{
			Block(m, on: true);
			N._TASK_COMBAT(m.Ped, t.Ped);
			Task(m, M_COMBAT, now);
			m.Target = t.Ped;
			m.Phase = P_NONE;
		}
		Say(m, (m.Armed ? "a los tiros con " : "a las pinas con ") + text);
	}

	private void Escape(Gang g, Member m, Threat t, double now)
	{
		if (t.Dist < 10f)
		{
			if (!m.SurrenderRolled)
			{
				m.SurrenderRolled = true;
				double num = ((g.Behavior == NpcBehavior.Pelea) ? 0.6 : ((g.Behavior != NpcBehavior.Robar) ? 0.3 : 0.45));
				m.Surrender = G.Rng.NextDouble() < num;
				log("[NPC] " + Name(g, m) + ((!m.Surrender) ? " no se entrega: sigue escapando" : " se entrega a la policia"));
			}
			if (m.Surrender && m.Mode != M_HANDS)
			{
				N._TASK_HANDS_UP(m.Ped, 15000);
				Task(m, 22, now);
				Block(m, on: false);
				m.HandsUpAt = now;
				Say(m, "manos arriba: lo arrestan");
				return;
			}
		}
		if (m.Mode == 6 || NearestStealable(g, m, 50f) != 0)
		{
			Roam(g, m, now);
			Say(m, "escapa de la policia: busca un auto", quiet: true);
			return;
		}
		if (m.Mode != 16 || now - m.LastTask > 5.0)
		{
			N._TASK_SMART_FLEE_CHAR(m.Ped, t.Ped, 100f, 6000u);
			Task(m, 16, now);
		}
		Say(m, "escapa de la policia a pie");
	}

	private bool CopWithin(Gang g, Vector3 p, float r)
	{
		foreach (Cop cop in g.Cops)
		{
			if (cop.Alive && Vector3.Distance(cop.Pos, p) < r)
			{
				return true;
			}
		}
		return false;
	}

	private void Skirmish(Gang g, Member m, Threat t, double now)
	{
		int ped = t.Ped;
		float dist = t.Dist;
		string text = (t.Cop ? "la policia" : ((t.Rival == null) ? "la gente" : ("la banda de " + t.Rival.User)));
		if (m.Target != ped)
		{
			m.Target = ped;
			m.Phase = 0;
			m.Fails = 0;
		}
		if (!m.Armed)
		{
			if (dist > 3.5f)
			{
				if ((m.Mode != 3 && m.Mode != 11) || now - m.LastTask > 2.5)
				{
					RunTo(m, t.Pos, now, m.Mode == 3 && !Moving(m, now, 0.5f) && StillFor(m, now) > 1.5);
				}
				Say(m, "va a las pinas contra " + text);
				return;
			}
			if (m.Mode != 2 || now - m.LastTask > 8.0)
			{
				N._TASK_COMBAT(m.Ped, ped);
				Task(m, 2, now);
			}
			Say(m, "a las pinas con " + text);
			return;
		}
		if (now - m.LastSeen > 1.0)
		{
			m.LastSeen = now;
			m.Seen = now - m.LastShot < 1.5 || CanSee(m.Ped, m.Pos, ped, t.Pos);
		}
		bool flag = now - m.LastHit < 1.5;
		if (dist > 32f || (!m.Seen && now - m.LastShot > 4.0 && dist > 7f) || (m.Fails >= 1 && now - m.LastShot > 6.0 && dist > 10f))
		{
			bool flag2 = m.Phase == 1 && GangRules.Flat(m.Pos, m.MoveTo) < 1.6f;
			bool flag3 = m.Phase == 1 && now - m.PhaseAt > 1.5 && !Moving(m, now, 0.5f) && StillFor(m, now) > 1.5;
			if (m.Phase != 1 || flag2 || flag3 || now - m.PhaseAt > 3.0)
			{
				Approach(m, t, now, flag3);
			}
			Say(m, "corre hacia " + text, quiet: true);
			return;
		}
		double num = now - m.PhaseAt;
		switch (m.Phase)
		{
		case 2:
			if (flag && num > 1.2)
			{
				TakeCover(m, t, now);
			}
			else if (num > m.PhaseLen)
			{
				if (G.Rng.NextDouble() < 0.7)
				{
					TakeCover(m, t, now);
				}
				else
				{
					Shoot(m, ped, now);
				}
			}
			else if (num > 3.0 && now - m.LastShot > 3.0)
			{
				m.Fails++;
				if (m.Fails == 1 && dist > 10f)
				{
					Approach(m, t, now, stuck: false);
					break;
				}
				N._TASK_COMBAT(m.Ped, ped);
				Task(m, 2, now);
				m.Phase = 2;
				m.PhaseAt = now;
				m.PhaseLen = 5.0;
			}
			break;
		case 3:
		{
			bool flag4 = GangRules.Flat(m.Pos, m.MoveTo) < 1.4f;
			bool flag5 = num > 1.5 && !Moving(m, now, 0.4f) && StillFor(m, now) > 1.0;
			if (flag4 || flag5 || num > 4.0)
			{
				if (m.CoverIsCar && flag4 && G.Rng.NextDouble() < 0.6)
				{
					Duck(m, now);
				}
				else
				{
					Shoot(m, ped, now);
				}
			}
			break;
		}
		case 4:
			if (num > m.PhaseLen)
			{
				Shoot(m, ped, now);
			}
			break;
		default:
			Shoot(m, ped, now);
			break;
		}
		if (now - m.LastShot < 2.0)
		{
			m.Fails = 0;
		}
		switch (m.Phase)
		{
		case 3:
			Say(m, (!m.CoverIsCar) ? "se mueve para todos lados" : "se cubre detras de un auto", quiet: true);
			break;
		case 4:
			Say(m, "agachado detras de un auto", quiet: true);
			break;
		case 1:
			Say(m, "corre hacia " + text, quiet: true);
			break;
		default:
			Say(m, "a los tiros con " + text);
			break;
		}
	}

	private static bool CanSee(int ped, Vector3 from, int target, Vector3 to)
	{
		if (!CombatPolicy.InSightRange(Vector3.Distance(from, to), to.Z - from.Z))
		{
			return false;
		}
		if (!N.HAS_CHAR_SPOTTED_CHAR(ped, target))
		{
			return false;
		}
		Vector3 value = from + new Vector3(0f, 0f, 0.7f);
		Vector3 value2 = to + new Vector3(0f, 0f, 0.7f);
		float num = Vector3.Distance(value, value2);
		if (num < 4f)
		{
			return true;
		}
		Vector3 hit;
		return !G.Raycast(Vector3.Lerp(value, value2, 1.5f / num), Vector3.Lerp(value, value2, 1f - 1.5f / num), out hit);
	}

	private void Approach(Member m, Threat t, double now, bool stuck)
	{
		float num = GangRules.Flat(m.Pos, t.Pos);
		m.Phase = 1;
		m.PhaseAt = now;
		if (num < 70f && !stuck)
		{
			float num2 = float.MaxValue;
			Vector3 p = Vector3.Zero;
			foreach (int item in G.VehiclesNear(m.Pos, 24f, 10, includeDead: true))
			{
				Vector3 car = G.CarPos(item);
				float num3 = GangRules.CoverScore(m.Pos, car, t.Pos, 24f, advance: true);
				if (num3 != float.MaxValue)
				{
					Vector3 vector = GangRules.CoverBehind(car, t.Pos, 2.3f);
					if (!(GangRules.Flat(vector, t.Pos) > num - 4f) && num3 < num2)
					{
						num2 = num3;
						p = vector;
					}
				}
			}
			if (num2 < float.MaxValue)
			{
				p = Ground(p, m.Pos.Z);
				RunTo(m, p, now, GangRules.Flat(m.Pos, p) < 14f);
				m.CoverIsCar = true;
				return;
			}
		}
		RunTo(m, t.Pos, now, stuck && m.Mode == 3);
		m.CoverIsCar = false;
	}

	private void TakeCover(Member m, Threat t, double now)
	{
		float num = float.MaxValue;
		Vector3 p = Vector3.Zero;
		foreach (int item in G.VehiclesNear(m.Pos, 18f, 10, includeDead: true))
		{
			Vector3 car = G.CarPos(item);
			float num2 = GangRules.CoverScore(m.Pos, car, t.Pos, 16f, t.Cop);
			if (num2 < num)
			{
				num = num2;
				p = GangRules.CoverBehind(car, t.Pos, 2.3f);
			}
		}
		m.Phase = 3;
		m.PhaseAt = now;
		if (num < float.MaxValue)
		{
			RunTo(m, Ground(p, m.Pos.Z), now, straight: true);
			Task(m, 12, now);
			m.CoverIsCar = true;
			return;
		}
		m.CoverIsCar = false;
		if (gameCover && G.Rng.NextDouble() < 0.5)
		{
			N._TASK_SEEK_COVER_FROM_PED(m.Ped, t.Ped, 5000);
			Task(m, 18, now);
			m.MoveTo = new Vector3(1000000f, 1000000f, 0f);
			if (!loggedGameCover)
			{
				loggedGameCover = true;
				log("[NPC] sin autos cerca: usa la cobertura del juego");
			}
		}
		else
		{
			float side = G.Rand(4f, 7f) * ((G.Rng.Next(2) != 0) ? 1f : (-1f));
			float forward = ((!t.Cop) ? G.Rand(-2f, 2f) : G.Rand(1f, 4f));
			RunTo(m, Ground(GangRules.Strafe(m.Pos, t.Pos, side, forward), m.Pos.Z), now, straight: true);
			Task(m, 19, now);
		}
	}

	private void StartFlee(Gang g, Member m, double now)
	{
		m.Fleeing = true;
		m.FleeSince = now;
		m.CalmSince = -1.0;
		m.Phase = 0;
		m.DriveSince = -1.0;
		log("[NPC] " + Name(g, m) + " tiene poca vida (" + (int)(m.Health01 * 100f) + "%): busca un auto para escapar");
	}

	private void ThinkFlee(Gang g, Member m, double now)
	{
		Threat threat = NearestThreat(g, m, 70f);
		if (m.InCar)
		{
			if (!m.Driving)
			{
				ThinkPassenger(g, m, now);
				return;
			}
			int car = m.Car;
			if (!CarOk(car))
			{
				BailOut(g, m, now, "se le rompio el auto");
				return;
			}
			if (m.Mode != 16)
			{
				Cruise(m, car, fleeSpeed + 8f, now, 16);
				m.DriveSince = now;
			}
			else if (!Moving(m, now, 2f))
			{
				double num = StillFor(m, now);
				if (num > 9.0)
				{
					BailOut(g, m, now, "quedo trabado: escapa a pie");
					return;
				}
				if (num > 4.0 && now - m.LastTask > 4.0)
				{
					Cruise(m, car, fleeSpeed + 8f, now, 16);
				}
			}
			if (threat.Ped == 0)
			{
				if (m.CalmSince < 0.0)
				{
					m.CalmSince = now;
				}
			}
			else
			{
				m.CalmSince = -1.0;
			}
			double num2 = now - m.DriveSince;
			if ((m.CalmSince > 0.0 && now - m.CalmSince > 6.0 && num2 > 12.0) || num2 > 45.0)
			{
				StartEat(g, m, now);
			}
			else
			{
				Say(m, "escapa en auto con poca vida");
			}
		}
		else
		{
			if (m.Mode == 7 && now - m.ModeAt < 1.0)
			{
				return;
			}
			if (m.Mode == 6)
			{
				if (CarOk(m.StealCar) && now - m.StealSince < 14.0 && Vector3.Distance(G.CarPos(m.StealCar), m.Pos) < 45f)
				{
					return;
				}
				m.Mode = 0;
				m.StealCar = 0;
				m.Seat = -1;
			}
			if (now - m.LastTask < 1.5)
			{
				return;
			}
			int num3 = NearestStealable(g, m, 60f);
			if (num3 != 0)
			{
				EnterAsDriver(m, num3, now);
				Say(m, "busca un auto para escapar");
				return;
			}
			if (threat.Ped == 0)
			{
				if (m.CalmSince < 0.0)
				{
					m.CalmSince = now;
				}
				if (now - m.CalmSince > 4.0)
				{
					StartEat(g, m, now);
					return;
				}
			}
			else
			{
				m.CalmSince = -1.0;
			}
			if (threat.Ped != 0 && (m.Mode != 16 || now - m.LastTask > 5.0))
			{
				N._TASK_SMART_FLEE_CHAR(m.Ped, threat.Ped, 80f, 5000u);
				Task(m, 16, now);
			}
			Say(m, "escapa a pie con poca vida");
		}
	}

	private void StartEat(Gang g, Member m, double now)
	{
		List<Member> list = new List<Member>();
		list.Add(m);
		List<Member> list2 = list;
		if (m.InCar && m.Driving)
		{
			foreach (Member member in g.Members)
			{
				if (member != m && !member.Dead && !member.Eating && member.InCar && member.Car == m.Car)
				{
					list2.Add(member);
				}
			}
		}
		foreach (Member item in list2)
		{
			item.Eating = true;
			item.EatStarted = false;
			item.EatUntil = now + (double)eatSeconds + 6.0;
			item.Phase = 0;
			if (item.InCar)
			{
				LeaveCar(item, now);
			}
			Say(item, "se fue a comprar comida");
		}
		if (now - g.LastEatNotice > 20.0)
		{
			g.LastEatNotice = now;
			notify(DisplayName(g, m) + " -> SE FUE A COMPRAR COMIDA");
		}
	}

	private void ThinkEat(Gang g, Member m, double now)
	{
		if (m.InCar)
		{
			if (now - m.LastTask > 4.0)
			{
				LeaveCar(m, now);
			}
			return;
		}
		if (!m.EatStarted)
		{
			m.EatStarted = true;
			m.EatUntil = now + (double)eatSeconds;
			N._TASK_WANDER_STANDARD(m.Ped);
			Task(m, 14, now);
		}
		if (now - m.LastHit < 0.6 && now - m.ModeAt > 1.0 && NearestThreat(g, m, 40f).Ped != 0)
		{
			m.Eating = false;
			if (m.Health01 < fleeAt)
			{
				m.Fleeing = true;
				m.FleeSince = now;
			}
			Say(m, "lo interrumpieron mientras comia");
			return;
		}
		if (now < m.EatUntil)
		{
			Say(m, "comprando comida", quiet: true);
			return;
		}
		N.SET_CHAR_HEALTH(m.Ped, m.StartHealth);
		N.ADD_ARMOUR_TO_CHAR(m.Ped, 50);
		m.LastHp = m.StartHealth;
		m.Health01 = 1f;
		m.Eating = false;
		m.Fleeing = false;
		m.Phase = 0;
		m.Mode = 0;
		m.CalmSince = -1.0;
		m.Flash = "COMIO: VIDA LLENA";
		m.FlashUntil = now + 4.0;
		log("[NPC] " + Name(g, m) + " comio y vuelve con toda la vida");
		Say(m, "comio y vuelve con toda la vida");
	}

	private Threat NearestThreat(Gang g, Member m, float radius)
	{
		Threat result = new Threat
		{
			Dist = radius
		};
		foreach (Gang gang in gangs)
		{
			foreach (Cop cop in AllCops(gang))
			{
				if (cop.Alive)
				{
					float num = Vector3.Distance(cop.Pos, m.Pos);
					if (num < result.Dist)
					{
						result.Dist = num;
						result.Ped = cop.Ped;
						result.Cop = true;
						result.Rival = null;
						result.Pos = cop.Pos;
					}
				}
			}
		}
		foreach (int item in g.Tasked)
		{
			if (N.DOES_CHAR_EXIST(item) && !N.IS_CHAR_DEAD(item))
			{
				Vector3 vector = G.CharPos(item);
				float num2 = Vector3.Distance(vector, m.Pos);
				if (num2 < result.Dist)
				{
					result.Dist = num2;
					result.Ped = item;
					result.Cop = true;
					result.Rival = null;
					result.Pos = vector;
				}
			}
		}
		if (Hunting(g, G.Now) && g.Fighter)
		{
			foreach (Gang gang2 in gangs)
			{
				if (gang2 == g || !gang2.Fighter || gang2.State != 1)
				{
					continue;
				}
				foreach (Member member in gang2.Members)
				{
					if (!member.Dead)
					{
						float num3 = Vector3.Distance(member.Pos, m.Pos);
						if (num3 < result.Dist)
						{
							result.Dist = num3;
							result.Ped = member.Ped;
							result.Cop = false;
							result.Rival = gang2;
							result.Pos = member.Pos;
						}
					}
				}
			}
		}
		return result;
	}

	private Threat Sticky(Gang g, Member m, Threat t, float radius)
	{
		if (m.Target == 0 || t.Ped == 0 || t.Ped == m.Target)
		{
			return t;
		}
		if (!ThreatOf(g, m, m.Target, out var t2))
		{
			return t;
		}
		return (!(t2.Dist < radius) || !(t2.Dist < t.Dist + 10f)) ? t : t2;
	}

	private bool ThreatOf(Gang g, Member m, int ped, out Threat t)
	{
		t = default(Threat);
		foreach (Gang gang in gangs)
		{
			foreach (Cop cop in AllCops(gang))
			{
				if (cop.Ped == ped)
				{
					if (!cop.Alive)
					{
						return false;
					}
					t.Ped = ped;
					t.Cop = true;
					t.Pos = cop.Pos;
					t.Dist = Vector3.Distance(cop.Pos, m.Pos);
					return true;
				}
			}
			if (gang == g || !Hunting(g, G.Now))
			{
				continue;
			}
			foreach (Member member in gang.Members)
			{
				if (member.Ped == ped)
				{
					if (member.Dead)
					{
						return false;
					}
					t.Ped = ped;
					t.Rival = gang;
					t.Pos = member.Pos;
					t.Dist = Vector3.Distance(member.Pos, m.Pos);
					return true;
				}
			}
		}
		if (g.Tasked.Contains(ped) && N.DOES_CHAR_EXIST(ped) && !N.IS_CHAR_DEAD(ped))
		{
			t.Ped = ped;
			t.Cop = true;
			t.Pos = G.CharPos(ped);
			t.Dist = Vector3.Distance(t.Pos, m.Pos);
			return true;
		}
		return false;
	}

	private Threat NearestCop(Gang g, Member m, float radius)
	{
		Threat result = new Threat
		{
			Dist = radius
		};
		foreach (Gang gang in gangs)
		{
			foreach (Cop cop in AllCops(gang))
			{
				if (cop.Alive)
				{
					float num = Vector3.Distance(cop.Pos, m.Pos);
					if (num < result.Dist)
					{
						result.Dist = num;
						result.Ped = cop.Ped;
						result.Cop = true;
						result.Pos = cop.Pos;
					}
				}
			}
		}
		foreach (int ped in g.Tasked)
		{
			if (!N.DOES_CHAR_EXIST(ped) || N.IS_CHAR_DEAD(ped) || !IsCop(ped))
				continue;
			Vector3 pos = G.CharPos(ped);
			float distance = Vector3.Distance(pos, m.Pos);
			if (distance < result.Dist)
				result = new Threat { Ped = ped, Cop = true, Pos = pos, Dist = distance };
		}
		return result;
	}

	private static IEnumerable<Cop> AllCops(Gang g)
	{
		foreach (Cop cop in g.Cops)
			if (cop.Alive)
				yield return cop;
		foreach (Cop cop in g.AmbientCops.Values)
			if (N.DOES_CHAR_EXIST(cop.Ped) && !N.IS_CHAR_DEAD(cop.Ped) && !N.IS_CHAR_FATALLY_INJURED(cop.Ped))
				yield return cop;
	}

	private bool Hunting(Gang g, double now)
	{
		return duel || (g.Order == 5 && now < g.OrderUntil);
	}

	private Gang NearestRivalGang(Gang g)
	{
		Member leader = g.Leader;
		if (leader == null)
		{
			return null;
		}
		Gang result = null;
		float num = float.MaxValue;
		foreach (Gang gang in gangs)
		{
			if (gang == g || !gang.Fighter || gang.State != 1 || gang.Alive == 0)
			{
				continue;
			}
			Member member = NearestMemberOf(gang, leader.Pos);
			if (member != null)
			{
				float num2 = Vector3.Distance(member.Pos, leader.Pos);
				if (num2 < num)
				{
					num = num2;
					result = gang;
				}
			}
		}
		return result;
	}

	private static Member NearestMemberOf(Gang g, Vector3 p)
	{
		Member result = null;
		float num = float.MaxValue;
		foreach (Member member in g.Members)
		{
			if (!member.Dead)
			{
				float num2 = Vector3.Distance(member.Pos, p);
				if (num2 < num)
				{
					num = num2;
					result = member;
				}
			}
		}
		return result;
	}

	private static float GangDistance(Gang a, Gang b)
	{
		float num = float.MaxValue;
		foreach (Member member in a.Members)
		{
			if (member.Dead)
			{
				continue;
			}
			foreach (Member member2 in b.Members)
			{
				if (!member2.Dead)
				{
					num = Math.Min(num, FlatDist(member.Pos, member2.Pos));
				}
			}
		}
		return num;
	}

	private int PolicePresent(Gang g)
	{
		int num = 0;
		try
		{
			foreach (Cop cop in g.Cops)
			{
				if (cop.Alive)
				{
					num++;
				}
			}
			foreach (int item in g.Tasked)
			{
				if (N.DOES_CHAR_EXIST(item) && !N.IS_CHAR_DEAD(item))
				{
					num++;
				}
			}
		}
		catch
		{
		}
		return num;
	}

	private int NearestCivilian(Member m, float radius, out float dist)
	{
		int result = 0;
		dist = radius;
		foreach (int item in G.PedsNear(m.Pos, radius, 12))
		{
			if (!N.IS_CHAR_IN_ANY_CAR(item) && !IsCop(item))
			{
				float num = Vector3.Distance(G.CharPos(item), m.Pos);
				if (num < dist)
				{
					dist = num;
					result = item;
				}
			}
		}
		return result;
	}

	private string Name(Gang g, Member m)
	{
		return (m.Index != 0) ? (g.User + " #" + (m.Index + 1)) : g.User;
	}

	private string DisplayName(Gang g, Member m)
	{
		return (m.Index != 0) ? GangRules.MemberName(g.User) : g.User;
	}

	private void Say(Member m, string status)
	{
		Say(m, status, quiet: false);
	}

	private void Say(Member m, string status, bool quiet)
	{
		m.Status = status;
		if (!quiet && !(status == m.LastLogged))
		{
			m.LastLogged = status;
			log("[NPC] " + Name(m.G, m) + ": " + status);
		}
	}

	private void ScanKills(Gang g, double now)
	{
		List<KeyValuePair<int, int>> list = new List<KeyValuePair<int, int>>();
		foreach (Gang gang in gangs)
		{
			foreach (Cop cop in gang.Cops)
			{
				if (cop.Ped != 0 && cop.DeadAt > 0.0 && !g.Counted.Contains(cop.Ped))
				{
					list.Add(new KeyValuePair<int, int>(cop.Ped, (gang == g) ? 1 : 2));
				}
			}
		}
		foreach (int item in g.Tasked)
		{
			if (!g.Counted.Contains(item) && N.DOES_CHAR_EXIST(item) && N.IS_CHAR_DEAD(item))
			{
				list.Add(new KeyValuePair<int, int>(item, 2));
			}
		}
		foreach (Gang gang2 in gangs)
		{
			if (gang2 == g)
			{
				continue;
			}
			foreach (Member member2 in gang2.Members)
			{
				if (member2.Dead && !g.Counted.Contains(member2.Ped))
				{
					list.Add(new KeyValuePair<int, int>(member2.Ped, 3));
				}
			}
		}
		if (now - g.LastCivScan > 1.0)
		{
			g.LastCivScan = now;
			Member leader = g.Leader;
			if (leader != null)
			{
				foreach (int item2 in G.DeadPedsNear(leader.Pos, 50f, 10))
				{
					if (!g.Counted.Contains(item2))
					{
						list.Add(new KeyValuePair<int, int>(item2, IsCop(item2) ? 2 : 0));
					}
				}
			}
		}
		foreach (KeyValuePair<int, int> item3 in list)
		{
			int key = item3.Key;
			if (!g.Counted.Add(key) || !N.DOES_CHAR_EXIST(key))
			{
				continue;
			}
			Member member = null;
			foreach (Member member3 in g.Members)
			{
				if (!member3.Dead && N.HAS_CHAR_BEEN_DAMAGED_BY_CHAR(key, member3.Ped, reset: false))
				{
					member = member3;
					break;
				}
			}
			if (member == null && item3.Value == 1)
			{
				Vector3 value = G.CharPos(key);
				float num = 35f;
				foreach (Member member4 in g.Members)
				{
					if (!member4.Dead && !(now - member4.LastShot > 3.0))
					{
						float num2 = Vector3.Distance(member4.Pos, value);
						if (num2 < num)
						{
							num = num2;
							member = member4;
						}
					}
				}
			}
			if (member != null)
			{
				OnKill(g, member, item3.Value == 1 || item3.Value == 2, item3.Value == 3, now);
			}
		}
		if (g.Counted.Count > 400)
		{
			g.Counted.Clear();
		}
	}

	private void OnKill(Gang g, Member killer, bool cop, bool gang, double now)
	{
		g.Kills++;
		killer.Kills++;
		string text = (cop ? "un policia" : ((!gang) ? "a alguien" : "uno de otra banda"));
		if (killBonus > 0f)
		{
			g.EndAt = Math.Min(g.EndAt + (double)killBonus, now + 3600.0);
			killer.Flash = "+" + killBonus.ToString("0") + " s";
			killer.FlashUntil = now + 2.5;
		}
		log("[NPC] " + Name(g, killer) + " mato a " + text + ((!(killBonus > 0f)) ? string.Empty : (" (+" + killBonus.ToString("0") + " s)")));
		if (cop)
		{
			g.CopKills++;
			SetStars(g, Math.Max(3, g.Stars + 1), "mato a un policia", now);
		}
		else if (!gang)
		{
			SetStars(g, Math.Min(4, Math.Max(2, g.Stars + 1)), "mato a alguien", now);
		}
		if (g.Fighter && GangRules.Grows(growRule, cop, gang))
		{
			Grow(g, killer, now, test: false);
		}
	}

	private void MakeLethal(Gang g)
	{
		if (g.Lethal)
		{
			return;
		}
		g.Lethal = true;
		foreach (Cop cop in g.Cops)
		{
			if (cop.Alive && N.DOES_CHAR_EXIST(cop.Ped))
			{
				CopLethal(cop.Ped);
				cop.Mode = 0;
			}
		}
	}

	private static void CopLethal(int cop)
	{
		N.SET_CHAR_RELATIONSHIP_GROUP(cop, 30);
		N.SET_CHAR_RELATIONSHIP(cop, 0u, GROUP_COPS);
		N.SET_CHAR_RELATIONSHIP(cop, 0u, 3);
		for (int i = 0; i < 7; i++)
		{
			N.SET_CHAR_RELATIONSHIP(cop, 5u, 23 + i);
		}
	}

	private bool Grow(Gang g, Member killer, double now, bool test)
	{
		int alive = g.Alive;
		if (alive >= gangMax)
		{
			if (!g.FullLogged)
			{
				g.FullLogged = true;
				log("[NPC] la banda de " + g.User + " ya esta completa (" + alive + ")");
			}
			return false;
		}
		if (TotalMembers() >= 16)
		{
			return false;
		}
		Vector3 p;
		if (killer.InCar)
		{
			p = FindSpawnPoint(killer.Pos);
		}
		else
		{
			double num = G.Rng.NextDouble() * Math.PI * 2.0;
			p = killer.Pos + new Vector3((float)Math.Cos(num) * 2.5f, (float)Math.Sin(num) * 2.5f, 0f);
			p = Ground(p, killer.Pos.Z);
		}
		Member member = NewMember(g, p, now);
		if (member == null)
		{
			log("[NPC] no se pudo sumar a la banda de " + g.User);
			return false;
		}
		SetupMember(g, member, now);
		g.FullLogged = false;
		Changed();
		int alive2 = g.Alive;
		log("[NPC] la banda de " + g.User + " suma uno" + ((!test) ? string.Empty : " (prueba)") + ": ahora son " + alive2);
		bool flag = !g.Control && unlockRule == 0;
		if (flag)
		{
			g.Control = true;
			log("[NPC] control por chat desbloqueado para la banda de " + g.User + ": " + GangRules.ChoicesLine(slots));
			notify(GangRules.GangTitle(g.User, alive2) + ": EL CHAT ELIGE " + GangRules.ChoicesLine(slots).ToUpperInvariant());
		}
		else
		{
			notify(GangRules.GangTitle(g.User, alive2));
		}
		g.Flash = ((!flag) ? "SE SUMA UNO A LA BANDA" : "CONTROL DESBLOQUEADO");
		g.FlashUntil = now + 4.0;
		return true;
	}

	public void OnChat(string user, string text, int level)
	{
		if (gangs.Count == 0 || string.IsNullOrEmpty(text))
		{
			return;
		}
		double now = G.Now;
		string text2 = text.Trim();
		bool flag = text2.StartsWith("!");
		Gang gang = OwnGang(user, level);
		if (gang != null && !flag)
		{
			if (ShowChat)
			{
				string[] array = GangRules.Bubble(text2, 30, 3);
				if (array.Length > 0)
				{
					gang.Bubble = array;
					gang.BubbleUntil = now + GangRules.BubbleSeconds(text2);
				}
			}
		}
		else
		{
			if (!flag)
			{
				return;
			}
			if (controlMode == 0)
			{
				if (gang != null && gang.Control)
				{
					int num = GangRules.ParseChoice(text2, slots);
					if (num >= 0 && !(now - gang.LastCmd < (double)cmdCooldown))
					{
						ApplyOrder(gang, slots[num], user, now);
					}
				}
				return;
			}
			Gang gang2 = FocusGang();
			if (gang2 == null)
			{
				return;
			}
			int num2 = GangRules.ParseChoice(text2, slots);
			if (num2 < 0)
			{
				return;
			}
			if (controlMode == 1)
			{
				bool flag2 = !gang2.Votes.Open;
				gang2.Votes.Add(user, num2, now, voteSeconds);
				if (flag2)
				{
					log("[NPC] votacion abierta para la banda de " + gang2.User + " (" + voteSeconds.ToString("0") + " s)");
				}
			}
			else if (now - gang2.LastCmd >= (double)cmdCooldown)
			{
				ApplyOrder(gang2, slots[num2], user, now);
			}
		}
	}

	private Gang OwnGang(string user, int level)
	{
		Gang gang = null;
		string text = (user ?? string.Empty).Trim();
		foreach (Gang gang2 in gangs)
		{
			if (gang2.State == 1 && gang2.Alive != 0 && (string.Equals(gang2.User, text, StringComparison.OrdinalIgnoreCase) || (gang2.Test && (level >= 4 || string.Equals(text, "prueba", StringComparison.OrdinalIgnoreCase)))) && (gang == null || gang2.Created > gang.Created))
			{
				gang = gang2;
			}
		}
		return gang;
	}

	private Gang FocusGang()
	{
		int followedPed = dir.FollowedPed;
		if (followedPed != 0)
		{
			foreach (Gang gang2 in gangs)
			{
				if (!gang2.Control || gang2.State != 1 || gang2.Alive <= 0)
				{
					continue;
				}
				foreach (Member member in gang2.Members)
				{
					if (member.Ped == followedPed)
					{
						return gang2;
					}
				}
			}
		}
		Gang gang = null;
		foreach (Gang gang3 in gangs)
		{
			if (gang3.Control && gang3.State == 1 && gang3.Alive > 0 && (gang == null || gang3.Created > gang.Created))
			{
				gang = gang3;
			}
		}
		return gang;
	}

	private void ApplyOrder(Gang g, int cmd, string who, double now)
	{
		g.LastCmd = now;
		string text = "!" + GangRules.CmdKeys[cmd];
		if (cmd == 5 && NearestRivalGang(g) == null)
		{
			g.Flash = text.ToUpperInvariant() + ": NO HAY OTRA BANDA";
			g.FlashUntil = now + 4.0;
			log("[NPC] orden " + text + " para la banda de " + g.User + ": no hay otra banda");
			return;
		}
		g.Order = cmd;
		g.OrderAt = now;
		g.OrderUntil = now + (double)((cmd != 5) ? orderSeconds : Math.Max(orderSeconds, 60f));
		g.Flash = ((controlMode != 0) ? "EL CHAT ELIGIO " : string.Empty) + text.ToUpperInvariant();
		g.FlashUntil = now + 4.0;
		foreach (Member member in g.Members)
		{
			if (!member.Dead && !member.Eating)
			{
				member.Phase = 0;
				if (member.Mode != 6 && member.Mode != 7)
				{
					member.Mode = 0;
				}
				if (cmd == 2 || cmd == 1)
				{
					member.Fleeing = false;
				}
			}
		}
		switch (cmd)
		{
		case 1:
			SetStars(g, Math.Max(3, g.Stars), "el chat eligio !atacar", now);
			UpdateHostility(g, now);
			break;
		case 3:
			foreach (Member member2 in g.Members)
			{
				if (!member2.Dead && !member2.Eating && (!member2.InCar || member2.Driving))
				{
					StartEat(g, member2, now);
				}
			}
			break;
		}
		log("[NPC] orden " + text + " para la banda de " + g.User + " (" + who + ")");
		notify(g.User + " -> " + text.ToUpperInvariant() + ((controlMode != 0) ? " (lo eligio el chat)" : string.Empty));
	}

	private bool DoOrder(Gang g, Member m, double now)
	{
		switch (g.Order)
		{
		case 0:
			if (m.InCar)
			{
				if (!CarOk(m.Car))
				{
					BailOut(g, m, now, "se le rompio el auto");
					return true;
				}
				if (WaitForCrew(g, m, m.Car, now))
				{
					return true;
				}
				DriveAround(g, m, m.Car, now, "da vueltas en un auto robado (lo eligio el chat)");
				return true;
			}
			if (m.Mode == 7 && now - m.ModeAt < 1.0)
			{
				return true;
			}
			Roam(g, m, now);
			return true;
		case 2:
			if (m.InCar)
			{
				if (!CarOk(m.Car))
				{
					BailOut(g, m, now, "se le rompio el auto");
					return true;
				}
				if (WaitForCrew(g, m, m.Car, now))
				{
					return true;
				}
				if (m.Mode != 16)
				{
					Cruise(m, m.Car, fleeSpeed + 8f, now, 16);
				}
				else if (!Moving(m, now, 2f) && StillFor(m, now) > 5.0 && now - m.LastTask > 4.0)
				{
					Cruise(m, m.Car, fleeSpeed + 8f, now, 16);
				}
				Say(m, "escapa en auto (lo eligio el chat)");
				return true;
			}
			if (m.Mode == 7 && now - m.ModeAt < 1.0)
			{
				return true;
			}
			Roam(g, m, now);
			return true;
		case 1:
		{
			Threat t = NearestCop(g, m, 400f);
			if (t.Ped == 0)
			{
				if (!m.InCar && m.Mode != 1)
				{
					Wander(m, now);
				}
				Say(m, "espera a la policia (lo eligio el chat)");
				return true;
			}
			if (m.InCar)
			{
				if (t.Dist < 40f || !CarOk(m.Car))
				{
					BailOut(g, m, now, "se baja a atacar a la policia");
					return true;
				}
				if (m.Mode != 4 || m.Target != t.Ped || now - m.LastTask > 20.0)
				{
					N._TASK_CAR_MISSION_PED_TARGET(m.Ped, m.Car, t.Ped, 4u, 30f, 2u, 15u, 10u);
					Task(m, 4, now);
					m.Target = t.Ped;
				}
				Say(m, "va en auto a atacar a la policia");
				return true;
			}
			Fight(g, m, t, now);
			return true;
		}
		case 4:
		{
			if (m.InCar)
			{
				BailOut(g, m, now, "se baja a pelear");
				return true;
			}
			float dist;
			int num = NearestCivilian(m, 40f, out dist);
			if (num == 0)
			{
				if (m.Mode != 1)
				{
					Wander(m, now);
				}
				Say(m, "busca con quien pelear");
				return true;
			}
			Fight(g, m, new Threat
			{
				Ped = num,
				Dist = dist,
				Pos = G.CharPos(num)
			}, now);
			return true;
		}
		default:
			return false;
		}
	}

	private void BringRival(Gang g, double now)
	{
		if (!bringGangs || !g.Fighter || !Hunting(g, now) || now - g.LastWarp < 25.0 || now - g.Created < 8.0)
		{
			return;
		}
		Gang gang = NearestRivalGang(g);
		if (gang == null)
		{
			return;
		}
		Member leader = g.Leader;
		Member leader2 = gang.Leader;
		if (leader == null || leader2 == null)
		{
			return;
		}
		float num = GangDistance(g, gang);
		if (num < 350f)
		{
			return;
		}
		Vector3 b = G.CharPos(G.PlayerPed);
		if (FlatDist(leader.Pos, b) < FlatDist(leader2.Pos, b))
		{
			return;
		}
		int followedPed = dir.FollowedPed;
		foreach (Member member in g.Members)
		{
			if (member.Dead || (member.Ped != followedPed && (!dir.Active || !dir.InFrame(member.Head, 2f)) && !member.Eating && !member.Fleeing))
			{
				continue;
			}
			return;
		}
		if (!FindStreetNear(leader2.Pos, 110f, 190f, new List<Vector3>(), out var spot, out var heading))
		{
			return;
		}
		g.LastWarp = now;
		HashSet<int> hashSet = new HashSet<int>();
		int num2 = 0;
		foreach (Member member2 in g.Members)
		{
			if (member2.Dead)
			{
				continue;
			}
			if (member2.InCar && CarOk(member2.Car))
			{
				if (hashSet.Add(member2.Car))
				{
					Vector3 vector = spot + MathX.Forward(new Vector3(0f, 0f, heading)) * (-7f * (float)(hashSet.Count - 1));
					N.SET_CAR_COORDINATES(member2.Car, vector + new Vector3(0f, 0f, 0.5f));
					N.SET_CAR_HEADING(member2.Car, heading);
				}
			}
			else
			{
				Vector3 vector2 = MathX.Right(new Vector3(0f, 0f, heading));
				Vector3 p = Ground(spot + vector2 * 5.5f + MathX.Forward(new Vector3(0f, 0f, heading)) * (1.5f * (float)num2), spot.Z);
				N.SET_CHAR_COORDINATES(member2.Ped, p);
				num2++;
			}
			member2.Mode = 0;
			member2.Phase = 0;
			member2.StillSince = -1.0;
		}
		log("[NPC] la banda de " + g.User + " sabe donde esta la de " + gang.User + ": llega a su barrio (estaba a " + num.ToString("0") + " m)");
	}

	private bool FindStreetNear(Vector3 target, float minD, float maxD, List<Vector3> used, out Vector3 spot, out float heading)
	{
		spot = Vector3.Zero;
		heading = 0f;
		Vector3 vector = Vector3.Zero;
		float num = 0f;
		for (int i = 0; i < 2; i++)
		{
			float num2 = ((i != 0) ? 7f : 15f);
			float num3 = ((i != 0) ? (minD * 0.7f) : minD);
			float num4 = ((i != 0) ? (maxD * 1.4f) : maxD);
			for (uint num5 = 4u; num5 <= 60; num5 += 2)
			{
				if (!N.GET_NTH_CLOSEST_CAR_NODE_WITH_HEADING(target, num5, out var result, out var heading2) || result == Vector3.Zero)
				{
					continue;
				}
				float num6 = FlatDist(result, target);
				if (num6 < num3 || num6 > num4 || Math.Abs(result.Z - target.Z) > 15f)
				{
					continue;
				}
				bool flag = false;
				foreach (Vector3 item in used)
				{
					if (Vector3.Distance(item, result) < num2)
					{
						flag = true;
						break;
					}
				}
				if (!flag && G.VehiclesNear(result, 5f, 1, includeDead: true).Count <= 0)
				{
					Vector3 vector2 = target - result;
					if (Vector3.Dot(MathX.Forward(new Vector3(0f, 0f, heading2)), vector2) < 0f)
					{
						heading2 += 180f;
					}
					if (vector == Vector3.Zero)
					{
						vector = result;
						num = heading2;
					}
					if (!dir.Active || !dir.InFrame(result + new Vector3(0f, 0f, 1f), 4f))
					{
						spot = result;
						heading = heading2;
						return true;
					}
				}
			}
		}
		if (vector == Vector3.Zero)
		{
			return false;
		}
		spot = vector;
		heading = num;
		return true;
	}

	private void SpawnPatrol(Gang g, double now)
	{
		Member leader = g.Leader;
		if (leader == null)
		{
			g.CopsPending = 0;
			return;
		}
		N.GET_CURRENT_BASIC_POLICE_CAR_MODEL(out var model);
		if (model == 0 || !N.IS_MODEL_IN_CDIMAGE((int)model))
		{
			model = (uint)N.GET_HASH_KEY("POLICE");
		}
		N.GET_CURRENT_BASIC_COP_MODEL(out var model2);
		if (model2 == 0 || !N.IS_MODEL_IN_CDIMAGE((int)model2))
		{
			model2 = (uint)N.GET_HASH_KEY("M_Y_COP");
		}
		int num = 0;
		foreach (Gang gang in gangs)
		{
			foreach (Cop cop in gang.Cops)
			{
				if (cop.Alive)
				{
					num++;
				}
			}
		}
		if (num >= 24)
		{
			g.NextCops = now + 4.0;
			return;
		}
		if (g.Stars >= 4 && g.PatrolsMade % 2 == 1)
		{
			bool flag = g.Stars >= 5 && g.PatrolsMade % 4 == 3;
			int num2 = N.GET_HASH_KEY((!flag) ? "NOOSE" : "FBI");
			int num3 = N.GET_HASH_KEY((!flag) ? "M_Y_SWAT" : "M_M_FBI");
			if (N.IS_MODEL_IN_CDIMAGE(num2) && N.IS_MODEL_IN_CDIMAGE(num3))
			{
				model = (uint)num2;
				model2 = (uint)num3;
			}
		}
		N.REQUEST_MODEL((int)model);
		N.REQUEST_MODEL((int)model2);
		if (!N.HAS_MODEL_LOADED((int)model) || !N.HAS_MODEL_LOADED((int)model2))
		{
			g.NextCops = now + 0.25;
			if (++g.CopWaits > 40)
			{
				g.CopsPending = 0;
				log("[NPC] no cargaron los modelos de la policia");
			}
			return;
		}
		g.CopWaits = 0;
		g.CopsPending--;
		g.NextCops = now + 2.5;
		while (g.UsedSpots.Count > 5)
		{
			g.UsedSpots.RemoveAt(0);
		}
		if (!FindStreetNear(leader.Pos, 35f, 150f, g.UsedSpots, out var spot, out var heading))
		{
			log("[NPC] no encontre una calle libre para el patrullero de " + g.User);
			return;
		}
		N.CREATE_CAR((int)model, spot + new Vector3(0f, 0f, 0.6f), out var veh, b: true);
		if (veh == 0 || !N.DOES_VEHICLE_EXIST(veh))
		{
			log("[NPC] no se pudo crear el patrullero");
			return;
		}
		N.SET_CAR_HEADING(veh, heading);
		N.SWITCH_CAR_SIREN(veh, v: true);
		g.UsedSpots.Add(spot);
		int ped = 0;
		int ped2 = 0;
		uint num4 = CopType(leader.Pos);
		try
		{
			N.CREATE_CHAR_INSIDE_CAR(veh, num4, model2, out ped);
		}
		catch
		{
			ped = 0;
		}
		try
		{
			N.CREATE_CHAR_AS_PASSENGER(veh, num4, model2, 0u, out ped2);
		}
		catch
		{
			ped2 = 0;
		}
		int[] array = ((g.Stars >= 4) ? new int[4] { 15, 14, 10, 13 } : ((g.Stars >= 3) ? new int[3] { 7, 10, 13 } : new int[1] { 7 }));
		int num5 = 0;
		int[] array2 = new int[2] { ped, ped2 };
		foreach (int num6 in array2)
		{
			if (num6 != 0 && N.DOES_CHAR_EXIST(num6))
			{
				madeCops.Add(num6);
				int num7 = array[G.Rng.Next(array.Length)];
				N.GIVE_WEAPON_TO_CHAR(num6, 7, 300, b: false);
				if (num7 != 7)
				{
					N.GIVE_WEAPON_TO_CHAR(num6, num7, 400, b: false);
				}
				N.SET_CURRENT_CHAR_WEAPON(num6, num7, b: true);
				N.SET_CHAR_KEEP_TASK(num6, v: true);
				N.SET_CHAR_ACCURACY(num6, (g.Stars < 4) ? 40u : 50u);
				N.SET_CHAR_SHOOT_RATE(num6, 100);
				N.SET_CHAR_WILL_MOVE_WHEN_INJURED(num6, v: true);
				if (g.Stars >= 4)
				{
					N.ADD_ARMOUR_TO_CHAR(num6, 100);
				}
				N.SET_CHAR_WILL_USE_COVER(num6, v: true);
				N.SET_SENSE_RANGE(num6, 80f);
				if (g.Lethal)
				{
					CopLethal(num6);
				}
				g.Cops.Add(new Cop
				{
					Ped = num6,
					Car = veh,
					Driver = (num6 == ped),
					Spawned = now,
					Pos = spot
				});
				num5++;
			}
		}
		if (num5 == 0)
		{
			g.Cops.Add(new Cop
			{
				Car = veh,
				Spawned = now,
				DeadAt = now
			});
		}
		g.PatrolsMade++;
		g.LastReinforce = now;
		Changed();
		Say(leader, "patrullero " + g.PatrolsMade + " en camino");
		log("[NPC] patrullero " + g.PatrolsMade + " para " + g.User + " a " + FlatDist(spot, leader.Pos).ToString("0") + " m, " + num5 + " policias" + ((g.PatrolsMade != 1) ? string.Empty : (" (tipo " + num4 + ", " + g.Stars + " estrellas)")));
		ManageCops(g, now);
	}

	private void Reinforce(Gang g, double now)
	{
		if (!g.Wanted || g.CopsPending > 0 || g.PatrolsMade >= 30 || now - g.LastContact > 15.0 || now - g.LastReinforce < (double)((g.Stars < 4) ? 14 : 8))
		{
			return;
		}
		int num = Math.Max(1, Math.Min(8, g.Stars + Math.Max(0, patrols - 2)));
		HashSet<int> hashSet = new HashSet<int>();
		foreach (Cop cop in g.Cops)
		{
			if (cop.Alive && cop.Car != 0)
			{
				hashSet.Add(cop.Car);
			}
		}
		if (hashSet.Count < num)
		{
			g.CopsPending = 1;
			g.NextCops = now;
			g.LastReinforce = now;
		}
	}

	private void ManageCops(Gang g, double now)
	{
		if (g.Leader == null)
		{
			return;
		}
		if (!g.Lethal)
		{
			foreach (Cop cop in g.Cops)
			{
				if (!cop.Alive)
				{
					continue;
				}
				foreach (Member member3 in g.Members)
				{
					if (!member3.Dead && N.HAS_CHAR_BEEN_DAMAGED_BY_CHAR(cop.Ped, member3.Ped, reset: false))
					{
						SetStars(g, Math.Max(3, g.Stars), Name(g, member3) + " ataco a la policia", now);
						break;
					}
				}
				if (!g.Lethal)
				{
					continue;
				}
				break;
			}
		}
		foreach (Cop cop2 in g.Cops)
		{
			if (!cop2.Alive)
			{
				continue;
			}
			foreach (Member member4 in g.Members)
			{
				if (!member4.Dead && Vector3.Distance(cop2.Pos, member4.Pos) < 120f)
				{
					g.LastContact = now;
					break;
				}
			}
		}
		HashSet<int> hashSet = new HashSet<int>();
		foreach (Cop cop3 in g.Cops)
		{
			if (cop3.Alive && cop3.Driver)
			{
				hashSet.Add(cop3.Car);
			}
		}
		foreach (Cop cop4 in g.Cops)
		{
			if (cop4.Alive && !cop4.Driver && cop4.Car != 0 && !hashSet.Contains(cop4.Car))
			{
				cop4.Driver = true;
				hashSet.Add(cop4.Car);
				cop4.Mode = 0;
			}
		}
		HashSet<int> hashSet2 = new HashSet<int>();
		HashSet<int> hashSet3 = new HashSet<int>();
		foreach (Cop cop5 in g.Cops)
		{
			if (cop5.Ped == 0 || !cop5.Driver)
			{
				continue;
			}
			if (!cop5.Alive)
			{
				hashSet2.Add(cop5.Car);
				continue;
			}
			bool flag = N.IS_CHAR_IN_ANY_CAR(cop5.Ped);
			if (flag)
			{
				hashSet3.Add(cop5.Car);
			}
			Member member = CopTarget(g, cop5);
			if (member == null || member.InCar || !flag)
			{
				continue;
			}
			float num = Vector3.Distance(cop5.Pos, member.Pos);
			bool flag2 = CarOk(cop5.Car);
			bool flag3 = false;
			if (flag2 && cop5.Mode == 4)
			{
				N.GET_CAR_SPEED(cop5.Car, out var speed);
				if (speed > 1.5f)
				{
					cop5.StillSince = -1.0;
				}
				else if (cop5.StillSince < 0.0)
				{
					cop5.StillSince = now;
				}
				flag3 = cop5.StillSince > 0.0 && now - cop5.StillSince > 4.0 && now - cop5.ModeAt > 4.0;
			}
			if (!flag2 || num < 30f || flag3 || now - cop5.Spawned > 25.0)
			{
				hashSet2.Add(cop5.Car);
			}
		}
		foreach (Cop cop6 in g.Cops)
		{
			if (!cop6.Alive)
			{
				continue;
			}
			Member member2 = CopTarget(g, cop6);
			if (member2 == null)
			{
				continue;
			}
			if (now - cop6.LastShot < 2.0)
			{
				cop6.Fails = 0;
			}
			bool flag4 = N.IS_CHAR_IN_ANY_CAR(cop6.Ped);
			float num2 = Vector3.Distance(cop6.Pos, member2.Pos);
			bool flag5 = CarOk(cop6.Car);
			bool inCar = member2.InCar;
			if (cop6.Target != member2.Ped)
			{
				cop6.Target = member2.Ped;
				cop6.Seen = false;
				cop6.LastSeen = -100.0;
				if (cop6.Mode != 7 && cop6.Mode != 6)
				{
					cop6.Mode = 0;
				}
			}
			if (now - cop6.LastSeen > 1.0)
			{
				cop6.LastSeen = now;
				cop6.Seen = CanSee(cop6.Ped, cop6.Pos, member2.Ped, member2.Pos);
			}
			int num3 = ((flag4 && inCar && flag5) ? (cop6.Driver ? 8 : ((!g.Lethal || !(num2 < 45f)) ? 15 : 9)) : ((flag4 && !inCar && flag5 && !hashSet2.Contains(cop6.Car) && hashSet3.Contains(cop6.Car)) ? ((!cop6.Driver) ? 15 : 4) : (flag4 ? 7 : ((inCar && flag5 && FlatDist(cop6.Pos, G.CarPos(cop6.Car)) < 35f && num2 > 25f) ? 6 : (g.Lethal ? ((!(num2 > 40f) && (cop6.Mode != 3 || !(num2 > 28f))) ? 2 : 3) : ((!(num2 > 60f)) ? 21 : 3))))));
			if (!flag4 && g.Lethal && (num3 == M_COMBAT || num3 == M_GOTO))
			{
				if (cop6.Mode == M_SHOOT && num2 <= CombatPolicy.ShootingRange && now - cop6.LastTask < cop6.PhaseLen)
					num3 = M_SHOOT;
				else if (cop6.Mode == M_COMBAT && num2 <= 60f && now - cop6.LastShot < 2.0)
					num3 = M_COMBAT;
			}
			bool retryCombat = false;
			if (!flag4 && g.Lethal && num3 == M_COMBAT)
			{
				if (cop6.Mode == M_SHOOT && now - cop6.LastTask < cop6.PhaseLen)
				{
					num3 = M_SHOOT;
				}
				else if (cop6.Mode == M_GOTO && !cop6.Seen && num2 > 10f && now - cop6.ModeAt < 3.0)
				{
					num3 = M_GOTO;
				}
				else
				{
					CombatPolicy.Recovery recovery = CombatPolicy.Decide(true,
						cop6.Mode != M_COMBAT, N.IS_PED_IN_COMBAT(cop6.Ped),
						cop6.Seen, num2, now - cop6.LastTask, now - cop6.LastShot);
					if (recovery == CombatPolicy.Recovery.Shoot)
						num3 = M_SHOOT;
					else if (recovery == CombatPolicy.Recovery.Advance)
						num3 = M_GOTO;
					else if (recovery == CombatPolicy.Recovery.Engage)
						retryCombat = true;
				}
			}
			bool flag6 = num3 != cop6.Mode || retryCombat;
			if (!flag6 && num3 == 3)
			{
				flag6 = now - cop6.LastTask > 3.0;
			}
			if (!flag6 && num3 == 21)
			{
				flag6 = now - cop6.LastTask > 10.0;
			}
			if (!flag6 && num3 == 6)
			{
				flag6 = now - cop6.LastTask > 8.0;
			}
			if (!flag6 && num3 == 8)
			{
				N.GET_CAR_SPEED(cop6.Car, out var speed2);
				if (speed2 > 1.5f)
				{
					cop6.StillSince = -1.0;
				}
				else if (cop6.StillSince < 0.0)
				{
					cop6.StillSince = now;
				}
				else if (now - cop6.StillSince > 6.0)
				{
					flag6 = true;
				}
			}
			if (!flag6 && num3 == 2)
			{
				flag6 = now - cop6.LastTask > 8.0 && !N.IS_PED_IN_COMBAT(cop6.Ped);
			}
			if (!flag6 && num3 == 7 && now - cop6.ModeAt > 4.0)
			{
				num3 = ((!g.Lethal) ? 21 : 2);
				flag6 = true;
			}
			if (!flag6)
			{
				continue;
			}
			Block(cop6, on: true);
			switch (num3)
			{
			case 15:
				cop6.Mode = 15;
				continue;
			case 8:
				N._TASK_CAR_MISSION_PED_TARGET(cop6.Ped, cop6.Car, member2.Ped, 2u, 40f, 2u, 10u, 10u);
				break;
			case 9:
				N.SET_CHAR_WILL_LEAVE_CAR_IN_COMBAT(cop6.Ped, v: false);
				N.SET_CHAR_WILL_DO_DRIVEBYS(cop6.Ped, v: true);
				N._TASK_COMBAT(cop6.Ped, member2.Ped);
				break;
			case 4:
				N._TASK_CAR_MISSION_PED_TARGET(cop6.Ped, cop6.Car, member2.Ped, 4u, 20f, 2u, 15u, 10u);
				break;
			case 7:
				N._TASK_LEAVE_ANY_CAR(cop6.Ped);
				break;
			case 21:
				N._TASK_CHAR_ARREST_CHAR(cop6.Ped, member2.Ped);
				break;
			case 6:
				if (cop6.Driver)
				{
					N._TASK_ENTER_CAR_AS_DRIVER(cop6.Ped, cop6.Car, 0u);
				}
				else
				{
					N._TASK_ENTER_CAR_AS_PASSENGER(cop6.Ped, cop6.Car, 0u, 0u);
				}
				break;
			case 3:
			{
				bool flag7 = cop6.Mode == 3 && now - cop6.ModeAt > 3.0;
				N.GET_CHAR_SPEED(cop6.Ped, out var v);
				if (flag7 && v < 0.5f)
				{
					N._TASK_GO_STRAIGHT_TO_COORD(cop6.Ped, member2.Pos, 4);
				}
				else
				{
					N._TASK_FOLLOW_NAV_MESH_TO_COORD(cop6.Ped, member2.Pos, 4);
				}
				break;
			}
			case 10:
			{
				int num4 = (int)(G.Rand(3f, 5f) * 1000f);
				N._TASK_SHOOT_AT_CHAR(cop6.Ped, member2.Ped, num4, 4);
				cop6.PhaseLen = (double)num4 / 1000.0;
				break;
			}
			case 19:
			{
				float side = G.Rand(3f, 6f) * ((G.Rng.Next(2) != 0) ? 1f : (-1f));
				N._TASK_GO_STRAIGHT_TO_COORD(cop6.Ped, Ground(GangRules.Strafe(cop6.Pos, member2.Pos, side, G.Rand(0f, 3f)), cop6.Pos.Z), 4);
				break;
			}
			default:
				N.SET_CHAR_WILL_LEAVE_CAR_IN_COMBAT(cop6.Ped, v: true);
				N._TASK_COMBAT(cop6.Ped, member2.Ped);
				break;
			}
			if (num3 != cop6.Mode || num3 == 10 || num3 == 3)
			{
				cop6.ModeAt = ((num3 != 3 || cop6.Mode != 3) ? now : cop6.ModeAt);
			}
			cop6.Mode = num3;
			cop6.LastTask = now;
			if (num3 != 8)
			{
				cop6.StillSince = -1.0;
			}
		}
		ManageAmbientCops(g, now);
	}

	private void ManageAmbientCops(Gang g, double now)
	{
		if (!g.Lethal)
			return;
		if (now - g.LastScan > 2.0)
		{
			g.LastScan = now;
			int ambientCount = 0;
			foreach (Gang otherGang in gangs)
				ambientCount += otherGang.AmbientCops.Count;
			foreach (int ped in G.PedsNear(g.Leader.Pos, CombatPolicy.SightRange, 20))
			{
				if (ambientCount >= MAX_AMBIENT_COPS)
					break;
				if (madeCops.Contains(ped) || g.AmbientCops.ContainsKey(ped) || !IsCop(ped))
					continue;
				bool owned = false;
				foreach (Gang otherGang in gangs)
					if (otherGang != g && otherGang.AmbientCops.ContainsKey(ped))
						owned = true;
				if (owned)
					continue;
				// Keep the real cop's relationship group and vehicle ownership intact.
				N.SET_CHAR_RELATIONSHIP(ped, 5u, g.Group);
				N.SET_CHAR_KEEP_TASK(ped, v: true);
				N.SET_CHAR_WILL_USE_COVER(ped, v: true);
				N.SET_CHAR_WILL_LEAVE_CAR_IN_COMBAT(ped, v: true);
				g.AmbientCops[ped] = new Cop { Ped = ped, Spawned = now, Pos = G.CharPos(ped) };
				ambientCount++;
				g.Tasked.Add(ped);
			}
		}
		List<int> released = new List<int>();
		foreach (KeyValuePair<int, Cop> entry in g.AmbientCops)
		{
			Cop c = entry.Value;
			if (!N.DOES_CHAR_EXIST(c.Ped) || N.IS_CHAR_DEAD(c.Ped) || N.IS_CHAR_FATALLY_INJURED(c.Ped))
			{
				released.Add(c.Ped);
				continue;
			}
			c.Pos = G.CharPos(c.Ped);
			Member target = CopTarget(g, c);
			float distance = target == null ? float.MaxValue : Vector3.Distance(c.Pos, target.Pos);
			if (target == null || distance > 240f || (distance > 140f && !N.IS_CHAR_ON_SCREEN(c.Ped)))
			{
				ReleaseAmbientCop(g, c);
				released.Add(c.Ped);
				g.Tasked.Remove(c.Ped);
				continue;
			}
			if (distance < 100f)
				g.LastContact = now;
			if (N.IS_CHAR_SHOOTING(c.Ped))
				c.LastShot = now;
			bool changed = c.Target != target.Ped;
			if (changed || now - c.LastSeen > 1.0)
			{
				c.LastSeen = now;
				c.Seen = CanSee(c.Ped, c.Pos, target.Ped, target.Pos);
			}
			if (!changed && c.Mode == M_SHOOT && now - c.LastTask < c.PhaseLen)
				continue;
			if (!changed && c.Mode == M_GOTO && now - c.LastTask < 3.0)
				continue;
			CombatPolicy.Recovery recovery = CombatPolicy.Decide(true,
				changed || c.Mode != M_COMBAT, N.IS_PED_IN_COMBAT(c.Ped),
				c.Seen, distance, now - c.LastTask, now - c.LastShot);
			if (recovery == CombatPolicy.Recovery.Keep)
				continue;
			if (!changed && recovery == CombatPolicy.Recovery.Advance && now - c.LastTask < 3.0)
				continue;
			Block(c, on: true);
			if (recovery == CombatPolicy.Recovery.Advance && !N.IS_CHAR_IN_ANY_CAR(c.Ped))
			{
				N._TASK_FOLLOW_NAV_MESH_TO_COORD(c.Ped, target.Pos, 4);
				c.Mode = M_GOTO;
			}
			else if (recovery == CombatPolicy.Recovery.Shoot && !N.IS_CHAR_IN_ANY_CAR(c.Ped))
			{
				c.PhaseLen = 3.5;
				N._TASK_SHOOT_AT_CHAR(c.Ped, target.Ped, 3500, 4);
				c.Mode = M_SHOOT;
			}
			else
			{
				N._TASK_COMBAT(c.Ped, target.Ped);
				c.Mode = M_COMBAT;
			}
			c.Target = target.Ped;
			c.ModeAt = now;
			c.LastTask = now;
		}
		foreach (int ped in released)
			g.AmbientCops.Remove(ped);
	}

	private static void ReleaseAmbientCop(Gang g, Cop c)
	{
		if (N.DOES_CHAR_EXIST(c.Ped) && !N.IS_CHAR_DEAD(c.Ped))
		{
			Block(c, on: false);
			N.SET_CHAR_KEEP_TASK(c.Ped, v: false);
			N.CLEAR_CHAR_RELATIONSHIP(c.Ped, 5, g.Group);
			N.CLEAR_CHAR_TASKS(c.Ped);
		}
	}

	private static void ReleaseAmbientCops(Gang g)
	{
		foreach (Cop cop in g.AmbientCops.Values)
			ReleaseAmbientCop(g, cop);
		g.AmbientCops.Clear();
	}

	private void ReleaseManagedCop(Cop c)
	{
		madeCops.Remove(c.Ped);
		if (c.Ped == 0 || !N.DOES_CHAR_EXIST(c.Ped))
			return;
		if (!N.IS_CHAR_DEAD(c.Ped) && !N.IS_CHAR_FATALLY_INJURED(c.Ped))
		{
			Block(c, on: false);
			N.SET_CHAR_KEEP_TASK(c.Ped, v: false);
			N.CLEAR_CHAR_TASKS(c.Ped);
			N.SET_CHAR_RELATIONSHIP_GROUP(c.Ped, 3);
			N.CLEAR_CHAR_RELATIONSHIP(c.Ped, 0, 3);
			N.CLEAR_CHAR_RELATIONSHIP(c.Ped, 0, GROUP_COPS);
			for (int slot = 0; slot < MAX_SLOTS; slot++)
				N.CLEAR_CHAR_RELATIONSHIP(c.Ped, 5, GROUP_BASE + slot);
		}
		N.MARK_CHAR_AS_NO_LONGER_NEEDED(c.Ped);
	}

	private Member CopTarget(Gang g, Cop c)
	{
		Member member = NearestMemberOf(g, c.Pos);
		Member member2 = MemberByPed(g, c.Target);
		if (member2 == null || member2.Dead || member == null)
		{
			return member;
		}
		return (!(Vector3.Distance(member2.Pos, c.Pos) < Vector3.Distance(member.Pos, c.Pos) + 15f)) ? member : member2;
	}

	private bool Cleanup(Gang g, double now)
	{
		bool result = false;
		for (int i = 0; i < g.Cops.Count; i++)
		{
			Cop cop = g.Cops[i];
			bool flag = cop.DeadAt > 0.0 && now - cop.DeadAt > 25.0;
			if (!flag && cop.Alive)
			{
				Member member = NearestMemberOf(g, cop.Pos);
				flag = member != null && FlatDist(member.Pos, cop.Pos) > 320f && !N.IS_CHAR_ON_SCREEN(cop.Ped);
			}
			if (!flag)
			{
				continue;
			}
			try
			{
				ReleaseManagedCop(cop);
				bool flag2 = false;
				foreach (Cop cop2 in g.Cops)
				{
					if (cop2 != cop && cop2.Car == cop.Car && cop2.Alive)
					{
						flag2 = true;
						break;
					}
				}
				if (!flag2 && cop.Car != 0 && N.DOES_VEHICLE_EXIST(cop.Car))
				{
					bool flag3 = false;
					foreach (Cop cop3 in g.Cops)
					{
						if (cop3 != cop && cop3.Car == cop.Car)
						{
							flag3 = true;
							break;
						}
					}
					if (!flag3)
					{
						N.MARK_CAR_AS_NO_LONGER_NEEDED(cop.Car);
					}
				}
			}
			catch
			{
			}
			g.Cops.RemoveAt(i--);
			result = true;
		}
		List<int> list = new List<int>();
		foreach (KeyValuePair<int, double> car in g.Cars)
		{
			int key = car.Key;
			bool flag4 = false;
			foreach (Member member2 in g.Members)
			{
				if (!member2.Dead && ((member2.Car == key && member2.InCar) || member2.StealCar == key))
				{
					flag4 = true;
					break;
				}
			}
			if (flag4)
			{
				continue;
			}
			bool flag5 = false;
			if (N.DOES_VEHICLE_EXIST(key))
			{
				Vector3 b = G.CarPos(key);
				foreach (Member member3 in g.Members)
				{
					if (!member3.Dead && FlatDist(member3.Pos, b) < 60f)
					{
						flag5 = true;
						break;
					}
				}
			}
			if ((!flag5 || !(now - car.Value < 90.0)) && !(now - car.Value < 20.0))
			{
				list.Add(key);
			}
		}
		foreach (int item in list)
		{
			try
			{
				if (N.DOES_VEHICLE_EXIST(item))
				{
					N.MARK_CAR_AS_NO_LONGER_NEEDED(item);
				}
			}
			catch
			{
			}
			g.Cars.Remove(item);
			foreach (Member member4 in g.Members)
			{
				if (member4.Car == item && !member4.InCar)
				{
					member4.Car = 0;
				}
			}
			result = true;
		}
		List<int> oldAmbientHandles = new List<int>();
		foreach (int ped in g.Tasked)
			if (!N.DOES_CHAR_EXIST(ped) || (g.Counted.Contains(ped) && N.IS_CHAR_DEAD(ped)))
				oldAmbientHandles.Add(ped);
		foreach (int ped in oldAmbientHandles)
			g.Tasked.Remove(ped);
		return result;
	}

	private bool IsCop(int ped)
	{
		if (copModels == null)
		{
			copModels = new HashSet<uint>();
			string[] array = new string[7] { "M_Y_COP", "M_M_FATCOP_01", "M_Y_COP_TRAFFIC", "M_Y_STROOPER", "M_Y_SWAT", "M_M_FBI", "M_Y_NHELIPILOT" };
			foreach (string s in array)
			{
				copModels.Add((uint)N.GET_HASH_KEY(s));
			}
			try
			{
				N.GET_CURRENT_BASIC_COP_MODEL(out var model);
				if (model != 0)
				{
					copModels.Add(model);
				}
			}
			catch
			{
			}
		}
		N.GET_CHAR_MODEL(ped, out var model2);
		return copModels.Contains(model2);
	}

	private void Diagnose(Gang g, Member m, double now)
	{
		try
		{
			N.GET_CHAR_SPEED(m.Ped, out var v);
			string text = string.Empty;
			if (m.Target != 0 && N.DOES_CHAR_EXIST(m.Target))
			{
				text = " objetivo a " + Vector3.Distance(G.CharPos(m.Target), m.Pos).ToString("0") + " m" + ((!N.IS_CHAR_DEAD(m.Target)) ? string.Empty : " (muerto)") + ((!m.Seen) ? string.Empty : " (lo ve)");
			}
			string text2 = "[diag] " + Name(g, m) + ": " + ModeName(m.Mode) + text + ", vida " + (int)(m.Health01 * 100f) + "%, tiro hace " + ((!(now - m.LastShot > 99.0)) ? ((now - m.LastShot).ToString("0") + " s") : "-") + ", vel " + v.ToString("0.0") + ((!m.InCar) ? string.Empty : ((!m.Driving) ? ", en auto" : ", maneja")) + ((!m.Fleeing) ? string.Empty : ", escapando") + ((!m.Eating) ? string.Empty : ", comiendo") + ((g.Order < 0) ? string.Empty : (", orden !" + GangRules.CmdKeys[g.Order])) + ((!N.IS_PED_IN_COMBAT(m.Ped)) ? string.Empty : ", combate del juego");
			Cop cop = null;
			float num = float.MaxValue;
			foreach (Gang gang in gangs)
			{
				foreach (Cop cop2 in AllCops(gang))
				{
					if (cop2.Alive)
					{
						float num2 = Vector3.Distance(cop2.Pos, m.Pos);
						if (num2 < num)
						{
							num = num2;
							cop = cop2;
						}
					}
				}
			}
			if (cop != null)
			{
				N.GET_CHAR_SPEED(cop.Ped, out var v2);
				string text3 = text2;
				text2 = text3 + " | poli a " + num.ToString("0") + " m: " + ModeName(cop.Mode) + ((!cop.Seen) ? string.Empty : " (lo ve)") + ((!N.IS_PED_IN_COMBAT(cop.Ped)) ? string.Empty : " (combate)") + ", tiro hace " + ((!(now - cop.LastShot > 99.0)) ? ((now - cop.LastShot).ToString("0") + " s") : "-") + ", vel " + v2.ToString("0.0") + ((!N.IS_CHAR_IN_ANY_CAR(cop.Ped)) ? string.Empty : ", en auto");
			}
			log(text2);
		}
		catch (Exception ex)
		{
			log("[diag] error: " + ex.Message);
		}
	}

	private bool ReleaseOldest()
	{
		if (gangs.Count == 0)
		{
			return false;
		}
		int num = -1;
		for (int i = 0; i < gangs.Count; i++)
		{
			if (gangs[i].State == 1 && gangs[i].Alive == 0)
			{
				num = i;
				break;
			}
		}
		if (num < 0)
		{
			num = 0;
		}
		Gang gang = gangs[num];
		log("[NPC] " + gang.User + " deja lugar para otro");
		ReleaseGang(gang);
		gangs.RemoveAt(num);
		Changed();
		return true;
	}

	private void ReleaseMember(Member m)
	{
		try
		{
			if (m.Ped != 0 && N.DOES_CHAR_EXIST(m.Ped))
			{
				if (m.FakeName)
				{
					N.REMOVE_FAKE_NETWORK_NAME_FROM_PED(m.Ped);
				}
				N.MARK_CHAR_AS_NO_LONGER_NEEDED(m.Ped);
			}
		}
		catch
		{
		}
	}

	private void ReleaseGang(Gang g)
	{
		try
		{
			ReleaseAmbientCops(g);
			if (g.State == 0 && g.CarModel != 0)
			{
				N.MARK_MODEL_AS_NO_LONGER_NEEDED(g.CarModel);
			}
			HashSet<int> hashSet = new HashSet<int>();
			foreach (Member member in g.Members)
			{
				ReleaseMember(member);
				if (member.Car != 0)
				{
					hashSet.Add(member.Car);
				}
			}
			foreach (KeyValuePair<int, double> car in g.Cars)
			{
				hashSet.Add(car.Key);
			}
			foreach (Cop cop in g.Cops)
			{
				ReleaseManagedCop(cop);
				if (cop.Car != 0)
				{
					hashSet.Add(cop.Car);
				}
			}
			foreach (int item in hashSet)
			{
				if (item != 0 && N.DOES_VEHICLE_EXIST(item))
				{
					N.MARK_CAR_AS_NO_LONGER_NEEDED(item);
				}
			}
		}
		catch
		{
		}
	}

	public void ReleaseAll()
	{
		foreach (Gang gang in gangs)
		{
			ReleaseGang(gang);
		}
		gangs.Clear();
		Changed();
	}

	public void ForgetState()
	{
		foreach (Gang gang in gangs)
			ReleaseAmbientCops(gang);
		gangs.Clear();
		G.ProtectedPeds.Clear();
		G.ProtectedCars.Clear();
		tags = null;
		SaveHandles();
	}

	private void Changed()
	{
		G.ProtectedPeds.Clear();
		G.ProtectedCars.Clear();
		foreach (Gang gang in gangs)
		{
			foreach (Member member in gang.Members)
			{
				if (member.Ped != 0)
				{
					G.ProtectedPeds.Add(member.Ped);
				}
				if (member.Car != 0)
				{
					G.ProtectedCars.Add(member.Car);
				}
			}
			foreach (KeyValuePair<int, double> car in gang.Cars)
			{
				G.ProtectedCars.Add(car.Key);
			}
			foreach (Cop cop in gang.Cops)
			{
				if (cop.Ped != 0)
				{
					G.ProtectedPeds.Add(cop.Ped);
				}
				if (cop.Car != 0)
				{
					G.ProtectedCars.Add(cop.Car);
				}
			}
		}
		SaveHandles();
	}

	private void SaveHandles()
	{
		try
		{
			List<int> list = new List<int>(G.ProtectedPeds);
			List<int> list2 = new List<int>(G.ProtectedCars);
			AppDomain.CurrentDomain.SetData("KickChaos.NpcPeds", (list.Count <= 0) ? null : list.ToArray());
			AppDomain.CurrentDomain.SetData("KickChaos.NpcCars", (list2.Count <= 0) ? null : list2.ToArray());
		}
		catch
		{
		}
	}

	public void RecoverLeftovers()
	{
		try
		{
			int[] array = AppDomain.CurrentDomain.GetData("KickChaos.NpcPeds") as int[];
			int[] array2 = AppDomain.CurrentDomain.GetData("KickChaos.NpcCars") as int[];
			int num = 0;
			if (array != null)
			{
				int[] array3 = array;
				foreach (int num2 in array3)
				{
					if (num2 != 0 && N.DOES_CHAR_EXIST(num2))
					{
						madeCops.Add(num2);
						N.MARK_CHAR_AS_NO_LONGER_NEEDED(num2);
						num++;
					}
				}
			}
			if (array2 != null)
			{
				int[] array4 = array2;
				foreach (int num3 in array4)
				{
					if (num3 != 0 && N.DOES_VEHICLE_EXIST(num3))
					{
						N.MARK_CAR_AS_NO_LONGER_NEEDED(num3);
					}
				}
			}
			if (num > 0)
			{
				log("[NPC] se soltaron " + num + " personajes que dejo una instancia anterior");
			}
			AppDomain.CurrentDomain.SetData("KickChaos.NpcPeds", null);
			AppDomain.CurrentDomain.SetData("KickChaos.NpcCars", null);
		}
		catch
		{
		}
	}

	public void BuildTags(Vector3 camPos, Vector3 camRot, float fov, float aspect, bool visible)
	{
		if (!visible || !ShowName || GameName || gangs.Count == 0)
		{
			tags = null;
			return;
		}
		tagAspect = aspect;
		double now = G.Now;
		Gang gang = ((controlMode != 0) ? FocusGang() : null);
		List<NpcTag> list = new List<NpcTag>();
		foreach (Gang gang2 in gangs)
		{
			if (gang2.State != 1)
			{
				continue;
			}
			Member leader = gang2.Leader;
			foreach (Member member in gang2.Members)
			{
				if (member.Ped == 0 || (member.Dead && now - member.DeadAt > 4.5))
				{
					continue;
				}
				Vector3 vector = ((!member.Dead) ? member.Head : (member.Pos + new Vector3(0f, 0f, 0.4f)));
				if (!NpcRules.Project(vector, camPos, camRot, fov, aspect, out var sx, out var sy, out var depth))
				{
					continue;
				}
				if (now - member.LastLos > 0.3)
				{
					member.LastLos = now;
					member.Occluded = Blocked(camPos, vector);
				}
				if (member.Occluded)
				{
					continue;
				}
				bool flag = member == leader;
				NpcTag npcTag = new NpcTag();
				npcTag.X = sx;
				npcTag.Y = sy;
				npcTag.Scale = Math.Max(0.5f, Math.Min(1f, NpcRules.ApparentHeight(depth, fov) / 0.12f));
				npcTag.Name = ((member.Index != 0) ? GangRules.MemberName(gang2.User) : gang2.User);
				npcTag.Health = member.Health01;
				npcTag.Dead = member.Dead;
				npcTag.Bar = HealthBar && !member.Dead;
				NpcTag npcTag2 = npcTag;
				npcTag2.Sub = SubFor(gang2, member, flag, now, out var color);
				npcTag2.SubColor = color;
				if (flag && !member.Dead)
				{
					npcTag2.Timer = ((!ShowTimer) ? string.Empty : GangRules.Clock(gang2.EndAt - now)) + ((gang2.Stars <= 0) ? string.Empty : (((!ShowTimer) ? string.Empty : "   ") + new string('*', gang2.Stars)));
					if (gang2.Control && (controlMode == 0 || gang2 == gang))
					{
						npcTag2.CmdsVote = gang2.Votes.Open;
						npcTag2.Cmds = ((!npcTag2.CmdsVote) ? GangRules.ChoicesLine(slots) : GangRules.VoteLine(slots, gang2.Votes, now));
					}
					if (ShowChat && now < gang2.BubbleUntil && gang2.Bubble != null && gang2.Bubble.Length > 0)
					{
						npcTag2.Bubble = gang2.Bubble;
					}
				}
				list.Add(npcTag2);
			}
		}
		tags = list;
	}

	private string SubFor(Gang g, Member m, bool main, double now, out int color)
	{
		color = 0;
		if (m.Dead)
		{
			color = 1;
			return (!m.Arrested) ? "FUERA DE COMBATE" : "ARRESTADO";
		}
		if (now < m.FlashUntil && m.Flash.Length > 0)
		{
			color = 3;
			return m.Flash;
		}
		if (m.Eating)
		{
			color = 2;
			return "COMPRANDO COMIDA";
		}
		if (m.Fleeing)
		{
			color = 2;
			return "POCA VIDA: SE ESCAPA";
		}
		if (!main)
		{
			return string.Empty;
		}
		if (now < g.FlashUntil && g.Flash.Length > 0)
		{
			color = 3;
			return g.Flash;
		}
		if (g.Members.Count > 1)
		{
			return GangRules.GangTitle(g.User, g.Alive);
		}
		return g.Sub;
	}

	public void ClearTags()
	{
		tags = null;
	}

	private static bool Blocked(Vector3 cam, Vector3 head)
	{
		float num = Vector3.Distance(cam, head);
		if (num < 6f)
		{
			return false;
		}
		Vector3 vector = Vector3.Lerp(cam, head, Math.Min(0.5f, 5f / num));
		Vector3 to = Vector3.Lerp(cam, head, 1f - Math.Min(0.3f, 1.5f / num));
		Vector3 hit;
		return G.Raycast(vector, to, out hit);
	}

	private Font MakeFont(float h, bool bold)
	{
		Font val = new Font("Arial", h, (FontScaling)1, bold, false);
		try
		{
			val.Effect = (FontEffect)2;
			val.EffectColor = Color.Black;
			val.EffectSize = 1;
		}
		catch
		{
		}
		return val;
	}

	private static Color SubColorOf(int c)
	{
		return c switch
		{
			1 => DeadRed, 
			2 => Orange, 
			3 => FlashYellow, 
			_ => KickGreen, 
		};
	}

	public void DrawTags(Graphics g)
	{
		List<NpcTag> list = tags;
		if (list == null || list.Count == 0)
		{
			return;
		}
		g.Scaling = (FontScaling)1;
		if (nameFonts == null || Math.Abs(fontScale - NameScale) > 0.001f)
		{
			DisposeFonts();
			fontScale = NameScale;
			float[] array = new float[3] { 1f, 0.82f, 0.66f };
			nameFonts = (Font[])(object)new Font[3];
			subFonts = (Font[])(object)new Font[3];
			smallFonts = (Font[])(object)new Font[3];
			bubbleFonts = (Font[])(object)new Font[3];
			for (int i = 0; i < 3; i++)
			{
				nameFonts[i] = MakeFont(0.032f * array[i] * fontScale, bold: true);
				subFonts[i] = MakeFont(0.02f * array[i] * fontScale, bold: true);
				smallFonts[i] = MakeFont(0.018f * array[i] * fontScale, bold: true);
				bubbleFonts[i] = MakeFont(0.021f * array[i] * fontScale, bold: false);
			}
		}
		float num = Math.Max(0.5f, tagAspect);
		foreach (NpcTag item in list)
		{
			int num2 = ((!(item.Scale >= 0.85f)) ? ((item.Scale >= 0.65f) ? 1 : 2) : 0);
			float num3 = num2 switch
			{
				0 => 1f, 
				1 => 0.82f, 
				_ => 0.66f, 
			} * fontScale;
			float num4 = 0.034f * num3;
			float num5 = 0.022f * num3;
			float num6 = 0.02f * num3;
			float num7 = 0.023f * num3;
			float num8 = 0.0055f * num3;
			float num9 = 0.06f * num3;
			float num10 = 0.004f * num3;
			float num11 = item.Y - 0.008f * num3;
			if (item.Bar)
			{
				num11 -= num8;
				g.DrawRectangle(new RectangleF(item.X - num9 / 2f - 0.0015f, num11 - 0.0015f, num9 + 0.003f, num8 + 0.003f), Color.FromArgb(170, 0, 0, 0));
				if (item.Health > 0.001f)
				{
					g.DrawRectangle(new RectangleF(item.X - num9 / 2f, num11, num9 * item.Health, num8), HealthColor(item.Health));
				}
				if (item.Timer.Length > 0)
				{
					g.DrawText(item.Timer, new RectangleF(item.X + num9 / 2f + 0.005f, num11 + num8 / 2f - num6 / 2f, 0.1f, num6), (TextAlignment)36, Color.White, smallFonts[num2]);
				}
				num11 -= num10;
			}
			else if (item.Timer.Length > 0 && !item.Dead)
			{
				num11 -= num6;
				g.DrawText(item.Timer, new RectangleF(item.X - 0.1f, num11, 0.2f, num6), (TextAlignment)37, Color.White, smallFonts[num2]);
			}
			if (item.Sub.Length > 0)
			{
				num11 -= num5;
				g.DrawText(item.Sub, new RectangleF(item.X - 0.25f, num11, 0.5f, num5), (TextAlignment)37, SubColorOf(item.SubColor), subFonts[num2]);
			}
			num11 -= num4;
			g.DrawText(item.Name, new RectangleF(item.X - 0.25f, num11, 0.5f, num4), (TextAlignment)37, (!item.Dead) ? Color.White : Color.FromArgb(255, 170, 170, 170), nameFonts[num2]);
			if (item.Cmds.Length > 0)
			{
				num11 -= num6;
				g.DrawText(item.Cmds, new RectangleF(item.X - 0.35f, num11, 0.7f, num6), (TextAlignment)37, CmdYellow, smallFonts[num2]);
			}
			if (item.Bubble != null && item.Bubble.Length > 0)
			{
				int num12 = 0;
				string[] bubble = item.Bubble;
				foreach (string text in bubble)
				{
					num12 = Math.Max(num12, text.Length);
				}
				float num13 = num7 * 1.05f;
				float num14 = Math.Min(0.5f, (float)num12 * num7 * 0.5f / num + 0.018f * num3);
				float num15 = (float)item.Bubble.Length * num13 + 0.01f * num3;
				num11 -= num10 * 1.5f + num15;
				g.DrawRectangle(new RectangleF(item.X - num14 / 2f, num11, num14, num15), Color.FromArgb(190, 12, 12, 12));
				g.DrawRectangle(new RectangleF(item.X - num14 / 2f, num11, num14, 0.0025f * num3), KickGreen);
				for (int k = 0; k < item.Bubble.Length; k++)
				{
					g.DrawText(item.Bubble[k], new RectangleF(item.X - num14 / 2f, num11 + 0.005f * num3 + (float)k * num13, num14, num13), (TextAlignment)37, Color.White, bubbleFonts[num2]);
				}
			}
		}
	}

	private static Color HealthColor(float h)
	{
		if (h > 0.5f)
		{
			int val = (int)(255f * (1f - h) * 2f);
			return Color.FromArgb(230, Math.Min(255, val), 220, 40);
		}
		int val2 = (int)(220f * h * 2f);
		return Color.FromArgb(230, 235, Math.Max(0, val2), 40);
	}

	public void DisposeFonts()
	{
		Font[][] array = new Font[4][] { nameFonts, subFonts, smallFonts, bubbleFonts };
		foreach (Font[] array2 in array)
		{
			if (array2 == null)
			{
				continue;
			}
			Font[] array3 = array2;
			foreach (Font val in array3)
			{
				try
				{
					if (val != null)
					{
						val.Dispose();
					}
				}
				catch
				{
				}
			}
		}
		nameFonts = (subFonts = (smallFonts = (bubbleFonts = null)));
	}
}
