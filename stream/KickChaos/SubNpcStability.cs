using System;
using System.Collections.Generic;
using static KickChaos.N;

namespace KickChaos
{
    public partial class SubNpcManager
    {
        readonly Dictionary<int, int> vehicleWeapons = new Dictionary<int, int>();

        void VehicleWeapon(Member m)
        {
            if (m.Weapon == 7 || m.Weapon == 9 || m.Weapon == 12 || m.Weapon == 13) return;
            if (!m.Armed) return;
            if (!vehicleWeapons.ContainsKey(m.Ped))
            {
                vehicleWeapons[m.Ped] = m.Weapon;
                float share = m.Index > 0 ? 0.65f : m.G != null && m.G.IsFollower ? 0.45f : 1f;
                int ammo = m.VehicleSidearmAmmoLeft >= 0 ? m.VehicleSidearmAmmoLeft : Math.Max(12, (int)(mags * 17 * share));
                GIVE_WEAPON_TO_CHAR(m.Ped, 7, ammo, false);
                SET_CHAR_AMMO(m.Ped, 7, ammo);
            }
            SET_CURRENT_CHAR_WEAPON(m.Ped, 7, true);
        }

        void RestoreVehicleWeapon(Member m)
        {
            int weapon;
            if (m.InCar || !vehicleWeapons.TryGetValue(m.Ped, out weapon)) return;
            vehicleWeapons.Remove(m.Ped);
            int remaining;
            GET_AMMO_IN_CHAR_WEAPON(m.Ped, 7, out remaining);
            m.VehicleSidearmAmmoLeft = Math.Max(0, remaining);
            // A perk earned in the car may have upgraded the primary weapon.
            if (m.Armed && m.Weapon > 0) SET_CURRENT_CHAR_WEAPON(m.Ped, m.Weapon, true);
        }

        bool CheckVehicleAmmo(Gang g, Member m, double now)
        {
            if (!m.InCar || !vehicleWeapons.ContainsKey(m.Ped)) return false;
            int ammo;
            GET_AMMO_IN_CHAR_WEAPON(m.Ped, 7, out ammo);
            m.VehicleSidearmAmmoLeft = Math.Max(0, ammo);
            m.Ammo = ammo;
            m.AmmoMax = Math.Max(m.AmmoMax, ammo);
            if (ammo <= 0 && m.Mode != M_LEAVE_CAR)
                BailOut(g, m, now, "pistola del auto sin balas: usa su arma principal a pie");
            return true;
        }

        void NativeVehicleCombat(Gang g, Member m, Threat target, double now, bool leaveCar = true)
        {
            VehicleWeapon(m);
            if (m.Mode == M_COMBAT && m.Target == target.Ped && StillFor(m, now) >= 25 && now - m.LastShot >= 15)
            { BailOut(g, m, now, "continua el combate a pie tras atasco"); return; }
            if (m.Mode != M_COMBAT || m.Target != target.Ped || (!IS_PED_IN_COMBAT(m.Ped) && now - m.LastTask >= 14))
            {
                SET_CHAR_WILL_USE_CARS_IN_COMBAT(m.Ped, true);
                SET_CHAR_WILL_DO_DRIVEBYS(m.Ped, true);
                SET_CHAR_WILL_LEAVE_CAR_IN_COMBAT(m.Ped, leaveCar);
                _TASK_COMBAT(m.Ped, target.Ped);
                Task(m, M_COMBAT, now); m.Target = target.Ped;
            }
            Say(m, "combate en vehiculo", true);
        }
    }
}
