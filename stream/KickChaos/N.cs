using System;
using System.Drawing;
using System.Numerics;
using GTA.Native;

namespace KickChaos
{
    /// <summary>
    /// Natives de GTA IV llamados a traves de ScriptHookDotNet (Function.Call), con los mismos
    /// nombres y argumentos que usa el resto del mod. Los handles (peds, autos, camaras) son int.
    /// </summary>
    public static class N
    {
        // Natives que el hook no encontro: se saltean (llamarlos haria que ScriptHookDotNet mate el script).
        static readonly System.Collections.Generic.HashSet<string> missing = new System.Collections.Generic.HashSet<string>();

        static void C(string name, params Parameter[] args) { if (missing.Contains(name)) return; Function.Call(name, args); }
        static bool B(string name, params Parameter[] args) { if (missing.Contains(name)) return false; return Function.Call<bool>(name, args); }
        static int I(string name, params Parameter[] args) { if (missing.Contains(name)) return 0; return Function.Call<int>(name, args); }

        public static bool IsMissing(string name) { return missing.Contains(name); }

        /// <summary>Comprueba que todos los natives que usa el mod existen en este juego. Devuelve los que faltan.</summary>
        public static System.Collections.Generic.List<string> Validate()
        {
            var result = new System.Collections.Generic.List<string>();
            int bogus;
            try { bogus = Function.GetAddress("KICKCHAOS_NATIVE_QUE_NO_EXISTE"); }
            catch { return result; }
            var src = typeof(N).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            var names = new System.Collections.Generic.HashSet<string>();
            foreach (var m in src)
                if (m.Name.ToUpperInvariant() == m.Name && m.Name.Length > 3) names.Add(m.Name.StartsWith("_") ? m.Name.Substring(1) : m.Name);
            foreach (string name in names)
            {
                int addr;
                try { addr = Function.GetAddress(name); } catch { continue; }
                if (addr == bogus) result.Add(name);
            }
            // Si "faltan" casi todos, el hook no sabe responder esta consulta: no desactivamos nada.
            if (result.Count > names.Count / 3) { result.Clear(); return result; }
            foreach (string name in result) missing.Add(name);
            return result;
        }

        static Pointer PI() { return new Pointer(typeof(int)); }
        static Pointer PF() { return new Pointer(typeof(float)); }
        static Vector3 V(Pointer x, Pointer y, Pointer z) { return new Vector3((float)x, (float)y, (float)z); }

        // ---------------- jugador ----------------
        public static int GET_PLAYER_ID() { return I("GET_PLAYER_ID"); }
        public static int CONVERT_INT_TO_PLAYERINDEX(int id) { return I("CONVERT_INT_TO_PLAYERINDEX", id); }
        public static void GET_PLAYER_CHAR(int player, out int ped) { Pointer p = PI(); C("GET_PLAYER_CHAR", player, p); ped = (int)p; }
        public static bool IS_PLAYER_PLAYING(int player) { return B("IS_PLAYER_PLAYING", player); }
        public static void SET_PLAYER_CONTROL(int player, bool v) { C("SET_PLAYER_CONTROL", player, v); }
        public static void SET_PLAYER_INVINCIBLE(int player, bool v) { C("SET_PLAYER_INVINCIBLE", player, v); }
        public static void SET_EVERYONE_IGNORE_PLAYER(int player, bool v) { C("SET_EVERYONE_IGNORE_PLAYER", player, v); }
        public static void SET_POLICE_IGNORE_PLAYER(int player, bool v) { C("SET_POLICE_IGNORE_PLAYER", player, v); }
        public static void ALTER_WANTED_LEVEL(int player, uint lvl) { C("ALTER_WANTED_LEVEL", player, (int)lvl); }
        public static void APPLY_WANTED_LEVEL_CHANGE_NOW(int player) { C("APPLY_WANTED_LEVEL_CHANGE_NOW", player); }
        public static void CLEAR_WANTED_LEVEL(int player) { C("CLEAR_WANTED_LEVEL", player); }
        public static void SET_MAX_WANTED_LEVEL(uint lvl) { C("SET_MAX_WANTED_LEVEL", (int)lvl); }
        public static bool IS_PLAYER_BEING_ARRESTED() { return B("IS_PLAYER_BEING_ARRESTED"); }

