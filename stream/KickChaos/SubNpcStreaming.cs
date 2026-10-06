using System;
using System.Collections.Generic;
using System.Numerics;
using static KickChaos.N;

namespace KickChaos
{
    public partial class SubNpcManager
    {
        int nextRowId = 1;
        float principalHealthScale = 1.35f, cloneHealthScale = 0.65f, npcCameraSeconds = 12, npcCameraCooldown = 120;
        uint followerHealth = 160;
        RogueBuild initialBuild = RogueBuild.Superviviente;
        bool ambientPacing = true, followersEnterBattles = true, followFollowersOnSpawn;
        float encounterRadius = 100, encounterEvery = 120, encounterChance = 0.20f, encounterSeconds = 60;
        float theftPoliceChance = 0.10f, theftPoliceCooldown = 360, policeEscapeSeconds = 60, policeMaxSeconds = 300, ambientChaseChance = 0.15f;
        double nextTheftPolice = -1;
        readonly Dictionary<string, double> npcCameraReady = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, double> recentNpcEvents = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        void ApplyStreamingNpcConfig(IniFile ini)
        {
            const string S = "StreamingNPC";
            principalHealthScale = Clamp(ini.GetFloat(S, "VidaPrincipal", 1.35f), 1, 4);
            cloneHealthScale = Clamp(ini.GetFloat(S, "VidaDuplicado", 0.65f), 0.2f, 0.85f);
            followerHealth = (uint)Math.Max(60, Math.Min(500, ini.GetInt(S, "VidaFollow", 160)));
            npcCameraSeconds = Clamp(ini.GetFloat(S, "DuracionNpc", 12), 3, 60);
            npcCameraCooldown = Clamp(ini.GetFloat(S, "EsperaNpc", 120), 10, 3600);
            buildCooldown = Clamp(ini.GetFloat(S, "EsperaBuild", 15), 1, 300);
            RogueBuild parsed;
            if (NpcRoguePolicy.ParseBuild(ini.Get(S, "BuildInicial", "SUPERVIVIENTE"), out parsed)) initialBuild = parsed;
            ambientPacing = ini.GetBool(S, "EntornoTranquilo", true);
            followersEnterBattles = ini.GetBool(S, "FollowersEnBatallas", true);
            followFollowersOnSpawn = ini.GetBool("CamaraStream", "SeguirFollows", false);
            encounterRadius = Clamp(ini.GetFloat(S, "EncuentroRadio", 100), 30, 250);
            encounterEvery = Clamp(ini.GetFloat(S, "EncuentroCada", 120), 30, 1800);
            encounterChance = Clamp(ini.GetFloat(S, "EncuentroProbabilidad", 20), 0, 100) / 100;
            encounterSeconds = Clamp(ini.GetFloat(S, "DuracionEncuentro", 60), 10, 180);
            theftPoliceChance = Clamp(ini.GetFloat(S, "ProbabilidadPoliciaRobo", 10), 0, 100) / 100;
            theftPoliceCooldown = Clamp(ini.GetFloat(S, "IntervaloPoliciaRobo", 360), 60, 3600);
            policeEscapeSeconds = Clamp(ini.GetFloat(S, "EscaparPoliciaTras", 60), 15, 300);
            policeMaxSeconds = Clamp(ini.GetFloat(S, "PersecucionMaxima", 300), 60, 1800);
            ambientChaseChance = Clamp(ini.GetFloat(S, "PersecucionProbabilidad", 15), 0, 100) / 100;
        }

        void InitializeStreamMember(Gang g, Member m, double now)
        {
            m.RowId = nextRowId++;
            m.Build = initialBuild;
            m.OwnerName = m.Index == 0 ? g.User : "";
            if (m.Index == 0) g.MainRowId = m.RowId;
            else m.ParentRowId = g.MainRowId;
            double recent;
            m.RecentEventUntil = recentNpcEvents.TryGetValue(g.User, out recent) ? recent : now + 30;
        }

