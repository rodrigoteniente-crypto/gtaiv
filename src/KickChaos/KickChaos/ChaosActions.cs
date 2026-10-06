using System;
using System.Collections.Generic;
using System.Numerics;

namespace KickChaos;

public class ChaosActions
{
	private class Job
	{
		public double At;

		public Action Fn;

		public string Name;
	}

	private Config cfg;

	private readonly Director dir;

	private readonly Action<string> log;

	private readonly List<Job> jobs = new List<Job>();

	private readonly List<KeyValuePair<int, double>> fires = new List<KeyValuePair<int, double>>();

	private readonly List<KeyValuePair<int, double>> spawnedCars = new List<KeyValuePair<int, double>>();

	private readonly Dictionary<int, int> requestedModels = new Dictionary<int, int>();

	private double wantedUntil = -1.0;

	private double slowmoUntil = -1.0;

	private double stormUntil = -1.0;

	private static readonly Dictionary<string, float> DefaultHold = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
	{
		{ "Policia", 30f },
		{ "Bomberos", 30f },
		{ "LluviaDeCoches", 15f },
		{ "Fuego", 20f },
		{ "Disturbio", 25f },
		{ "Terremoto", 10f },
		{ "Bombardeo", 10f },
		{ "ExplotarCoches", 10f },
		{ "Persecucion", 15f },
		{ "Panico", 10f },
		{ "AutosLocos", 15f },
		{ "AutosVoladores", 10f },
		{ "SiguienteCamara", 0f }
	};

	public SubNpcManager Npcs;

	public string LastLabel;

	private static readonly string DefaultCarModels = "ADMIRAL,BANSHEE,CABBY,INFERNUS,PMP600,SULTAN,TAXI,STOCKADE,BOBCAT,PATRIOT,COMET,MRTASTY,FLATBED,BUS";

	public int PendingJobs => jobs.Count;

	public ChaosActions(Config cfg, Director dir, Action<string> log)
	{
		this.cfg = cfg;
		this.dir = dir;
		this.log = log;
	}

	public void SetConfig(Config c)
	{
		cfg = c;
	}

	private float P(string a, string k, float d)
	{
		return cfg.P(a, k, d);
	}

	private int PI(string a, string k, int d)
	{
		return cfg.PI(a, k, d);
	}

	private void Later(double seconds, string name, Action fn)
	{
		jobs.Add(new Job
		{
			At = G.Now + seconds,
			Fn = fn,
			Name = name
		});
	}

	public bool Execute(QueuedAction qa)
	{
		LastLabel = null;
		if (qa.Name.StartsWith("Npc", StringComparison.Ordinal))
		{
			if (Npcs == null)
			{
				return false;
			}
			string label;
			bool result = Npcs.Spawn(qa, dir.ActionCenter(), NpcRules.FromAction(qa.Name), out label);
			LastLabel = label;
			return result;
		}
		return Execute(qa.Name);
	}