        // ---------------- personajes ----------------
        public static bool DOES_CHAR_EXIST(int ped) { return B("DOES_CHAR_EXIST", ped); }
        public static bool IS_CHAR_DEAD(int ped) { return B("IS_CHAR_DEAD", ped); }
        public static bool IS_CHAR_IN_ANY_CAR(int ped) { return B("IS_CHAR_IN_ANY_CAR", ped); }
        public static void GET_CAR_CHAR_IS_USING(int ped, out int veh) { Pointer p = PI(); C("GET_CAR_CHAR_IS_USING", ped, p); veh = (int)p; }
        public static void GET_CHAR_COORDINATES(int ped, out Vector3 pos)
        {
            Pointer x = PF(), y = PF(), z = PF();
            C("GET_CHAR_COORDINATES", ped, x, y, z);
            pos = V(x, y, z);
        }
        public static void SET_CHAR_COORDINATES(int ped, Vector3 p) { C("SET_CHAR_COORDINATES", ped, p.X, p.Y, p.Z); }
        public static void GET_CHAR_HEADING(int ped, out float h) { Pointer p = PF(); C("GET_CHAR_HEADING", ped, p); h = (float)p; }
        public static void SET_CHAR_HEADING(int ped, float h) { C("SET_CHAR_HEADING", ped, h); }
        public static void SET_CHAR_VISIBLE(int ped, bool v) { C("SET_CHAR_VISIBLE", ped, v); }
        public static void SET_CHAR_INVINCIBLE(int ped, bool v) { C("SET_CHAR_INVINCIBLE", ped, v); }
        public static void SET_CHAR_COLLISION(int ped, bool v) { C("SET_CHAR_COLLISION", ped, v); }
        public static void FREEZE_CHAR_POSITION(int ped, bool v) { C("FREEZE_CHAR_POSITION", ped, v); }
        /// <summary>(balas, fuego, explosiones, choques, golpes)</summary>
        public static void SET_CHAR_PROOFS(int ped, bool a, bool b, bool c, bool d, bool e) { C("SET_CHAR_PROOFS", ped, a, b, c, d, e); }
        public static void SET_CHAR_KEEP_TASK(int ped, bool v) { C("SET_CHAR_KEEP_TASK", ped, v); }
        public static void SET_CURRENT_CHAR_WEAPON(int ped, int weapon, bool b) { C("SET_CURRENT_CHAR_WEAPON", ped, weapon, b); }
        public static void GIVE_WEAPON_TO_CHAR(int ped, int weapon, int ammo, bool b) { C("GIVE_WEAPON_TO_CHAR", ped, weapon, ammo, b); }
        public static void WARP_CHAR_FROM_CAR_TO_COORD(int ped, Vector3 p) { C("WARP_CHAR_FROM_CAR_TO_COORD", ped, p.X, p.Y, p.Z); }
        public static void WARP_CHAR_INTO_CAR(int ped, int veh) { C("WARP_CHAR_INTO_CAR", ped, veh); }
        public static void CLEAR_ROOM_FOR_CHAR(int ped) { C("CLEAR_ROOM_FOR_CHAR", ped); }
        public static void GET_KEY_FOR_CHAR_IN_ROOM(int ped, out uint key) { Pointer p = PI(); C("GET_KEY_FOR_CHAR_IN_ROOM", ped, p); key = (uint)(int)p; }
        public static void SET_ROOM_FOR_CHAR_BY_KEY(int ped, uint key) { C("SET_ROOM_FOR_CHAR_BY_KEY", ped, (int)key); }
        public static void _TASK_COMBAT(int ped, int target) { C("TASK_COMBAT", ped, target); }
        public static void _TASK_SMART_FLEE_POINT(int ped, float x, float y, float z, float dist, uint ms) { C("TASK_SMART_FLEE_POINT", ped, x, y, z, dist, (int)ms); }