        public bool SpawnCapacityFull(QueuedAction qa)
        {
            bool follower = NpcRoguePolicy.IsFollowSource(qa.Source);
            string user = (qa.User ?? "").Trim();
            foreach (var g in gangs)
                if (g.IsFollower == follower && string.Equals(g.User, user, StringComparison.OrdinalIgnoreCase) &&
                    (g.State != 1 || g.Alive > 0)) return false;
            int reserved = TotalMembers();
            foreach (var g in gangs) if (g.State != 1) reserved++;
            return LiveGangs() >= maxNpcs || reserved >= maxCharacters || (!follower && FreeSlot() < 0);
        }

        Member FindStreamMember(int id, out Gang gang)
        {
            gang = null;
            foreach (var g in gangs) foreach (var m in g.Members)
                if (m.RowId == id && !m.Dead) { gang = g; return m; }
            return null;
        }

        Member OwnedMember(string user, int level = 0, bool subscriberOnly = false)
        {
            string name = (user ?? "").Trim();
            if (name.Length == 0) return null;
            Member best = null;
            foreach (var g in gangs)
            {
                if (g.State != 1 || (subscriberOnly && g.IsFollower)) continue;
                foreach (var m in g.Members)
                {
                    if (m.Dead || m.Ped == 0) continue;
                    if (!string.Equals(m.OwnerName, name, StringComparison.OrdinalIgnoreCase) &&
                        !(g.Test && m.Index == 0 && level >= 4)) continue;
                    if (best == null || (best.G.IsFollower && !g.IsFollower) ||
                        (best.Index > 0 && m.Index == 0 && !g.IsFollower)) best = m;
                }
            }
            return best;
        }

        public int FindOwnedNpc(string user, bool subscriberOnly = false)
        {
            Member m = OwnedMember(user, 0, subscriberOnly);
            return m != null && DOES_CHAR_EXIST(m.Ped) && !IS_CHAR_DEAD(m.Ped) && !IS_CHAR_FATALLY_INJURED(m.Ped) ? m.Ped : 0;
        }

        public void MarkRecentEvent(string user, double until)
        {
            if (string.IsNullOrWhiteSpace(user)) return;
            string name = user.Trim();
            double old;
            recentNpcEvents[name] = recentNpcEvents.TryGetValue(name, out old) ? Math.Max(old, until) : until;
            foreach (var g in gangs) foreach (var m in g.Members)
                if (string.Equals(m.OwnerName, name, StringComparison.OrdinalIgnoreCase)) m.RecentEventUntil = Math.Max(m.RecentEventUntil, until);
            if (recentNpcEvents.Count > 1000)
            {
                var expired = new List<string>();
                foreach (var pair in recentNpcEvents) if (pair.Value < G.Now) expired.Add(pair.Key);
                foreach (var key in expired) recentNpcEvents.Remove(key);
            }
        }

        public List<StreamNpcRow> SnapshotRows()
        {
            double now = G.Now;
            var rows = new List<StreamNpcRow>();
            foreach (var g in gangs)
            {
                if (g.State != 1) continue;
                foreach (var m in g.Members)
                {
                    if (m.Dead || m.Ped == 0 || !DOES_CHAR_EXIST(m.Ped) || IS_CHAR_DEAD(m.Ped) || IS_CHAR_FATALLY_INJURED(m.Ped)) continue;
                    double ready;
                    float heading = 0;
                    try { GET_CHAR_HEADING(m.Ped, out heading); } catch { }
                    var row = new StreamNpcRow
                    {
                        Id = m.RowId, Ped = m.Ped, ParentId = m.ParentRowId, Name = DisplayName(g, m), Owner = m.OwnerName,
                        Kind = g.IsFollower ? "FOLLOW" : m.Index == 0 ? "SUB" : "DUPLICADO", Kills = m.Kills,
                        Characters = g.Alive, Level = m.Index == 0 ? g.Level : Math.Max(1, 1 + (g.Level - 1) / 2),
                        Health = m.Health01 * 100, Armor = m.Armor, X = m.Pos.X, Y = m.Pos.Y, Z = m.Pos.Z, Heading = heading,
                        InCar = m.InCar, Driving = m.Driving, Vehicle = m.InCar ? m.Car : 0,
                        VehicleName = m.InCar ? (m.Driving ? "conductor" : "acompanante") : "a pie",
                        Build = NpcRoguePolicy.BuildName(m.Build), State = m.Status, Objective = CurrentObjective(m),
                        RecentUntil = m.RecentEventUntil, Claimable = m.Index > 0 && m.OwnerName.Length == 0,
                        Combat = m.Mode == M_COMBAT || m.Mode == M_SHOOT || m.Mode == M_DRIVEBY || now - m.LastShot < 4,
                        Pursuit = m.Mode == M_CHASE || g.ChaseWith != null || g.Wanted, Racing = m.EventKind == 2,
                        CameraReadyIn = npcCameraReady.TryGetValue(m.OwnerName, out ready) ? Math.Max(0, ready - now) : 0,
                        CameraAvailable = m.RoomKey == 0 || Vector3.Distance(m.Pos, dir.ActionCenter()) < 45
                    };
                    var perks = new List<string>();
                    foreach (var p in g.Perks)
                        if (p >= 0 && p < PerkRules.Labels.Length && perks.Count < 3) perks.Add(PerkRules.Labels[p]);
                    row.Perks = string.Join(" / ", perks.ToArray()) + (m.Index > 0 && perks.Count > 0 ? " (menores)" : "");
                    rows.Add(row);
                }
            }
            return rows;
        }

