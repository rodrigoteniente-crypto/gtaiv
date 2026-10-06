using System;

namespace KickChaos;

// Pure decisions shared by subscriber NPCs and police. Native combat remains the
// normal behaviour; this watchdog only recovers a task that stopped making progress.
internal static class CombatPolicy
{
	internal enum Recovery { Keep, Engage, Advance, Shoot }

	internal const float SightRange = 80f;
	internal const float ShootingRange = 45f;
	internal const double IdleSeconds = 6.0;

	internal static bool InSightRange(float distance, float heightDifference)
	{
		return !float.IsNaN(distance) && !float.IsInfinity(distance)
			&& distance >= 0f && distance <= SightRange
			&& Math.Abs(heightDifference) <= 8f;
	}

	internal static Recovery Decide(bool armed, bool targetChanged, bool combatActive,
		bool visible, float distance, double taskAge, double shotAge)
	{
		// A remembered "spotted" flag is not permission to fire across unloaded city blocks.
		if (!InSightRange(distance, 0f) || distance > 60f)
			return Recovery.Advance;
		if (targetChanged)
			return Recovery.Engage;
		if (shotAge < 2.0 || taskAge < IdleSeconds)
			return Recovery.Keep;
		if (!combatActive || !armed)
			return Recovery.Engage;
		if (visible && distance <= ShootingRange)
			return Recovery.Shoot;
		return distance > 10f ? Recovery.Advance : Recovery.Engage;
	}
}
