using KickChaos;
using Recovery = KickChaos.CombatPolicy.Recovery;

var scenarios = new[]
{
    // The reported failure: the native says "in combat", but no shots ever occur.
    ("idle police combat recovers", true, false, true, true, 25f, 9d, 100d, Recovery.Shoot),
    ("idle subscriber combat recovers", true, false, true, true, 40f, 10d, 100d, Recovery.Shoot),
    ("new opponent redirects the actual task", true, true, true, true, 25f, 1d, 0.2d, Recovery.Engage),
    ("fresh task has time to aim", true, false, true, true, 25f, 2d, 100d, Recovery.Keep),
    ("recent shooting preserves organic native combat", true, false, true, true, 25f, 10d, 1d, Recovery.Keep),
    ("finished combat task is restarted", true, false, false, true, 25f, 8d, 100d, Recovery.Engage),
    ("lost sight triggers movement instead of blind shooting", true, false, true, false, 30f, 9d, 100d, Recovery.Advance),
    ("stale spotted flag at 1600m cannot trigger shooting", true, false, true, true, 1600f, 10d, 100d, Recovery.Advance),
    ("distant enemy is approached", true, true, false, true, 90f, 10d, 100d, Recovery.Advance),
    ("unarmed brawl never selects gunfire", false, false, true, true, 2f, 8d, 100d, Recovery.Engage),
    ("close blocked enemy is reacquired", true, false, true, false, 5f, 8d, 100d, Recovery.Engage),
    ("outside weapon range seeks a new angle", true, false, true, true, 50f, 8d, 100d, Recovery.Advance),
};

foreach (var (name, armed, changed, active, visible, distance, taskAge, shotAge, expected) in scenarios)
{
    var actual = CombatPolicy.Decide(armed, changed, active, visible, distance, taskAge, shotAge);
    if (actual != expected)
        throw new Exception($"{name}: expected {expected}, got {actual}");
}

var sightCases = new[]
{
    (20f, 1f, true), (80f, 8f, true), (81f, 0f, false),
    (1600f, 0f, false), (20f, 9f, false), (20f, -9f, false),
    (float.NaN, 0f, false), (float.PositiveInfinity, 0f, false), (-1f, 0f, false)
};
foreach (var (distance, height, expected) in sightCases)
    if (CombatPolicy.InSightRange(distance, height) != expected)
        throw new Exception($"Invalid visibility decision for distance={distance}, height={height}");

Console.WriteLine($"PASS: {scenarios.Length} combat recovery scenarios and {sightCases.Length} sight limits.");

var progressCases = new[]
{
    ("native flanking continues without shots", true, 12d, 1.2f, false, false, Recovery.Keep),
    ("native cover has a grace period", true, 12d, 0f, true, false, Recovery.Keep),
    ("inactive combat restarts even when ped walks", false, 12d, 1.2f, false, false, Recovery.Engage),
    ("game AI does not receive directed firing bursts", true, 18d, 0f, false, true, Recovery.Engage),
    ("game AI gets time to aim and reposition", true, 10d, 0f, false, true, Recovery.Keep),
    ("game AI cover stays with engine", true, 24d, 0f, true, true, Recovery.Keep),
    ("very long idle cover eventually recovers", true, 35d, 0f, true, true, Recovery.Engage),
};
foreach (var (name, active, age, speed, cover, native, expected) in progressCases)
{
    var actual = CombatPolicy.Decide(true, false, active, true, 25f, age, 100d, speed, cover, native);
    if (actual != expected) throw new Exception($"{name}: expected {expected}, got {actual}");
}

void Expect(bool expected, bool actual, string name)
{
    if (expected != actual) throw new Exception(name);
}

// Regressions extracted from the reported behavior, not simulations of GTA.
Expect(false, AiTaskPolicy.CanArrest(29f, false, true, 0f), "Police must close 29m before arrest");
Expect(false, AiTaskPolicy.CanArrest(3f, true, true, 0f), "Police cannot arrest a suspect inside a moving car");
Expect(false, AiTaskPolicy.CanArrest(3f, false, false, 3f), "A runner is pursued rather than arrested from afar");
Expect(true, AiTaskPolicy.CanArrest(3f, false, true, 0f), "Close surrender permits arrest");
Expect(true, AiTaskPolicy.CanArrest(3f, false, false, 0f), "Close stationary suspect permits arrest");
Expect(true, AiTaskPolicy.KeepVehicleEntry(true, 20f, 12d, false), "Threat polling preserves active vehicle entry");
Expect(true, AiTaskPolicy.KeepVehicleEntry(true, 2f, 18d, true), "An ongoing door animation is not cancelled");
Expect(false, AiTaskPolicy.KeepVehicleEntry(true, 2f, 23d, true), "Failed entry has a finite timeout");
Expect(false, AiTaskPolicy.KeepVehicleEntry(false, 2f, 1d, true), "Destroyed target vehicle cancels entry");
Expect(false, AiTaskPolicy.RefreshPath(3d, 0f, 1d, false), "Walking path is not restarted every3s");
Expect(true, AiTaskPolicy.RefreshPath(5d, 0f, 4d, false), "Continuous still observation unlocks recovery");
Expect(true, AiTaskPolicy.RefreshPath(5d, 15f, 0d, false), "A moved destination refreshes route");
Expect(true, AiTaskPolicy.RefreshPath(5d, 0f, 0d, true), "Arrival at old destination advances pursuit");
Expect(false, AiTaskPolicy.RefreshPath(1d, 30f, 0d, false), "A new path has time to start");