        // ---------------- NPC de suscriptores ----------------
        public static void CREATE_RANDOM_CHAR(Vector3 p, out int ped) { Pointer r = PI(); C("CREATE_RANDOM_CHAR", p.X, p.Y, p.Z, r); ped = (int)r; }
        public static void CREATE_RANDOM_CHAR_AS_DRIVER(int veh, out int ped) { Pointer r = PI(); C("CREATE_RANDOM_CHAR_AS_DRIVER", veh, r); ped = (int)r; }
        public static void DELETE_CHAR(int ped) { Pointer p = ped; C("DELETE_CHAR", p); }
        public static void MARK_CHAR_AS_NO_LONGER_NEEDED(int ped) { Pointer p = ped; C("MARK_CHAR_AS_NO_LONGER_NEEDED", p); }
        public static void GET_CHAR_MODEL(int ped, out uint model) { Pointer p = PI(); C("GET_CHAR_MODEL", ped, p); model = (uint)(int)p; }
        public static void SET_CHAR_MAX_HEALTH(int ped, uint v) { C("SET_CHAR_MAX_HEALTH", ped, (int)v); }
        public static void SET_CHAR_HEALTH(int ped, uint v) { C("SET_CHAR_HEALTH", ped, (int)v); }
        public static void GET_CHAR_HEALTH(int ped, out uint v) { Pointer p = PI(); C("GET_CHAR_HEALTH", ped, p); v = (uint)(int)p; }
        public static void ADD_ARMOUR_TO_CHAR(int ped, int v) { C("ADD_ARMOUR_TO_CHAR", ped, v); }
        public static void GET_CHAR_SPEED(int ped, out float v) { Pointer p = PF(); C("GET_CHAR_SPEED", ped, p); v = (float)p; }
        public static bool IS_CHAR_FATALLY_INJURED(int ped) { return B("IS_CHAR_FATALLY_INJURED", ped); }
        public static bool IS_PED_IN_COMBAT(int ped) { return B("IS_PED_IN_COMBAT", ped); }
        public static bool IS_CHAR_ON_SCREEN(int ped) { return B("IS_CHAR_ON_SCREEN", ped); }
        public static void SET_CHAR_RELATIONSHIP(int ped, uint level, int group) { C("SET_CHAR_RELATIONSHIP", ped, (int)level, group); }
        public static void SET_CHAR_WANTED_BY_POLICE(int ped, bool v) { C("SET_CHAR_WANTED_BY_POLICE", ped, v); }
        public static void SET_CHAR_IS_TARGET_PRIORITY(int ped, bool v) { C("SET_CHAR_IS_TARGET_PRIORITY", ped, v); }
        public static void SET_CHAR_ACCURACY(int ped, uint v) { C("SET_CHAR_ACCURACY", ped, (int)v); }
        public static void SET_CHAR_SHOOT_RATE(int ped, int v) { C("SET_CHAR_SHOOT_RATE", ped, v); }
        public static void SET_CHAR_WILL_DO_DRIVEBYS(int ped, bool v) { C("SET_CHAR_WILL_DO_DRIVEBYS", ped, v); }
        public static void SET_CHAR_WILL_USE_COVER(int ped, bool v) { C("SET_CHAR_WILL_USE_COVER", ped, v); }
        public static void SET_CHAR_WILL_USE_CARS_IN_COMBAT(int ped, bool v) { C("SET_CHAR_WILL_USE_CARS_IN_COMBAT", ped, v); }
        public static void SET_CHAR_WILL_LEAVE_CAR_IN_COMBAT(int ped, bool v) { C("SET_CHAR_WILL_LEAVE_CAR_IN_COMBAT", ped, v); }
        public static void SET_CHAR_CANT_BE_DRAGGED_OUT(int ped, bool v) { C("SET_CHAR_CANT_BE_DRAGGED_OUT", ped, v); }
        public static void SET_CHAR_STAY_IN_CAR_WHEN_JACKED(int ped, bool v) { C("SET_CHAR_STAY_IN_CAR_WHEN_JACKED", ped, v); }
        public static void SET_CHAR_SUFFERS_CRITICAL_HITS(int ped, bool v) { C("SET_CHAR_SUFFERS_CRITICAL_HITS", ped, v); }
        public static void SET_CHAR_DROPS_WEAPONS_WHEN_DEAD(int ped, bool v) { C("SET_CHAR_DROPS_WEAPONS_WHEN_DEAD", ped, v); }
        public static void CLEAR_CHAR_TASKS(int ped) { C("CLEAR_CHAR_TASKS", ped); }
        public static void ENABLE_CAM_COLLISION(int cam, bool enable) { C("ENABLE_CAM_COLLISION", cam, enable); }
        public static void SET_CAM_TARGET_PED(int cam, int ped) { C("SET_CAM_TARGET_PED", cam, ped); }
        public static bool IS_CHAR_DUCKING(int ped) { return B("IS_CHAR_DUCKING", ped); }
        public static bool IS_CHAR_GETTING_UP(int ped) { return B("IS_CHAR_GETTING_UP", ped); }
        public static bool IS_PED_RAGDOLL(int ped) { return B("IS_PED_RAGDOLL", ped); }
        public static bool IS_PED_DOING_DRIVEBY(int ped) { return B("IS_PED_DOING_DRIVEBY", ped); }
        public static void SET_CHAR_WILL_ONLY_FIRE_WITH_CLEAR_LOS(int ped, bool value) { C("SET_CHAR_WILL_ONLY_FIRE_WITH_CLEAR_LOS", ped, value); }
        public static void SET_CHAR_DECISION_MAKER_TO_DEFAULT(int ped) { C("SET_CHAR_DECISION_MAKER_TO_DEFAULT", ped); }
        public static void SET_BLOCKING_OF_NON_TEMPORARY_EVENTS(int ped, bool v) { C("SET_BLOCKING_OF_NON_TEMPORARY_EVENTS", ped, v); }
        public static void _TASK_ENTER_CAR_AS_DRIVER(int ped, int veh, uint ms) { C("TASK_ENTER_CAR_AS_DRIVER", ped, veh, (int)ms); }
        public static void _TASK_FOLLOW_NAV_MESH_TO_COORD(int ped, Vector3 p, int moveState) { C("TASK_FOLLOW_NAV_MESH_TO_COORD", ped, p.X, p.Y, p.Z, moveState, -1, 1.0f); }
        public static void _TASK_GO_STRAIGHT_TO_COORD(int ped, Vector3 p, int moveState) { C("TASK_GO_STRAIGHT_TO_COORD", ped, p.X, p.Y, p.Z, moveState, -1); }
        public static void SET_CAR_AS_MISSION_CAR(int veh) { C("SET_CAR_AS_MISSION_CAR", veh); }
        public static void SET_CHAR_RELATIONSHIP_GROUP(int ped, int group) { C("SET_CHAR_RELATIONSHIP_GROUP", ped, group); }
        public static bool IS_CHAR_SHOOTING(int ped) { return B("IS_CHAR_SHOOTING", ped); }
        public static void _TASK_SHOOT_AT_CHAR(int ped, int target, int ms, int mode) { C("TASK_SHOOT_AT_CHAR", ped, target, ms, mode); }
        public static void SET_CAR_FORWARD_SPEED(int veh, float v) { C("SET_CAR_FORWARD_SPEED", veh, v); }
        public static void CREATE_CHAR_INSIDE_CAR(int veh, uint type, uint model, out int ped) { Pointer r = PI(); C("CREATE_CHAR_INSIDE_CAR", veh, (int)type, (int)model, r); ped = (int)r; }
        public static void CREATE_CHAR_AS_PASSENGER(int veh, uint type, uint model, uint seat, out int ped) { Pointer r = PI(); C("CREATE_CHAR_AS_PASSENGER", veh, (int)type, (int)model, (int)seat, r); ped = (int)r; }
        public static void _TASK_WANDER_STANDARD(int ped) { C("TASK_WANDER_STANDARD", ped); }
        public static void _TASK_COMBAT_HATED_TARGETS_AROUND_CHAR(int ped, float radius) { C("TASK_COMBAT_HATED_TARGETS_AROUND_CHAR", ped, radius); }
        public static void _TASK_CAR_DRIVE_WANDER(int ped, int veh, float speed, uint style) { C("TASK_CAR_DRIVE_WANDER", ped, veh, speed, (int)style); }
        public static void _TASK_CAR_MISSION_PED_TARGET(int ped, int veh, int target, uint mission, float speed, uint style, uint a, uint b)
        {
            C("TASK_CAR_MISSION_PED_TARGET", ped, veh, target, (int)mission, speed, (int)style, (int)a, (int)b);
        }
        public static void _TASK_LEAVE_ANY_CAR(int ped) { C("TASK_LEAVE_ANY_CAR", ped); }
        public static void _TASK_SMART_FLEE_CHAR(int ped, int from, float dist, uint ms) { C("TASK_SMART_FLEE_CHAR", ped, from, dist, (int)ms); }
        // ---- bandas (1.5) ----
        public static bool HAS_CHAR_BEEN_DAMAGED_BY_CHAR(int ped, int other, bool reset) { return B("HAS_CHAR_BEEN_DAMAGED_BY_CHAR", ped, other, reset); }
        public static bool HAS_CHAR_SPOTTED_CHAR(int ped, int other) { return B("HAS_CHAR_SPOTTED_CHAR", ped, other); }
        public static void _TASK_ENTER_CAR_AS_PASSENGER(int ped, int veh, uint ms, uint seat) { C("TASK_ENTER_CAR_AS_PASSENGER", ped, veh, (int)ms, (int)seat); }
        public static bool IS_CAR_PASSENGER_SEAT_FREE(int veh, uint seat) { return B("IS_CAR_PASSENGER_SEAT_FREE", veh, (int)seat); }
        public static void GET_MAXIMUM_NUMBER_OF_PASSENGERS(int veh, out int max) { Pointer p = PI(); C("GET_MAXIMUM_NUMBER_OF_PASSENGERS", veh, p); max = (int)p; }
        public static bool IS_CHAR_IN_CAR(int ped, int veh) { return B("IS_CHAR_IN_CAR", ped, veh); }
        public static bool IS_CHAR_GETTING_IN_TO_A_CAR(int ped) { return B("IS_CHAR_GETTING_IN_TO_A_CAR", ped); }
        public static bool IS_CHAR_SITTING_IN_ANY_CAR(int ped) { return B("IS_CHAR_SITTING_IN_ANY_CAR", ped); }
        public static void GET_CHAR_IN_CAR_PASSENGER_SEAT(int veh, uint seat, out int ped) { Pointer p = PI(); C("GET_CHAR_IN_CAR_PASSENGER_SEAT", veh, (int)seat, p); ped = (int)p; }
        public static void _TASK_SEEK_COVER_FROM_PED(int ped, int threat, int ms) { C("TASK_SEEK_COVER_FROM_PED", ped, threat, ms); }
        public static void _TASK_DUCK(int ped, int ms) { C("TASK_DUCK", ped, ms); }
        public static void _TASK_PAUSE(int ped, int ms) { C("TASK_PAUSE", ped, ms); }
        public static void _TASK_AIM_GUN_AT_CHAR(int ped, int target, int ms) { C("TASK_AIM_GUN_AT_CHAR", ped, target, ms); }
        public static void SET_CHAR_MOVE_ANIM_SPEED_MULTIPLIER(int ped, float v) { C("SET_CHAR_MOVE_ANIM_SPEED_MULTIPLIER", ped, v); }
        public static void GET_CHAR_ARMOUR(int ped, out uint v) { Pointer p = PI(); C("GET_CHAR_ARMOUR", ped, p); v = (uint)(int)p; }
        public static bool IS_PED_IN_COVER(int ped) { return B("IS_PED_IN_COVER", ped); }
        public static void _TASK_CHAR_ARREST_CHAR(int cop, int ped) { C("TASK_CHAR_ARREST_CHAR", cop, ped); }
        public static void SET_LOUD_VEHICLE_RADIO(int veh, bool v) { C("SET_LOUD_VEHICLE_RADIO", veh, v); }
        // ---------------- 1.8: paseo intocable, baile, moverse apuntando, radio ----------------
        public static void SET_CHAR_NEVER_TARGETTED(int ped, bool v) { C("SET_CHAR_NEVER_TARGETTED", ped, v); }
        public static bool IS_CHAR_MALE(int ped) { return B("IS_CHAR_MALE", ped); }
        public static void REQUEST_ANIMS(string set) { C("REQUEST_ANIMS", set); }
        public static bool HAVE_ANIMS_LOADED(string set) { return B("HAVE_ANIMS_LOADED", set); }
        public static bool IS_CHAR_PLAYING_ANIM(int ped, string set, string anim) { return B("IS_CHAR_PLAYING_ANIM", ped, set, anim); }
        /// <summary>TASK_PLAY_ANIM(ped, anim, set, velocidad, loop, x, y, z, ms) (gtamods: x/y/z = 0).</summary>
        public static void _TASK_PLAY_ANIM(int ped, string anim, string set, float speed, bool loop, int ms)
        { C("TASK_PLAY_ANIM", ped, anim, set, speed, loop ? 1 : 0, 0, 0, 0, ms); }
        public static void _TASK_STAND_STILL(int ped, int ms) { C("TASK_STAND_STILL", ped, ms); }
        /// <summary>
        /// Ir hasta un punto apuntando a alguien (gtamods: ped, x, y, z, modo de andar, 0, 0, tipo de apunte (2 = a un
        /// personaje), x/y/z para apuntar, personaje). Modo 2 = caminando, 4 = corriendo.
        /// </summary>
        public static void _TASK_GO_TO_COORD_WHILE_AIMING(int ped, Vector3 to, int move, Vector3 aim, int target)
        { C("TASK_GO_TO_COORD_WHILE_AIMING", ped, to.X, to.Y, to.Z, move, 0, 0, target != 0 ? 2 : 0, aim.X, aim.Y, aim.Z, target); }
        public static void SET_MOBILE_RADIO_ENABLED_DURING_GAMEPLAY(bool v) { C("SET_MOBILE_RADIO_ENABLED_DURING_GAMEPLAY", v); }
        public static void SET_MOBILE_PHONE_RADIO_STATE(bool v) { C("SET_MOBILE_PHONE_RADIO_STATE", v); }
        public static bool IS_MOBILE_PHONE_RADIO_ACTIVE() { return B("IS_MOBILE_PHONE_RADIO_ACTIVE"); }
        public static void RETUNE_RADIO_TO_STATION_INDEX(uint idx) { C("RETUNE_RADIO_TO_STATION_INDEX", (int)idx); }
        public static void RETUNE_RADIO_UP() { C("RETUNE_RADIO_UP"); }
        // ---------------- 1.9 ----------------
        public static void CLEAR_CHAR_RELATIONSHIP(int ped, uint rel, int group) { C("CLEAR_CHAR_RELATIONSHIP", ped, (int)rel, group); }
        public static void CREATE_CHAR(uint type, int model, Vector3 p, out int ped) { Pointer r = PI(); C("CREATE_CHAR", (int)type, model, p.X, p.Y, p.Z, r, true); ped = (int)r; }
        public static void OPEN_CAR_DOOR(int car, uint door) { C("OPEN_CAR_DOOR", car, (int)door); }
        public static void SET_CAR_ENGINE_ON(int car, bool on, bool instant) { C("SET_CAR_ENGINE_ON", car, on, instant); }
        public static void SET_CAR_PROOFS(int car, bool bullet, bool fire, bool explosion, bool collision, bool melee) { C("SET_CAR_PROOFS", car, bullet, fire, explosion, collision, melee); }
        public static void _TASK_CHAT_WITH_CHAR(int ped, int other, bool a, bool b) { C("TASK_CHAT_WITH_CHAR", ped, other, a, b); }
        public static void _TASK_TURN_CHAR_TO_FACE_CHAR(int ped, int other) { C("TASK_TURN_CHAR_TO_FACE_CHAR", ped, other); }
        public static void GET_AMMO_IN_CHAR_WEAPON(int ped, int weapon, out int ammo) { Pointer r = PI(); C("GET_AMMO_IN_CHAR_WEAPON", ped, weapon, r); ammo = (int)r; }
        public static void SET_CHAR_AMMO(int ped, int weapon, int ammo) { C("SET_CHAR_AMMO", ped, weapon, ammo); }
        public static bool IS_CHAR_IN_WATER(int ped) { return B("IS_CHAR_IN_WATER", ped); }
        public static bool IS_EXPLOSION_IN_SPHERE(int type, Vector3 p, float r) { return B("IS_EXPLOSION_IN_SPHERE", type, p.X, p.Y, p.Z, r); }
        public static void SET_CAR_CAN_BE_VISIBLY_DAMAGED(int car, bool v) { C("SET_CAR_CAN_BE_VISIBLY_DAMAGED", car, v); }
        public static void SET_CHAR_RANDOM_COMPONENT_VARIATION(int ped) { C("SET_CHAR_RANDOM_COMPONENT_VARIATION", ped); }
        public static void _TASK_JUMP(int ped, bool flag) { C("TASK_JUMP", ped, flag); }
        public static void _TASK_HANDS_UP(int ped, int ms) { C("TASK_HANDS_UP", ped, ms); }
        public static void _TASK_COWER(int ped) { C("TASK_COWER", ped); }
        public static bool SWITCH_PED_TO_RAGDOLL(int ped, int a, int ms, bool f0, bool f1, bool f2, bool f3) { return B("SWITCH_PED_TO_RAGDOLL", ped, a, ms, f0, f1, f2, f3); }
        public static void SET_CHAR_AS_MISSION_CHAR(int ped) { C("SET_CHAR_AS_MISSION_CHAR", ped); }
        public static void MUTE_POSITIONED_RADIO(bool v) { C("MUTE_POSITIONED_RADIO", v); }
        public static void _TASK_CAR_MISSION_COORS_TARGET(int ped, int veh, Vector3 p, uint mission, float speed, uint style, uint a, uint b)
        {
            C("TASK_CAR_MISSION_COORS_TARGET", ped, veh, p.X, p.Y, p.Z, (int)mission, speed, (int)style, (int)a, (int)b);
        }
        public static bool HAS_CHAR_BEEN_ARRESTED(int ped) { return B("HAS_CHAR_BEEN_ARRESTED", ped); }
        public static void GET_PED_TYPE(int ped, out uint type) { Pointer p = PI(); C("GET_PED_TYPE", ped, p); type = (uint)(int)p; }
        public static void SET_SENSE_RANGE(int ped, float v) { C("SET_SENSE_RANGE", ped, v); }
        public static void SET_CHAR_WILL_MOVE_WHEN_INJURED(int ped, bool v) { C("SET_CHAR_WILL_MOVE_WHEN_INJURED", ped, v); }
        public static bool CREATE_EMERGENCY_SERVICES_CAR_RETURN_DRIVER(uint model, Vector3 p, out int car, out int driver, out int passenger)
        {
            Pointer v = PI(), d = PI(), s = PI();
            bool ok = B("CREATE_EMERGENCY_SERVICES_CAR_RETURN_DRIVER", (int)model, p.X, p.Y, p.Z, v, d, s);
            car = (int)v; driver = (int)d; passenger = (int)s;
            return ok;
        }
        public static void GET_CURRENT_BASIC_COP_MODEL(out uint model) { Pointer p = PI(); C("GET_CURRENT_BASIC_COP_MODEL", p); model = (uint)(int)p; }
        public static void SWITCH_CAR_SIREN(int veh, bool v) { C("SWITCH_CAR_SIREN", veh, v); }
        public static void GIVE_PED_FAKE_NETWORK_NAME(int ped, string name, int r, int g, int b, int a) { C("GIVE_PED_FAKE_NETWORK_NAME", ped, name, r, g, b, a); }
        public static void REMOVE_FAKE_NETWORK_NAME_FROM_PED(int ped) { C("REMOVE_FAKE_NETWORK_NAME_FROM_PED", ped); }
        public static bool GET_SAFE_POSITION_FOR_CHAR(Vector3 p, bool onGround, out Vector3 result)
        {
            Pointer x = PF(), y = PF(), z = PF();
            bool ok = B("GET_SAFE_POSITION_FOR_CHAR", p.X, p.Y, p.Z, onGround, x, y, z);
            result = V(x, y, z);
            return ok;
        }