	public bool Execute(string canonical)
	{
		Vector3 vector = dir.ActionCenter();
		if (!DefaultHold.TryGetValue(canonical, out var value))
		{
			value = 8f;
		}
		if (canonical != "SiguienteCamara")
		{
			dir.HoldFor(P(canonical, "Mantener", value));
		}
		switch (canonical)
		{
		case "Explosion":
		case "Bombardeo":
		case "ExplotarCoches":
		case "Policia":
		case "Persecucion":
		case "Bomberos":
		case "Fuego":
		case "LluviaDeCoches":
		case "Disturbio":
		case "Terremoto":
		case "Panico":
		case "AutosLocos":
		case "AutosVoladores":
			dir.Repair.MarkDirty(vector);
			break;
		}
		switch (canonical)
		{
		case "Explosion":
			DoExplosion(vector);
			return true;
		case "Bombardeo":
			DoAirStrike(vector);
			return true;
		case "ExplotarCoches":
			DoCarBombs(vector);
			return true;
		case "Policia":
			DoPolice(vector);
			return true;
		case "Persecucion":
			DoWanted();
			return true;
		case "Bomberos":
			DoEmergency(vector);
			return true;
		case "Fuego":
			DoFire(vector);
			return true;
		case "LluviaDeCoches":
			DoCarRain(vector);
			return true;
		case "Disturbio":
			DoRiot(vector);
			return true;
		case "Panico":
			DoPanic(vector, 60f);
			return true;
		case "Tormenta":
			DoStorm();
			return true;
		case "Noche":
			SetTime("Noche", 0, 0);
			return true;
		case "Dia":
			SetTime("Dia", 13, 0);
			return true;
		case "Atardecer":
			SetTime("Atardecer", 19, 30);
			return true;
		case "CamaraLenta":
			DoSlowMo();
			return true;
		case "Terremoto":
			DoEarthquake(vector);
			return true;
		case "AutosLocos":
			DoCrazyCars(vector);
			return true;
		case "AutosVoladores":
			DoFlyingCars(vector);
			return true;
		case "Lluvia":
			DoWeather("Lluvia", 4u, 90f);
			return true;
		case "Niebla":
			DoWeather("Niebla", 6u, 90f);
			return true;
		case "SiguienteCamara":
			dir.Next();
			return true;
		default:
			return false;
		}
	}

	private void SetTime(string action, int defHour, int defMinute)
	{
		N.SET_TIME_OF_DAY((uint)PI(action, "Hora", defHour), (uint)PI(action, "Minuto", defMinute));
		dir.TimeLockUntil = G.Now + (double)P(action, "Duracion", 180f);
	}

	private void RequestModel(int m)
	{
		N.REQUEST_MODEL(m);
		requestedModels.TryGetValue(m, out var value);
		requestedModels[m] = value + 1;
	}

	private void ReleaseModel(int m)
	{
		if (requestedModels.TryGetValue(m, out var value))
		{
			if (value <= 1)
			{
				requestedModels.Remove(m);
				N.MARK_MODEL_AS_NO_LONGER_NEEDED(m);
			}
			else
			{
				requestedModels[m] = value - 1;
			}
		}
	}

	private void DoExplosion(Vector3 c)
	{
		float num = P("Explosion", "Radio", 20f);
		List<int> list = G.VehiclesNear(c, num, 1);
		if (list.Count > 0 && G.Rng.NextDouble() < (double)P("Explosion", "ProbabilidadCoche", 0.7f))
		{
			N.EXPLODE_CAR(list[0], a: true, b: false);
		}
		else
		{
			Vector3 p = G.RandomGroundPoint(c, 0f, num * 0.6f);
			N.ADD_EXPLOSION(p, PI("Explosion", "Tipo", 4), P("Explosion", "Tamano", 6f), sound: true, invisible: false, 0.3f);
		}
		dir.Shake(P("Explosion", "Sacudida", 0.6f), 0.9f);
	}

	private void DoAirStrike(Vector3 c)
	{
		int num = PI("Bombardeo", "Cantidad", 8);
		float r = P("Bombardeo", "Radio", 30f);
		float num2 = P("Bombardeo", "Intervalo", 0.35f);
		int[] types = new int[4] { 2, 3, 4, 5 };
		for (int i = 0; i < num; i++)
		{
			Later((float)i * num2, "bombardeo", delegate
			{
				Vector3 p = G.RandomGroundPoint(c, 0f, r);
				N.ADD_EXPLOSION(p, types[G.Rng.Next(types.Length)], G.Rand(4f, 8f), sound: true, invisible: false, 0.2f);
				dir.Shake(P("Bombardeo", "Sacudida", 0.8f), 0.7f);
			});
		}
	}

