using System;
using System.Collections.Generic;
using System.Numerics;
using static KickChaos.N;

namespace KickChaos
{
    /// <summary>
    /// 1.9: comandos divertidos del chat ("!skin", "!saltar", "!desmayo"...). No van en las 3 opciones:
    /// estan siempre (los usa el sub con su banda, o el chat con la que esta en camara, segun config).
    /// </summary>
    public partial class SubNpcManager
    {
        int funMode = 2;              // 0 apagado, 1 solo el suscriptor, 2 tambien el chat (la banda en camara)
        float funCooldown = 8f;
        readonly bool[] funOn = new bool[GangRules.FunKeys.Length];
        readonly Dictionary<int, double> lastFun = new Dictionary<int, double>();

        void DoFun(Gang g, int cmd, string user, double now, Member only = null)
        {
            if (cmd < 0 || cmd >= funOn.Length || !funOn[cmd]) return;
            double last;
            if (lastFun.TryGetValue(g.Id, out last) && now - last < funCooldown) return;
            lastFun[g.Id] = now;
            int done = 0;
            foreach (var m in g.Members)
            {
                if (only != null && m != only) continue;
                if (m.Dead || m.Ped == 0 || !DOES_CHAR_EXIST(m.Ped) || IS_CHAR_DEAD(m.Ped) || IS_CHAR_FATALLY_INJURED(m.Ped)) continue;
                if (m.EventKind == 2) continue; // corriendo una carrera
                try { if (FunOne(g, m, cmd, now)) done++; }
                catch (Exception ex) { log("[NPC] error con !" + GangRules.FunKeys[cmd] + ": " + ex.Message); }
            }
            if (done == 0) { log("[NPC] !" + GangRules.FunKeys[cmd] + " de " + user + ": no se pudo con la banda de " + g.User); return; }
            Feed(GangRules.GangTitle(g.User, 0), 0, "!" + GangRules.FunKeys[cmd].ToUpperInvariant(), 3, now);
            log("[NPC] !" + GangRules.FunKeys[cmd] + " (" + user + ") -> banda de " + g.User + " (" + done + ")");
        }

        readonly Dictionary<int, int> radioPreferences = new Dictionary<int, int>();
        readonly Dictionary<int, double> radioCommands = new Dictionary<int, double>();
        int lastRadioOwner, lastRadioStation = -1, lastRadioVehicle;

        bool HandleRadioCommand(string user, string message, double now)
        {
            string[] words = message.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0 || !string.Equals(words[0], "!radio", StringComparison.OrdinalIgnoreCase)) return false;
            Member owner = OwnedMember(user);
            if (owner == null || !owner.InCar || !CarOk(owner.Car)) return true;
            double last;
            if (radioCommands.TryGetValue(owner.RowId, out last) && now - last < funCooldown) return true;
            int station;
            if (words.Length > 1)
            {
                if (!int.TryParse(words[1], out station) || station < 0 || station > 18) return true;
            }
            else
            {
                int current;
                if (!radioPreferences.TryGetValue(owner.RowId, out current)) current = 0;
                station = (current + 1) % 19;
            }
            radioPreferences[owner.RowId] = station;
            radioCommands[owner.RowId] = now;
            ApplyRadioPreference(owner);
            Flash(owner, dir.FollowedPed == owner.Ped ? "RADIO " + station : "RADIO GUARDADA " + station, now);
            log("[NPC] !radio de " + user + ": estacion " + station + " (al observar su NPC)");
            return true;
        }

        void ApplyRadioPreference(Member m)
        {
            int station;
            // IV exposes global retuning, not a per-car station setter. Ownership is scoped to the
            // observed NPC so one follower cannot retune the soundtrack while another is on camera.
            if (m == null || dir.FollowedPed != m.Ped) return;
            if (!radioPreferences.TryGetValue(m.RowId, out station)) { lastRadioOwner = 0; return; }
            if (lastRadioOwner == m.RowId && lastRadioStation == station && lastRadioVehicle == m.Car) return;
            RETUNE_RADIO_TO_STATION_INDEX((uint)station);
            lastRadioOwner = m.RowId;
            lastRadioStation = station;
            lastRadioVehicle = m.Car;
        }

        bool FunOne(Gang g, Member m, int cmd, double now)
        {
            int ped = m.Ped;
            switch (cmd)
            {
                case GangRules.FunSkin:
                    return SwapSkin(g, m, now);
                case GangRules.FunRopa:
                    SET_CHAR_RANDOM_COMPONENT_VARIATION(ped);
                    Flash(m, "ROPA NUEVA", now);
                    return true;
                case GangRules.FunSaltar:
                    if (m.InCar) return false;
                    _TASK_JUMP(ped, true);
                    FunHold(m, 1.5, now);
                    Flash(m, "SALTA!", now);
                    return true;
                case GangRules.FunDesmayo:
                    if (m.InCar) return false;
                    SWITCH_PED_TO_RAGDOLL(ped, 3000, 3000, false, false, false, false);
                    FunHold(m, 4.0, now);
                    Flash(m, "SE DESMAYO", now);
                    return true;
                case GangRules.FunManos:
                    if (m.InCar) return false;
                    _TASK_HANDS_UP(ped, 4000);
                    FunHold(m, 4.0, now);
                    Flash(m, "MANOS ARRIBA", now);
                    return true;
                case GangRules.FunMiedo:
                    if (m.InCar) return false;
                    _TASK_COWER(ped);
                    FunHold(m, 4.0, now);
                    Flash(m, "QUE MIEDO!", now);
                    return true;
                case GangRules.FunTurbo:
                    {
                        if (!m.InCar || !m.Driving || !CarOk(m.Car)) return false;
                        float sp;
                        GET_CAR_SPEED(m.Car, out sp);
                        SET_CAR_FORWARD_SPEED(m.Car, Math.Min(70f, sp + 20f));
                        Flash(m, "TURBO!", now);
                        return true;
                    }
                case GangRules.FunFestejar:
                    if (m.InCar) { Flash(m, "FESTEJA!", now); return true; }
                    _TASK_JUMP(ped, true);
                    FunHold(m, 1.5, now);
                    Flash(m, "FESTEJA!", now);
                    return true;
            }
            return false;
        }

