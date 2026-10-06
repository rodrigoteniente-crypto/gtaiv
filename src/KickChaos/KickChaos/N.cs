using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using System.Reflection;
using GTA.Native;

namespace KickChaos;

public static class N
{
	private static readonly HashSet<string> missing = new HashSet<string>();

	private static void C(string name, params Parameter[] args)
	{
		if (!missing.Contains(name))
		{
			Function.Call(name, args);
		}
	}

	private static bool B(string name, params Parameter[] args)
	{
		if (missing.Contains(name))
		{
			return false;
		}
		return Function.Call<bool>(name, args);
	}

	private static int I(string name, params Parameter[] args)
	{
		if (missing.Contains(name))
		{
			return 0;
		}
		return Function.Call<int>(name, args);
	}

	public static bool IsMissing(string name)
	{
		return missing.Contains(name);
	}

	public static List<string> Validate()
	{
		List<string> list = new List<string>();
		int address;
		try
		{
			address = Function.GetAddress("KICKCHAOS_NATIVE_QUE_NO_EXISTE");
		}
		catch
		{
			return list;
		}
		MethodInfo[] methods = typeof(N).GetMethods(BindingFlags.Static | BindingFlags.Public);
		HashSet<string> hashSet = new HashSet<string>();
		MethodInfo[] array = methods;
		foreach (MethodInfo methodInfo in array)
		{
			if (methodInfo.Name.ToUpperInvariant() == methodInfo.Name && methodInfo.Name.Length > 3)
			{
				hashSet.Add((!methodInfo.Name.StartsWith("_")) ? methodInfo.Name : methodInfo.Name.Substring(1));
			}
		}
		foreach (string item in hashSet)
		{
			int address2;
			try
			{
				address2 = Function.GetAddress(item);
			}
			catch
			{
				continue;
			}
			if (address2 == address)
			{
				list.Add(item);
			}
		}
		if (list.Count > hashSet.Count / 3)
		{
			list.Clear();
			return list;
		}
		foreach (string item2 in list)
		{
			missing.Add(item2);
		}
		return list;
	}

	private static GTA.Native.Pointer PI()
	{
		return new GTA.Native.Pointer(typeof(int));
	}

	private static GTA.Native.Pointer PF()
	{
		return new GTA.Native.Pointer(typeof(float));
	}

	private static Vector3 V(GTA.Native.Pointer x, GTA.Native.Pointer y, GTA.Native.Pointer z)
	{
		return new Vector3(x, y, z);
	}

	public static int GET_PLAYER_ID()
	{
		return I("GET_PLAYER_ID");
	}

	public static int CONVERT_INT_TO_PLAYERINDEX(int id)
	{
		return I("CONVERT_INT_TO_PLAYERINDEX", id);
	}

	public static void GET_PLAYER_CHAR(int player, out int ped)
	{
		GTA.Native.Pointer pointer = PI();
		C("GET_PLAYER_CHAR", player, pointer);
		ped = pointer;
	}

	public static bool IS_PLAYER_PLAYING(int player)
	{
		return B("IS_PLAYER_PLAYING", player);
	}

	public static void SET_PLAYER_CONTROL(int player, bool v)
	{
		C("SET_PLAYER_CONTROL", player, v);
	}

	public static void SET_PLAYER_INVINCIBLE(int player, bool v)
	{
		C("SET_PLAYER_INVINCIBLE", player, v);
	}

	public static void SET_EVERYONE_IGNORE_PLAYER(int player, bool v)
	{
		C("SET_EVERYONE_IGNORE_PLAYER", player, v);
	}

	public static void SET_POLICE_IGNORE_PLAYER(int player, bool v)
	{
		C("SET_POLICE_IGNORE_PLAYER", player, v);
	}

	public static void ALTER_WANTED_LEVEL(int player, uint lvl)
	{
		C("ALTER_WANTED_LEVEL", player, (int)lvl);
	}

	public static void APPLY_WANTED_LEVEL_CHANGE_NOW(int player)
	{
		C("APPLY_WANTED_LEVEL_CHANGE_NOW", player);
	}

	public static void CLEAR_WANTED_LEVEL(int player)
	{
		C("CLEAR_WANTED_LEVEL", player);
	}

	public static void SET_MAX_WANTED_LEVEL(uint lvl)
	{
		C("SET_MAX_WANTED_LEVEL", (int)lvl);
	}

	public static bool IS_PLAYER_BEING_ARRESTED()
	{
		return B("IS_PLAYER_BEING_ARRESTED");
	}

	public static bool DOES_CHAR_EXIST(int ped)
	{
		return B("DOES_CHAR_EXIST", ped);
	}

	public static bool IS_CHAR_DEAD(int ped)
	{
		return B("IS_CHAR_DEAD", ped);
	}