        // ---------------- vehiculos y modelos ----------------
        public static bool DOES_VEHICLE_EXIST(int veh) { return B("DOES_VEHICLE_EXIST", veh); }
        public static bool IS_CAR_DEAD(int veh) { return B("IS_CAR_DEAD", veh); }
        public static void GET_CAR_COORDINATES(int veh, out Vector3 pos)
        {
            Pointer x = PF(), y = PF(), z = PF();
            C("GET_CAR_COORDINATES", veh, x, y, z);
            pos = V(x, y, z);
        }
        public static void SET_CAR_COORDINATES(int veh, Vector3 p) { C("SET_CAR_COORDINATES", veh, p.X, p.Y, p.Z); }
        public static void SET_CAR_HEADING(int veh, float h) { C("SET_CAR_HEADING", veh, h); }
        public static void GET_CAR_MODEL(int veh, out uint model) { Pointer p = PI(); C("GET_CAR_MODEL", veh, p); model = (uint)(int)p; }
        public static void EXPLODE_CAR(int veh, bool a, bool b) { C("EXPLODE_CAR", veh, a, b); }
        public static void CREATE_CAR(int model, Vector3 p, out int veh, bool b)
        {
            Pointer v = PI();
            C("CREATE_CAR", model, p.X, p.Y, p.Z, v, b);
            veh = (int)v;
        }
        public static void MARK_CAR_AS_NO_LONGER_NEEDED(int veh) { Pointer p = veh; C("MARK_CAR_AS_NO_LONGER_NEEDED", p); }
        public static void APPLY_FORCE_TO_CAR(int veh, int type, float x, float y, float z, float sx, float sy, float sz, int a, int b, int c, int d)
        {
            C("APPLY_FORCE_TO_CAR", veh, type, x, y, z, sx, sy, sz, a, b, c, d);
        }
        public static bool IS_THIS_MODEL_A_TRAIN(uint model) { return B("IS_THIS_MODEL_A_TRAIN", (int)model); }
        public static bool IS_THIS_MODEL_A_HELI(uint model) { return B("IS_THIS_MODEL_A_HELI", (int)model); }
        public static int GET_HASH_KEY(string s) { return I("GET_HASH_KEY", s); }
        public static bool IS_MODEL_IN_CDIMAGE(int model) { return B("IS_MODEL_IN_CDIMAGE", model); }
        public static void REQUEST_MODEL(int model) { C("REQUEST_MODEL", model); }
        public static bool HAS_MODEL_LOADED(int model) { return B("HAS_MODEL_LOADED", model); }
        public static void MARK_MODEL_AS_NO_LONGER_NEEDED(int model) { C("MARK_MODEL_AS_NO_LONGER_NEEDED", model); }
        public static void GET_CURRENT_BASIC_POLICE_CAR_MODEL(out uint model) { Pointer p = PI(); C("GET_CURRENT_BASIC_POLICE_CAR_MODEL", p); model = (uint)(int)p; }
        public static bool CREATE_EMERGENCY_SERVICES_CAR_THEN_WALK(uint model, Vector3 p) { return B("CREATE_EMERGENCY_SERVICES_CAR_THEN_WALK", (int)model, p.X, p.Y, p.Z); }
        public static bool IS_CAR_ON_SCREEN(int veh) { return B("IS_CAR_ON_SCREEN", veh); }
        public static bool IS_CAR_UPSIDEDOWN(int veh) { return B("IS_CAR_UPSIDEDOWN", veh); }
        public static bool IS_CAR_STUCK_ON_ROOF(int veh) { return B("IS_CAR_STUCK_ON_ROOF", veh); }
        public static bool IS_CAR_STUCK(int veh) { return B("IS_CAR_STUCK", veh); }
        public static bool IS_CAR_ON_FIRE(int veh) { return B("IS_CAR_ON_FIRE", veh); }
        public static bool IS_EMERGENCY_SERVICES_VEHICLE(int veh) { return B("IS_EMERGENCY_SERVICES_VEHICLE", veh); }
        public static void GET_CAR_HEALTH(int veh, out uint health) { Pointer p = PI(); C("GET_CAR_HEALTH", veh, p); health = (uint)(int)p; }
        public static void GET_CAR_SPEED(int veh, out float speed) { Pointer p = PF(); C("GET_CAR_SPEED", veh, p); speed = (float)p; }
        public static void GET_DRIVER_OF_CAR(int veh, out int ped) { Pointer p = PI(); C("GET_DRIVER_OF_CAR", veh, p); ped = (int)p; }
        public static void CLEAR_AREA_OF_CARS(Vector3 p, float radius) { C("CLEAR_AREA_OF_CARS", p.X, p.Y, p.Z, radius); }
        public static void CLEAR_AREA_OF_CHARS(Vector3 p, float radius) { C("CLEAR_AREA_OF_CHARS", p.X, p.Y, p.Z, radius); }
        public static void CLEAR_AREA_OF_OBJECTS(Vector3 p, float radius) { C("CLEAR_AREA_OF_OBJECTS", p.X, p.Y, p.Z, radius); }
        public static void CLEAR_AREA_OF_COPS(Vector3 p, float radius) { C("CLEAR_AREA_OF_COPS", p.X, p.Y, p.Z, radius); }
        public static void EXTINGUISH_FIRE_AT_POINT(Vector3 p, float radius) { C("EXTINGUISH_FIRE_AT_POINT", p.X, p.Y, p.Z, radius); }
        public static void SET_PED_DENSITY_MULTIPLIER(float v) { C("SET_PED_DENSITY_MULTIPLIER", v); }
        public static void SET_CAR_DENSITY_MULTIPLIER(float v) { C("SET_CAR_DENSITY_MULTIPLIER", v); }