        bool HandleStreamingChat(string user, string msg, int level, Member owned, double now)
        {
            if (!msg.StartsWith("!"))
            {
                if (owned != null && ShowChat)
                {
                    string[] lines = GangRules.Bubble(msg, 30, 3);
                    double until = now + GangRules.BubbleSeconds(msg);
                    foreach (var g in gangs) foreach (var member in g.Members)
                        if (!member.Dead && (member == owned || string.Equals(member.OwnerName, user, StringComparison.OrdinalIgnoreCase)))
                        { member.OwnerBubble = lines; member.OwnerBubbleUntil = until; }
                }
                return owned != null;
            }
            int id;
            if (msg.StartsWith("!unirme", StringComparison.OrdinalIgnoreCase))
            {
                if (!NpcRoguePolicy.ClaimId(msg, out id) || string.IsNullOrWhiteSpace(user)) return true;
                Gang g;
                Member claim = FindStreamMember(id, out g);
                if (claim == null || !DOES_CHAR_EXIST(claim.Ped) || IS_CHAR_DEAD(claim.Ped) || IS_CHAR_FATALLY_INJURED(claim.Ped)) return true;
                // One viewer can possess one clone; a follow/sub NPC can remain associated too.
                bool viewerHasClone = false;
                foreach (var band in gangs) foreach (var m in band.Members)
                    if (!m.Dead && m.Index > 0 && string.Equals(m.OwnerName, user.Trim(), StringComparison.OrdinalIgnoreCase)) viewerHasClone = true;
                if (!NpcRoguePolicy.ClaimAllowed(g.IsFollower, claim.Index == 0, !claim.Dead, claim.OwnerName.Length > 0, viewerHasClone)) return true;
                claim.OwnerName = user.Trim().Length > 25 ? user.Trim().Substring(0, 25) : user.Trim();
                claim.RecentEventUntil = now + 30;
                claim.Flash = "SE UNIO " + claim.OwnerName.ToUpperInvariant(); claim.FlashUntil = now + 4;
                if (claim.FakeName) { try { REMOVE_FAKE_NETWORK_NAME_FROM_PED(claim.Ped); } catch { } claim.FakeName = false; }
                if (GameName) { GIVE_PED_FAKE_NETWORK_NAME(claim.Ped, claim.OwnerName, 255, 255, 255, 255); claim.FakeName = true; }
                StreamRuntime.AddFeed("UNIRSE", claim.OwnerName, "posee el duplicado #" + claim.RowId, now);
                log("[NPC] " + claim.OwnerName + " posee el duplicado #" + claim.RowId);
                return true;
            }
            RogueBuild build;
            if (NpcRoguePolicy.ParseBuild(msg, out build))
            {
                if (owned != null && now - owned.LastBuildChange >= buildCooldown) SetRogueBuild(owned, build, now, user);
                return true;
            }
            if (msg.Equals("!npc", StringComparison.OrdinalIgnoreCase))
            {
                Member cameraNpc = OwnedMember(user, level, true);
                if (cameraNpc == null) return true;
                double ready;
                if (npcCameraReady.TryGetValue(user, out ready) && now < ready)
                {
                    cameraNpc.Flash = "!NPC EN " + Math.Ceiling(ready - now) + "s"; cameraNpc.FlashUntil = now + 4;
                    return true;
                }
                if (dir.ObserveNpc(cameraNpc.Ped, npcCameraSeconds, "chat !npc", 60, true))
                {
                    npcCameraReady[user] = now + npcCameraCooldown;
                    cameraNpc.RecentEventUntil = now + npcCameraSeconds;
                    StreamRuntime.AddFeed("CAMARA", user, "!npc disponible en " + npcCameraCooldown.ToString("0") + " s", now);
                }
                return true;
            }
            return false;
        }