	public static bool IS_CHAR_IN_ANY_CAR(int ped)
	{
		return B("IS_CHAR_IN_ANY_CAR", ped);
	}

	public static void GET_CAR_CHAR_IS_USING(int ped, out int veh)
	{
		GTA.Native.Pointer pointer = PI();
		C("GET_CAR_CHAR_IS_USING", ped, pointer);
		veh = pointer;
	}

	public static void GET_CHAR_COORDINATES(int ped, out Vector3 pos)
	{
		GTA.Native.Pointer pointer = PF();
		GTA.Native.Pointer pointer2 = PF();
		GTA.Native.Pointer pointer3 = PF();
		C("GET_CHAR_COORDINATES", ped, pointer, pointer2, pointer3);
		pos = V(pointer, pointer2, pointer3);
	}

	public static void SET_CHAR_COORDINATES(int ped, Vector3 p)
	{
		C("SET_CHAR_COORDINATES", ped, p.X, p.Y, p.Z);
	}

	public static void GET_CHAR_HEADING(int ped, out float h)
	{
		GTA.Native.Pointer pointer = PF();
		C("GET_CHAR_HEADING", ped, pointer);
		h = pointer;
	}

	public static void SET_CHAR_HEADING(int ped, float h)
	{
		C("SET_CHAR_HEADING", ped, h);
	}

	public static void SET_CHAR_VISIBLE(int ped, bool v)
	{
		C("SET_CHAR_VISIBLE", ped, v);
	}

	public static void SET_CHAR_INVINCIBLE(int ped, bool v)
	{
		C("SET_CHAR_INVINCIBLE", ped, v);
	}

	public static void SET_CHAR_COLLISION(int ped, bool v)
	{
		C("SET_CHAR_COLLISION", ped, v);
	}

	public static void FREEZE_CHAR_POSITION(int ped, bool v)
	{
		C("FREEZE_CHAR_POSITION", ped, v);
	}

	public static void SET_CHAR_PROOFS(int ped, bool a, bool b, bool c, bool d, bool e)
	{
		C("SET_CHAR_PROOFS", ped, a, b, c, d, e);
	}

	public static void SET_CHAR_KEEP_TASK(int ped, bool v)
	{
		C("SET_CHAR_KEEP_TASK", ped, v);
	}

	public static void SET_CURRENT_CHAR_WEAPON(int ped, int weapon, bool b)
	{
		C("SET_CURRENT_CHAR_WEAPON", ped, weapon, b);
	}

	public static void GIVE_WEAPON_TO_CHAR(int ped, int weapon, int ammo, bool b)
	{
		C("GIVE_WEAPON_TO_CHAR", ped, weapon, ammo, b);
	}

	public static void WARP_CHAR_FROM_CAR_TO_COORD(int ped, Vector3 p)
	{
		C("WARP_CHAR_FROM_CAR_TO_COORD", ped, p.X, p.Y, p.Z);
	}

	public static void WARP_CHAR_INTO_CAR(int ped, int veh)
	{
		C("WARP_CHAR_INTO_CAR", ped, veh);
	}

	public static void CLEAR_ROOM_FOR_CHAR(int ped)
	{
		C("CLEAR_ROOM_FOR_CHAR", ped);
	}

	public static void GET_KEY_FOR_CHAR_IN_ROOM(int ped, out uint key)
	{
		GTA.Native.Pointer pointer = PI();
		C("GET_KEY_FOR_CHAR_IN_ROOM", ped, pointer);
		key = (uint)(int)pointer;
	}

	public static void SET_ROOM_FOR_CHAR_BY_KEY(int ped, uint key)
	{
		C("SET_ROOM_FOR_CHAR_BY_KEY", ped, (int)key);
	}

	public static void _TASK_COMBAT(int ped, int target)
	{
		C("TASK_COMBAT", ped, target);
	}

	public static void _TASK_SMART_FLEE_POINT(int ped, float x, float y, float z, float dist, uint ms)
	{
		C("TASK_SMART_FLEE_POINT", ped, x, y, z, dist, (int)ms);
	}

	public static void CREATE_RANDOM_CHAR(Vector3 p, out int ped)
	{
		GTA.Native.Pointer pointer = PI();
		C("CREATE_RANDOM_CHAR", p.X, p.Y, p.Z, pointer);
		ped = pointer;
	}

	public static void CREATE_RANDOM_CHAR_AS_DRIVER(int veh, out int ped)
	{
		GTA.Native.Pointer pointer = PI();
		C("CREATE_RANDOM_CHAR_AS_DRIVER", veh, pointer);
		ped = pointer;
	}

	public static void DELETE_CHAR(int ped)
	{
		GTA.Native.Pointer pointer = ped;
		C("DELETE_CHAR", pointer);
	}

