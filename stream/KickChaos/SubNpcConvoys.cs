using System;
using System.Collections.Generic;
using System.Numerics;
using static KickChaos.N;

namespace KickChaos
{
    public partial class SubNpcManager
    {
        sealed class Convoy
        {
            public Member Leader, Follower;
            public double Until;
            public double StillSince = -1;
            public double LeaderStillSince = -1;
        }
        Convoy convoy;
        bool eventConvoys = true;
        float convoyEvery = 120f, convoyChance = 12f, convoyCooldown = 600f, convoySeconds = 150f, convoyRadius = 85f;
        double nextConvoy = -1, lastConvoy = -10000;

        void ReadConvoyConfig(IniFile ini)
        {
            const string section = "Suscriptor";
            eventConvoys = ini.GetBool(section, "EventoConvoy", true);
            convoyEvery = Clamp(ini.GetFloat(section, "ConvoyCada", 120f), 30f, 1800f);
            convoyChance = Clamp(ini.GetFloat(section, "ProbabilidadConvoy", 12f), 0f, 100f);
            convoyCooldown = Clamp(ini.GetFloat(section, "CooldownConvoy", 600f), 60f, 7200f);
            convoySeconds = Clamp(ini.GetFloat(section, "DuracionConvoy", 150f), 30f, 600f);
            convoyRadius = Clamp(ini.GetFloat(section, "RadioConvoy", 85f), 25f, 150f);
            if (!eventConvoys) EndConvoy("eventos desactivados");
        }

        bool IsInConvoy(Gang g)
        {
            return convoy != null && (convoy.Leader.G == g || convoy.Follower.G == g);
        }

        bool IsConvoyAlly(Gang a, Gang b)
        {
            return convoy != null && a != b && IsInConvoy(a) && IsInConvoy(b);
        }

        void StepConvoys(double now)
        {
            if (convoy != null)
            {
                Convoy current = convoy;
                Member a = current.Leader, b = current.Follower;
                bool alive = !a.Dead && !b.Dead && gangs.Contains(a.G) && gangs.Contains(b.G);
                try { alive = alive && DOES_CHAR_EXIST(a.Ped) && DOES_CHAR_EXIST(b.Ped) && !IS_CHAR_DEAD(a.Ped) && !IS_CHAR_DEAD(b.Ped); } catch { alive = false; }
                bool driving = a.InCar && b.InCar && a.Driving && b.Driving && CarOk(a.Car) && CarOk(b.Car) && a.Car != b.Car;
                bool order = (a.G.Order >= 0 && now < a.G.OrderUntil) || (b.G.Order >= 0 && now < b.G.OrderUntil) ||
                    a.G.Offer != null || b.G.Offer != null || a.FunUntil > now || b.FunUntil > now;
                bool attacked = now - a.LastHit < 3 || now - b.LastHit < 3 || a.Fleeing || b.Fleeing || a.G.Wanted || b.G.Wanted;
                if (alive && driving)
                {
                    float speed = 0; try { GET_CAR_SPEED(a.Car, out speed); } catch { }
                    if (speed > 1.5f) current.LeaderStillSince = -1;
                    else if (current.LeaderStillSince < 0) current.LeaderStillSince = now;
                }
                if (AmbientRules.EndAlliance(alive, driving, order, attacked, FlatDist(a.Pos, b.Pos), now, current.Until, current.LeaderStillSince))
                    EndConvoy("cada uno sigue su camino");
                return;
            }
            if (nextConvoy < 0) nextConvoy = now + convoyEvery;
            if (!eventConvoys || race != null || now < nextConvoy) return;
            nextConvoy = now + convoyEvery;
            if (!AmbientRules.Roll(now, lastConvoy, convoyCooldown, convoyChance, G.Rng.NextDouble())) return;
            var drivers = new List<Member>();
            foreach (var g in gangs)
            {
                if (g.State != 1) continue;
                foreach (var m in g.Members)
                    if (m.InCar && m.Driving && CarOk(m.Car) && Calm(g, m, now)) { drivers.Add(m); break; }
            }
            G.Shuffle(drivers);
            for (int i = 0; i < drivers.Count; i++)
                for (int j = i + 1; j < drivers.Count; j++)
                {
                    Member a = drivers[i], b = drivers[j];
                    if (a.G == b.G || a.Car == b.Car) continue;
                    bool combat = a.G.HostileNow || b.G.HostileNow || a.G.Wanted || b.G.Wanted;
                    if (!AmbientRules.CompatibleDrivers(true, true, a.Driving, b.Driving, a.EventKind != 0, b.EventKind != 0,
                        combat, FlatDist(a.Pos, b.Pos), a.Pos.Z - b.Pos.Z, convoyRadius)) continue;
                    convoy = new Convoy { Leader = a, Follower = b, Until = now + convoySeconds };
                    lastConvoy = now;
                    a.G.NoChaseUntil = Math.Max(a.G.NoChaseUntil, convoy.Until);
                    b.G.NoChaseUntil = Math.Max(b.G.NoChaseUntil, convoy.Until);
                    RestoreBandRelations(a.G, b.G, true);
                    RestoreBandRelations(b.G, a.G, true);
                    foreach (var g in new[] { a.G, b.G })
                        foreach (var m in g.Members)
                        {
                            Gang targetGang;
                            Member target = FindMember(m.Target, out targetGang);
                            if (target != null && IsConvoyAlly(g, targetGang)) { m.Target = 0; m.Mode = M_NONE; }
                        }
                    b.Mode = M_NONE;
                    log("[evento] convoy espontaneo: " + Name(b.G, b) + " acompana a " + Name(a.G, a));
                    StreamRuntime.AddFeed("convoy", Name(a.G, a), "Viaja con " + Name(b.G, b), now);
                    return;
                }
        }

