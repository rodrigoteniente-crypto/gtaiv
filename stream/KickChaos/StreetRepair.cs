using System;
using System.Collections.Generic;
using System.Numerics;
using static KickChaos.N;

namespace KickChaos
{
    /// <summary>
    /// "Arreglar la calle": un rato despues de las acciones saca autos destruidos, cuerpos, fuego,
    /// escombros y policias que quedaron, para que la escena vuelva a la normalidad.
    /// </summary>
    public class StreetRepair
    {
        public float Delay = 60f;             // segundos despues de la ultima accion (0 = nunca)
        public string Mode = "fundido";       // fundido | cambio | directo
        public float Radius = 70f;
        public bool Debris = true, Cops = true;
        public int Repairs;

        readonly List<Vector3> dirty = new List<Vector3>();
        double lastDirty = -1, firstDirty = -1;

        public void ApplyConfig(IniFile ini)
        {
            Delay = Math.Max(0f, ini.GetFloat("Arreglar", "Tras", 60f));
            Mode = Config.Normalize(ini.Get("Arreglar", "Forma", "fundido")).Trim();
            Radius = Math.Max(20f, ini.GetFloat("Arreglar", "Radio", 70f));
            Debris = ini.GetBool("Arreglar", "Escombros", true);
            Cops = ini.GetBool("Arreglar", "Policias", true);
        }

        public bool IsDirty { get { return dirty.Count > 0; } }

        /// <summary>Segundos hasta el arreglo (-1 si no hay nada que arreglar o esta apagado).</summary>
        public double SecondsLeft()
        {
            if (dirty.Count == 0 || Delay <= 0) return -1;
            double due = Math.Min(lastDirty + Delay, firstDirty + Math.Max(Delay * 3, 180));
            return Math.Max(0, due - G.Now);
        }

        public void MarkDirty(Vector3 where)
        {
            double now = G.Now;
            if (dirty.Count == 0) firstDirty = now;
            lastDirty = now;
            foreach (var d in dirty) if (Vector3.Distance(d, where) < Radius * 0.5f) return;
            dirty.Add(where);
            if (dirty.Count > 12) dirty.RemoveAt(0);
        }

        public bool IsDue()
        {
            if (Delay <= 0 || dirty.Count == 0) return false;
            double now = G.Now;
            // espera X segundos sin acciones; si el chat no para, igual arregla cada tanto
            return now - lastDirty >= Delay || now - firstDirty >= Math.Max(Delay * 3, 180);
        }

        /// <summary>Arregla todas las zonas marcadas. Llamar idealmente con la pantalla en negro.</summary>
        public void RepairNow(ChaosActions actions)
        {
            if (actions != null) actions.RemoveFires();
            foreach (Vector3 c in dirty.ToArray()) RepairArea(c);
            dirty.Clear();
            lastDirty = firstDirty = -1;
            Repairs++;
        }

        public void RepairArea(Vector3 c)
        {
            EXTINGUISH_FIRE_AT_POINT(c, Radius);
            // autos destruidos, en llamas, dados vuelta o muy golpeados, y los abandonados
            foreach (int car in G.VehiclesNear(c, Radius, 80, true))
            {
                if (G.ProtectedCars.Contains(car)) continue;
                bool bad = IS_CAR_DEAD(car) || IS_CAR_ON_FIRE(car) || IS_CAR_UPSIDEDOWN(car) || IS_CAR_STUCK_ON_ROOF(car);
                if (!bad)
                {
                    uint health;
                    GET_CAR_HEALTH(car, out health);
                    int driver;
                    GET_DRIVER_OF_CAR(car, out driver);
                    // abandonados: solo si estan golpeados o son de emergencia (los estacionados normales se quedan)
                    bad = health < 650 || (driver == 0 && (health < 950 || IS_EMERGENCY_SERVICES_VEHICLE(car)));
                }
                if (bad) CLEAR_AREA_OF_CARS(G.CarPos(car), 1.8f);
            }
            // cuerpos
            foreach (int ped in G.DeadPedsNear(c, Radius, 60))
                CLEAR_AREA_OF_CHARS(G.CharPos(ped), 0.6f);
            if (Debris) CLEAR_AREA_OF_OBJECTS(c, Radius);
            if (Cops) CLEAR_AREA_OF_COPS(c, Radius);
        }

        public void Forget() { dirty.Clear(); lastDirty = firstDirty = -1; }
    }
}