        // ---------------- mundo ----------------
        public static void ADD_EXPLOSION(Vector3 p, int type, float radius, bool sound, bool invisible, float shake) { C("ADD_EXPLOSION", p.X, p.Y, p.Z, type, radius, sound, invisible, shake); }
        public static int START_SCRIPT_FIRE(Vector3 p, uint gens, uint strength) { return I("START_SCRIPT_FIRE", p.X, p.Y, p.Z, (int)gens, (int)strength); }
        public static void REMOVE_SCRIPT_FIRE(int fire) { C("REMOVE_SCRIPT_FIRE", fire); }
        public static bool DOES_SCRIPT_FIRE_EXIST(int fire) { return B("DOES_SCRIPT_FIRE_EXIST", fire); }
        public static void GET_GROUND_Z_FOR_3D_COORD(Vector3 p, out float z) { Pointer r = PF(); C("GET_GROUND_Z_FOR_3D_COORD", p.X, p.Y, p.Z, r); z = (float)r; }
        public static bool GET_WATER_HEIGHT(Vector3 p, out float h) { Pointer r = PF(); bool ok = B("GET_WATER_HEIGHT", p.X, p.Y, p.Z, r); h = (float)r; return ok; }
        public static void LOAD_SCENE(Vector3 p) { C("LOAD_SCENE", p.X, p.Y, p.Z); }
        public static void REQUEST_COLLISION_AT_POSN(Vector3 p) { C("REQUEST_COLLISION_AT_POSN", p.X, p.Y, p.Z); }
        public static bool GET_NTH_CLOSEST_CAR_NODE_WITH_HEADING(Vector3 p, uint n, out Vector3 result, out float heading)
        {
            Pointer x = PF(), y = PF(), z = PF(), h = PF();
            bool ok = B("GET_NTH_CLOSEST_CAR_NODE_WITH_HEADING", p.X, p.Y, p.Z, (int)n, x, y, z, h);
            result = V(x, y, z);
            heading = (float)h;
            return ok;
        }
        public static void FORCE_WEATHER_NOW(uint w) { C("FORCE_WEATHER_NOW", (int)w); }
        public static void RELEASE_WEATHER() { C("RELEASE_WEATHER"); }
        public static void SET_TIME_OF_DAY(uint h, uint m) { C("SET_TIME_OF_DAY", (int)h, (int)m); }
        public static void SET_TIME_SCALE(float s) { C("SET_TIME_SCALE", s); }

