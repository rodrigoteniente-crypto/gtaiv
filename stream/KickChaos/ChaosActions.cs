using System;
using System.Collections.Generic;
using System.Numerics;
using static KickChaos.N;

namespace KickChaos
{
    /// <summary>
    /// Las cosas que pasan en el juego cuando el chat las pide. Todas ocurren alrededor del
    /// punto que esta mirando la camara del director.
    /// </summary>
    public class ChaosActions
    {
        Config cfg;
        readonly Director dir;
        readonly Action<string> log;

        class Job { public double At; public Action Fn; public string Name; }
        readonly List<Job> jobs = new List<Job>();
        readonly List<KeyValuePair<int, double>> fires = new List<KeyValuePair<int, double>>();
        readonly List<KeyValuePair<int, double>> spawnedCars = new List<KeyValuePair<int, double>>();
        readonly Dictionary<int, int> requestedModels = new Dictionary<int, int>(); // modelo -> cuantas acciones lo usan
        double wantedUntil = -1, slowmoUntil = -1, stormUntil = -1;

        public ChaosActions(Config cfg, Director dir, Action<string> log)
        {
            this.cfg = cfg;
            this.dir = dir;
            this.log = log;
        }

        public void SetConfig(Config c) { cfg = c; }

        float P(string a, string k, float d) { return cfg.P(a, k, d); }
        int PI(string a, string k, int d) { return cfg.PI(a, k, d); }

        void Later(double seconds, string name, Action fn)
        {
            jobs.Add(new Job { At = G.Now + seconds, Fn = fn, Name = name });
        }

        // ------------------------------------------------------------------
        static readonly Dictionary<string, float> DefaultHold = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
        {
            { "Policia", 30f }, { "Bomberos", 30f }, { "LluviaDeCoches", 15f }, { "Fuego", 20f }, { "Disturbio", 25f },
            { "Terremoto", 10f }, { "Bombardeo", 10f }, { "ExplotarCoches", 10f }, { "Persecucion", 15f }, { "Panico", 10f },
            { "AutosLocos", 15f }, { "AutosVoladores", 10f },
            { "SiguienteCamara", 0f }
        };

        public SubNpcManager Npcs;
        /// <summary>Texto para OBS de la ultima accion (si no es el generico).</summary>
        public string LastLabel;

        public bool Execute(QueuedAction qa)
        {
            LastLabel = null;
            if (qa.Name.StartsWith("Npc", StringComparison.Ordinal))
            {
                // aparece el suscriptor con su nombre (no frena la camara: la camara se va con el).
                // "Npc" = segun los meses; "NpcTiroteo", "NpcRobar"... = siempre eso.
                if (Npcs == null) return false;
                string label;
                bool ok = Npcs.Spawn(qa, dir.ActionCenter(), NpcRules.FromAction(qa.Name), out label);
                LastLabel = label;
                return ok;
            }
            return Execute(qa.Name);
        }

        public bool Execute(string canonical)
        {
            Vector3 c = dir.ActionCenter();
            float defHold;
            if (!DefaultHold.TryGetValue(canonical, out defHold)) defHold = 8f;
            if (canonical != "SiguienteCamara") dir.HoldFor(P(canonical, "Mantener", defHold));
            switch (canonical)
            {
                case "Explosion": case "Bombardeo": case "ExplotarCoches": case "Policia": case "Persecucion":
                case "Bomberos": case "Fuego": case "LluviaDeCoches": case "Disturbio": case "Terremoto": case "Panico":
                case "AutosLocos": case "AutosVoladores":
                    dir.Repair.MarkDirty(c);
                    break;
            }
            switch (canonical)
            {
                case "Explosion": DoExplosion(c); return true;
                case "Bombardeo": DoAirStrike(c); return true;
                case "ExplotarCoches": DoCarBombs(c); return true;
                case "Policia": DoPolice(c); return true;
                case "Persecucion": DoWanted(); return true;
                case "Bomberos": DoEmergency(c); return true;
                case "Fuego": DoFire(c); return true;
                case "LluviaDeCoches": DoCarRain(c); return true;
                case "Disturbio": DoRiot(c); return true;
                case "Panico": DoPanic(c, 60f); return true;
                case "Tormenta": DoStorm(); return true;
                case "Noche": SetTime("Noche", 0, 0); return true;
                case "Dia": SetTime("Dia", 13, 0); return true;
                case "Atardecer": SetTime("Atardecer", 19, 30); return true;
                case "CamaraLenta": DoSlowMo(); return true;
                case "Terremoto": DoEarthquake(c); return true;
                case "AutosLocos": DoCrazyCars(c); return true;
                case "AutosVoladores": DoFlyingCars(c); return true;
                case "Lluvia": DoWeather("Lluvia", 4, 90f); return true;
                case "Niebla": DoWeather("Niebla", 6, 90f); return true;
                case "SiguienteCamara": dir.Next(); return true;
            }
            return false;
        }

