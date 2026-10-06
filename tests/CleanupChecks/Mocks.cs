using System.Numerics;

namespace KickChaos;

// This harness replaces only the game boundary. Both cleanup controllers above
// are the production source; calls mutate an in-memory world of native handles.
internal sealed class CarState
{
    public Vector3 Position;
    public uint Health = 1000;
    public int Driver = 77;
    public float Speed = 10;
    public bool Dead { get; set; }
    public bool OnFire { get; set; }
    public bool UpsideDown { get; set; }
    public bool OnRoof { get; set; }
    public bool Emergency { get; set; }
    public bool Stuck { get; set; }
    public bool OnScreen { get; set; }
}

internal sealed class PedState
{
    public Vector3 Position;
    public bool Dead;
}

internal static class G
{
    public static double Now;
    public static readonly HashSet<int> ProtectedCars = new();
    public static readonly HashSet<int> ProtectedPeds = new();
    public static readonly Dictionary<int, CarState> Cars = new();
    public static readonly Dictionary<int, PedState> Peds = new();
    public static readonly List<bool> IncludeDeadQueries = new();

    public static List<int> VehiclesNear(Vector3 center, float radius, int max, bool includeDead)
    {
        IncludeDeadQueries.Add(includeDead);
        return Cars.Where(car => Vector3.Distance(center, car.Value.Position) <= radius
                && (includeDead || !car.Value.Dead))
            .Select(car => car.Key).Take(max).ToList();
    }

    // Mirrors G.DeadPedsNear's documented game boundary: only unprotected corpses.
    public static List<int> DeadPedsNear(Vector3 center, float radius, int max) =>
        Peds.Where(ped => ped.Value.Dead && !ProtectedPeds.Contains(ped.Key)
                && Vector3.Distance(center, ped.Value.Position) <= radius)
            .Select(ped => ped.Key).Take(max).ToList();

    public static Vector3 CarPos(int car) => Cars[car].Position;

    public static void Reset()
    {
        Now = 10;
        ProtectedCars.Clear();
        ProtectedPeds.Clear();
        Cars.Clear();
        Peds.Clear();
        IncludeDeadQueries.Clear();
        N.Reset();
    }
}

internal static class N
{
    public static readonly List<int> DeletedCars = new();
    public static readonly List<int> DeletedPeds = new();
    public static int AreaCars, AreaChars, AreaCops, AreaObjects, Extinguished;

    public static bool IS_CAR_DEAD(int car) => G.Cars[car].Dead;
    public static bool IS_CAR_ON_FIRE(int car) => G.Cars[car].OnFire;
    public static bool IS_CAR_UPSIDEDOWN(int car) => G.Cars[car].UpsideDown;
    public static bool IS_CAR_STUCK_ON_ROOF(int car) => G.Cars[car].OnRoof;
    public static bool IS_EMERGENCY_SERVICES_VEHICLE(int car) => G.Cars[car].Emergency;
    public static bool IS_CAR_STUCK(int car) => G.Cars[car].Stuck;
    public static bool IS_CAR_ON_SCREEN(int car) => G.Cars[car].OnScreen;
    public static void GET_CAR_HEALTH(int car, out uint health) => health = G.Cars[car].Health;
    public static void GET_DRIVER_OF_CAR(int car, out int ped) => ped = G.Cars[car].Driver;
    public static void GET_CAR_SPEED(int car, out float speed) => speed = G.Cars[car].Speed;
    public static void SET_CAR_AS_MISSION_CAR(int car) { }
    public static void DELETE_CAR(int car)
    {
        DeletedCars.Add(car);
        G.Cars.Remove(car);
    }
    public static void DELETE_CHAR(int ped)
    {
        DeletedPeds.Add(ped);
        G.Peds.Remove(ped);
    }
    public static void EXTINGUISH_FIRE_AT_POINT(Vector3 center, float radius) => Extinguished++;
    public static void CLEAR_AREA_OF_OBJECTS(Vector3 center, float radius) => AreaObjects++;
    public static void CLEAR_AREA_OF_COPS(Vector3 center, float radius) => AreaCops++;

    // Intentionally retain unsafe area natives so a regression still compiles
    // and fails by deleting the healthy/protected neighbors in these scenarios.
    public static void CLEAR_AREA_OF_CARS(Vector3 center, float radius)
    {
        AreaCars++;
        foreach (int car in G.Cars.Where(pair => Vector3.Distance(center, pair.Value.Position) <= radius)
                     .Select(pair => pair.Key).ToArray())
            DELETE_CAR(car);
    }
    public static void CLEAR_AREA_OF_CHARS(Vector3 center, float radius)
    {
        AreaChars++;
        foreach (int ped in G.Peds.Where(pair => Vector3.Distance(center, pair.Value.Position) <= radius)
                     .Select(pair => pair.Key).ToArray())
            DELETE_CHAR(ped);
    }

    public static void Reset()
    {
        DeletedCars.Clear();
        DeletedPeds.Clear();
        AreaCars = AreaChars = AreaCops = AreaObjects = Extinguished = 0;
    }
}

public sealed class IniFile
{
    public float GetFloat(string section, string key, float def) => def;
    public string Get(string section, string key, string def) => def;
    public bool GetBool(string section, string key, bool def) => def;
}

public static class Config
{
    public static string Normalize(string value) => value.ToLowerInvariant();
}

public sealed class ChaosActions
{
    public int FiresRemoved;
    public void RemoveFires() => FiresRemoved++;
}