	public static void MARK_CHAR_AS_NO_LONGER_NEEDED(int ped)
	{
		GTA.Native.Pointer pointer = ped;
		C("MARK_CHAR_AS_NO_LONGER_NEEDED", pointer);
	}

	public static void GET_CHAR_MODEL(int ped, out uint model)
	{
		GTA.Native.Pointer pointer = PI();
		C("GET_CHAR_MODEL", ped, pointer);
		model = (uint)(int)pointer;
	}

	public static void SET_CHAR_MAX_HEALTH(int ped, uint v)
	{
		C("SET_CHAR_MAX_HEALTH", ped, (int)v);
	}

	public static void SET_CHAR_HEALTH(int ped, uint v)
	{
		C("SET_CHAR_HEALTH", ped, (int)v);
	}

	public static void GET_CHAR_HEALTH(int ped, out uint v)
	{
		GTA.Native.Pointer pointer = PI();
		C("GET_CHAR_HEALTH", ped, pointer);
		v = (uint)(int)pointer;
	}

	public static void ADD_ARMOUR_TO_CHAR(int ped, int v)
	{
		C("ADD_ARMOUR_TO_CHAR", ped, v);
	}

	public static void GET_CHAR_SPEED(int ped, out float v)
	{
		GTA.Native.Pointer pointer = PF();
		C("GET_CHAR_SPEED", ped, pointer);
		v = pointer;
	}

	public static bool IS_CHAR_FATALLY_INJURED(int ped)
	{
		return B("IS_CHAR_FATALLY_INJURED", ped);
	}

	public static bool IS_PED_IN_COMBAT(int ped)
	{
		return B("IS_PED_IN_COMBAT", ped);
	}

	public static bool IS_CHAR_ON_SCREEN(int ped)
	{
		return B("IS_CHAR_ON_SCREEN", ped);
	}

	public static void SET_CHAR_RELATIONSHIP(int ped, uint level, int group)
	{
		C("SET_CHAR_RELATIONSHIP", ped, (int)level, group);
	}

	public static void SET_CHAR_WANTED_BY_POLICE(int ped, bool v)
	{
		C("SET_CHAR_WANTED_BY_POLICE", ped, v);
	}

	public static void SET_CHAR_IS_TARGET_PRIORITY(int ped, bool v)
	{
		C("SET_CHAR_IS_TARGET_PRIORITY", ped, v);
	}

	public static void SET_CHAR_ACCURACY(int ped, uint v)
	{
		C("SET_CHAR_ACCURACY", ped, (int)v);
	}

	public static void SET_CHAR_SHOOT_RATE(int ped, int v)
	{
		C("SET_CHAR_SHOOT_RATE", ped, v);
	}

	public static void SET_CHAR_WILL_DO_DRIVEBYS(int ped, bool v)
	{
		C("SET_CHAR_WILL_DO_DRIVEBYS", ped, v);
	}

	public static void SET_CHAR_WILL_USE_COVER(int ped, bool v)
	{
		C("SET_CHAR_WILL_USE_COVER", ped, v);
	}

	public static void SET_CHAR_WILL_USE_CARS_IN_COMBAT(int ped, bool v)
	{
		C("SET_CHAR_WILL_USE_CARS_IN_COMBAT", ped, v);
	}

	public static void SET_CHAR_WILL_LEAVE_CAR_IN_COMBAT(int ped, bool v)
	{
		C("SET_CHAR_WILL_LEAVE_CAR_IN_COMBAT", ped, v);
	}

	public static void SET_CHAR_CANT_BE_DRAGGED_OUT(int ped, bool v)
	{
		C("SET_CHAR_CANT_BE_DRAGGED_OUT", ped, v);
	}

	public static void SET_CHAR_STAY_IN_CAR_WHEN_JACKED(int ped, bool v)
	{
		C("SET_CHAR_STAY_IN_CAR_WHEN_JACKED", ped, v);
	}

	public static void SET_CHAR_SUFFERS_CRITICAL_HITS(int ped, bool v)
	{
		C("SET_CHAR_SUFFERS_CRITICAL_HITS", ped, v);
	}

	public static void SET_CHAR_DROPS_WEAPONS_WHEN_DEAD(int ped, bool v)
	{
		C("SET_CHAR_DROPS_WEAPONS_WHEN_DEAD", ped, v);
	}

	public static void CLEAR_CHAR_TASKS(int ped)
	{
		C("CLEAR_CHAR_TASKS", ped);
	}

	public static void SET_BLOCKING_OF_NON_TEMPORARY_EVENTS(int ped, bool v)
	{
		C("SET_BLOCKING_OF_NON_TEMPORARY_EVENTS", ped, v);
	}

