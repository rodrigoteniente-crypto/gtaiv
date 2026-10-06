using System.Numerics;
using KickChaos;

int checks = 0;
void Check(bool condition, string message)
{
    checks++;
    if (!condition) throw new Exception(message);
}
void ExactCars(params int[] handles) => Check(N.DeletedCars.Order().SequenceEqual(handles.Order()),
    $"Deleted cars were [{string.Join(",", N.DeletedCars)}], expected [{string.Join(",", handles)}]");
void NoAreaDeletion()
{
    Check(N.AreaCars == 0, "Cleanup used CLEAR_AREA_OF_CARS and risks neighboring vehicles");
    Check(N.AreaChars == 0, "Cleanup used CLEAR_AREA_OF_CHARS and risks living/protected NPCs");
}
void Car(int handle, uint health = 1000, bool dead = false, float x = 0, bool onScreen = false)
{
    G.Cars[handle] = new CarState
    {
        Health = health, Dead = dead, Position = new Vector3(x, 0, 0), OnScreen = onScreen
    };
}

// A repair occurring during subscriber combat must preserve police and vehicles
// owned by the active scene while removing only unrelated wrecks and corpses.
G.Reset();
Car(101, dead: true); G.ProtectedCars.Add(101);
Car(102, health: 100);
Car(103); // A healthy vehicle directly beside the damaged car.
G.Peds[201] = new PedState { Dead = true };
G.Peds[202] = new PedState { Dead = true }; G.ProtectedPeds.Add(202);
G.Peds[203] = new PedState { Dead = false };
G.Peds[204] = new PedState { Dead = true, Position = new Vector3(500, 0, 0) };
float checkedRadius = 0;
var activeRepair = new StreetRepair
{
    Radius = 70,
    IsActiveNpcZone = (center, radius) => { checkedRadius = radius; return true; }
};
activeRepair.RepairArea(Vector3.Zero);
Check(N.AreaCops == 0, "Repair cleared police during an active subscriber fight");
Check(checkedRadius == 150, "Active scene protection must include the police approach margin");
ExactCars(102);
Check(G.Cars.ContainsKey(101) && G.Cars.ContainsKey(103), "Protected car or healthy neighbor disappeared");
Check(N.DeletedPeds.SequenceEqual(new[] { 201 }), "Corpse cleanup deleted a living/protected/distant ped");
Check(G.Peds.ContainsKey(202) && G.Peds.ContainsKey(203) && G.Peds.ContainsKey(204), "Corpse cleanup lost neighbors");
Check(N.AreaObjects == 1 && N.Extinguished == 1, "Safe fire/debris cleanup no longer runs");
Check(G.IncludeDeadQueries.All(includeDead => includeDead), "Repair failed to enumerate wrecked vehicles");
NoAreaDeletion();

G.Reset();
var inactiveRepair = new StreetRepair { IsActiveNpcZone = (center, radius) => false };
inactiveRepair.RepairArea(Vector3.Zero);
Check(N.AreaCops == 1, "Police cleanup outside subscriber scenes should still honor its enabled setting");
NoAreaDeletion();

// 'todo' previously bypassed protected-car exclusions through an area native.
G.Reset();
Car(11, dead: true); G.ProtectedCars.Add(11);
Car(12); G.ProtectedCars.Add(12);
Car(13);
Car(14, dead: true);
var allTraffic = new TrafficCleaner { OnSwitch = "todo" };
allTraffic.OnCameraSwitch(Vector3.Zero);
ExactCars(13, 14);
Check(G.Cars.ContainsKey(11) && G.Cars.ContainsKey(12), "Camera switch removed protected cars in 'todo' mode");
Check(allTraffic.Removed == 2, "Removed count must exclude retained protected cars");
NoAreaDeletion();

G.Reset();
Car(21, dead: true); G.ProtectedCars.Add(21);
Car(22, health: 100);
Car(23);
var wreckTraffic = new TrafficCleaner { OnSwitch = "chocados" };
wreckTraffic.OnCameraSwitch(Vector3.Zero);
ExactCars(22);
Check(G.Cars.ContainsKey(21) && G.Cars.ContainsKey(23), "Wreck cleanup removed protected or healthy neighboring traffic");
Check(wreckTraffic.Removed == 1, "Wreck cleanup removed counter is incorrect");
NoAreaDeletion();

// Automatic scans keep visible cars and cars in a cinematic frame as well.
G.Reset();
Car(31, dead: true); G.ProtectedCars.Add(31);
Car(32, health: 100, x: 1);
Car(33, health: 100, x: 2, onScreen: true);
Car(34, health: 100, x: 3);
Car(35, x: 4);
var automaticTraffic = new TrafficCleaner();
automaticTraffic.Update(Vector3.Zero, calm: true, secondary: false,
    inFrame: (position, radius) => position.X == 3);
ExactCars(32);
Check(new[] { 31, 33, 34, 35 }.All(G.Cars.ContainsKey), "Automatic cleanup deleted protected/visible/in-frame/healthy traffic");
Check(automaticTraffic.Removed == 1, "Automatic cleanup removed counter is incorrect");
NoAreaDeletion();

G.Reset();
Car(41, dead: true);
new TrafficCleaner { OnSwitch = "nada" }.OnCameraSwitch(Vector3.Zero);
ExactCars();
Check(G.Cars.ContainsKey(41), "Disabled camera-switch cleanup removed a vehicle");
NoAreaDeletion();

Console.WriteLine($"PASS: {checks} assertions across six cleanup scenarios using production StreetRepair/TrafficCleaner source.");