	private void DoCarBombs(Vector3 c)
	{
		List<int> list = G.VehiclesNear(c, P("ExplotarCoches", "Radio", 40f), PI("ExplotarCoches", "Cantidad", 6));
		if (list.Count == 0)
		{
			DoExplosion(c);
			return;
		}
		for (int i = 0; i < list.Count; i++)
		{
			int v = list[i];
			Later((float)i * P("ExplotarCoches", "Intervalo", 0.45f), "coches", delegate
			{
				if (N.DOES_VEHICLE_EXIST(v) && !N.IS_CAR_DEAD(v))
				{
					N.EXPLODE_CAR(v, a: true, b: false);
				}
				dir.Shake(0.7f, 0.8f);
			});
		}
	}

	private void DoPolice(Vector3 c)
	{
		N.GET_CURRENT_BASIC_POLICE_CAR_MODEL(out var model);
		if (model == 0)
		{
			model = (uint)N.GET_HASH_KEY("POLICE");
		}
		int count = PI("Policia", "Patrulleros", 3);
		SpawnEmergency(new uint[1] { model }, count, c, "Policia");
		if (cfg.Ini.GetBool("Accion.Policia", "TambienEstrellas", def: false))
		{
			DoWanted();
		}
	}

	private void DoEmergency(Vector3 c)
	{
		uint num = (uint)N.GET_HASH_KEY("FIRETRUK");
		uint num2 = (uint)N.GET_HASH_KEY("AMBULANCE");
		SpawnEmergency(new uint[2] { num, num2 }, 2, c, "Bomberos");
	}

	private void SpawnEmergency(uint[] models, int count, Vector3 c, string tag)
	{
		uint[] array = models;
		foreach (uint m in array)
		{
			RequestModel((int)m);
		}
		double deadline = G.Now + 6.0;
		int spawned = 0;
		int tries = 0;
		Action step = null;
		step = delegate
		{
			bool flag = true;
			uint[] array2 = models;
			foreach (uint model in array2)
			{
				if (!N.HAS_MODEL_LOADED((int)model))
				{
					flag = false;
				}
			}
			if (!flag && G.Now < deadline)
			{
				Later(0.2, tag, step);
			}
			else
			{
				uint model2 = models[spawned % models.Length];
				Vector3 p = G.RandomGroundPoint(c, 2f, 10f);
				bool flag2 = false;
				if (N.HAS_MODEL_LOADED((int)model2))
				{
					try
					{
						flag2 = N.CREATE_EMERGENCY_SERVICES_CAR_THEN_WALK(model2, p);
					}
					catch
					{
					}
				}
				tries++;
				if (flag2)
				{
					spawned++;
				}
				if (spawned < count && tries < count * 2)
				{
					Later(1.2, tag, step);
				}
				else
				{
					uint[] array3 = models;
					foreach (uint m2 in array3)
					{
						ReleaseModel((int)m2);
					}
					if (spawned == 0)
					{
						log("[Acciones] el juego no creo vehiculos de emergencia aca; uso estrellas de busqueda");
						if (tag == "Policia")
						{
							DoWanted();
						}
					}
				}
			}
		};
		Later(0.05, tag, step);
	}

	private void DoWanted()
	{
		int lvl = Math.Max(1, Math.Min(6, PI("Persecucion", "Estrellas", 3)));
		float num = P("Persecucion", "Duracion", 45f);
		int playerIndex = G.PlayerIndex;
		dir.PoliceMode = true;
		N.SET_MAX_WANTED_LEVEL(6u);
		N.SET_POLICE_IGNORE_PLAYER(playerIndex, v: false);
		N.SET_EVERYONE_IGNORE_PLAYER(playerIndex, v: false);
		N.ALTER_WANTED_LEVEL(playerIndex, (uint)lvl);
		N.APPLY_WANTED_LEVEL_CHANGE_NOW(playerIndex);
		wantedUntil = Math.Max(wantedUntil, G.Now + (double)num);
	}

	private void EndWanted()
	{
		int playerIndex = G.PlayerIndex;
		N.CLEAR_WANTED_LEVEL(playerIndex);
		dir.PoliceMode = false;
		if (dir.Active)
		{
			N.SET_MAX_WANTED_LEVEL(6u);
			N.SET_POLICE_IGNORE_PLAYER(playerIndex, v: true);
			N.SET_EVERYONE_IGNORE_PLAYER(playerIndex, v: true);
		}
		wantedUntil = -1.0;
	}