	public static void _TASK_ENTER_CAR_AS_DRIVER(int ped, int veh, uint ms)
	{
		C("TASK_ENTER_CAR_AS_DRIVER", ped, veh, (int)ms);
	}

	public static void _TASK_FOLLOW_NAV_MESH_TO_COORD(int ped, Vector3 p, int moveState)
	{
		C("TASK_FOLLOW_NAV_MESH_TO_COORD", ped, p.X, p.Y, p.Z, moveState, -1, 1f);
	}

	public static void _TASK_GO_STRAIGHT_TO_COORD(int ped, Vector3 p, int moveState)
	{
		C("TASK_GO_STRAIGHT_TO_COORD", ped, p.X, p.Y, p.Z, moveState, -1);
	}

	public static void DELETE_CAR(int car)
	{
		Pointer pointer = car;
		C("DELETE_CAR", pointer);
	}

	public static void SET_CAR_AS_MISSION_CAR(int veh)
	{
		C("SET_CAR_AS_MISSION_CAR", veh);
	}

	public static void CLEAR_CHAR_RELATIONSHIP(int ped, int level, int group)
	{
		C("CLEAR_CHAR_RELATIONSHIP", ped, level, group);
	}

	public static void SET_CHAR_RELATIONSHIP_GROUP(int ped, int group)
	{
		C("SET_CHAR_RELATIONSHIP_GROUP", ped, group);
	}

	public static bool IS_CHAR_SHOOTING(int ped)
	{
		return B("IS_CHAR_SHOOTING", ped);
	}

	public static void _TASK_SHOOT_AT_CHAR(int ped, int target, int ms, int mode)
	{
		C("TASK_SHOOT_AT_CHAR", ped, target, ms, mode);
	}

	public static void SET_CAR_FORWARD_SPEED(int veh, float v)
	{
		C("SET_CAR_FORWARD_SPEED", veh, v);
	}

	public static void CREATE_CHAR_INSIDE_CAR(int veh, uint type, uint model, out int ped)
	{
		GTA.Native.Pointer pointer = PI();
		C("CREATE_CHAR_INSIDE_CAR", veh, (int)type, (int)model, pointer);
		ped = pointer;
	}

	public static void CREATE_CHAR_AS_PASSENGER(int veh, uint type, uint model, uint seat, out int ped)
	{
		GTA.Native.Pointer pointer = PI();
		C("CREATE_CHAR_AS_PASSENGER", veh, (int)type, (int)model, (int)seat, pointer);
		ped = pointer;
	}

	public static void _TASK_WANDER_STANDARD(int ped)
	{
		C("TASK_WANDER_STANDARD", ped);
	}

	public static void _TASK_COMBAT_HATED_TARGETS_AROUND_CHAR(int ped, float radius)
	{
		C("TASK_COMBAT_HATED_TARGETS_AROUND_CHAR", ped, radius);
	}

	public static void _TASK_CAR_DRIVE_WANDER(int ped, int veh, float speed, uint style)
	{
		C("TASK_CAR_DRIVE_WANDER", ped, veh, speed, (int)style);
	}

	public static void _TASK_CAR_MISSION_PED_TARGET(int ped, int veh, int target, uint mission, float speed, uint style, uint a, uint b)
	{
		C("TASK_CAR_MISSION_PED_TARGET", ped, veh, target, (int)mission, speed, (int)style, (int)a, (int)b);
	}

	public static void _TASK_LEAVE_ANY_CAR(int ped)
	{
		C("TASK_LEAVE_ANY_CAR", ped);
	}

	public static void _TASK_SMART_FLEE_CHAR(int ped, int from, float dist, uint ms)
	{
		C("TASK_SMART_FLEE_CHAR", ped, from, dist, (int)ms);
	}

	public static bool HAS_CHAR_BEEN_DAMAGED_BY_CHAR(int ped, int other, bool reset)
	{
		return B("HAS_CHAR_BEEN_DAMAGED_BY_CHAR", ped, other, reset);
	}

	public static bool HAS_CHAR_SPOTTED_CHAR(int ped, int other)
	{
		return B("HAS_CHAR_SPOTTED_CHAR", ped, other);
	}

	public static void _TASK_ENTER_CAR_AS_PASSENGER(int ped, int veh, uint ms, uint seat)
	{
		C("TASK_ENTER_CAR_AS_PASSENGER", ped, veh, (int)ms, (int)seat);
	}

	public static bool IS_CAR_PASSENGER_SEAT_FREE(int veh, uint seat)
	{
		return B("IS_CAR_PASSENGER_SEAT_FREE", veh, (int)seat);
	}

	public static void GET_MAXIMUM_NUMBER_OF_PASSENGERS(int veh, out int max)
	{
		GTA.Native.Pointer pointer = PI();
		C("GET_MAXIMUM_NUMBER_OF_PASSENGERS", veh, pointer);
		max = pointer;
	}

