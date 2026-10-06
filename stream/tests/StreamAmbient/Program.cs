using System;
using KickChaos;

static class Program
{
    static int checks;
    static void Require(bool condition, string reason) { checks++; if (!condition) throw new Exception(reason); }
    static void Main()
    {
        Require(!AmbientRules.Roll(599, 0, 600, 100, 0), "race cooldown survives favorable roll");
        Require(AmbientRules.Roll(600, 0, 600, 15, .149), "race eligible at the cooldown boundary");
        Require(!AmbientRules.Roll(600, 0, 600, 15, .15), "percentage threshold is exclusive");
        Require(!AmbientRules.Roll(600, 0, 600, 0, 0), "disabled probability remains disabled");
        Require(!AmbientRules.Roll(600, 0, 600, 100, -1), "invalid roll cannot start an event");
        Require(AmbientRules.Quiet(100, 0, 0, 0, false, false, false), "quiet driver remains eligible");
        Require(!AmbientRules.Quiet(100, 90, 0, 0, false, false, false), "recent injury preserves combat priority");
        Require(!AmbientRules.Quiet(100, 0, 90, 0, false, false, false), "recent shot preserves combat priority");
        Require(!AmbientRules.Quiet(100, 0, 0, 90, false, false, false), "spawn reveal is not stolen by ambient event");
        Require(!AmbientRules.Quiet(100, 0, 0, 0, true, false, false), "manual order has priority");
        Require(!AmbientRules.Quiet(100, 0, 0, 0, false, true, false), "perk choice has priority");
        Require(!AmbientRules.Quiet(100, 0, 0, 0, false, false, true), "cannot join two events");
        Require(AmbientRules.CompatibleDrivers(true, true, true, true, false, false, false, 50, 0, 85), "nearby calm drivers can travel together");
        Require(!AmbientRules.CompatibleDrivers(true, true, true, true, false, false, true, 50, 0, 85), "combat rivals cannot be quietly allied");
        Require(!AmbientRules.CompatibleDrivers(true, true, true, true, false, false, false, 50, 20, 85), "overpass does not create a convoy below it");
        Require(!AmbientRules.CompatibleDrivers(true, true, true, true, false, false, false, 100, 0, 85), "convoy is a local encounter");
        Require(!AmbientRules.CompatibleDrivers(true, true, true, true, false, false, false, 5, 0, 85), "avoid bumper-to-bumper formation");
        Require(!AmbientRules.CompatibleDrivers(false, true, true, true, false, false, false, 50, 0, 85), "dead driver is excluded");
        Require(!AmbientRules.CompatibleDrivers(true, true, false, true, false, false, false, 50, 0, 85), "passenger cannot receive a driving task");
        Require(AmbientRules.RefreshDrive(true, 0, 0, -1, 0), "initial route task is issued");
        for (int i = 1; i < 12; i++) Require(!AmbientRules.RefreshDrive(false, i, 0, 0, 100), "moving leader does not restart driving each tick");
        Require(!AmbientRules.RefreshDrive(false, 60, 0, -1, 20), "healthy race route remains unchanged after one minute");
        Require(AmbientRules.RefreshDrive(false, 12, 0, 0, 0), "stationary driver receives bounded route recovery");
        Require(AmbientRules.RefreshDrive(false, 12, 0, -1, 65), "convoy can refresh a meaningfully moved destination");
        Require(!AmbientRules.EndAlliance(true, true, false, false, 80, 99, 100), "ordinary travel preserves alliance");
        Require(AmbientRules.EndAlliance(false, true, false, false, 80, 99, 100), "driver death releases alliance");
        Require(AmbientRules.EndAlliance(true, false, false, false, 80, 99, 100), "driver exit releases alliance");
        Require(AmbientRules.EndAlliance(true, true, true, false, 80, 99, 100), "viewer order releases alliance");
        Require(AmbientRules.EndAlliance(true, true, false, true, 80, 99, 100), "real incoming damage ends scripted calmness");
        Require(AmbientRules.EndAlliance(true, true, false, false, 301, 99, 100), "separated convoy dissolves");
        Require(AmbientRules.EndAlliance(true, true, false, false, 80, 100, 100), "alliance lease expires");
        Require(!AmbientRules.EndAlliance(true, true, false, false, 80, 99, 150, 80), "normal red light preserves convoy");
        Require(AmbientRules.EndAlliance(true, true, false, false, 80, 99, 150, 60), "stalled leader releases convoy for normal recovery");
        Require(AmbientRules.RestoredRelationship(false, false, true, true, false, false) == 5, "expired alliance restores enabled rivalry");
        Require(AmbientRules.RestoredRelationship(false, false, true, true, true, false) == 3, "ending convoy preserves concurrent race truce");
        Require(AmbientRules.RestoredRelationship(false, false, true, true, false, true) == 3, "ending race preserves other alliance");
        Require(AmbientRules.RestoredRelationship(false, true, true, true, false, false) == 3, "follower cohort stays friendly after event");
        Require(AmbientRules.RestoredRelationship(false, false, false, true, false, false) == 3, "quiet wanderer is not made hostile by cleanup");
        var random = new Random(42);
        double last = -10000; int events = 0;
        for (int seconds = 120; seconds <= 10800; seconds += 120)
        {
            if (!AmbientRules.Roll(seconds, last, 600, 15, random.NextDouble())) continue;
            Require(seconds - last >= 600, "three-hour schedule preserves race cooldown");
            last = seconds; events++;
        }
        Require(events >= 3 && events <= 18, "stream defaults produce occasional events over three hours");
        Console.WriteLine("StreamAmbient: " + checks + " checks passed; " + events + " potential races in a seeded three-hour schedule.");
    }
}