	private void DoFire(Vector3 c)
	{
		int num = PI("Fuego", "Focos", 4);
		float r = P("Fuego", "Radio", 12f);
		float dur = P("Fuego", "Duracion", 25f);
		for (int i = 0; i < num; i++)
		{
			Later((double)i * 0.4, "fuego", delegate
			{
				Vector3 p = G.RandomGroundPoint(c, 0f, r);
				N.ADD_EXPLOSION(p, 1, 2f, sound: true, invisible: false, 0f);
				int key = N.START_SCRIPT_FIRE(p, (uint)PI("Fuego", "Generaciones", 5), (uint)PI("Fuego", "Fuerza", 1));
				fires.Add(new KeyValuePair<int, double>(key, G.Now + (double)dur));
			});
		}
	}

	private void DoCarRain(Vector3 c)
	{
		int n = PI("LluviaDeCoches", "Cantidad", 6);
		float r = P("LluviaDeCoches", "Radio", 15f);
		float hMin = P("LluviaDeCoches", "AlturaMin", 25f);
		float hMax = P("LluviaDeCoches", "AlturaMax", 40f);
		List<int> models = new List<int>();
		string[] array = cfg.PS("LluviaDeCoches", "Modelos", DefaultCarModels).Split(new char[1] { ',' });
		foreach (string text in array)
		{
			string text2 = text.Trim();
			if (text2.Length != 0)
			{
				int num = N.GET_HASH_KEY(text2);
				if (N.IS_MODEL_IN_CDIMAGE(num))
				{
					models.Add(num);
				}
			}
		}
		if (models.Count == 0)
		{
			log("[Acciones] ningun modelo valido para LluviaDeCoches");
			return;
		}
		G.Shuffle(models);
		if (models.Count > n)
		{
			models.RemoveRange(n, models.Count - n);
		}
		foreach (int item in models)
		{
			RequestModel(item);
		}
		double deadline = G.Now + 6.0;
		int made = 0;
		Action step = null;
		step = delegate
		{
			int model = models[made % models.Count];
			if (!N.HAS_MODEL_LOADED(model) && G.Now < deadline)
			{
				Later(0.15, "lluvia", step);
			}
			else
			{
				if (N.HAS_MODEL_LOADED(model))
				{
					double num2 = G.Rng.NextDouble() * Math.PI * 2.0;
					float num3 = G.Rand(0f, r);
					Vector3 p = new Vector3(c.X + (float)Math.Cos(num2) * num3, c.Y + (float)Math.Sin(num2) * num3, c.Z + G.Rand(hMin, hMax));
					N.CREATE_CAR(model, p, out var veh, b: true);
					if (veh != 0 && N.DOES_VEHICLE_EXIST(veh))
					{
						N.SET_CAR_COORDINATES(veh, p);
						N.SET_CAR_HEADING(veh, G.Rand(0f, 360f));
						try
						{
							N.APPLY_FORCE_TO_CAR(veh, 3, 0f, 0f, -2f, G.Rand(-2f, 2f), G.Rand(-2f, 2f), G.Rand(-1f, 1f), 0, 1, 1, 1);
						}
						catch
						{
						}
						spawnedCars.Add(new KeyValuePair<int, double>(veh, G.Now + (double)P("LluviaDeCoches", "Limpiar", 40f)));
					}
				}
				made++;
				if (made >= n)
				{
					foreach (int item2 in models)
					{
						ReleaseModel(item2);
					}
					return;
				}
				Later(G.Rand(0.2f, 0.5f), "lluvia", step);
			}
		};
		Later(0.05, "lluvia", step);
	}