        // ---------------- camara y pantalla ----------------
        public static void CREATE_CAM(int type, out int cam) { Pointer p = PI(); C("CREATE_CAM", type, p); cam = (int)p; }
        public static void DESTROY_CAM(int cam) { C("DESTROY_CAM", cam); }
        public static bool DOES_CAM_EXIST(int cam) { return B("DOES_CAM_EXIST", cam); }
        public static bool IS_CAM_ACTIVE(int cam) { return B("IS_CAM_ACTIVE", cam); }
        public static void SET_CAM_ACTIVE(int cam, bool v) { C("SET_CAM_ACTIVE", cam, v); }
        public static void SET_CAM_PROPAGATE(int cam, bool v) { C("SET_CAM_PROPAGATE", cam, v); }
        public static void ACTIVATE_SCRIPTED_CAMS(bool a, bool b) { C("ACTIVATE_SCRIPTED_CAMS", a, b); }
        public static void SET_CAM_POS(int cam, Vector3 p) { C("SET_CAM_POS", cam, p.X, p.Y, p.Z); }
        public static void SET_CAM_ROT(int cam, Vector3 r) { C("SET_CAM_ROT", cam, r.X, r.Y, r.Z); }
        public static void SET_CAM_FOV(int cam, float fov) { C("SET_CAM_FOV", cam, fov); }
        public static void GET_GAME_CAM(out int cam) { Pointer p = PI(); C("GET_GAME_CAM", p); cam = (int)p; }
        public static void GET_CAM_POS(int cam, out Vector3 pos) { Pointer x = PF(), y = PF(), z = PF(); C("GET_CAM_POS", cam, x, y, z); pos = V(x, y, z); }
        public static void GET_CAM_ROT(int cam, out Vector3 rot) { Pointer x = PF(), y = PF(), z = PF(); C("GET_CAM_ROT", cam, x, y, z); rot = V(x, y, z); }
        public static void GET_CAM_FOV(int cam, out float fov) { Pointer p = PF(); C("GET_CAM_FOV", cam, p); fov = (float)p; }
        public static void DO_SCREEN_FADE_OUT(uint ms) { C("DO_SCREEN_FADE_OUT", (int)ms); }
        public static void DO_SCREEN_FADE_IN(uint ms) { C("DO_SCREEN_FADE_IN", (int)ms); }
        public static bool IS_SCREEN_FADED_OUT() { return B("IS_SCREEN_FADED_OUT"); }
        public static void DISPLAY_HUD(bool v) { C("DISPLAY_HUD", v); }
        public static void DISPLAY_RADAR(bool v) { C("DISPLAY_RADAR", v); }
        public static void HIDE_HUD_AND_RADAR_THIS_FRAME() { C("HIDE_HUD_AND_RADAR_THIS_FRAME"); }
        public static void CLEAR_HELP() { C("CLEAR_HELP"); }
        public static bool IS_PAUSE_MENU_ACTIVE() { return B("IS_PAUSE_MENU_ACTIVE"); }
        public static void DISABLE_PAUSE_MENU(bool v) { C("DISABLE_PAUSE_MENU", v); }
        public static void SET_TEXT_INPUT_ACTIVE(bool v) { C("SET_TEXT_INPUT_ACTIVE", v); }
        public static void DRAW_CHECKPOINT(Vector3 p, float radius, Color c) { C("DRAW_CHECKPOINT", p.X, p.Y, p.Z, radius, (int)c.R, (int)c.G, (int)c.B); }
        public static void GET_MOUSE_INPUT(out Point delta)
        {
            Pointer x = PI(), y = PI();
            C("GET_MOUSE_INPUT", x, y);
            delta = new Point((int)x, (int)y);
        }
    }
}
