using System;
using System.Numerics;

namespace KickChaos
{
    /// <summary>Matematica de camaras (sin dependencias del juego).</summary>
    public static class MathX
    {
        public static readonly Random Rng = new Random();
        public static float Rand(float min, float max) { return min + (float)Rng.NextDouble() * (max - min); }

        public static Vector3 Forward(Vector3 rot)
        {
            // rot = (pitch, roll, yaw) en grados, convencion de GTA IV
            double p = rot.X * Math.PI / 180.0, y = rot.Z * Math.PI / 180.0;
            return new Vector3((float)(-Math.Sin(y) * Math.Cos(p)), (float)(Math.Cos(y) * Math.Cos(p)), (float)Math.Sin(p));
        }

        public static Vector3 Right(Vector3 rot)
        {
            double y = rot.Z * Math.PI / 180.0;
            return new Vector3((float)Math.Cos(y), (float)Math.Sin(y), 0f);
        }

        /// <summary>Rotacion (pitch, 0, yaw) que mira desde 'from' hacia 'to'.</summary>
        public static Vector3 LookRotation(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            float flat = (float)Math.Sqrt(d.X * d.X + d.Y * d.Y);
            float pitch = (float)(Math.Atan2(d.Z, flat) * 180.0 / Math.PI);
            float yaw = (float)(Math.Atan2(-d.X, d.Y) * 180.0 / Math.PI);
            return new Vector3(pitch, 0f, yaw);
        }

        public static float LerpAngle(float a, float b, float t)
        {
            float diff = ((b - a) % 360f + 540f) % 360f - 180f;
            return a + diff * t;
        }

        /// <summary>Angulo (yaw, grados) de una direccion en el plano: la inversa de Forward((0, 0, az)).</summary>
        public static float AzOf(Vector3 v) { return (float)(Math.Atan2(-v.X, v.Y) * 180.0 / Math.PI); }

        /// <summary>Diferencia de angulos en -180..180.</summary>
        /// <summary>Distancia (en el plano) de p al segmento a-b.</summary>
        public static float SegmentDistance(Vector3 p, Vector3 a, Vector3 b)
        {
            float abx = b.X - a.X, aby = b.Y - a.Y;
            float l2 = abx * abx + aby * aby;
            float t = l2 < 0.0001f ? 0f : ((p.X - a.X) * abx + (p.Y - a.Y) * aby) / l2;
            t = Math.Max(0f, Math.Min(1f, t));
            float dx = a.X + abx * t - p.X, dy = a.Y + aby * t - p.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        public static float AngleDiff(float a, float b) { return ((b - a) % 360f + 540f) % 360f - 180f; }

        /// <summary>
        /// A que distancia poner la camara para que entre un grupo de radio 'radius' (metros) con ese FOV vertical
        /// (pantalla 16:9), con un poco de margen.
        /// </summary>
        public static float FitDistance(float radius, float vfovDeg, float aspect)
        {
            double tv = Math.Tan(Math.Max(5.0, Math.Min(120.0, vfovDeg)) * 0.5 * Math.PI / 180.0);
            double th = tv * (aspect > 0.1f ? aspect : 16f / 9f);
            double r = Math.Max(1.0, radius) + 2.5;
            double d = Math.Max(r / th, (r * 0.6) / tv) * 1.15;
            return (float)d;
        }

        public static float Smooth(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            return t * t * (3f - 2f * t);
        }
    }
}
