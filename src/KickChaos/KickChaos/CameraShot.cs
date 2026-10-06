using System;
using System.Collections.Generic;
using System.Numerics;

namespace KickChaos;

public class CameraShot
{
	public string Name = string.Empty;

	public Vector3 Pos;

	public Vector3 Rot;

	public float Fov = 25f;

	public float Duration = -1f;

	public bool Active = true;

	public float Weight = 1f;

	public int Transition = -1;

	public bool HasEnd;

	public Vector3 EndPos;

	public Vector3 EndRot;

	public float EndFov = -1f;

	public bool HasTarget;

	public Vector3 Target;

	public bool HasAnchor;

	public Vector3 Anchor;

	public int Hour = -1;

	public int Minute;

	public int Weather = -1;

	public float Sway = -1f;

	public bool IsAuto;

	public bool IsFollow;

	public int FollowPed;

	// Shared by the director and offline checks: never select disabled cameras,
	// and avoid immediate repeats when another enabled camera exists.
	public static int SelectNextIndex(IList<CameraShot> shots, int currentIndex, bool random, Random rng, Func<CameraShot, double> weightMultiplier = null)
	{
		if (currentIndex < -1 || currentIndex >= shots.Count)
		{
			currentIndex = -1;
		}
		int count = 0;
		for (int i = 0; i < shots.Count; i++)
		{
			if (shots[i].Active && !shots[i].IsAuto)
			{
				count++;
			}
		}
		if (count == 0)
		{
			return -1;
		}
		if ((!random && weightMultiplier == null) || count == 1)
		{
			for (int step = 1; step <= shots.Count; step++)
			{
				int next = (currentIndex + step) % shots.Count;
				if (shots[next].Active && !shots[next].IsAuto)
				{
					return next;
				}
			}
		}
		double[] weights = new double[shots.Count];
		double total = 0.0;
		int lastCandidate = -1;
		for (int i = 0; i < shots.Count; i++)
		{
			if (i == currentIndex || !shots[i].Active || shots[i].IsAuto)
			{
				continue;
			}
			double multiplier = weightMultiplier == null ? 1.0 : weightMultiplier(shots[i]);
			if (double.IsNaN(multiplier) || double.IsInfinity(multiplier) || multiplier <= 0.0)
			{
				multiplier = 1.0;
			}
			weights[i] = MathX.Clamp(shots[i].Weight, 0.01f, 100f, 1f) * Math.Min(1000000.0, multiplier);
			total += weights[i];
			lastCandidate = i;
		}
		double ticket = rng.NextDouble() * total;
		for (int i = 0; i < weights.Length; i++)
		{
			if (weights[i] <= 0.0)
			{
				continue;
			}
			ticket -= weights[i];
			if (ticket < 0.0)
			{
				return i;
			}
		}
		return lastCandidate;
	}
}