        float buildCooldown = 15;
        string CurrentObjective(Member m)
        {
            if (m.EventKind == 2) return "llegar a la meta de la carrera";
            if (m.EventKind == 1) return "charlar con un vecino";
            if (m.Eating) return "recuperarse comprando comida";
            if (m.Fleeing) return "buscar un vehiculo o refugio para sobrevivir";
            if (m.Mode == M_ENTER_CAR) return "conseguir transporte";
            if (m.Target != 0 && (m.Mode == M_COMBAT || m.Mode == M_SHOOT || m.Mode == M_DRIVEBY || m.Mode == M_CHASE))
                return m.Build == RogueBuild.Cazador ? "eliminar un enemigo debilitado o aislado" :
                    m.Build == RogueBuild.Superviviente ? "combatir desde una posicion segura" : "presionar al enemigo";
            return m.Objective;
        }
        void SetRogueBuild(Member m, RogueBuild build, double now, string who)
        {
            m.Build = build; m.LastBuildChange = now;
            ApplyRogueStats(m.G, m);
            m.Flash = NpcRoguePolicy.BuildName(build); m.FlashUntil = now + 4;
            m.Objective = build == RogueBuild.Agresivo ? "buscar encuentros y presionar" :
                build == RogueBuild.Cazador ? "buscar enemigos debilitados o aislados" : "viajar y combatir con ventaja";
            StreamRuntime.AddFeed("BUILD", Name(m.G, m), NpcRoguePolicy.BuildName(build), now);
            log("[NPC] " + Name(m.G, m) + " elige " + NpcRoguePolicy.BuildName(build) + " (" + who + ")");
        }

        void ApplyRogueStats(Gang g, Member m)
        {
            if (m.Ped == 0 || m.Dead || !DOES_CHAR_EXIST(m.Ped) || IS_CHAR_DEAD(m.Ped)) return;
            float share = m.Index > 0 ? 0.5f : 1;
            int accuracy = (g.IsFollower ? 25 : m.Index == 0 ? 50 : 32) +
                (int)((g.AccBonus + (g.Level - 1) * 5) * share) + (m.Build == RogueBuild.Cazador ? (int)(12 * share) : 0);
            int rate = (g.IsFollower ? 65 : m.Index == 0 ? 110 : 75) +
                (int)((g.FireBonus + (m.Build == RogueBuild.Agresivo ? 30 : 0)) * share);
            SET_CHAR_ACCURACY(m.Ped, (uint)Math.Max(10, Math.Min(m.Index > 0 ? 75 : 90, accuracy)));
            SET_CHAR_SHOOT_RATE(m.Ped, Math.Max(30, Math.Min(m.Index > 0 ? 135 : 200, rate)));
            SET_CHAR_MOVE_ANIM_SPEED_MULTIPLIER(m.Ped, Math.Min(speedMax, speedBase + (g.SpeedMul - speedBase) * share));
            SET_CHAR_WILL_DO_DRIVEBYS(m.Ped, true);
            SET_CHAR_WILL_USE_COVER(m.Ped, m.Build != RogueBuild.Agresivo);
        }

        float RogueThreatScore(Member m, int ped, float distance, int nearby)
        {
            float health = 1;
            if (m.Build == RogueBuild.Cazador)
            {
                Gang other;
                Member target = FindMember(ped, out other);
                if (target != null) health = target.Health01;
                else { uint hp; GET_CHAR_HEALTH(ped, out hp); health = Math.Min(1, hp / 200f); }
            }
            return NpcRoguePolicy.TargetScore(m.Build, distance, health, nearby);
        }

