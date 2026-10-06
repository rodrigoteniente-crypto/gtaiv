using System;
using System.Collections.Generic;
using System.Numerics;
using static KickChaos.N;

namespace KickChaos
{
    public partial class SubNpcManager
    {
        bool eventChat = true, eventRace = true;
        float chatEvery = 90f, chatSeconds = 12f, raceEvery = 120f, raceSeconds = 150f, raceSpeed = 32f;
        int raceMax = 3;
        float raceChance = 15f, raceCooldown = 600f, raceNearby = 450f;
        double nextChat = -1, nextRace = -1, lastRace = -10000, lastEventScan = -100;

        class Race
        {
            public int Phase, Shot = -1, Count;
            public double PhaseAt, StartedAt, LastCount = -100;
            public Vector3 Start, Finish;
            public readonly List<Member> Racers = new List<Member>();
            public readonly HashSet<Gang> Bands = new HashSet<Gang>();
            public readonly Dictionary<Member, double> StillSince = new Dictionary<Member, double>();
        }
        Race race;
        double lastSummary = -100;

        void ReadAmbientConfig(IniFile ini)
        {
            raceChance = Clamp(ini.GetFloat("Suscriptor", "ProbabilidadCarrera", 15f), 0f, 100f);
            raceCooldown = Clamp(ini.GetFloat("Suscriptor", "CooldownCarrera", 600f), 60f, 7200f);
            raceNearby = Clamp(ini.GetFloat("Suscriptor", "RadioCarreraCamara", 450f), 100f, 1000f);
            ReadConvoyConfig(ini);
            if (!eventRace && race != null) EndRace("eventos desactivados");
        }

        void LogSummary(double now)
        {
            if (!diag || now - lastSummary < 60) return;
            lastSummary = now;
            int members = 0, cops = 0, inCar = 0;
            foreach (var g in gangs)
            {
                if (g.State != 1) continue;
                foreach (var m in g.Members) if (!m.Dead) { members++; if (m.InCar) inCar++; }
                foreach (var c in g.Cops) if (c.Alive) cops++;
            }
            log("[estado] " + gangs.Count + " bandas, " + members + " NPC (" + inCar + " en auto), " + cops + " policias" +
                (race != null ? ", carrera en curso" : "") + (convoy != null ? ", convoy en curso" : ""));
        }

        void StepEvents(double now)
        {
            if (now - lastEventScan < 0.5) return;
            lastEventScan = now;
            LogSummary(now);
            StepConvoys(now);
            if (nextChat < 0) nextChat = now + chatEvery;
            if (nextRace < 0) nextRace = now + raceEvery;
            if (race != null) { StepRace(now); return; }
            if (eventChat && now >= nextChat)
            {
                nextChat = now + chatEvery * G.Rand(0.9f, 1.4f);
                TryChat(now);
            }
            if (eventRace && now >= nextRace)
            {
                nextRace = now + raceEvery;
                if (AmbientRules.Roll(now, lastRace, raceCooldown, raceChance, G.Rng.NextDouble())) TryRace(now, false);
            }
        }

        bool Calm(Gang g, Member m, double now)
        {
            bool busy = m.Dead || m.Ped == 0 || m.Fleeing || m.Eating || m.EventKind != 0 || m.Phase == P_SPECIAL ||
                g.ChaseWith != null || m.FunUntil > now || IsInConvoy(g) ||
                policeOn && (g.PoliceAt > 0 || g.CopsPending > 0);
            if (!AmbientRules.Quiet(now, m.LastHit, m.LastShot, m.Created,
                g.Order >= 0 && now < g.OrderUntil, g.Offer != null, busy)) return false;
            if (g.Wanted || now - g.LastCmd < 45) return false;
            try { if (!DOES_CHAR_EXIST(m.Ped) || IS_CHAR_DEAD(m.Ped) || IS_PED_IN_COMBAT(m.Ped)) return false; } catch { return false; }
            if (g.Fighter && NearestThreat(g, m, 55f).Ped != 0) return false;
            return true;
        }

        void TryChat(double now)
        {
            var candidates = new List<Member>();
            foreach (var g in gangs)
                if (g.State == 1)
                    foreach (var m in g.Members)
                        if (!m.InCar && Calm(g, m, now)) candidates.Add(m);
            G.Shuffle(candidates);
            foreach (var m in candidates)
            {
                foreach (int p in G.PedsNear(m.Pos, 14f, 8))
                {
                    Gang other;
                    if (p == m.Ped || FindMember(p, out other) != null) continue;
                    try
                    {
                        if (IsCop(p) || !DOES_CHAR_EXIST(p) || IS_CHAR_DEAD(p) || IS_PED_IN_COMBAT(p) || IS_CHAR_SHOOTING(p) || IS_CHAR_IN_ANY_CAR(p) ||
                            Math.Abs(G.CharPos(p).Z - m.Pos.Z) > 2f) continue;
                    }
                    catch { continue; }
                    m.EventKind = 1;
                    m.EventPed = p;
                    m.EventAt = now;
                    m.EventUntil = now + 10 + chatSeconds;
                    m.EventStarted = false;
                    log("[evento] " + Name(m.G, m) + " se acerca a charlar");
                    return;
                }
            }
        }

