using System;

namespace KickChaos;

// Pure decisions shared by subscriber NPCs and police. Native combat remains the
// normal behaviour; this watchdog only recovers a task that stopped making progress.
internal static class CombatPolicy
{
	internal enum Recovery { Keep, Engage, Advance, Shoot }

	internal const float SightRange = 80f;
	internal const float ShootingRange = 45f;
	internal const double IdleSeconds = 8.0;

	internal static bool InSightRange(float distance, float heightDifference)
	{
		return !float.IsNaN(distance) && !float.IsInfinity(distance)
			&& distance >= 0f && distance <= SightRange
			&& Math.Abs(heightDifference) <= 8f;
	}

	internal static Recovery Decide(bool armed, bool targetChanged, bool combatActive,
		bool visible, float distance, double taskAge, double shotAge,
		float speed = 0f, bool inCover = false, bool useGameAi = false)
	{
		// A remembered "spotted" flag is not permission to fire across unloaded city blocks.
		if (!InSightRange(distance, 0f))
			return Recovery.Advance;
		if (targetChanged)
			return Recovery.Engage;
		// Moving to cover, flanking and aiming are valid native combat progress.
		// A stationary ped is not stuck merely because it has not fired yet.
		double grace = useGameAi ? 14.0 : IdleSeconds;
		if (shotAge < 3.0 || taskAge < grace)
			return Recovery.Keep;
		if (!combatActive)
			return Recovery.Engage;
		if (speed >= 0.4f || (inCover && taskAge < (useGameAi ? 30.0 : 20.0)))
			return Recovery.Keep;
		if (!armed || useGameAi)
			return Recovery.Engage;
		if (visible && distance <= ShootingRange)
			return Recovery.Shoot;
		return distance > 10f ? Recovery.Advance : Recovery.Engage;
	}
}