        void SetTime(string action, int defHour, int defMinute)
        {
            SET_TIME_OF_DAY((uint)PI(action, "Hora", defHour), (uint)PI(action, "Minuto", defMinute));
            dir.TimeLockUntil = G.Now + P(action, "Duracion", 180f); // durante este tiempo HoraFija no la pisa
        }

        void RequestModel(int m)
        {
            REQUEST_MODEL(m);
            int n;
            requestedModels.TryGetValue(m, out n);
            requestedModels[m] = n + 1;
        }

        void ReleaseModel(int m)
        {
            int n;
            if (!requestedModels.TryGetValue(m, out n)) return;
            if (n <= 1) { requestedModels.Remove(m); MARK_MODEL_AS_NO_LONGER_NEEDED(m); }
            else requestedModels[m] = n - 1;
        }

        void DoExplosion(Vector3 c)
        {
            float r = P("Explosion", "Radio", 20f);
            var cars = G.VehiclesNear(c, r, 1);
            if (cars.Count > 0 && G.Rng.NextDouble() < P("Explosion", "ProbabilidadCoche", 0.7f))
                EXPLODE_CAR(cars[0], true, false);
            else
            {
                Vector3 p = G.RandomGroundPoint(c, 0f, r * 0.6f);
                ADD_EXPLOSION(p, PI("Explosion", "Tipo", 4), P("Explosion", "Tamano", 6f), true, false, 0.3f);
            }
            dir.Shake(P("Explosion", "Sacudida", 0.6f), 0.9f);
        }

        void DoAirStrike(Vector3 c)
        {
            int n = PI("Bombardeo", "Cantidad", 8);
            float r = P("Bombardeo", "Radio", 30f);
            float gap = P("Bombardeo", "Intervalo", 0.35f);
            int[] types = { 2, 3, 4, 5 };
            for (int i = 0; i < n; i++)
            {
                Later(i * gap, "bombardeo", () =>
                {
                    Vector3 p = G.RandomGroundPoint(c, 0f, r);
                    ADD_EXPLOSION(p, types[G.Rng.Next(types.Length)], G.Rand(4f, 8f), true, false, 0.2f);
                    dir.Shake(P("Bombardeo", "Sacudida", 0.8f), 0.7f);
                });
            }
        }

        void DoCarBombs(Vector3 c)
        {
            var cars = G.VehiclesNear(c, P("ExplotarCoches", "Radio", 40f), PI("ExplotarCoches", "Cantidad", 6));
            if (cars.Count == 0) { DoExplosion(c); return; }
            for (int i = 0; i < cars.Count; i++)
            {
                int v = cars[i];
                Later(i * P("ExplotarCoches", "Intervalo", 0.45f), "coches", () =>
                {
                    if (DOES_VEHICLE_EXIST(v) && !IS_CAR_DEAD(v)) EXPLODE_CAR(v, true, false);
                    dir.Shake(0.7f, 0.8f);
                });
            }
        }

        /// <summary>Patrulleros que llegan con sirena al lugar y los policias se bajan.</summary>
        void DoPolice(Vector3 c)
        {
            uint model;
            GET_CURRENT_BASIC_POLICE_CAR_MODEL(out model);
            if (model == 0) model = (uint)GET_HASH_KEY("POLICE");
            int n = PI("Policia", "Patrulleros", 3);
            SpawnEmergency(new[] { model }, n, c, "Policia");
            if (cfg.Ini.GetBool("Accion.Policia", "TambienEstrellas", false)) DoWanted();
        }

        void DoEmergency(Vector3 c)
        {
            uint fire = (uint)GET_HASH_KEY("FIRETRUK");
            uint amb = (uint)GET_HASH_KEY("AMBULANCE");
            SpawnEmergency(new[] { fire, amb }, 2, c, "Bomberos");
        }