	private void DoRiot(Vector3 c)
	{
		List<int> list = G.PedsNear(c, P("Disturbio", "Radio", 40f), PI("Disturbio", "Personas", 14));
		if (list.Count < 2)
		{
			log("[Acciones] Disturbio: no hay suficientes peatones cerca");
			DoPanic(c, 60f);
			return;
		}
		int[] array = new int[6] { 1, 3, 7, 7, 12, 10 };
		bool flag = cfg.Ini.GetBool("Accion.Disturbio", "Armas", def: true);
		for (int i = 0; i < list.Count; i++)
		{
			int ped = list[i];
			int target = list[(i + 1) % list.Count];
			if (flag)
			{
				N.GIVE_WEAPON_TO_CHAR(ped, array[G.Rng.Next(array.Length)], 300, b: false);
			}
			N.SET_CHAR_KEEP_TASK(ped, v: true);
			N._TASK_COMBAT(ped, target);
		}
	}

	private void DoPanic(Vector3 c, float radius)
	{
		foreach (int item in G.PedsNear(c, radius, 40))
		{
			N._TASK_SMART_FLEE_POINT(item, c.X, c.Y, c.Z, 100f, 15000u);
		}
	}

	private void DoStorm()
	{
		N.FORCE_WEATHER_NOW(7u);
		stormUntil = Math.Max(stormUntil, G.Now + (double)P("Tormenta", "Duracion", 60f));
		dir.WeatherLockUntil = stormUntil;
	}

	private void DoWeather(string action, uint weather, float defDuration)
	{
		N.FORCE_WEATHER_NOW(weather);
		stormUntil = Math.Max(stormUntil, G.Now + (double)P(action, "Duracion", defDuration));
		dir.WeatherLockUntil = stormUntil;
	}

	private void DoCrazyCars(Vector3 c)
	{
		int num = 0;
		int num2 = PI("AutosLocos", "Cantidad", 8);
		float speed = P("AutosLocos", "Velocidad", 35f);
		foreach (int item in G.VehiclesNear(c, P("AutosLocos", "Radio", 60f), num2 * 2))
		{
			if (G.ProtectedCars.Contains(item))
			{
				continue;
			}
			N.GET_DRIVER_OF_CAR(item, out var ped);
			if (ped != 0 && !G.ProtectedPeds.Contains(ped) && N.DOES_CHAR_EXIST(ped))
			{
				N._TASK_CAR_DRIVE_WANDER(ped, item, speed, 2u);
				if (++num >= num2)
				{
					break;
				}
			}
		}
		if (num == 0)
		{
			log("[Acciones] AutosLocos: no hay autos con conductor cerca");
		}
	}

	private void DoFlyingCars(Vector3 c)
	{
		List<int> list = G.VehiclesNear(c, P("AutosVoladores", "Radio", 35f), PI("AutosVoladores", "Cantidad", 6));
		float up = P("AutosVoladores", "Fuerza", 14f);
		for (int i = 0; i < list.Count; i++)
		{
			int v = list[i];
			if (G.ProtectedCars.Contains(v))
			{
				continue;
			}
			Later((double)i * 0.35, "autos voladores", delegate
			{
				if (!N.DOES_VEHICLE_EXIST(v) || N.IS_CAR_DEAD(v))
				{
					return;
				}
				try
				{
					N.APPLY_FORCE_TO_CAR(v, 3, G.Rand(-2f, 2f), G.Rand(-2f, 2f), up, G.Rand(-1f, 1f), G.Rand(-1f, 1f), 0f, 0, 1, 1, 1);
				}
				catch
				{
				}
			});
		}
		if (list.Count == 0)
		{
			DoExplosion(c);
		}
		dir.Shake(0.4f, 1f);
	}

	private void DoSlowMo()
	{
		N.SET_TIME_SCALE(P("CamaraLenta", "Velocidad", 0.35f));
		slowmoUntil = Math.Max(slowmoUntil, G.Now + (double)P("CamaraLenta", "Duracion", 6f));
	}