	public static bool IS_CHAR_IN_CAR(int ped, int veh)
	{
		return B("IS_CHAR_IN_CAR", ped, veh);
	}

	public static bool IS_CHAR_GETTING_IN_TO_A_CAR(int ped)
	{
		return B("IS_CHAR_GETTING_IN_TO_A_CAR", ped);
	}

	public static bool IS_CHAR_SITTING_IN_ANY_CAR(int ped)
	{
		return B("IS_CHAR_SITTING_IN_ANY_CAR", ped);
	}

	public static void GET_CHAR_IN_CAR_PASSENGER_SEAT(int veh, uint seat, out int ped)
	{
		GTA.Native.Pointer pointer = PI();
		C("GET_CHAR_IN_CAR_PASSENGER_SEAT", veh, (int)seat, pointer);
		ped = pointer;
	}

	public static void _TASK_SEEK_COVER_FROM_PED(int ped, int threat, int ms)
	{
		C("TASK_SEEK_COVER_FROM_PED", ped, threat, ms);
	}

	public static void _TASK_DUCK(int ped, int ms)
	{
		C("TASK_DUCK", ped, ms);
	}

	public static void _TASK_PAUSE(int ped, int ms)
	{
		C("TASK_PAUSE", ped, ms);
	}

	public static void _TASK_AIM_GUN_AT_CHAR(int ped, int target, int ms)
	{
		C("TASK_AIM_GUN_AT_CHAR", ped, target, ms);
	}

	public static void SET_CHAR_MOVE_ANIM_SPEED_MULTIPLIER(int ped, float v)
	{
		C("SET_CHAR_MOVE_ANIM_SPEED_MULTIPLIER", ped, v);
	}

	public static void GET_CHAR_ARMOUR(int ped, out uint v)
	{
		GTA.Native.Pointer pointer = PI();
		C("GET_CHAR_ARMOUR", ped, pointer);
		v = (uint)(int)pointer;
	}

	public static bool IS_PED_IN_COVER(int ped)
	{
		return B("IS_PED_IN_COVER", ped);
	}

	public static void _TASK_CHAR_ARREST_CHAR(int cop, int ped)
	{
		C("TASK_CHAR_ARREST_CHAR", cop, ped);
	}

	public static void _TASK_HANDS_UP(int ped, int ms)
	{
		C("TASK_HANDS_UP", ped, ms);
	}

	public static bool HAS_CHAR_BEEN_ARRESTED(int ped)
	{
		return B("HAS_CHAR_BEEN_ARRESTED", ped);
	}

	public static void GET_PED_TYPE(int ped, out uint type)
	{
		GTA.Native.Pointer pointer = PI();
		C("GET_PED_TYPE", ped, pointer);
		type = (uint)(int)pointer;
	}

	public static void SET_SENSE_RANGE(int ped, float v)
	{
		C("SET_SENSE_RANGE", ped, v);
	}

	public static void SET_CHAR_WILL_MOVE_WHEN_INJURED(int ped, bool v)
	{
		C("SET_CHAR_WILL_MOVE_WHEN_INJURED", ped, v);
	}

	public static bool CREATE_EMERGENCY_SERVICES_CAR_RETURN_DRIVER(uint model, Vector3 p, out int car, out int driver, out int passenger)
	{
		GTA.Native.Pointer pointer = PI();
		GTA.Native.Pointer pointer2 = PI();
		GTA.Native.Pointer pointer3 = PI();
		bool result = B("CREATE_EMERGENCY_SERVICES_CAR_RETURN_DRIVER", (int)model, p.X, p.Y, p.Z, pointer, pointer2, pointer3);
		car = pointer;
		driver = pointer2;
		passenger = pointer3;
		return result;
	}

	public static void GET_CURRENT_BASIC_COP_MODEL(out uint model)
	{
		GTA.Native.Pointer pointer = PI();
		C("GET_CURRENT_BASIC_COP_MODEL", pointer);
		model = (uint)(int)pointer;
	}

	public static void SWITCH_CAR_SIREN(int veh, bool v)
	{
		C("SWITCH_CAR_SIREN", veh, v);
	}

	public static void GIVE_PED_FAKE_NETWORK_NAME(int ped, string name, int r, int g, int b, int a)
	{
		C("GIVE_PED_FAKE_NETWORK_NAME", ped, name, r, g, b, a);
	}

	public static void REMOVE_FAKE_NETWORK_NAME_FROM_PED(int ped)
	{
		C("REMOVE_FAKE_NETWORK_NAME_FROM_PED", ped);
	}