        void SpawnEmergency(uint[] models, int count, Vector3 c, string tag)
        {
            foreach (uint m in models) RequestModel((int)m);
            double deadline = G.Now + 6.0;
            int spawned = 0, tries = 0;
            Action step = null;
            step = () =>
            {
                bool loaded = true;
                foreach (uint m in models) if (!HAS_MODEL_LOADED((int)m)) loaded = false;
                if (!loaded && G.Now < deadline) { Later(0.2, tag, step); return; }
                uint model = models[spawned % models.Length];
                Vector3 p = G.RandomGroundPoint(c, 2f, 10f);
                bool ok = false;
                if (HAS_MODEL_LOADED((int)model))
                    try { ok = CREATE_EMERGENCY_SERVICES_CAR_THEN_WALK(model, p); } catch { }
                tries++;
                if (ok) spawned++;
                if (spawned < count && tries < count * 2) { Later(1.2, tag, step); return; }
                foreach (uint m in models) ReleaseModel((int)m);
                if (spawned == 0)
                {
                    log("[Acciones] el juego no creo vehiculos de emergencia aca; uso estrellas de busqueda");
                    if (tag == "Policia") DoWanted();
                }
            };
            Later(0.05, tag, step);
        }

        /// <summary>Nivel de busqueda: la policia (y a 3+ estrellas los helicopteros) cae sobre la zona.</summary>
        void DoWanted()
        {
            int stars = Math.Max(1, Math.Min(6, PI("Persecucion", "Estrellas", 3)));
            float dur = P("Persecucion", "Duracion", 45f);
            int pl = G.PlayerIndex;
            dir.PoliceMode = true;
            SET_MAX_WANTED_LEVEL(6);
            SET_POLICE_IGNORE_PLAYER(pl, false);
            SET_EVERYONE_IGNORE_PLAYER(pl, false);
            ALTER_WANTED_LEVEL(pl, (uint)stars);
            APPLY_WANTED_LEVEL_CHANGE_NOW(pl);
            wantedUntil = Math.Max(wantedUntil, G.Now + dur);
        }

        void EndWanted()
        {
            int pl = G.PlayerIndex;
            CLEAR_WANTED_LEVEL(pl);
            dir.PoliceMode = false;
            if (dir.Active) { SET_MAX_WANTED_LEVEL(6); SET_POLICE_IGNORE_PLAYER(pl, true); SET_EVERYONE_IGNORE_PLAYER(pl, true); }
            wantedUntil = -1;
        }

        void DoFire(Vector3 c)
        {
            int n = PI("Fuego", "Focos", 4);
            float r = P("Fuego", "Radio", 12f);
            float dur = P("Fuego", "Duracion", 25f);
            for (int i = 0; i < n; i++)
            {
                Later(i * 0.4, "fuego", () =>
                {
                    Vector3 p = G.RandomGroundPoint(c, 0f, r);
                    ADD_EXPLOSION(p, 1, 2f, true, false, 0f); // molotov: prende fuego
                    int f = START_SCRIPT_FIRE(p, (uint)PI("Fuego", "Generaciones", 5), (uint)PI("Fuego", "Fuerza", 1));
                    fires.Add(new KeyValuePair<int, double>(f, G.Now + dur));
                });
            }
        }

        static readonly string DefaultCarModels = "ADMIRAL,BANSHEE,CABBY,INFERNUS,PMP600,SULTAN,TAXI,STOCKADE,BOBCAT,PATRIOT,COMET,MRTASTY,FLATBED,BUS";