// Exhaustive range check: game mode never silently falls back to scripted bursts.
int nativeCases = 0;
for (int distance = 0; distance <= 120; distance += 5)
for (int age = 0; age <= 40; age += 2)
for (int active = 0; active <= 1; active++)
for (int moving = 0; moving <= 1; moving++)
{
    var decision = CombatPolicy.Decide(true, false, active != 0, true, distance, age, 100d,
        moving, false, true);
    if (decision == Recovery.Shoot) throw new Exception("Game AI was given a directed shoot task");
    nativeCases++;
}
Console.WriteLine($"PASS: {progressCases.Length} progress/cover scenarios, 14 transition cases and {nativeCases} native AI decisions.");

int drivingCases = 0;
void DriveExpect(bool expected, bool actual, string name)
{
    Expect(expected, actual, name);
    drivingCases++;
}
DriveExpect(false, AiTaskPolicy.ExitPoliceCar(true, false, 70f, 0d),
    "Police still 70m away keep driving even after the old 25s spawn deadline");
DriveExpect(true, AiTaskPolicy.ExitPoliceCar(true, false, 20f, 0d),
    "Police stop near a suspect on foot rather than driving into them");
DriveExpect(false, AiTaskPolicy.ExitPoliceCar(true, true, 20f, 0d),
    "Police keep chasing a suspect in a vehicle");
DriveExpect(false, AiTaskPolicy.ExitPoliceCar(true, true, 80f, 10d),
    "A short obstruction does not immediately cancel the pursuit");
DriveExpect(true, AiTaskPolicy.ExitPoliceCar(true, true, 80f, 25d),
    "A persistently trapped patrol eventually continues on foot");
DriveExpect(true, AiTaskPolicy.ExitPoliceCar(false, true, 80f, 0d),
    "A destroyed patrol is abandoned");
DriveExpect(true, AiTaskPolicy.RefreshVehiclePursuit(false, false, false, 0d, 0d),
    "An initial vehicle pursuit starts immediately");
DriveExpect(true, AiTaskPolicy.RefreshVehiclePursuit(true, true, false, 1d, 0d),
    "A dead/replaced suspect redirects the patrol");
DriveExpect(false, AiTaskPolicy.RefreshVehiclePursuit(true, false, true, 2d, 0d),
    "Entering/exiting animations do not repeatedly restart the driving task");
DriveExpect(true, AiTaskPolicy.RefreshVehiclePursuit(true, false, true, 8d, 0d),
    "A stable vehicle change eventually updates pursuit mode");
DriveExpect(false, AiTaskPolicy.RefreshVehiclePursuit(true, false, false, 10d, 10d),
    "Recovery gives an obstruction time to clear");
DriveExpect(true, AiTaskPolicy.RefreshVehiclePursuit(true, false, false, 12d, 12d),
    "A genuinely blocked driving task is retried");
DriveExpect(true, AiTaskPolicy.KeepPoliceTarget(true, 110f, 60f, 5d),
    "Driving preserves the suspect through nearby changes in gang positions");
DriveExpect(false, AiTaskPolicy.KeepPoliceTarget(true, 110f, 60f, 13d),
    "A substantially closer suspect can be acquired after pursuit grace");
DriveExpect(false, AiTaskPolicy.KeepPoliceTarget(true, 250f, 20f, 1d),
    "A suspect out of local range does not lock the patrol forever");
DriveExpect(true, AiTaskPolicy.KeepPoliceTarget(false, 25f, 15f, 20d),
    "Foot combat preserves its current nearby target");
bool restartedWhileMoving = false;
for (int tick = 1; tick <= 600; tick++)
    restartedWhileMoving |= AiTaskPolicy.RefreshVehiclePursuit(true, false, false, tick / 10d, 0d);
DriveExpect(false, restartedWhileMoving,
    "A minute of progressing pursuit does not reset the driving task on any of 600 ticks");
Console.WriteLine($"PASS: {drivingCases} driving/target scenarios, including 600 pursuit ticks.");
