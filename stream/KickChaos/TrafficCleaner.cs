using System;
using System.Collections.Generic;
using System.Numerics;
using static KickChaos.N;

namespace KickChaos
{
    /// <summary>
    /// Saca autos chocados, dados vuelta, prendidos fuego o trabados alrededor de la escena,
    /// pero solo cuando la camara no los esta mostrando (o con la pantalla en negro), asi no se nota.
    /// </summary>
    public class TrafficCleaner
    {
        public bool Enabled = true;
        public string OnSwitch = "chocados"; // chocados | todo | nada
        public float Radius = 90f;
        public int Removed;

        readonly Dictionary<int, double> stoppedSince = new Dictionary<int, double>();
        double lastScan;

        public void ApplyConfig(IniFile ini)
        {
            Enabled = ini.GetBool("Trafico", "SacarChocados", true);
            OnSwitch = Config.Normalize(ini.Get("Trafico", "AlCambiarCamara", "chocados")).Trim();
            Radius = Math.Max(20f, ini.GetFloat("Trafico", "Radio", 90f));
        }

        /// <summary>Un auto "problematico": chocado fuerte, dado vuelta, en llamas, destruido o trabado.</summary>
        bool IsProblem(int car, double now, bool calm)
        {
            if (IS_CAR_DEAD(car) || IS_CAR_ON_FIRE(car) || IS_CAR_UPSIDEDOWN(car) || IS_CAR_STUCK_ON_ROOF(car)) return true;
            uint health;
            GET_CAR_HEALTH(car, out health);
            if (health < 400) return true;

            float speed;
            GET_CAR_SPEED(car, out speed);
            if (speed > 0.7f) { stoppedSince.Remove(car); return false; }
            double since;
            if (!stoppedSince.TryGetValue(car, out since)) { stoppedSince[car] = now; return false; }
            double stopped = now - since;
            if (!calm) return false; // durante una accion no tocamos autos parados (patrulleros, etc.)
            int driver;
            GET_DRIVER_OF_CAR(car, out driver);
            // abandonado en la calle (golpeado o de emergencia; los estacionados sanos no se tocan)
            if (driver == 0 && stopped > 40 && (health < 950 || IS_EMERGENCY_SERVICES_VEHICLE(car))) return true;
            if (stopped > 20 && IS_CAR_STUCK(car)) return true;         // trabado contra algo
            if (driver != 0 && stopped > 75) return true;               // embotellamiento eterno
            return false;
        }

        static void Remove(int car)
        {
            Vector3 p = G.CarPos(car);
            CLEAR_AREA_OF_CARS(p, 1.8f);
        }

        /// <summary>Cada segundo: sacar los problematicos que NO se ven.</summary>
        public void Update(Vector3 center, bool calm, bool secondary, Func<Vector3, float, bool> inFrame)
        {
            if (!Enabled) return;
            double now = G.Now;
            if (!secondary)
            {
                if (now - lastScan < 1.0) return;
                lastScan = now;
            }
            else if (now - lastScan > 0.05) return; // el segundo centro solo en el mismo frame que el primero
            var seen = new HashSet<int>();
            foreach (int car in G.VehiclesNear(center, Radius, 60, true))
            {
                seen.Add(car);
                if (G.ProtectedCars.Contains(car)) continue; // auto del NPC o de la policia que lo persigue
                if (!IsProblem(car, now, calm)) continue;
                // en cuadro (o casi): se saca en el proximo cambio de camara, con la pantalla en negro
                if (IS_CAR_ON_SCREEN(car) || (inFrame != null && inFrame(G.CarPos(car), 6f))) continue;
                Remove(car);
                stoppedSince.Remove(car);
                Removed++;
            }
            // olvidar autos que ya no estan cerca
            if (stoppedSince.Count > 200)
            {
                var old = new List<int>();
                foreach (var kv in stoppedSince) if (!seen.Contains(kv.Key)) old.Add(kv.Key);
                foreach (int k in old) stoppedSince.Remove(k);
            }
        }

        /// <summary>Al cambiar de camara (la toma nueva todavia no se ve): limpiar la zona nueva.</summary>
        public void OnCameraSwitch(Vector3 center)
        {
            if (OnSwitch.StartsWith("nada")) return;
            if (OnSwitch.StartsWith("todo"))
            {
                CLEAR_AREA_OF_CARS(center, Radius);
                stoppedSince.Clear();
                return;
            }
            if (!Enabled && !OnSwitch.StartsWith("choc")) return;
            double now = G.Now;
            foreach (int car in G.VehiclesNear(center, Radius, 80, true))
            {
                if (G.ProtectedCars.Contains(car)) continue;
                if (IsProblem(car, now, true) || IsLongStopped(car, now)) { Remove(car); stoppedSince.Remove(car); Removed++; }
            }
        }

        bool IsLongStopped(int car, double now)
        {
            double since;
            if (!stoppedSince.TryGetValue(car, out since) || now - since <= 30) return false;
            int driver;
            GET_DRIVER_OF_CAR(car, out driver);
            return driver != 0; // trabado con conductor (los estacionados no cuentan)
        }

        public void Reset() { stoppedSince.Clear(); }
    }
}
