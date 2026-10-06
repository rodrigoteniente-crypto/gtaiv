namespace KickChaos;

// Decisions are independent of GTA so transitions can be checked without a game.
internal static class AiTaskPolicy
{
	// IV DrivingStyle.IgnoreLightsAndLanes: emergency routing along roads.
	// Style 2 is IgnoreStreets and attempts a direct line through obstacles.
	internal const uint RoadDrivingStyle = 1u;

	internal static bool RefreshVehiclePursuit(bool active, bool targetChanged,
		bool suspectChangedVehicle, double taskAge, double stationarySeconds)
	{
		return !active || targetChanged
			|| (suspectChangedVehicle && taskAge >= 8.0)
			|| (stationarySeconds >= 12.0 && taskAge >= 12.0);
	}

	internal static bool ExitPoliceCar(bool vehicleUsable, bool suspectInCar,
		float distance, double stationarySeconds)
	{
		return !vehicleUsable || (!suspectInCar && distance <= 24f)
			|| stationarySeconds >= 25.0;
	}

	internal static bool KeepPoliceTarget(bool inCar, float currentDistance,
		float nearestDistance, double taskAge)
	{
		return inCar ? currentDistance <= 240f && (taskAge < 12.0
			|| currentDistance <= nearestDistance + 40f)
			: currentDistance < nearestDistance + 15f;
	}

	internal static bool KeepVehicleEntry(bool vehicleUsable, float distance,
		double age, bool enteringAnimation)
	{
		return vehicleUsable && distance <= 55f && age < (enteringAnimation ? 22.0 : 16.0);
	}

	internal static bool RefreshPath(double taskAge, float destinationDrift,
		double stationarySeconds, bool atDestination)
	{
		return taskAge >= 3.0 && (atDestination || stationarySeconds >= 4.0
			|| destinationDrift >= 12f || taskAge >= 20.0);
	}

	internal static bool CanArrest(float distance, bool suspectInCar,
		bool handsUp, float suspectSpeed)
	{
		return !suspectInCar && distance <= 4f && (handsUp || suspectSpeed < 0.8f);
	}
}