        void DoCarRain(Vector3 c)
        {
            int n = PI("LluviaDeCoches", "Cantidad", 6);
            float r = P("LluviaDeCoches", "Radio", 15f);
            float hMin = P("LluviaDeCoches", "AlturaMin", 25f), hMax = P("LluviaDeCoches", "AlturaMax", 40f);
            var models = new List<int>();
            foreach (string name in cfg.PS("LluviaDeCoches", "Modelos", DefaultCarModels).Split(','))
            {
                string nm = name.Trim();
                if (nm.Length == 0) continue;
                int h = GET_HASH_KEY(nm);
                if (IS_MODEL_IN_CDIMAGE(h)) models.Add(h);
            }
            if (models.Count == 0) { log("[Acciones] ningun modelo valido para LluviaDeCoches"); return; }
            G.Shuffle(models);
            if (models.Count > n) models.RemoveRange(n, models.Count - n);
            foreach (int m in models) RequestModel(m);

            double deadline = G.Now + 6.0;
            int made = 0;
            Action step = null;
            step = () =>
            {
                int m = models[made % models.Count];
                if (!HAS_MODEL_LOADED(m) && G.Now < deadline) { Later(0.15, "lluvia", step); return; }
                if (HAS_MODEL_LOADED(m))
                {
                    double a = G.Rng.NextDouble() * Math.PI * 2;
                    float rr = G.Rand(0f, r);
                    var p = new Vector3(c.X + (float)Math.Cos(a) * rr, c.Y + (float)Math.Sin(a) * rr, c.Z + G.Rand(hMin, hMax));
                    int veh;
                    CREATE_CAR(m, p, out veh, true);
                    if (veh != 0 && DOES_VEHICLE_EXIST(veh))
                    {
                        SET_CAR_COORDINATES(veh, p);
                        SET_CAR_HEADING(veh, G.Rand(0f, 360f));
                        try { APPLY_FORCE_TO_CAR(veh, 3, 0f, 0f, -2f, G.Rand(-2f, 2f), G.Rand(-2f, 2f), G.Rand(-1f, 1f), 0, 1, 1, 1); } catch { }
                        spawnedCars.Add(new KeyValuePair<int, double>(veh, G.Now + P("LluviaDeCoches", "Limpiar", 40f)));
                    }
                }
                made++;
                if (made < n) { Later(G.Rand(0.2f, 0.5f), "lluvia", step); return; }
                foreach (int mm in models) ReleaseModel(mm);
            };
            Later(0.05, "lluvia", step);
        }

        void DoRiot(Vector3 c)
        {
            var peds = G.PedsNear(c, P("Disturbio", "Radio", 40f), PI("Disturbio", "Personas", 14));
            if (peds.Count < 2) { log("[Acciones] Disturbio: no hay suficientes peatones cerca"); DoPanic(c, 60f); return; }
            int[] weapons = { 1, 3, 7, 7, 12, 10 }; // bate, cuchillo, pistola, uzi, escopeta
            bool armed = cfg.Ini.GetBool("Accion.Disturbio", "Armas", true);
            for (int i = 0; i < peds.Count; i++)
            {
                int ped = peds[i], target = peds[(i + 1) % peds.Count];
                if (armed) GIVE_WEAPON_TO_CHAR(ped, weapons[G.Rng.Next(weapons.Length)], 300, false);
                SET_CHAR_KEEP_TASK(ped, true);
                _TASK_COMBAT(ped, target);
            }
        }

        void DoPanic(Vector3 c, float radius)
        {
            foreach (int ped in G.PedsNear(c, radius, 40))
                _TASK_SMART_FLEE_POINT(ped, c.X, c.Y, c.Z, 100f, 15000);
        }

        void DoStorm()
        {
            FORCE_WEATHER_NOW(7);
            stormUntil = Math.Max(stormUntil, G.Now + P("Tormenta", "Duracion", 60f));
            dir.WeatherLockUntil = stormUntil;
        }

        void DoWeather(string action, uint weather, float defDuration)
        {
            FORCE_WEATHER_NOW(weather);
            stormUntil = Math.Max(stormUntil, G.Now + P(action, "Duracion", defDuration));
            dir.WeatherLockUntil = stormUntil;
        }

        /// <summary>Los que manejan cerca salen a toda velocidad sin respetar nada.</summary>
        void DoCrazyCars(Vector3 c)
        {
            int n = 0, max = PI("AutosLocos", "Cantidad", 8);
            float speed = P("AutosLocos", "Velocidad", 35f);
            foreach (int car in G.VehiclesNear(c, P("AutosLocos", "Radio", 60f), max * 2))
            {
                if (G.ProtectedCars.Contains(car)) continue;
                int driver;
                GET_DRIVER_OF_CAR(car, out driver);
                if (driver == 0 || G.ProtectedPeds.Contains(driver) || !DOES_CHAR_EXIST(driver)) continue;
                _TASK_CAR_DRIVE_WANDER(driver, car, speed, 2);
                if (++n >= max) break;
            }
            if (n == 0) log("[Acciones] AutosLocos: no hay autos con conductor cerca");
        }

        /// <summary>Los autos de alrededor salen volando por el aire, uno atras de otro.</summary>
        void DoFlyingCars(Vector3 c)
        {
            var cars = G.VehiclesNear(c, P("AutosVoladores", "Radio", 35f), PI("AutosVoladores", "Cantidad", 6));
            float up = P("AutosVoladores", "Fuerza", 14f);
            for (int i = 0; i < cars.Count; i++)
            {
                int v = cars[i];
                if (G.ProtectedCars.Contains(v)) continue;
                Later(i * 0.35, "autos voladores", () =>
                {
                    if (!DOES_VEHICLE_EXIST(v) || IS_CAR_DEAD(v)) return;
                    try { APPLY_FORCE_TO_CAR(v, 3, G.Rand(-2f, 2f), G.Rand(-2f, 2f), up, G.Rand(-1f, 1f), G.Rand(-1f, 1f), 0f, 0, 1, 1, 1); } catch { }
                });
            }
            if (cars.Count == 0) DoExplosion(c);
            dir.Shake(0.4f, 1f);
        }