        bool ThinkEvent(Gang g, Member m, double now)
        {
            if (m.EventKind == 1) return ThinkChat(g, m, now);
            if (m.EventKind == 2) return ThinkRacer(g, m, now);
            m.EventKind = 0;
            return false;
        }

        void EndChat(Member m, string why)
        {
            int civilian = m.EventPed;
            m.EventKind = 0;
            m.EventPed = 0;
            m.EventUntil = -1;
            if (m.Mode == M_CHAT || m.Mode == M_GOTO) m.Mode = M_NONE;
            // Only release the civilian if our chat actually installed a task on them.
            if (m.EventStarted)
                try
                {
                    if (civilian != 0 && DOES_CHAR_EXIST(civilian) && !IS_CHAR_DEAD(civilian))
                    {
                        SET_CHAR_KEEP_TASK(civilian, false);
                        if (!IS_PED_IN_COMBAT(civilian) && !IS_CHAR_SHOOTING(civilian)) CLEAR_CHAR_TASKS(civilian);
                    }
                }
                catch { }
            m.EventStarted = false;
            if (why != null && diag) log("[evento] " + Name(m.G, m) + " deja la charla: " + why);
        }

        bool ThinkChat(Gang g, Member m, double now)
        {
            bool valid = false;
            Vector3 position = Vector3.Zero;
            try { valid = m.EventPed != 0 && DOES_CHAR_EXIST(m.EventPed) && !IS_CHAR_DEAD(m.EventPed); if (valid) position = G.CharPos(m.EventPed); } catch { }
            if (!valid || m.InCar || now - m.LastHit < 3 || g.Order >= 0 && now < g.OrderUntil)
            { EndChat(m, "otra prioridad"); return false; }
            if (g.Fighter && NearestThreat(g, m, 35f).Ped != 0) { EndChat(m, "peligro cerca"); return false; }
            if (now > m.EventUntil) { EndChat(m, null); return false; }
            float distance = Vector3.Distance(m.Pos, position);
            if (!m.EventStarted)
            {
                if (distance > 25f || now - m.EventAt > 10) { EndChat(m, "no llego"); return false; }
                if (distance > 2.2f)
                {
                    if (m.Mode != M_GOTO || now - m.LastTask > 6 && Vector3.Distance(position, m.MoveTo) > 4)
                    {
                        _TASK_FOLLOW_NAV_MESH_TO_COORD(m.Ped, position, 2);
                        m.MoveTo = position;
                        Task(m, M_GOTO, now);
                    }
                    Say(m, "se acerca a charlar", true);
                    return true;
                }
                m.EventStarted = true;
                m.EventUntil = now + chatSeconds;
                try
                {
                    SET_CHAR_KEEP_TASK(m.EventPed, true);
                    _TASK_TURN_CHAR_TO_FACE_CHAR(m.EventPed, m.Ped);
                    _TASK_CHAT_WITH_CHAR(m.Ped, m.EventPed, true, true);
                    _TASK_CHAT_WITH_CHAR(m.EventPed, m.Ped, false, true);
                }
                catch { EndChat(m, "no pudo empezar"); return false; }
                Task(m, M_CHAT, now);
                m.Flash = "CHARLANDO";
                m.FlashUntil = now + 3;
            }
            else if (distance > 5f) { EndChat(m, "la otra persona se fue"); return false; }
            Say(m, "charla con alguien", true);
            return true;
        }

        public bool StartRaceNow() { return TryRace(G.Now, true); }