        int NearbyThreatAllies(Threat t)
        {
            if (t.Rival == null) return 1;
            int count = 0;
            foreach (var ally in t.Rival.Members) if (!ally.Dead && Vector3.Distance(ally.Pos, t.Pos) < 20) count++;
            return count;
        }

        bool PeacefulAlly(Gang a, Gang b)
        {
            return a == b || a.Group == b.Group || (a.IsFollower && b.IsFollower) || IsConvoyAlly(a, b);
        }

        uint RivalRelationship(Gang a, Gang b)
        {
            uint relation = AmbientRules.RestoredRelationship(a.Group == b.Group, a.IsFollower && b.IsFollower,
                a.Fighter && b.Fighter, duel, a.Truce && b.Truce, IsConvoyAlly(a, b));
            return relation == 5 && ambientPacing && (!a.HostileNow || !b.HostileNow) ? 3u : relation;
        }

        void RefreshRivalRelationships(Gang g)
        {
            foreach (var other in gangs)
            {
                if (other == g || other.State != 1) continue;
                uint relation = RivalRelationship(g, other);
                foreach (var m in g.Members) if (!m.Dead && m.Ped != 0 && DOES_CHAR_EXIST(m.Ped)) SET_CHAR_RELATIONSHIP(m.Ped, relation, other.Group);
                foreach (var m in other.Members) if (!m.Dead && m.Ped != 0 && DOES_CHAR_EXIST(m.Ped)) SET_CHAR_RELATIONSHIP(m.Ped, relation, g.Group);
            }
        }

        void StepAmbientEncounter(Gang g, double now)
        {
            if (!ambientPacing || g.IsFollower || !g.Fighter || now - g.LastEncounterCheck < 1) return;
            g.LastEncounterCheck = now;
            StepWantedPacing(g, now);
            bool fighting = false;
            foreach (var m in g.Members)
                if (!m.Dead && (now - m.LastShot < 5 || now - m.LastHit < 5)) { fighting = true; break; }
            bool previous = g.HostileNow;
            if (fighting) g.EncounterUntil = Math.Max(g.EncounterUntil, now + 12);
            g.HostileNow = g.Wanted || now < g.EncounterUntil || (g.Order == GangRules.CmdBanda && now < g.OrderUntil);
            if (!g.HostileNow && !g.Truce && !IsInConvoy(g) && now >= g.NextEncounterCheck)
            {
                g.NextEncounterCheck = now + encounterEvery;
                if (duel && G.Rng.NextDouble() < encounterChance)
                {
                    foreach (var other in gangs)
                    {
                        if (other == g || !other.Fighter || other.State != 1 || other.Alive == 0 || other.Truce || IsInConvoy(other) || PeacefulAlly(g, other) ||
                            GangDistance(g, other) > encounterRadius || now < other.NextEncounterCheck) continue;
                        g.EncounterUntil = other.EncounterUntil = now + encounterSeconds;
                        g.HostileNow = other.HostileNow = true;
                        other.NextEncounterCheck = now + encounterEvery;
                        StreamRuntime.AddFeed("ENCUENTRO", g.User, "se cruza con " + other.User, now);
                        break;
                    }
                }
            }
            if (previous != g.HostileNow) RefreshRivalRelationships(g);
        }

        void StepWantedPacing(Gang g, double now)
        {
            if (!g.Wanted) return;
            if (g.WantedSince < 0) g.WantedSince = now;
            bool danger = false;
            foreach (var m in g.Members)
            {
                if (m.Dead) continue;
                if (now - m.LastHit < 8 || now - m.LastShot < 8) danger = true;
                foreach (var c in g.Cops)
                    if (c.Alive && Vector3.Distance(c.Pos, m.Pos) < 75) { danger = true; break; }
            }
            if (danger) g.LastContact = now;
            else if (g.LastContact < g.WantedSince) g.LastContact = g.WantedSince;
            bool escaped = now - g.LastContact >= policeEscapeSeconds;
            bool cooled = now - g.WantedSince >= policeMaxSeconds && !GangShotRecently(g, now, 15);
            if (!escaped && !cooled) return;
            g.Wanted = false; g.WantedSince = -1; g.Stars = 0; g.PendingStars = 0;
            g.CopsPending = 0; g.NextCops = -1; g.PoliceAt = -1;
            ReleaseCops(g);
            foreach (var m in g.Members) if (!m.Dead)
            {
                m.Target = 0; m.Mode = M_NONE; m.Phase = P_NONE;
                m.Flash = "PERDIO A LA POLICIA"; m.FlashUntil = now + 4;
            }
            StreamRuntime.AddFeed("ESCAPE", g.User, "la persecucion termina", now);
            log("[NPC] " + g.User + " termina su persecucion y vuelve a recorrer la ciudad");
            RefreshRivalRelationships(g);
        }

