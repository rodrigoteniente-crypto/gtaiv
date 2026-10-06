using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using static KickChaos.N;

namespace KickChaos
{
    /// <summary>Utilidades del lado del juego (siempre llamar desde el Tick).</summary>
    public static class G
    {
        public static Random Rng { get { return MathX.Rng; } }
        static readonly Stopwatch clock = Stopwatch.StartNew();

        /// <summary>Tiempo real en segundos (no se ve afectado por la camara lenta).</summary>
        public static double Now { get { return clock.Elapsed.TotalSeconds; } }

        public static int PlayerIndex { get { return CONVERT_INT_TO_PLAYERINDEX(GET_PLAYER_ID()); } }

        public static int PlayerPed
        {
            get
            {
                int ped;
                GET_PLAYER_CHAR(PlayerIndex, out ped);
                return ped;
            }
        }

        public static bool IsPlayerReady()
        {
            try { return IS_PLAYER_PLAYING(PlayerIndex) && PlayerPed != 0; }
            catch { return false; }
        }

        public static float Rand(float min, float max) { return MathX.Rand(min, max); }
        public static Vector3 Forward(Vector3 rot) { return MathX.Forward(rot); }

        /// <summary>Altura del suelo debajo de un punto (requiere colision cargada).</summary>
        public static bool GroundZ(Vector3 p, out float z)
        {
            z = 0f;
            try
            {
                float gz;
                GET_GROUND_Z_FOR_3D_COORD(new Vector3(p.X, p.Y, p.Z + 2f), out gz);
                if (gz == 0f) return false;
                z = gz;
                return true;
            }
            catch { return false; }
        }

        /// <summary>Altura de lo mas alto que hay en (x, y): techos, puentes o el suelo.</summary>
        public static bool TopZ(float x, float y, out float z)
        {
            return GroundZ(new Vector3(x, y, 998f), out z);
        }

