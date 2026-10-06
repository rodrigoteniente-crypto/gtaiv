using System;
using System.Collections.Generic;
using System.Numerics;

namespace KickChaos;

public class TrafficCleaner
{
	public bool Enabled = true;

	public string OnSwitch = "chocados";

	public float Radius = 90f;

	public int Removed;

	private readonly Dictionary<int, double> stoppedSince = new Dictionary<int, double>();

	private double lastScan;

	public void ApplyConfig(IniFile ini)
	{
		Enabled = ini.GetBool("Trafico", "SacarChocados", def: true);
		OnSwitch = Config.Normalize(ini.Get("Trafico", "AlCambiarCamara", "chocados")).Trim();
		Radius = Math.Max(20f, ini.GetFloat("Trafico", "Radio", 90f));
	}

	private bool IsProblem(int car, double now, bool calm)
	{
		if (N.IS_CAR_DEAD(car) || N.IS_CAR_ON_FIRE(car) || N.IS_CAR_UPSIDEDOWN(car) || N.IS_CAR_STUCK_ON_ROOF(car))
		{
			return true;
		}
		N.GET_CAR_HEALTH(car, out var health);
		if (health < 400)
		{
			return true;
		}
		N.GET_CAR_SPEED(car, out var speed);
		if (speed > 0.7f)
		{
			stoppedSince.Remove(car);
			return false;
		}
		if (!stoppedSince.TryGetValue(car, out var value))
		{
			stoppedSince[car] = now;
			return false;
		}
		double num = now - value;
		if (!calm)
		{
			return false;
		}
		N.GET_DRIVER_OF_CAR(car, out var ped);
		if (ped == 0 && num > 40.0 && (health < 950 || N.IS_EMERGENCY_SERVICES_VEHICLE(car)))
		{
			return true;
		}
		if (num > 20.0 && N.IS_CAR_STUCK(car))
		{
			return true;
		}
		if (ped != 0 && num > 75.0)
		{
			return true;
		}
		return false;
	}

	private static void Remove(int car)
	{
		if (G.ProtectedCars.Contains(car)) return;
		N.SET_CAR_AS_MISSION_CAR(car);
		N.DELETE_CAR(car);
	}

	public void Update(Vector3 center, bool calm, bool secondary, Func<Vector3, float, bool> inFrame)
	{
		if (!Enabled)
		{
			return;
		}
		double now = G.Now;
		if (!secondary)
		{
			if (now - lastScan < 1.0)
			{
				return;
			}
			lastScan = now;
		}
		else if (now - lastScan > 0.05)
		{
			return;
		}
		HashSet<int> hashSet = new HashSet<int>();
		foreach (int item in G.VehiclesNear(center, Radius, 60, includeDead: true))
		{
			hashSet.Add(item);
			if (!G.ProtectedCars.Contains(item) && IsProblem(item, now, calm) && !N.IS_CAR_ON_SCREEN(item) && (inFrame == null || !inFrame(G.CarPos(item), 6f)))
			{
				Remove(item);
				stoppedSince.Remove(item);
				Removed++;
			}
		}
		if (stoppedSince.Count <= 200)
		{
			return;
		}
		List<int> list = new List<int>();
		foreach (KeyValuePair<int, double> item2 in stoppedSince)
		{
			if (!hashSet.Contains(item2.Key))
			{
				list.Add(item2.Key);
			}
		}
		foreach (int item3 in list)
		{
			stoppedSince.Remove(item3);
		}
	}

	public void OnCameraSwitch(Vector3 center)
	{
		if (OnSwitch.StartsWith("nada"))
		{
			return;
		}
		if (OnSwitch.StartsWith("todo"))
		{
			foreach (int car in G.VehiclesNear(center, Radius, 80, includeDead: true))
			{
				if (G.ProtectedCars.Contains(car)) continue;
				Remove(car);
				Removed++;
			}
			stoppedSince.Clear();
		}
		else
		{
			if (!Enabled && !OnSwitch.StartsWith("choc"))
			{
				return;
			}
			double now = G.Now;
			foreach (int item in G.VehiclesNear(center, Radius, 80, includeDead: true))
			{
				if (!G.ProtectedCars.Contains(item) && (IsProblem(item, now, calm: true) || IsLongStopped(item, now)))
				{
					Remove(item);
					stoppedSince.Remove(item);
					Removed++;
				}
			}
		}
	}

	private bool IsLongStopped(int car, double now)
	{
		if (!stoppedSince.TryGetValue(car, out var value) || now - value <= 30.0)
		{
			return false;
		}
		N.GET_DRIVER_OF_CAR(car, out var ped);
		return ped != 0;
	}

	public void Reset()
	{
		stoppedSince.Clear();
	}
}