        void StepFollowerEncounter(Gang g, Member m, double now)
        {
            if (!g.IsFollower || !followersEnterBattles) return;
            bool combatNearby = false;
            foreach (var band in gangs)
            {
                if (band == g || band.IsFollower || !band.Fighter || band.State != 1) continue;
                foreach (var other in band.Members)
                    if (!other.Dead && Vector3.Distance(other.Pos, m.Pos) < 45 &&
                        (now - other.LastShot < 5 || band.ChaseWith != null || (band.Wanted && band.Cops.Count > 0)))
                    { combatNearby = true; break; }
                if (combatNearby) break;
            }
            if (!g.Fighter && combatNearby && now >= m.NextFollowerBattle)
            {
                g.Fighter = true; g.Behavior = NpcBehavior.Batalla; g.HostileNow = true;
                m.FollowerBattleUntil = now + encounterSeconds; m.NextFollowerBattle = now + encounterEvery;
                GiveGun(m, 7, 150); SetupFighter(g, m, false);
                m.Flash = "SE SUMA A LA BATALLA"; m.FlashUntil = now + 4;
                StreamRuntime.AddFeed("BATALLA", g.User, "encuentra una batalla mientras paseaba", now);
            }
            if (g.Fighter && now >= m.FollowerBattleUntil && now - m.LastHit > 8 && now - m.LastShot > 8)
            {
                g.Fighter = false; g.Behavior = NpcBehavior.Pasear; g.HostileNow = false;
                g.Wanted = false; g.PoliceAt = -1; g.CopsPending = 0; g.Order = -1;
                m.Target = 0; m.Fleeing = false; m.Phase = P_NONE; m.Mode = M_NONE;
                SET_CHAR_RELATIONSHIP(m.Ped, 3, 3); SET_CHAR_RELATIONSHIP_GROUP(m.Ped, g.Group); Block(m, false);
                RefreshRivalRelationships(g);
                m.Objective = "seguir paseando por la ciudad";
            }
        }

        bool ThinkFollowerDriver(Gang g, Member m, Threat target, double now)
        {
            if (target.Ped == 0 || !CarOk(m.Car)) return false;
            bool continuingCombat = m.Mode == M_COMBAT && m.Target == target.Ped && target.Dist <= 60;
            bool ram = !continuingCombat && m.Build == RogueBuild.Agresivo && target.Dist < 25 &&
                (m.Mode != M_CHASE || m.Target != target.Ped || now - m.ModeAt < 6);
            if (!ram && (target.Dist <= 35 || continuingCombat))
            {
                NativeVehicleCombat(g, m, target, now, false);
                m.Objective = "disparar desde el auto al rival cercano";
                return true;
            }
            bool renew = m.Target != target.Ped || m.Mode != M_CHASE ||
                (!Moving(m, now, 1.5f) && StillFor(m, now) > 12 && now - m.LastTask > 12);
            if (renew)
            {
                // Mission 2 is ram; mission 4 routes to a ped along roads. Native combat owns drive-bys.
                uint mission = ram ? 2u : 4u;
                _TASK_CAR_MISSION_PED_TARGET(m.Ped, m.Car, target.Ped, mission, 23, AiTaskPolicy.RoadDrivingStyle, 10, 10);
                Task(m, M_CHASE, now); m.Target = target.Ped;
                SET_CHAR_WILL_DO_DRIVEBYS(m.Ped, true); SET_CHAR_WILL_LEAVE_CAR_IN_COMBAT(m.Ped, false);
            }
            Say(m, "se suma a una batalla desde el auto");
            m.Objective = m.Build == RogueBuild.Agresivo ? "alcanzar y chocar al rival cercano" : "acompanar la batalla desde el auto";
            return true;
        }
    }
}