	private void DoEarthquake(Vector3 c)
	{
		float num = P("Terremoto", "Duracion", 6f);
		dir.Shake(P("Terremoto", "Intensidad", 1.5f), num);
		DoPanic(c, 80f);
		int num2 = PI("Terremoto", "Explosiones", 2);
		for (int i = 0; i < num2; i++)
		{
			Later(G.Rand(0.5f, num), "terremoto", delegate
			{
				N.ADD_EXPLOSION(G.RandomGroundPoint(c, 5f, 30f), PI("Terremoto", "TipoExplosion", 0), 3f, sound: true, invisible: false, 0f);
			});
		}
	}

	public void Update()
	{
		double now = G.Now;
		for (int i = 0; i < jobs.Count; i++)
		{
			Job job = jobs[i];
			if (!(job.At > now))
			{
				jobs.RemoveAt(i--);
				try
				{
					job.Fn();
				}
				catch (Exception ex)
				{
					log("[Acciones] error en " + job.Name + ": " + ex.Message);
				}
			}
		}
		if (wantedUntil > 0.0)
		{
			if (now > wantedUntil)
			{
				EndWanted();
			}
			else if (N.IS_PLAYER_BEING_ARRESTED())
			{
				N.CLEAR_WANTED_LEVEL(G.PlayerIndex);
			}
		}
		if (slowmoUntil > 0.0 && now > slowmoUntil)
		{
			N.SET_TIME_SCALE(1f);
			slowmoUntil = -1.0;
		}
		if (stormUntil > 0.0 && now > stormUntil)
		{
			N.RELEASE_WEATHER();
			stormUntil = -1.0;
			dir.WeatherLockUntil = -1.0;
			dir.ReapplyWeather();
		}
		for (int j = 0; j < fires.Count; j++)
		{
			if (now > fires[j].Value)
			{
				RemoveFire(fires[j].Key);
				fires.RemoveAt(j--);
			}
		}
		for (int k = 0; k < spawnedCars.Count; k++)
		{
			if (!(now > spawnedCars[k].Value))
			{
				continue;
			}
			int key = spawnedCars[k].Key;
			try
			{
				if (N.DOES_VEHICLE_EXIST(key))
				{
					N.MARK_CAR_AS_NO_LONGER_NEEDED(key);
				}
			}
			catch
			{
			}
			spawnedCars.RemoveAt(k--);
		}
	}

	public void RemoveFires()
	{
		foreach (KeyValuePair<int, double> fire in fires)
		{
			RemoveFire(fire.Key);
		}
		fires.Clear();
	}

	private static void RemoveFire(int id)
	{
		try
		{
			if (N.DOES_SCRIPT_FIRE_EXIST(id))
			{
				N.REMOVE_SCRIPT_FIRE(id);
			}
		}
		catch
		{
		}
	}

	public void ForgetState()
	{
		jobs.Clear();
		fires.Clear();
		spawnedCars.Clear();
		requestedModels.Clear();
		wantedUntil = (slowmoUntil = (stormUntil = -1.0));
	}

	public void StopAll()
	{
		jobs.Clear();
		try
		{
			if (wantedUntil > 0.0)
			{
				EndWanted();
			}
			if (slowmoUntil > 0.0)
			{
				N.SET_TIME_SCALE(1f);
				slowmoUntil = -1.0;
			}
			if (stormUntil > 0.0)
			{
				N.RELEASE_WEATHER();
				stormUntil = -1.0;
			}
			foreach (KeyValuePair<int, double> fire in fires)
			{
				RemoveFire(fire.Key);
			}
			fires.Clear();
			foreach (KeyValuePair<int, double> spawnedCar in spawnedCars)
			{
				int key = spawnedCar.Key;
				if (N.DOES_VEHICLE_EXIST(key))
				{
					N.MARK_CAR_AS_NO_LONGER_NEEDED(key);
				}
			}
			spawnedCars.Clear();
			foreach (int key2 in requestedModels.Keys)
			{
				N.MARK_MODEL_AS_NO_LONGER_NEEDED(key2);
			}
			requestedModels.Clear();
			dir.WeatherLockUntil = -1.0;
		}
		catch
		{
		}
	}
}