	public static bool GET_SAFE_POSITION_FOR_CHAR(Vector3 p, bool onGround, out Vector3 result)
	{
		GTA.Native.Pointer pointer = PF();
		GTA.Native.Pointer pointer2 = PF();
		GTA.Native.Pointer pointer3 = PF();
		bool result2 = B("GET_SAFE_POSITION_FOR_CHAR", p.X, p.Y, p.Z, onGround, pointer, pointer2, pointer3);
		result = V(pointer, pointer2, pointer3);
		return result2;
	}

	public static bool DOES_VEHICLE_EXIST(int veh)
	{
		return B("DOES_VEHICLE_EXIST", veh);
	}

	public static bool IS_CAR_DEAD(int veh)
	{
		return B("IS_CAR_DEAD", veh);
	}

	public static void GET_CAR_COORDINATES(int veh, out Vector3 pos)
	{
		GTA.Native.Pointer pointer = PF();
		GTA.Native.Pointer pointer2 = PF();
		GTA.Native.Pointer pointer3 = PF();
		C("GET_CAR_COORDINATES", veh, pointer, pointer2, pointer3);
		pos = V(pointer, pointer2, pointer3);
	}

	public static void SET_CAR_COORDINATES(int veh, Vector3 p)
	{
		C("SET_CAR_COORDINATES", veh, p.X, p.Y, p.Z);
	}

	public static void SET_CAR_HEADING(int veh, float h)
	{
		C("SET_CAR_HEADING", veh, h);
	}

	public static void GET_CAR_MODEL(int veh, out uint model)
	{
		GTA.Native.Pointer pointer = PI();
		C("GET_CAR_MODEL", veh, pointer);
		model = (uint)(int)pointer;
	}

	public static void EXPLODE_CAR(int veh, bool a, bool b)
	{
		C("EXPLODE_CAR", veh, a, b);
	}

	public static void CREATE_CAR(int model, Vector3 p, out int veh, bool b)
	{
		GTA.Native.Pointer pointer = PI();
		C("CREATE_CAR", model, p.X, p.Y, p.Z, pointer, b);
		veh = pointer;
	}

	public static void MARK_CAR_AS_NO_LONGER_NEEDED(int veh)
	{
		GTA.Native.Pointer pointer = veh;
		C("MARK_CAR_AS_NO_LONGER_NEEDED", pointer);
	}

	public static void APPLY_FORCE_TO_CAR(int veh, int type, float x, float y, float z, float sx, float sy, float sz, int a, int b, int c, int d)
	{
		C("APPLY_FORCE_TO_CAR", veh, type, x, y, z, sx, sy, sz, a, b, c, d);
	}

	public static bool IS_THIS_MODEL_A_TRAIN(uint model)
	{
		return B("IS_THIS_MODEL_A_TRAIN", (int)model);
	}

	public static bool IS_THIS_MODEL_A_HELI(uint model)
	{
		return B("IS_THIS_MODEL_A_HELI", (int)model);
	}

	public static int GET_HASH_KEY(string s)
	{
		return I("GET_HASH_KEY", s);
	}

	public static bool IS_MODEL_IN_CDIMAGE(int model)
	{
		return B("IS_MODEL_IN_CDIMAGE", model);
	}

	public static void REQUEST_MODEL(int model)
	{
		C("REQUEST_MODEL", model);
	}

	public static bool HAS_MODEL_LOADED(int model)
	{
		return B("HAS_MODEL_LOADED", model);
	}

	public static void MARK_MODEL_AS_NO_LONGER_NEEDED(int model)
	{
		C("MARK_MODEL_AS_NO_LONGER_NEEDED", model);
	}

	public static void GET_CURRENT_BASIC_POLICE_CAR_MODEL(out uint model)
	{
		GTA.Native.Pointer pointer = PI();
		C("GET_CURRENT_BASIC_POLICE_CAR_MODEL", pointer);
		model = (uint)(int)pointer;
	}

	public static bool CREATE_EMERGENCY_SERVICES_CAR_THEN_WALK(uint model, Vector3 p)
	{
		return B("CREATE_EMERGENCY_SERVICES_CAR_THEN_WALK", (int)model, p.X, p.Y, p.Z);
	}

	public static bool IS_CAR_ON_SCREEN(int veh)
	{
		return B("IS_CAR_ON_SCREEN", veh);
	}

	public static bool IS_CAR_UPSIDEDOWN(int veh)
	{
		return B("IS_CAR_UPSIDEDOWN", veh);
	}

	public static bool IS_CAR_STUCK_ON_ROOF(int veh)
	{
		return B("IS_CAR_STUCK_ON_ROOF", veh);
	}

	public static bool IS_CAR_STUCK(int veh)
	{
		return B("IS_CAR_STUCK", veh);
	}

	public static bool IS_CAR_ON_FIRE(int veh)
	{
		return B("IS_CAR_ON_FIRE", veh);
	}