        /// <summary>
        /// "Raycast" con mapa de alturas: recorre el segmento y ve si en algun punto hay algo
        /// (edificio, techo, puente, terreno) por encima del rayo. ScriptHookDotNet no tiene raycast real.
        /// </summary>
        public static bool Raycast(Vector3 from, Vector3 to, out Vector3 hit)
        {
            hit = to;
            float len = Vector3.Distance(from, to);
            if (len < 0.5f) return false;
            int steps = Math.Max(2, Math.Min(200, (int)(len / 2.5f)));
            for (int i = 1; i <= steps; i++)
            {
                Vector3 p = Vector3.Lerp(from, to, i / (float)steps);
                float tz;
                if (TopZ(p.X, p.Y, out tz) && tz > p.Z + 0.3f)
                {
                    hit = p;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Punto donde "mira" una camara. Devuelve true si el rayo pego contra algo real;
        /// false si es un punto estimado (por ejemplo, el lugar esta tan lejos que su colision no esta cargada).
        /// </summary>
        public static bool TryFindTarget(Vector3 camPos, Vector3 camRot, float maxDist, bool fine, out Vector3 target)
        {
            Vector3 dir = Forward(camRot);
            float d = 2f, prev = 0f;
            int calls = 0, maxCalls = fine ? 700 : 220;
            while (d < maxDist && calls < maxCalls)
            {
                Vector3 p = camPos + dir * d;
                float tz;
                calls++;
                if (TopZ(p.X, p.Y, out tz) && p.Z <= tz + 0.2f)
                {
                    if (tz - p.Z < 3f)
                    {
                        // pego en el suelo o en un techo
                        target = new Vector3(p.X, p.Y, tz);
                        return true;
                    }
                    // pego contra una pared: usamos el suelo justo antes de la pared
                    Vector3 q = camPos + dir * Math.Max(1f, prev);
                    float gz;
                    if (GroundZ(q, out gz)) { target = new Vector3(q.X, q.Y, gz); return true; }
                    target = new Vector3(p.X, p.Y, p.Z);
                    return true;
                }
                prev = d;
                d += Math.Max(1.5f, d * (fine ? 0.01f : 0.03f));
            }
            target = camPos + dir * Math.Min(60f, maxDist);
            float g;
            if (GroundZ(target, out g) && g < target.Z) target.Z = g;
            return false;
        }

        public static Vector3 FindTarget(Vector3 camPos, Vector3 camRot, float maxDist)
        {
            Vector3 t;
            TryFindTarget(camPos, camRot, maxDist, true, out t);
            return t;
        }

        public static Vector3 CharPos(int ped)
        {
            Vector3 p;
            GET_CHAR_COORDINATES(ped, out p);
            return p;
        }

        public static Vector3 CarPos(int veh)
        {
            Vector3 p;
            GET_CAR_COORDINATES(veh, out p);
            return p;
        }

        static GTA.Vector3 GV(Vector3 v) { return new GTA.Vector3(v.X, v.Y, v.Z); }

        // ScriptHookDotNet no expone el handle de Ped/Vehicle como publico: lo leemos por reflexion (una sola vez).
        static Func<GTA.@base.HandleObject, int> handleGetter;

        static bool handleReflectionFailed;

        public static int HandleOf(GTA.@base.HandleObject o)
        {
            if (o == null) return 0;
            if (handleGetter == null && !handleReflectionFailed)
            {
                try
                {
                    var mi = typeof(GTA.@base.HandleObject).GetProperty("Handle",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                        .GetGetMethod(true);
                    handleGetter = (Func<GTA.@base.HandleObject, int>)Delegate.CreateDelegate(typeof(Func<GTA.@base.HandleObject, int>), mi);
                }
                catch { handleReflectionFailed = true; }
            }
            // GetHashCode de ScriptHookDotNet devuelve el mismo handle (plan B)
            return handleGetter != null ? handleGetter(o) : o.GetHashCode();
        }

        /// <summary>NPC de suscriptores y sus policias: las acciones (disturbio, panico) no los tocan.</summary>
        public static readonly HashSet<int> ProtectedPeds = new HashSet<int>();
        /// <summary>Autos del NPC y de la policia que lo persigue: la limpieza de trafico no los saca.</summary>
        public static readonly HashSet<int> ProtectedCars = new HashSet<int>();

        /// <summary>Peatones vivos cerca de un punto (sin contar al jugador ni a los NPC de suscriptores).</summary>
        public static List<int> PedsNear(Vector3 center, float radius, int max) { return PedsNear(center, radius, max, true); }

        public static List<int> PedsNear(Vector3 center, float radius, int max, bool skipProtected)
        {
            var list = new List<int>();
            try
            {
                int player = PlayerPed;
                GTA.Ped[] peds = GTA.World.GetPeds(GV(center), radius, max * 3);
                if (peds != null)
                    foreach (GTA.Ped p in peds)
                    {
                        if (p == null) continue;
                        int h = HandleOf(p);
                        if (h == 0 || h == player || !DOES_CHAR_EXIST(h) || IS_CHAR_DEAD(h)) continue;
                        if (skipProtected && ProtectedPeds.Contains(h)) continue;
                        list.Add(h);
                    }
            }
            catch { }
            Shuffle(list);
            if (list.Count > max) list.RemoveRange(max, list.Count - max);
            return list;
        }

        /// <summary>Personas muertas cerca de un punto.</summary>
        public static List<int> DeadPedsNear(Vector3 center, float radius, int max)
        {
            var list = new List<int>();
            try
            {
                int player = PlayerPed;
                GTA.Ped[] peds = GTA.World.GetPeds(GV(center), radius, max * 2);
                if (peds != null)
                    foreach (GTA.Ped p in peds)
                    {
                        if (p == null) continue;
                        int h = HandleOf(p);
                        if (h == 0 || h == player || ProtectedPeds.Contains(h) || !DOES_CHAR_EXIST(h)) continue;
                        if (IS_CHAR_DEAD(h)) list.Add(h);
                        if (list.Count >= max) break;
                    }
            }
            catch { }
            return list;
        }

        /// <summary>Vehiculos sanos cerca de un punto (excluye el del jugador y los trenes).</summary>
        public static List<int> VehiclesNear(Vector3 center, float radius, int max) { return VehiclesNear(center, radius, max, false); }

        public static List<int> VehiclesNear(Vector3 center, float radius, int max, bool includeDead)
        {
            var list = new List<int>();
            try
            {
                int playerCar = 0;
                int pp = PlayerPed;
                if (IS_CHAR_IN_ANY_CAR(pp)) GET_CAR_CHAR_IS_USING(pp, out playerCar);
                GTA.Vehicle[] cars = GTA.World.GetVehicles(GV(center), radius);
                if (cars != null)
                    foreach (GTA.Vehicle v in cars)
                    {
                        if (v == null) continue;
                        int h = HandleOf(v);
                        if (h == 0 || h == playerCar || !DOES_VEHICLE_EXIST(h) || (!includeDead && IS_CAR_DEAD(h))) continue;
                        uint model;
                        GET_CAR_MODEL(h, out model);
                        if (IS_THIS_MODEL_A_TRAIN(model)) continue;
                        list.Add(h);
                    }
            }
            catch { }
            Shuffle(list);
            if (list.Count > max) list.RemoveRange(max, list.Count - max);
            return list;
        }

        public static void Shuffle<T>(List<T> l)
        {
            for (int i = l.Count - 1; i > 0; i--)
            {
                int j = Rng.Next(i + 1);
                T t = l[i]; l[i] = l[j]; l[j] = t;
            }
        }

        /// <summary>Punto aleatorio en un anillo alrededor de 'c', apoyado en el suelo si se puede.</summary>
        public static Vector3 RandomGroundPoint(Vector3 c, float minR, float maxR)
        {
            double a = Rng.NextDouble() * Math.PI * 2;
            float r = Rand(minR, maxR);
            var p = new Vector3(c.X + (float)Math.Cos(a) * r, c.Y + (float)Math.Sin(a) * r, c.Z + 3f);
            float gz;
            if (GroundZ(p, out gz) && Math.Abs(gz - c.Z) < 15f) p.Z = gz; else p.Z = c.Z;
            return p;
        }

        // ---------- foco de la ventana ----------
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
        static readonly uint myPid = (uint)Process.GetCurrentProcess().Id;

        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vKey);

        public static bool KeyDown(System.Windows.Forms.Keys k)
        {
            try { return (GetAsyncKeyState((int)k) & 0x8000) != 0; }
            catch { return false; }
        }

        public static bool IsGameFocused()
        {
            try
            {
                uint pid;
                GetWindowThreadProcessId(GetForegroundWindow(), out pid);
                return pid == myPid;
            }
            catch { return true; }
        }
    }
}
