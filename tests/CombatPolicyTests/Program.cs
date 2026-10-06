using KickChaos;
using Recovery = KickChaos.CombatPolicy.Recovery;

var scenarios = new[]
{
    // The reported failure: the native says "in combat", but no shots ever occur.
    ("idle police combat recovers", true, false, true, true, 25f, 7d, 100d, Recovery.Shoot),
    ("idle subscriber combat recovers", true, false, true, true, 40f, 10d, 100d, Recovery.Shoot),
    ("new opponent redirects the actual task", true, true, true, true, 25f, 1d, 0.2d, Recovery.Engage),
    ("fresh task has time to aim", true, false, true, true, 25f, 2d, 100d, Recovery.Keep),
    ("recent shooting preserves organic native combat", true, false, true, true, 25f, 10d, 1d, Recovery.Keep),
    ("finished combat task is restarted", true, false, false, true, 25f, 8d, 100d, Recovery.Engage),
    ("lost sight triggers movement instead of blind shooting", true, false, true, false, 30f, 8d, 100d, Recovery.Advance),
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