        bool TryRace(double now, bool forced)
        {
            if (race != null || convoy != null) return false;
            bool city = dir.Active && dir.Current != null && !dir.Current.IsFollow && !dir.Current.IsBattle;
            if (!city && !forced) return false;
            Vector3 start = city ? dir.CurrentTarget : Vector3.Zero;
            var candidates = new List<Member>();
            foreach (var g in gangs)
            {
                if (g.State != 1) continue;
                foreach (var m in g.Members)
                {
                    if (!m.InCar || !m.Driving || !CarOk(m.Car) || !Calm(g, m, now)) continue;
                    if (city && (FlatDist(m.Pos, start) > raceNearby || Math.Abs(m.Pos.Z - start.Z) > 20)) continue;
                    candidates.Add(m);
                    break; // one car per band; no shared seats or duplicate slots
                }
            }
            if (candidates.Count < 2)
            {
                if (forced) notify("Carrera: hacen falta 2 conductores tranquilos cerca de la camara");
                return false;
            }
            G.Shuffle(candidates);
            if (!city) start = candidates[0].Pos;
            Vector3 road; float heading;
            if (GET_NTH_CLOSEST_CAR_NODE_WITH_HEADING(start, 1, out road, out heading) && road != Vector3.Zero) start = road;
            else return false;
            var destinations = new List<Vector3>();
            foreach (var shot in dir.Shots)
            {
                if (!shot.Active || shot.IsAuto) continue;
                Vector3 target = shot.HasTarget ? shot.Target : G.FindTarget(shot.Pos, shot.Rot, 120f);
                float distance = FlatDist(start, target);
                if (distance < 350 || distance > 900) continue;
                if (GET_NTH_CLOSEST_CAR_NODE_WITH_HEADING(target, 1, out road, out heading) && road != Vector3.Zero) destinations.Add(road);
            }
            if (destinations.Count == 0)
            {
                double angle = G.Rng.NextDouble() * Math.PI * 2;
                Vector3 guess = start + new Vector3((float)Math.Cos(angle) * 550, (float)Math.Sin(angle) * 550, 0);
                if (!GET_NTH_CLOSEST_CAR_NODE_WITH_HEADING(guess, 1, out road, out heading) || road == Vector3.Zero) return false;
                destinations.Add(road);
            }
            race = new Race { Phase = 0, PhaseAt = now, Shot = city ? dir.CurrentIndex : -1, Start = start, Finish = destinations[G.Rng.Next(destinations.Count)] };
            foreach (var m in candidates)
            {
                if (race.Racers.Count >= raceMax) break;
                race.Racers.Add(m);
                race.Bands.Add(m.G);
                m.EventKind = 2;
                m.EventAt = now;
                m.EventStarted = false;
                m.Target = 0;
                m.G.Truce = true;
            }
            lastRace = now;
            SetTruce(true);
            StreamRuntime.AddFeed("carrera", "", "Carrera cerca de la camara: " + race.Racers.Count + " participantes", now);
            log("[evento] carrera: " + race.Racers.Count + " conductores van a la largada (sin teletransporte)");
            return true;
        }

        void SetTruce(bool on)
        {
            if (race == null) return;
            foreach (var g in race.Bands)
                foreach (var other in race.Bands)
                {
                    if (g == other) continue;
                    RestoreBandRelations(g, other, on);
                }
        }

        void RestoreBandRelations(Gang a, Gang b, bool forceFriendly = false)
        {
            uint relation = forceFriendly ? 3u : RivalRelationship(a, b);
            foreach (var m in a.Members)
                if (!m.Dead && m.Ped != 0)
                    try { if (DOES_CHAR_EXIST(m.Ped)) SET_CHAR_RELATIONSHIP(m.Ped, relation, b.Group); } catch { }
        }

        void EndRace(string why)
        {
            if (race == null) return;
            Race previous = race;
            foreach (var m in previous.Racers) ReleaseRacer(m);
            foreach (var g in previous.Bands) g.Truce = false;
            SetTruce(false);
            log("[evento] carrera terminada: " + why);
            race = null;
        }

        static void ReleaseRacer(Member m)
        {
            m.EventKind = 0;
            m.EventStarted = false;
            if (m.Mode == M_RACE || m.Mode == M_WAIT) m.Mode = M_NONE;
        }