	public static bool IS_EMERGENCY_SERVICES_VEHICLE(int veh)
	{
		return B("IS_EMERGENCY_SERVICES_VEHICLE", veh);
	}

	public static void GET_CAR_HEALTH(int veh, out uint health)
	{
		GTA.Native.Pointer pointer = PI();
		C("GET_CAR_HEALTH", veh, pointer);
		health = (uint)(int)pointer;
	}

	public static void GET_CAR_SPEED(int veh, out float speed)
	{
		GTA.Native.Pointer pointer = PF();
		C("GET_CAR_SPEED", veh, pointer);
		speed = pointer;
	}

	public static void GET_DRIVER_OF_CAR(int veh, out int ped)
	{
		GTA.Native.Pointer pointer = PI();
		C("GET_DRIVER_OF_CAR", veh, pointer);
		ped = pointer;
	}

	public static void CLEAR_AREA_OF_CARS(Vector3 p, float radius)
	{
		C("CLEAR_AREA_OF_CARS", p.X, p.Y, p.Z, radius);
	}

	public static void CLEAR_AREA_OF_CHARS(Vector3 p, float radius)
	{
		C("CLEAR_AREA_OF_CHARS", p.X, p.Y, p.Z, radius);
	}

	public static void CLEAR_AREA_OF_OBJECTS(Vector3 p, float radius)
	{
		C("CLEAR_AREA_OF_OBJECTS", p.X, p.Y, p.Z, radius);
	}

	public static void CLEAR_AREA_OF_COPS(Vector3 p, float radius)
	{
		C("CLEAR_AREA_OF_COPS", p.X, p.Y, p.Z, radius);
	}

	public static void EXTINGUISH_FIRE_AT_POINT(Vector3 p, float radius)
	{
		C("EXTINGUISH_FIRE_AT_POINT", p.X, p.Y, p.Z, radius);
	}

	public static void SET_PED_DENSITY_MULTIPLIER(float v)
	{
		C("SET_PED_DENSITY_MULTIPLIER", v);
	}

	public static void SET_CAR_DENSITY_MULTIPLIER(float v)
	{
		C("SET_CAR_DENSITY_MULTIPLIER", v);
	}

	public static void ADD_EXPLOSION(Vector3 p, int type, float radius, bool sound, bool invisible, float shake)
	{
		C("ADD_EXPLOSION", p.X, p.Y, p.Z, type, radius, sound, invisible, shake);
	}

	public static int START_SCRIPT_FIRE(Vector3 p, uint gens, uint strength)
	{
		return I("START_SCRIPT_FIRE", p.X, p.Y, p.Z, (int)gens, (int)strength);
	}

	public static void REMOVE_SCRIPT_FIRE(int fire)
	{
		C("REMOVE_SCRIPT_FIRE", fire);
	}

	public static bool DOES_SCRIPT_FIRE_EXIST(int fire)
	{
		return B("DOES_SCRIPT_FIRE_EXIST", fire);
	}

	public static void GET_GROUND_Z_FOR_3D_COORD(Vector3 p, out float z)
	{
		GTA.Native.Pointer pointer = PF();
		C("GET_GROUND_Z_FOR_3D_COORD", p.X, p.Y, p.Z, pointer);
		z = pointer;
	}

	public static bool GET_WATER_HEIGHT(Vector3 p, out float h)
	{
		GTA.Native.Pointer pointer = PF();
		bool result = B("GET_WATER_HEIGHT", p.X, p.Y, p.Z, pointer);
		h = pointer;
		return result;
	}

	public static void LOAD_SCENE(Vector3 p)
	{
		C("LOAD_SCENE", p.X, p.Y, p.Z);
	}

	public static void REQUEST_COLLISION_AT_POSN(Vector3 p)
	{
		C("REQUEST_COLLISION_AT_POSN", p.X, p.Y, p.Z);
	}

	public static bool GET_NTH_CLOSEST_CAR_NODE_WITH_HEADING(Vector3 p, uint n, out Vector3 result, out float heading)
	{
		GTA.Native.Pointer pointer = PF();
		GTA.Native.Pointer pointer2 = PF();
		GTA.Native.Pointer pointer3 = PF();
		GTA.Native.Pointer pointer4 = PF();
		bool result2 = B("GET_NTH_CLOSEST_CAR_NODE_WITH_HEADING", p.X, p.Y, p.Z, (int)n, pointer, pointer2, pointer3, pointer4);
		result = V(pointer, pointer2, pointer3);
		heading = pointer4;
		return result2;
	}

	public static void FORCE_WEATHER_NOW(uint w)
	{
		C("FORCE_WEATHER_NOW", (int)w);
	}

	public static void RELEASE_WEATHER()
	{
		C("RELEASE_WEATHER");
	}