        void DoSlowMo()
        {
            SET_TIME_SCALE(P("CamaraLenta", "Velocidad", 0.35f));
            slowmoUntil = Math.Max(slowmoUntil, G.Now + P("CamaraLenta", "Duracion", 6f));
        }

        void DoEarthquake(Vector3 c)
        {
            float dur = P("Terremoto", "Duracion", 6f);
            dir.Shake(P("Terremoto", "Intensidad", 1.5f), dur);
            DoPanic(c, 80f);
            int booms = PI("Terremoto", "Explosiones", 2);
            for (int i = 0; i < booms; i++)
                Later(G.Rand(0.5f, dur), "terremoto", () => ADD_EXPLOSION(G.RandomGroundPoint(c, 5f, 30f), PI("Terremoto", "TipoExplosion", 0), 3f, true, false, 0f));
        }

        // ------------------------------------------------------------------
        public void Update()
        {
            double now = G.Now;
            for (int i = 0; i < jobs.Count; i++)
            {
                Job j = jobs[i];
                if (j.At > now) continue;
                jobs.RemoveAt(i--);
                try { j.Fn(); }
                catch (Exception ex) { log("[Acciones] error en " + j.Name + ": " + ex.Message); }
            }

            if (wantedUntil > 0)
            {
                if (now > wantedUntil) EndWanted();
                else if (IS_PLAYER_BEING_ARRESTED()) CLEAR_WANTED_LEVEL(G.PlayerIndex);
            }
            if (slowmoUntil > 0 && now > slowmoUntil) { SET_TIME_SCALE(1f); slowmoUntil = -1; }
            if (stormUntil > 0 && now > stormUntil) { RELEASE_WEATHER(); stormUntil = -1; dir.WeatherLockUntil = -1; dir.ReapplyWeather(); }

            for (int i = 0; i < fires.Count; i++)
                if (now > fires[i].Value) { RemoveFire(fires[i].Key); fires.RemoveAt(i--); }

            for (int i = 0; i < spawnedCars.Count; i++)
                if (now > spawnedCars[i].Value)
                {
                    int v = spawnedCars[i].Key;
                    try { if (DOES_VEHICLE_EXIST(v)) MARK_CAR_AS_NO_LONGER_NEEDED(v); } catch { }
                    spawnedCars.RemoveAt(i--);
                }
        }

        public int PendingJobs { get { return jobs.Count; } }

        /// <summary>Apaga los incendios que prendio el mod (al arreglar la calle).</summary>
        public void RemoveFires()
        {
            foreach (var f in fires) RemoveFire(f.Key);
            fires.Clear();
        }

        static void RemoveFire(int id)
        {
            try { if (DOES_SCRIPT_FIRE_EXIST(id)) REMOVE_SCRIPT_FIRE(id); } catch { }
        }

        /// <summary>Olvidar todo sin llamar al juego (despues de cargar una partida).</summary>
        public void ForgetState()
        {
            jobs.Clear();
            fires.Clear();
            spawnedCars.Clear();
            requestedModels.Clear();
            wantedUntil = slowmoUntil = stormUntil = -1;
        }

        /// <summary>Deshace todo lo temporal (al apagar o recargar).</summary>
        public void StopAll()
        {
            jobs.Clear();
            try
            {
                if (wantedUntil > 0) EndWanted();
                if (slowmoUntil > 0) { SET_TIME_SCALE(1f); slowmoUntil = -1; }
                if (stormUntil > 0) { RELEASE_WEATHER(); stormUntil = -1; }
                foreach (var f in fires) RemoveFire(f.Key);
                fires.Clear();
                foreach (var kv in spawnedCars) { int v = kv.Key; if (DOES_VEHICLE_EXIST(v)) MARK_CAR_AS_NO_LONGER_NEEDED(v); }
                spawnedCars.Clear();
                foreach (int m in requestedModels.Keys) MARK_MODEL_AS_NO_LONGER_NEEDED(m);
                requestedModels.Clear();
                dir.WeatherLockUntil = -1;
            }
            catch { }
        }
    }
}