        void StepRace(double now)
        {
            Race r = race;
            for (int i = r.Racers.Count - 1; i >= 0; i--)
            {
                Member m = r.Racers[i];
                bool valid = !m.Dead && m.Ped != 0 && gangs.Contains(m.G) && m.EventKind == 2 && m.InCar && m.Driving && CarOk(m.Car);
                try { valid = valid && DOES_CHAR_EXIST(m.Ped) && !IS_CHAR_DEAD(m.Ped); } catch { valid = false; }
                if (valid && !(m.G.Order >= 0 && now < m.G.OrderUntil) && now - m.LastHit >= 2) continue;
                ReleaseRacer(m);
                r.Racers.RemoveAt(i);
            }
            // A retired/dead participant's band must immediately lose its temporary protection.
            foreach (var g in new List<Gang>(r.Bands))
            {
                bool active = r.Racers.Exists(m => m.G == g);
                if (active) continue;
                g.Truce = false;
                foreach (var other in r.Bands) { if (other != g) { RestoreBandRelations(g, other); RestoreBandRelations(other, g); } }
                r.Bands.Remove(g);
            }
            if (r.Racers.Count < 2) { EndRace("quedaron menos de 2 conductores"); return; }
            if (r.Phase == 0)
            {
                bool all = r.Racers.TrueForAll(m => FlatDist(m.Pos, r.Start) < 55);
                if (!all && now - r.PhaseAt < 45) return;
                for (int i = r.Racers.Count - 1; i >= 0; i--)
                    if (FlatDist(r.Racers[i].Pos, r.Start) > 90) { ReleaseRacer(r.Racers[i]); r.Racers.RemoveAt(i); }
                if (r.Racers.Count < 2) { EndRace("no llegaron a la largada"); return; }
                r.Phase = 1; r.PhaseAt = now; r.Count = 3;
                foreach (var m in r.Racers) m.EventStarted = false;
                return;
            }
            if (r.Phase == 1)
            {
                if (now - r.LastCount < 1) return;
                r.LastCount = now;
                foreach (var m in r.Racers) { m.Flash = "CARRERA " + (r.Count > 0 ? r.Count.ToString() : "YA!"); m.FlashUntil = now + 1.1; }
                if (r.Count-- > 0) return;
                r.Phase = 2; r.StartedAt = now;
                foreach (var m in r.Racers) { m.EventStarted = false; r.StillSince[m] = -1; }
                return;
            }
            foreach (var m in r.Racers)
            {
                if (FlatDist(m.Pos, r.Finish) > 28 || Math.Abs(m.Pos.Z - r.Finish.Z) > 10) continue;
                string winner = Name(m.G, m);
                m.Flash = "GANO LA CARRERA"; m.FlashUntil = now + 5;
                Feed(winner, 3, "GANO LA CARRERA", 0, now);
                StreamRuntime.AddFeed("carrera", winner, "Gano la carrera en " + (now - r.StartedAt).ToString("0") + " s", now);
                if (perksOn && m.G.Fighter && !m.G.IsFollower) QueueOffer(m.G, now);
                EndRace("gano " + winner);
                return;
            }
            if (now - r.StartedAt >= raceSeconds) EndRace("termino el tiempo");
        }

        bool ThinkRacer(Gang g, Member m, double now)
        {
            Race r = race;
            if (r == null || !r.Racers.Contains(m)) { ReleaseRacer(m); return false; }
            if (!m.InCar || !m.Driving || !CarOk(m.Car) || now - m.LastHit < 2 || g.Order >= 0 && now < g.OrderUntil)
            { ReleaseRacer(m); return false; }
            if (r.Phase == 1)
            {
                if (!m.EventStarted) { _TASK_PAUSE(m.Ped, 6000); Task(m, M_RACE, now); m.EventStarted = true; }
                Say(m, "espera la largada", true);
                return true;
            }
            float speed = 0; try { GET_CAR_SPEED(m.Car, out speed); } catch { }
            double still; if (!r.StillSince.TryGetValue(m, out still)) still = -1;
            if (speed > 1.5f) still = -1; else if (still < 0) still = now;
            r.StillSince[m] = still;
            Vector3 destination = r.Phase == 0 ? r.Start : r.Finish;
            if (AmbientRules.RefreshDrive(!m.EventStarted, now, m.LastTask, still, 0))
            {
                _TASK_CAR_MISSION_COORS_TARGET(m.Ped, m.Car, destination, 4, r.Phase == 0 ? 20f : raceSpeed, AiTaskPolicy.RoadDrivingStyle, 5, 10);
                Task(m, M_RACE, now);
                m.EventStarted = true;
                m.Target = 0;
            }
            if (still >= 0 && now - still > 35 && FlatDist(m.Pos, destination) > 55) { ReleaseRacer(m); return false; }
            Say(m, r.Phase == 0 ? "va a la largada" : "corre una carrera", true);
            return true;
        }

        public List<StreamMarker> StreamMarkers()
        {
            var markers = new List<StreamMarker>();
            if (race != null)
            {
                markers.Add(new StreamMarker { Kind = "carrera", Name = "Largada", X = race.Start.X, Y = race.Start.Y, Z = race.Start.Z });
                markers.Add(new StreamMarker { Kind = "carrera", Name = "Meta", X = race.Finish.X, Y = race.Finish.Y, Z = race.Finish.Z });
            }
            if (convoy != null)
            {
                markers.Add(new StreamMarker { Kind = "convoy", Name = "Convoy: " + Name(convoy.Leader.G, convoy.Leader), X = convoy.Leader.Pos.X, Y = convoy.Leader.Pos.Y, Z = convoy.Leader.Pos.Z });
            }
            return markers;
        }
    }
}