	public static void SET_TIME_OF_DAY(uint h, uint m)
	{
		C("SET_TIME_OF_DAY", (int)h, (int)m);
	}

	public static void SET_TIME_SCALE(float s)
	{
		C("SET_TIME_SCALE", s);
	}

	public static void CREATE_CAM(int type, out int cam)
	{
		GTA.Native.Pointer pointer = PI();
		C("CREATE_CAM", type, pointer);
		cam = pointer;
	}

	public static void DESTROY_CAM(int cam)
	{
		C("DESTROY_CAM", cam);
	}

	public static bool DOES_CAM_EXIST(int cam)
	{
		return B("DOES_CAM_EXIST", cam);
	}

	public static bool IS_CAM_ACTIVE(int cam)
	{
		return B("IS_CAM_ACTIVE", cam);
	}

	public static void SET_CAM_ACTIVE(int cam, bool v)
	{
		C("SET_CAM_ACTIVE", cam, v);
	}

	public static void SET_CAM_PROPAGATE(int cam, bool v)
	{
		C("SET_CAM_PROPAGATE", cam, v);
	}

	public static void ACTIVATE_SCRIPTED_CAMS(bool a, bool b)
	{
		C("ACTIVATE_SCRIPTED_CAMS", a, b);
	}

	public static void SET_CAM_POS(int cam, Vector3 p)
	{
		C("SET_CAM_POS", cam, p.X, p.Y, p.Z);
	}

	public static void SET_CAM_ROT(int cam, Vector3 r)
	{
		C("SET_CAM_ROT", cam, r.X, r.Y, r.Z);
	}

	public static void SET_CAM_FOV(int cam, float fov)
	{
		C("SET_CAM_FOV", cam, fov);
	}

	public static void GET_GAME_CAM(out int cam)
	{
		GTA.Native.Pointer pointer = PI();
		C("GET_GAME_CAM", pointer);
		cam = pointer;
	}

	public static void GET_CAM_POS(int cam, out Vector3 pos)
	{
		GTA.Native.Pointer pointer = PF();
		GTA.Native.Pointer pointer2 = PF();
		GTA.Native.Pointer pointer3 = PF();
		C("GET_CAM_POS", cam, pointer, pointer2, pointer3);
		pos = V(pointer, pointer2, pointer3);
	}

	public static void GET_CAM_ROT(int cam, out Vector3 rot)
	{
		GTA.Native.Pointer pointer = PF();
		GTA.Native.Pointer pointer2 = PF();
		GTA.Native.Pointer pointer3 = PF();
		C("GET_CAM_ROT", cam, pointer, pointer2, pointer3);
		rot = V(pointer, pointer2, pointer3);
	}

	public static void GET_CAM_FOV(int cam, out float fov)
	{
		GTA.Native.Pointer pointer = PF();
		C("GET_CAM_FOV", cam, pointer);
		fov = pointer;
	}

	public static void DO_SCREEN_FADE_OUT(uint ms)
	{
		C("DO_SCREEN_FADE_OUT", (int)ms);
	}

	public static void DO_SCREEN_FADE_IN(uint ms)
	{
		C("DO_SCREEN_FADE_IN", (int)ms);
	}

	public static bool IS_SCREEN_FADED_OUT()
	{
		return B("IS_SCREEN_FADED_OUT");
	}

	public static void DISPLAY_HUD(bool v)
	{
		C("DISPLAY_HUD", v);
	}

	public static void DISPLAY_RADAR(bool v)
	{
		C("DISPLAY_RADAR", v);
	}

	public static void HIDE_HUD_AND_RADAR_THIS_FRAME()
	{
		C("HIDE_HUD_AND_RADAR_THIS_FRAME");
	}

	public static void CLEAR_HELP()
	{
		C("CLEAR_HELP");
	}

	public static bool IS_PAUSE_MENU_ACTIVE()
	{
		return B("IS_PAUSE_MENU_ACTIVE");
	}

	public static void DISABLE_PAUSE_MENU(bool v)
	{
		C("DISABLE_PAUSE_MENU", v);
	}

	public static void SET_TEXT_INPUT_ACTIVE(bool v)
	{
		C("SET_TEXT_INPUT_ACTIVE", v);
	}

	public static void DRAW_CHECKPOINT(Vector3 p, float radius, Color c)
	{
		C("DRAW_CHECKPOINT", p.X, p.Y, p.Z, radius, c.R, c.G, c.B);
	}

	public static void GET_MOUSE_INPUT(out Point delta)
	{
		GTA.Native.Pointer pointer = PI();
		GTA.Native.Pointer pointer2 = PI();
		C("GET_MOUSE_INPUT", pointer, pointer2);
		delta = new Point(pointer, pointer2);
	}
}