        void EndConvoy(string why)
        {
            if (convoy == null) return;
            Convoy previous = convoy;
            convoy = null; // restore uses the current alliance state, after removing this lease
            if (previous.Follower.Mode == M_FOLLOW) previous.Follower.Mode = M_NONE;
            if (previous.Follower.Target == previous.Leader.Ped) previous.Follower.Target = 0;
            previous.Leader.Objective = "Viajar por la ciudad";
            previous.Follower.Objective = "Viajar por la ciudad";
            RestoreBandRelations(previous.Leader.G, previous.Follower.G);
            RestoreBandRelations(previous.Follower.G, previous.Leader.G);
            if (diag) log("[evento] convoy terminado: " + why);
        }

        bool ThinkConvoy(Gang g, Member m, double now)
        {
            if (convoy == null || m != convoy.Leader && m != convoy.Follower) return false;
            if (!m.InCar || !m.Driving || !CarOk(m.Car)) { EndConvoy("dejo de conducir"); return false; }
            if (m == convoy.Leader)
            {
                if (m.Mode != M_CRUISE) CruiseCalm(m, m.Car, now);
                m.Objective = "Guiar un convoy tranquilo";
                Say(m, "viaja con " + Name(convoy.Follower.G, convoy.Follower));
                return true;
            }
            Member leader = convoy.Leader;
            float speed = 0; try { GET_CAR_SPEED(m.Car, out speed); } catch { }
            if (speed > 1.5f) convoy.StillSince = -1; else if (convoy.StillSince < 0) convoy.StillSince = now;
            if (AmbientRules.RefreshDrive(m.Mode != M_FOLLOW || m.Target != leader.Ped, now, m.LastTask, convoy.StillSince, 0))
            {
                // Keep one moving native target. A frozen coordinate would make the follower stop
                // at each previous leader position, and mission 2 would ram the other car.
                _TASK_CAR_MISSION_PED_TARGET(m.Ped, m.Car, leader.Ped, 4, 18f, AiTaskPolicy.RoadDrivingStyle, 20, 10);
                Task(m, M_FOLLOW, now);
                m.Target = leader.Ped;
            }
            if (convoy.StillSince >= 0 && now - convoy.StillSince > 35) { EndConvoy("se separaron en el trafico"); return false; }
            m.Objective = "Acompanar a " + Name(leader.G, leader);
            Say(m, "convoy con " + Name(leader.G, leader));
            return true;
        }

        void EndAmbientEvents(string why)
        {
            EndRace(why);
            EndConvoy(why);
            foreach (var g in gangs)
                foreach (var m in g.Members) if (m.EventKind == 1) EndChat(m, null);
        }

        void ForgetAmbientState()
        {
            // Called after a save/load transition, when old native handles cannot be touched.
            if (race != null)
            {
                foreach (var g in race.Bands) g.Truce = false;
                foreach (var m in race.Racers) ReleaseRacer(m);
            }
            race = null;
            convoy = null;
            radioPreferences.Clear();
            radioCommands.Clear();
            lastRadioOwner = lastRadioVehicle = 0;
            lastRadioStation = -1;
            lastFun.Clear();
            nextRace = nextConvoy = nextChat = -1;
        }
    }
}
