using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using GTA;
using GTA.@base;

namespace KickChaos;

public static class G
{
	private static readonly Stopwatch clock = Stopwatch.StartNew();

	private static Func<HandleObject, int> handleGetter;

	private static bool handleReflectionFailed;

	public static readonly HashSet<int> ProtectedPeds = new HashSet<int>();

	public static readonly HashSet<int> ProtectedCars = new HashSet<int>();

	private static readonly uint myPid = (uint)Process.GetCurrentProcess().Id;

	public static Random Rng => MathX.Rng;

	public static double Now => clock.Elapsed.TotalSeconds;

	public static int PlayerIndex => N.CONVERT_INT_TO_PLAYERINDEX(N.GET_PLAYER_ID());

	public static int PlayerPed
	{
		get
		{
			N.GET_PLAYER_CHAR(PlayerIndex, out var ped);
			return ped;
		}
	}

	public static bool IsPlayerReady()
	{
		try
		{
			return N.IS_PLAYER_PLAYING(PlayerIndex) && PlayerPed != 0;
		}
		catch
		{
			return false;
		}
	}

	public static float Rand(float min, float max)
	{
		return MathX.Rand(min, max);
	}

	public static Vector3 Forward(Vector3 rot)
	{
		return MathX.Forward(rot);
	}

	public static bool GroundZ(Vector3 p, out float z)
	{
		z = 0f;
		try
		{
			N.GET_GROUND_Z_FOR_3D_COORD(new Vector3(p.X, p.Y, p.Z + 2f), out var z2);
			if (z2 == 0f)
			{
				return false;
			}
			z = z2;
			return true;
		}
		catch
		{
			return false;
		}
	}

	public static bool TopZ(float x, float y, out float z)
	{
		return GroundZ(new Vector3(x, y, 998f), out z);
	}

	public static bool Raycast(Vector3 from, Vector3 to, out Vector3 hit)
	{
		hit = to;
		float num = Vector3.Distance(from, to);
		if (num < 0.5f)
		{
			return false;
		}
		int num2 = Math.Max(2, Math.Min(200, (int)(num / 2.5f)));
		for (int i = 1; i <= num2; i++)
		{
			Vector3 vector = Vector3.Lerp(from, to, (float)i / (float)num2);
			if (TopZ(vector.X, vector.Y, out var z) && z > vector.Z + 0.3f)
			{
				hit = vector;
				return true;
			}
		}
		return false;
	}

	public static bool TryFindTarget(Vector3 camPos, Vector3 camRot, float maxDist, bool fine, out Vector3 target)
	{
		Vector3 vector = Forward(camRot);
		float num = 2f;
		float val = 0f;
		int num2 = 0;
		int num3 = ((!fine) ? 220 : 700);
		for (; num < maxDist; num += Math.Max(1.5f, num * ((!fine) ? 0.03f : 0.01f)))
		{
			if (num2 >= num3)
			{
				break;
			}
			Vector3 vector2 = camPos + vector * num;
			num2++;
			if (TopZ(vector2.X, vector2.Y, out var z) && vector2.Z <= z + 0.2f)
			{
				if (z - vector2.Z < 3f)
				{
					target = new Vector3(vector2.X, vector2.Y, z);
					return true;
				}
				Vector3 p = camPos + vector * Math.Max(1f, val);
				if (GroundZ(p, out var z2))
				{
					target = new Vector3(p.X, p.Y, z2);
					return true;
				}
				target = new Vector3(vector2.X, vector2.Y, vector2.Z);
				return true;
			}
			val = num;
		}
		target = camPos + vector * Math.Min(60f, maxDist);
		if (GroundZ(target, out var z3) && z3 < target.Z)
		{
			target.Z = z3;
		}
		return false;
	}

	public static Vector3 FindTarget(Vector3 camPos, Vector3 camRot, float maxDist)
	{
		TryFindTarget(camPos, camRot, maxDist, fine: true, out var target);
		return target;
	}

	public static Vector3 CharPos(int ped)
	{
		N.GET_CHAR_COORDINATES(ped, out var pos);
		return pos;
	}

	public static Vector3 CarPos(int veh)
	{
		N.GET_CAR_COORDINATES(veh, out var pos);
		return pos;
	}

	private static GTA.Vector3 GV(Vector3 value)
	{
		return new GTA.Vector3(value.X, value.Y, value.Z);
	}

