using System;
using System.Numerics;

namespace KickChaos
{
    // Independent of GTA: interruptions pause the city clock, including animated shots.
    public static class CityCameraClock
    {
        public static double Elapsed(double now, double started) { return Math.Max(0, now - started); }
        public static double ResumeStart(double now, double elapsed) { return now - Math.Max(0, elapsed); }
        public static double Remaining(double duration, double elapsed) { return Math.Max(0, duration - Math.Max(0, elapsed)); }
    }

    public sealed class CameraInterruptionPolicy
    {
        public double EvaluationSeconds = 20, CooldownSeconds = 120;
        public double Probability = 30;
        public bool Active { get; private set; }
        public int Priority { get; private set; }
        public double Until { get; private set; }
        public double AutomaticReadyAt { get; private set; }
        double nextEvaluation;
        bool awaitingVisible;
        double duration;

        public bool CanAccept(int priority, bool manual, bool pinned)
        {
            return (!pinned || manual) && (!Active || priority >= Priority);
        }

        public bool Begin(double now, double seconds, int priority, bool manual, bool pinned)
        {
            if (!CanAccept(priority, manual, pinned)) return false;
            Active = true;
            Priority = priority;
            duration = Clamp(seconds, 1, 600);
            Until = now + duration;
            awaitingVisible = true;
            AutomaticReadyAt = Math.Max(AutomaticReadyAt, now + Math.Max(0, CooldownSeconds));
            return true;
        }

        public void Visible(double now)
        {
            if (!Active || !awaitingVisible) return;
            awaitingVisible = false;
            Until = now + duration;
        }

        public bool Expired(double now) { return Active && !awaitingVisible && now >= Until; }

        public void Finish(double now)
        {
            if (Active) AutomaticReadyAt = Math.Max(AutomaticReadyAt, now + Math.Max(0, CooldownSeconds));
            Active = false;
            Priority = 0;
            Until = 0;
            awaitingVisible = false;
        }

        public bool Evaluate(double now, double roll, bool available)
        {
            if (now < nextEvaluation) return false;
            nextEvaluation = now + Clamp(EvaluationSeconds, 1, 3600);
            return available && !Active && now >= AutomaticReadyAt && roll * 100 < Clamp(Probability, 0, 100);
        }

        public void Reset(double now)
        {
            Active = false;
            Priority = 0;
            Until = 0;
            awaitingVisible = false;
            AutomaticReadyAt = now;
            nextEvaluation = now + Clamp(EvaluationSeconds, 1, 3600);
        }

        static double Clamp(double value, double min, double max)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return min;
            return Math.Max(min, Math.Min(max, value));
        }
    }

    public static class StreamCameraCandidates
    {
        public static double Weight(StreamNpcRow row, double now, float cameraX, float cameraY)
        {
            if (row == null || row.Ped == 0 || row.Health <= 0 || !row.CameraAvailable) return 0;
            double dx = row.X - cameraX, dy = row.Y - cameraY;
            double weight = 1 + Math.Max(0, 1 - Math.Sqrt(dx * dx + dy * dy) / 250) * 3;
            if (row.Combat) weight += 4;
            if (row.Pursuit) weight += 3;
            if (row.Racing) weight += 3;
            if (row.Health < 35) weight += 3;
            if (row.RecentUntil > now) weight += 4;
            return weight;
        }
    }

    public static class FollowCameraGeometry
    {
        public static bool Usable(Vector3 camera, Vector3 target)
        {
            if (!Finite(camera.X) || !Finite(camera.Y) || !Finite(camera.Z) ||
                !Finite(target.X) || !Finite(target.Y) || !Finite(target.Z)) return false;
            Vector3 delta = camera - target;
            double horizontal = Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y);
            // Close tunnel offsets and ordinary dynamic shots pass; almost vertical floor shots do not.
            return horizontal >= 1 && horizontal <= 300 && Math.Abs(delta.Z) <= Math.Max(8, horizontal * 1.2);
        }
        static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    }
}
