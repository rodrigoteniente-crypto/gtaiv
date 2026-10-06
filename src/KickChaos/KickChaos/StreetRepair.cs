using System;
using System.Collections.Generic;
using System.Numerics;

namespace KickChaos;

public class StreetRepair
{
	public float Delay = 60f;

	public string Mode = "fundido";

	public float Radius = 70f;

	public bool Debris = true;

	public bool Cops = true;

	public int Repairs;

	public Func<Vector3, float, bool> IsActiveNpcZone;

	private readonly List<Vector3> dirty = new List<Vector3>();

	private double lastDirty = -1.0;

	private double firstDirty = -1.0;

	public bool IsDirty => dirty.Count > 0;

	public void ApplyConfig(IniFile ini)
	{
		Delay = Math.Max(0f, ini.GetFloat("Arreglar", "Tras", 60f));
		Mode = Config.Normalize(ini.Get("Arreglar", "Forma", "fundido")).Trim();
		Radius = Math.Max(20f, ini.GetFloat("Arreglar", "Radio", 70f));
		Debris = ini.GetBool("Arreglar", "Escombros", def: true);
		Cops = ini.GetBool("Arreglar", "Policias", def: true);
	}

	public double SecondsLeft()
	{
		if (dirty.Count == 0 || Delay <= 0f)
		{
			return -1.0;
		}
		double num = Math.Min(lastDirty + (double)Delay, firstDirty + (double)Math.Max(Delay * 3f, 180f));
		return Math.Max(0.0, num - G.Now);
	}

	public void MarkDirty(Vector3 where)
	{
		double now = G.Now;
		if (dirty.Count == 0)
		{
			firstDirty = now;
		}
		lastDirty = now;
		foreach (Vector3 item in dirty)
		{
			if (Vector3.Distance(item, where) < Radius * 0.5f)
			{
				return;
			}
		}
		dirty.Add(where);
		if (dirty.Count > 12)
		{
			dirty.RemoveAt(0);
		}
	}

	public bool IsDue()
	{
		if (Delay <= 0f || dirty.Count == 0)
		{
			return false;
		}
		double now = G.Now;
		return now - lastDirty >= (double)Delay || now - firstDirty >= (double)Math.Max(Delay * 3f, 180f);
	}

	public void RepairNow(ChaosActions actions)
	{
		actions?.RemoveFires();
		Vector3[] array = dirty.ToArray();
		foreach (Vector3 c in array)
		{
			RepairArea(c);
		}
		dirty.Clear();
		lastDirty = (firstDirty = -1.0);
		Repairs++;
	}

	public void RepairArea(Vector3 c)
	{
		N.EXTINGUISH_FIRE_AT_POINT(c, Radius);
		foreach (int item in G.VehiclesNear(c, Radius, 80, includeDead: true))
		{
			if (!G.ProtectedCars.Contains(item))
			{
				bool flag = N.IS_CAR_DEAD(item) || N.IS_CAR_ON_FIRE(item) || N.IS_CAR_UPSIDEDOWN(item) || N.IS_CAR_STUCK_ON_ROOF(item);
				if (!flag)
				{
					N.GET_CAR_HEALTH(item, out var health);
					N.GET_DRIVER_OF_CAR(item, out var ped);
					flag = health < 650 || (ped == 0 && (health < 950 || N.IS_EMERGENCY_SERVICES_VEHICLE(item)));
				}
				if (flag)
				{
					N.SET_CAR_AS_MISSION_CAR(item);
					N.DELETE_CAR(item);
				}
			}
		}
		foreach (int item2 in G.DeadPedsNear(c, Radius, 60))
		{
			N.DELETE_CHAR(item2);
		}
		if (Debris)
		{
			N.CLEAR_AREA_OF_OBJECTS(c, Radius);
		}
		if (Cops && (IsActiveNpcZone == null || !IsActiveNpcZone(c, Radius + 80f)))
		{
			N.CLEAR_AREA_OF_COPS(c, Radius);
		}
	}

	public void Forget()
	{
		dirty.Clear();
		lastDirty = (firstDirty = -1.0);
	}
}
