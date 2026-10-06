using System.Numerics;
using KickChaos;

int checks = 0;
void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
void Close(float actual, float expected, string message) { Check(Math.Abs(actual - expected) < 0.001f, message); }
LocalCameraFloorQuery Ground(Func<float, float, float> terrain, List<Vector3> probes = null) =>
    (Vector3 probe, out float floor) =>
    {
        probes?.Add(probe);
        floor = terrain(probe.X, probe.Y);
        return floor <= probe.Z;
    };

var look = new Vector3(100, 200, 11.5f);
var desired = new Vector3(122, 200, 17.5f);
Vector3 camera;
Check(FollowCameraPlacementPolicy.TryPlace(desired, look, Ground((x, y) => 10), out camera), "ordinary outdoor shot remains available");
Check(camera == desired, "valid outdoor shot keeps its composition");

var probes = new List<Vector3>();
Check(FollowCameraPlacementPolicy.TryPlace(desired, look, Ground((x, y) => 10, probes), out camera), "local floor query succeeds");
Check(probes.Count > 3, "sightline samples loaded terrain rather than only the endpoints");
Check(probes.All(p => p.Z < 20), "local ground probes never use a remote roof height");
Check(probes.Skip(1).All(p => p.Z <= Math.Max(look.Z, camera.Z) + 0.5f), "intermediate probes follow the actual sightline");

// A zero-height street is real terrain, not a query failure.
Check(FollowCameraPlacementPolicy.TryPlace(new Vector3(6, 0, 2), new Vector3(0, 0, 1.5f), Ground((x, y) => 0), out camera), "zero-height street accepted");

// A camera carried across a sudden change of height cannot interpolate beneath the street.
Check(FollowCameraPlacementPolicy.TryPlace(new Vector3(108, 200, 16), new Vector3(100, 200, 26), Ground((x, y) => 25), out camera), "interpolated previous frame corrected above new street");
Check(camera.Z >= 26.4f && camera.Z >= 25.9f, "height correction respects actor and ground clearances");
Check(camera.X == 108 && camera.Y == 200, "height correction retains requested lateral position");

// The near endpoint may be on a gently rising road; lifting by a small amount is allowed.
var hillLook = new Vector3(0, 0, 1.5f);
Check(FollowCameraPlacementPolicy.TryPlace(new Vector3(6, 0, 2), hillLook, Ground((x, y) => 1 + x * 0.3f), out camera), "uphill road gets a safe camera height");
Close(camera.Z, 3.7f, "uphill endpoint clears street by 0.9 m");
Check(!FollowCameraPlacementPolicy.TryPlace(new Vector3(20, 0, 3), hillLook, Ground((x, y) => 1 + x * 0.3f), out camera), "steep or distant elevation requires a nearer candidate");

// A ridge between safe endpoints blocks sight; endpoint-only math cannot detect it.
Check(!FollowCameraPlacementPolicy.TryPlace(new Vector3(12, 0, 3), hillLook, Ground((x, y) => x > 4 && x < 8 ? 2.7f : 0), out camera), "intermediate ridge rejects under-terrain sightline");
Check(!FollowCameraPlacementPolicy.TryPlace(new Vector3(12, 0, 3), hillLook, Ground((x, y) => x > 4 && x < 8 ? 80f : 0), out camera), "unloaded local terrain cannot masquerade as a visible shot");

// A roof query above the actor is rejected rather than lifting the camera onto the building.
Check(!FollowCameraPlacementPolicy.TryPlace(new Vector3(6, 0, 8), hillLook, Ground((x, y) => 7), out camera), "local roof rejected");
Check(camera.Z <= 8, "roof does not raise camera to roof top");
Check(!FollowCameraPlacementPolicy.TryPlace(new Vector3(6, 0, 90), hillLook, Ground((x, y) => 0), out camera), "near-vertical high camera rejected");

// Tunnel queries stay below a far-away bridge; a valid floor is enough for a close pose.
var tunnelProbes = new List<Vector3>();
LocalCameraFloorQuery tunnel = (Vector3 probe, out float floor) =>
{
    tunnelProbes.Add(probe);
    floor = probe.Z >= 50 ? 50 : 5;
    return floor <= probe.Z;
};
Check(FollowCameraPlacementPolicy.TryPlace(new Vector3(6, 0, 8), new Vector3(0, 0, 6.5f), tunnel, out camera), "close tunnel shot uses its local floor");
Check(tunnelProbes.All(p => p.Z < 12), "tunnel query never jumps to bridge roof");

LocalCameraFloorQuery missing = (Vector3 probe, out float floor) => { floor = 0; return false; };
Check(!FollowCameraPlacementPolicy.TryPlace(desired, look, missing, out camera), "unloaded collision rejects candidate");
Check(camera == desired && camera != Vector3.Zero, "failed query does not return world origin as usable fallback");
Check(!FollowCameraPlacementPolicy.TryPlace(desired, look, null, out camera), "missing floor provider rejected");

// If the rear side is obstructed, another direction can keep the actor in view.
LocalCameraFloorQuery obstructedRear = (Vector3 probe, out float floor) => { floor = probe.Y < 195 ? 80 : 10; return floor <= probe.Z; };
Check(FollowCameraPlacementPolicy.TryFallback(look, 0, false, obstructedRear, out camera), "fallback tries multiple nearby headings");
Check(camera.Y >= 195 && Vector3.Distance(camera, look) < 7, "fallback remains short and avoids rear obstacle");
Check(FollowCameraPlacementPolicy.TryFallback(look, 0, true, Ground((x, y) => 10), out camera), "vehicle fallback available");
Close(new Vector2(camera.X - look.X, camera.Y - look.Y).Length(), 10, "vehicle fallback starts at ten meters");
Check(!FollowCameraPlacementPolicy.TryFallback(look, 0, false, missing, out camera), "unloaded fallback rejects every direction");
Check(camera == look && camera != Vector3.Zero, "failed fallback preserves actor position rather than world origin");

foreach (float bad in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
{
    Check(!FollowCameraPlacementPolicy.TryPlace(new Vector3(bad, 200, 17), look, Ground((x, y) => 10), out camera), "nonfinite requested coordinate rejected");
    Check(!FollowCameraPlacementPolicy.TryPlace(desired, new Vector3(100, bad, 11), Ground((x, y) => 10), out camera), "nonfinite target coordinate rejected");
    LocalCameraFloorQuery badFloor = (Vector3 probe, out float floor) => { floor = bad; return true; };
    Check(!FollowCameraPlacementPolicy.TryPlace(desired, look, badFloor, out camera), "nonfinite floor rejected");
    Check(!FollowCameraPlacementPolicy.TryFallback(look, bad, false, Ground((x, y) => 10), out camera), "nonfinite heading rejected");
}

// Repeated uphill/downhill actor movement validates the camera against the current floor.
for (int frame = 0; frame < 60; frame++)
{
    float floor = 10 + frame * 0.25f;
    Vector3 actorLook = new Vector3(100 + frame, 200, floor + 1.5f);
    Vector3 stale = actorLook + new Vector3(6, 0, -2);
    Check(FollowCameraPlacementPolicy.TryPlace(stale, actorLook, Ground((x, y) => floor), out camera), "moving-height frame remains above current street");
    Check(camera.Z >= actorLook.Z + FollowCameraPlacementPolicy.ActorClearance && camera.Z >= floor + FollowCameraPlacementPolicy.FloorClearance, "no moving-height frame goes under floor");
}

Console.WriteLine($"StreamCameraPlacement: {checks} PASS");
