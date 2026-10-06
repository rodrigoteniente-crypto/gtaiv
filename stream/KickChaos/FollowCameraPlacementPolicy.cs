using System;
using System.Numerics;

namespace KickChaos
{
    /// <summary>A local, loaded ground query. Zero is a valid height; false means unavailable.</summary>
    public delegate bool LocalCameraFloorQuery(Vector3 probe, out float floorZ);

    /// <summary>
    /// Places a manual camera relative to the actor and loaded nearby terrain.
    /// Height queries are deliberately local: a roof at world height 998 is not a wall test.
    /// </summary>
    public static class FollowCameraPlacementPolicy
    {
        public const float FloorClearance = 0.9f;
        public const float ActorClearance = 0.4f;
        public const float MaximumLocalRise = 3f;
        public const float SegmentClearance = 0.15f;
        public const float SegmentSpacing = 1.5f;

        /// <summary>
        /// Returns false if terrain is missing, a local roof blocks the pose, or the sightline
        /// passes through terrain. Callers must use the output only when this returns true.
        /// </summary>
        public static bool TryPlace(Vector3 desired, Vector3 look, LocalCameraFloorQuery query, out Vector3 camera)
        {
            // Preserve the requested point on failure rather than manufacturing world origin.
            camera = desired;
            if (query == null || !Finite(desired) || !Finite(look)) return false;
            Vector3 delta = desired - look;
            float horizontal = (float)Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y);
            if (horizontal < 1f || horizontal > 150f || Math.Abs(delta.Z) > Math.Max(12f, horizontal * 1.2f)) return false;

            float floor;
            Vector3 probe = desired;
            probe.Z = Math.Max(look.Z + MaximumLocalRise + 0.5f, desired.Z + 0.5f);
            if (!ReadFloor(query, probe, out floor) || floor > look.Z + MaximumLocalRise) return false;
            camera.Z = Math.Max(desired.Z, Math.Max(look.Z + ActorClearance, floor + FloorClearance));

            // Raising a low point can expose a ceiling that the original query did not reach.
            if (camera.Z + 0.5f > probe.Z)
            {
                probe.Z = camera.Z + 0.5f;
                if (!ReadFloor(query, probe, out floor) || floor > look.Z + MaximumLocalRise || floor > camera.Z - FloorClearance + 0.001f)
                    return false;
            }

            float distance = Vector3.Distance(look, camera);
            int segments = Math.Max(1, (int)Math.Ceiling(distance / SegmentSpacing));
            // The endpoint was checked above; intermediate points catch rising streets and ridges.
            for (int i = 1; i < segments; i++)
            {
                Vector3 sample = Vector3.Lerp(look, camera, i / (float)segments);
                Vector3 localProbe = sample + new Vector3(0, 0, 0.5f);
                if (!ReadFloor(query, localProbe, out floor) || floor > look.Z + MaximumLocalRise || floor > sample.Z - SegmentClearance)
                    return false;
            }
            return true;
        }

        /// <summary>Tries short rear/side poses without lifting the camera onto a distant roof.</summary>
        public static bool TryFallback(Vector3 look, float heading, bool inVehicle, LocalCameraFloorQuery query, out Vector3 camera)
        {
            camera = look;
            if (!Finite(look) || !Finite(heading)) return false;
            float[] distances = inVehicle ? new[] { 10f, 6f, 3f } : new[] { 6f, 4f, 2f };
            float[] offsets = { 0f, 45f, -45f, 90f, -90f, 135f, -135f, 180f };
            foreach (float distance in distances)
                foreach (float offset in offsets)
                {
                    double angle = (heading + 180f + offset) * Math.PI / 180.0;
                    Vector3 desired = look + new Vector3((float)-Math.Sin(angle) * distance, (float)Math.Cos(angle) * distance, inVehicle ? 2.5f : 1.6f);
                    Vector3 placed;
                    if (TryPlace(desired, look, query, out placed)) { camera = placed; return true; }
                }
            return false;
        }

        static bool ReadFloor(LocalCameraFloorQuery query, Vector3 probe, out float floor)
        {
            return query(probe, out floor) && Finite(floor);
        }

        static bool Finite(Vector3 value) { return Finite(value.X) && Finite(value.Y) && Finite(value.Z); }
        static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    }
}