	public static int HandleOf(HandleObject o)
	{
		if (o == (HandleObject)null)
		{
			return 0;
		}
		if (handleGetter == null && !handleReflectionFailed)
		{
			try
			{
				MethodInfo getMethod = typeof(HandleObject).GetProperty("Handle", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetGetMethod(nonPublic: true);
				handleGetter = (Func<HandleObject, int>)Delegate.CreateDelegate(typeof(Func<HandleObject, int>), getMethod);
			}
			catch
			{
				handleReflectionFailed = true;
			}
		}
		return (handleGetter == null) ? ((object)o).GetHashCode() : handleGetter(o);
	}

	public static List<int> PedsNear(Vector3 center, float radius, int max)
	{
		return PedsNear(center, radius, max, skipProtected: true);
	}

	public static List<int> PedsNear(Vector3 center, float radius, int max, bool skipProtected)
	{
		List<int> list = new List<int>();
		try
		{
			int playerPed = PlayerPed;
			Ped[] peds = World.GetPeds(GV(center), radius, max * 3);
			if (peds != null)
			{
				Ped[] array = peds;
				foreach (Ped val in array)
				{
					if (!((HandleObject)(object)val == (HandleObject)null))
					{
						int num = HandleOf((HandleObject)(object)val);
						if (num != 0 && num != playerPed && N.DOES_CHAR_EXIST(num) && !N.IS_CHAR_DEAD(num) && (!skipProtected || !ProtectedPeds.Contains(num)))
						{
							list.Add(num);
						}
					}
				}
			}
		}
		catch
		{
		}
		Shuffle(list);
		if (list.Count > max)
		{
			list.RemoveRange(max, list.Count - max);
		}
		return list;
	}

	public static List<int> DeadPedsNear(Vector3 center, float radius, int max)
	{
		List<int> list = new List<int>();
		try
		{
			int playerPed = PlayerPed;
			Ped[] peds = World.GetPeds(GV(center), radius, max * 2);
			if (peds != null)
			{
				Ped[] array = peds;
				foreach (Ped val in array)
				{
					if ((HandleObject)(object)val == (HandleObject)null)
					{
						continue;
					}
					int num = HandleOf((HandleObject)(object)val);
					if (num != 0 && num != playerPed && !ProtectedPeds.Contains(num) && N.DOES_CHAR_EXIST(num))
					{
						if (N.IS_CHAR_DEAD(num))
						{
							list.Add(num);
						}
						if (list.Count >= max)
						{
							break;
						}
					}
				}
			}
		}
		catch
		{
		}
		return list;
	}

	public static List<int> VehiclesNear(Vector3 center, float radius, int max)
	{
		return VehiclesNear(center, radius, max, includeDead: false);
	}

	public static List<int> VehiclesNear(Vector3 center, float radius, int max, bool includeDead)
	{
		List<int> list = new List<int>();
		try
		{
			int veh = 0;
			int playerPed = PlayerPed;
			if (N.IS_CHAR_IN_ANY_CAR(playerPed))
			{
				N.GET_CAR_CHAR_IS_USING(playerPed, out veh);
			}
			Vehicle[] vehicles = World.GetVehicles(GV(center), radius);
			if (vehicles != null)
			{
				Vehicle[] array = vehicles;
				foreach (Vehicle val in array)
				{
					if ((HandleObject)(object)val == (HandleObject)null)
					{
						continue;
					}
					int num = HandleOf((HandleObject)(object)val);
					if (num != 0 && num != veh && N.DOES_VEHICLE_EXIST(num) && (includeDead || !N.IS_CAR_DEAD(num)))
					{
						N.GET_CAR_MODEL(num, out var model);
						if (!N.IS_THIS_MODEL_A_TRAIN(model))
						{
							list.Add(num);
						}
					}
				}
			}
		}
		catch
		{
		}
		Shuffle(list);
		if (list.Count > max)
		{
			list.RemoveRange(max, list.Count - max);
		}
		return list;
	}

	public static void Shuffle<T>(List<T> l)
	{
		for (int num = l.Count - 1; num > 0; num--)
		{
			int index = Rng.Next(num + 1);
			T value = l[num];
			l[num] = l[index];
			l[index] = value;
		}
	}

	public static Vector3 RandomGroundPoint(Vector3 c, float minR, float maxR)
	{
		double num = Rng.NextDouble() * Math.PI * 2.0;
		float num2 = Rand(minR, maxR);
		Vector3 vector = new Vector3(c.X + (float)Math.Cos(num) * num2, c.Y + (float)Math.Sin(num) * num2, c.Z + 3f);
		if (GroundZ(vector, out var z) && Math.Abs(z - c.Z) < 15f)
		{
			vector.Z = z;
		}
		else
		{
			vector.Z = c.Z;
		}
		return vector;
	}

	[DllImport("user32.dll")]
	private static extern IntPtr GetForegroundWindow();

	[DllImport("user32.dll")]
	private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);

	[DllImport("user32.dll")]
	private static extern short GetAsyncKeyState(int vKey);

	public static bool KeyDown(Keys k)
	{
		try
		{
			return (GetAsyncKeyState((int)k) & 0x8000) != 0;
		}
		catch
		{
			return false;
		}
	}

	public static bool IsGameFocused()
	{
		try
		{
			GetWindowThreadProcessId(GetForegroundWindow(), out var pid);
			return pid == myPid;
		}
		catch
		{
			return true;
		}
	}
}