        static void Flash(Member m, string text, double now) { m.Flash = text; m.FlashUntil = now + 2.5; }

        void FunHold(Member m, double secs, double now)
        {
            m.FunUntil = now + secs;
            m.Mode = M_NONE; // al terminar, que vuelva a dar la orden que corresponda
            m.LastTask = now;
        }

        /// <summary>
        /// !skin: otra persona (otro modelo) en el mismo lugar, con el mismo nombre, vida, chaleco y armas.
        /// En auto (o si no se pudo crear) se cambia la ropa nomas.
        /// </summary>
        bool SwapSkin(Gang g, Member m, double now)
        {
            int old = m.Ped;
            if (m.InCar || m.Fleeing || m.EventKind != 0)
            {
                SET_CHAR_RANDOM_COMPONENT_VARIATION(old);
                Flash(m, "ROPA NUEVA", now);
                return true;
            }
            RestoreVehicleWeapon(m);
            uint oldModel = 0;
            try { GET_CHAR_MODEL(old, out oldModel); } catch { }
            Vector3 p = m.Pos;
            float heading = 0f;
            try { GET_CHAR_HEADING(old, out heading); } catch { }
            int np = 0;
            for (int tries = 0; tries < 4 && np == 0; tries++)
            {
                CREATE_RANDOM_CHAR(p + new Vector3(0, 0, 3f + tries), out np);
                if (np == 0 || !DOES_CHAR_EXIST(np)) { np = 0; continue; }
                uint nm = 0;
                try { GET_CHAR_MODEL(np, out nm); } catch { }
                if (IsCop(np) || (nm == oldModel && tries < 3)) { try { DELETE_CHAR(np); } catch { } np = 0; }
            }
            if (np == 0)
            {
                SET_CHAR_RANDOM_COMPONENT_VARIATION(old);
                Flash(m, "ROPA NUEVA", now);
                log("[NPC] !skin: no se pudo crear otra persona para " + Name(g, m) + " (se cambia la ropa)");
                return true;
            }
            uint remainingHealth = m.LastHp;
            try { GET_CHAR_HEALTH(old, out remainingHealth); } catch { }
            if (remainingHealth == 0) { try { DELETE_CHAR(np); } catch { } return false; }
            uint armor = m.Armor;
            try { GET_CHAR_ARMOUR(old, out armor); } catch { }
            int weapon = m.Weapon;
            int remainingAmmo = Math.Max(0, m.Ammo);
            if (weapon > 0) try { GET_AMMO_IN_CHAR_WEAPON(old, weapon, out remainingAmmo); } catch { }
            try { if (m.FakeName) REMOVE_FAKE_NETWORK_NAME_FROM_PED(old); } catch { }
            try { DELETE_CHAR(old); } catch { try { MARK_CHAR_AS_NO_LONGER_NEEDED(old); } catch { } }
            SET_CHAR_COORDINATES(np, p);
            SET_CHAR_HEADING(np, heading);
            m.Ped = np;
            m.Mode = M_NONE;
            m.LastTask = -100;
            m.Block = -1;
            m.Safe = false;
            m.Target = 0;
            // lo mismo que tenia (sin regalar vida, chaleco, balas ni explosivos: si no, !skin seria un truco)
            m.Pos = p;
            m.Head = p + new Vector3(0, 0, 1f);
            SET_CHAR_MAX_HEALTH(np, m.StartHealth);
            uint hp = Math.Min(m.StartHealth, remainingHealth);
            SET_CHAR_HEALTH(np, hp);
            m.LastHp = hp;
            m.Health01 = (float)hp / Math.Max(1u, m.StartHealth);
            SET_CHAR_KEEP_TASK(np, true);
            SET_CHAR_DROPS_WEAPONS_WHEN_DEAD(np, false);
            if (armor > 0) ADD_ARMOUR_TO_CHAR(np, (int)armor);
            m.Armor = armor;
            if (weapon > 0)
            {
                GIVE_WEAPON_TO_CHAR(np, weapon, Math.Max(0, remainingAmmo), false);
                SET_CHAR_AMMO(np, weapon, Math.Max(0, remainingAmmo));
                m.Ammo = Math.Max(0, remainingAmmo);
                m.AmmoCapped = weapon;
            }
            if (!g.Fighter)
            {
                MakeSafe(g, m, !g.IsFollower && paseoSafe);
                if (g.IsFollower) { SET_CHAR_RELATIONSHIP_GROUP(np, g.Group); Block(m, false); }
                ApplyRogueStats(g, m);
            }
            else
            {
                SetupFighter(g, m, false); // bando, cobertura, punteria y su arma (sin regalos)
            }
            if (g.Wanted && g.Fighter) OutlawPed(g, m);
            if (GameName)
            {
                try { GIVE_PED_FAKE_NETWORK_NAME(np, Name(g, m), 255, 255, 255, 255); m.FakeName = true; } catch { }
            }
            Changed();
            dir.ReplaceNpcPed(old, np);
            Flash(m, "SKIN NUEVA", now);
            return true;
        }
    }
}
