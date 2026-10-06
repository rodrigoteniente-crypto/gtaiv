using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using KickChaos;

static class Program
{
    static int assertions;
    static void Check(bool ok, string label)
    {
        assertions++;
        if (!ok) throw new Exception(label);
    }
    static void Main()
    {
        var rng = new Random(184);
        var shots = new List<CameraShot>
        {
            new CameraShot { Name = "A" },
            new CameraShot { Name = "Disabled", Active = false },
            new CameraShot { Name = "B", Weight = 9 },
            new CameraShot { Name = "Auto", IsAuto = true }
        };
        Check(CameraShot.SelectNextIndex(shots, 0, false, rng) == 2, "Sequential skips disabled/generated");
        Check(CameraShot.SelectNextIndex(shots, 2, false, rng) == 0, "Loop wraps");
        for (int i = 0; i < 1000; i++) Check(CameraShot.SelectNextIndex(shots, 0, true, rng) == 2, "No immediate random repeat");
        shots[2].Active = false;
        Check(CameraShot.SelectNextIndex(shots, 0, true, rng) == 0, "Only active camera repeats");
        shots[0].Active = false;
        Check(CameraShot.SelectNextIndex(shots, -1, true, rng) == -1, "All disabled => no manual camera");
        shots[0].Active = shots[2].Active = true;
        int weightedSelections = 0;
        for (int i = 0; i < 20000; i++)
        {
            int selected = CameraShot.SelectNextIndex(shots, -1, true, rng);
            if (selected == 2) weightedSelections++;
            else if (selected != 0) throw new Exception("Ineligible random selection");
        }
        // A weight of 9 against 1 should select B approximately 90% of the
        // time. A fixed seed makes this check repeatable on each run.
        Check(weightedSelections > 17000 && weightedSelections < 19000, "Weights affect random selection");
        shots[2].Weight = float.NaN;
        Check(CameraShot.SelectNextIndex(shots, 0, true, rng) == 2, "Invalid weight uses default");
        Check(CameraShot.SelectNextIndex(shots, 999, false, rng) == 0, "Stale index recovers");
        Check(MathX.Clamp(float.NaN, 3, 120, 25) == 25, "FOV rejects NaN");
        Check(Math.Abs(MathX.LerpAngle(350, 10, 0.5f) - 360) < 0.001, "Angles use shortest path");
        var warnings = new List<string>();
        var original = CameraStore.Load(Path.Combine(AppContext.BaseDirectory, "fixtures"), 25, 20, warnings);
        Check(original.Count == 33, "Preserve all 33 original cameras");
        var folder = Path.Combine(Path.GetTempPath(), "camera-store-" + Guid.NewGuid().ToString("N"));
        try
        {
            CameraStore.Save(folder, original);
            var roundtrip = CameraStore.Load(folder, 25, 20, warnings);
            Check(roundtrip.Count == original.Count, "Camera roundtrip count");
            for (int i = 0; i < original.Count; i++)
            {
                Check(roundtrip[i].Name == original[i].Name, "Names persist");
                Check(Vector3.Distance(roundtrip[i].Pos, original[i].Pos) < 0.001f, "Positions persist");
                Check(Vector3.Distance(roundtrip[i].Rot, original[i].Rot) < 0.001f, "Rotations persist");
                Check(Math.Abs(roundtrip[i].Fov - original[i].Fov) < 0.001f, "FOV persists");
                Check(roundtrip[i].Active == original[i].Active, "Enabled state persists");
                Check(Math.Abs(roundtrip[i].Weight - original[i].Weight) < 0.001f, "Weights persist");
                Check(roundtrip[i].HasTarget == original[i].HasTarget, "Target state persists");
                Check(Vector3.Distance(roundtrip[i].Target, original[i].Target) < 0.001f, "Targets persist");
            }
            var path = Path.Combine(folder, CameraStore.FileName);
            string previous = File.ReadAllText(path);
            roundtrip[0].Fov = 52;
            CameraStore.Save(folder, roundtrip);
            Check(File.ReadAllText(path + ".bak") == previous, "Atomic save backs up previous cameras");
            Check(CameraStore.Load(folder, 25, 20, warnings)[0].Fov == 52, "Atomic save installs latest cameras");
            string persisted = File.ReadAllText(path);
            File.Delete(path + ".bak");
            Directory.CreateDirectory(path + ".bak");
            roundtrip[0].Fov = 63;
            bool saveFailed = false;
            try { CameraStore.Save(folder, roundtrip); }
            catch (IOException) { saveFailed = true; }
            catch (UnauthorizedAccessException) { saveFailed = true; }
            Check(saveFailed, "Unsupported backup path causes save failure");
            Check(File.ReadAllText(path) == persisted, "Failed save preserves existing cameras");
            Check(Directory.GetFiles(folder, "*.tmp").Length == 0, "Failed save cleans temporary files");
            Directory.Delete(folder, true);
            Directory.CreateDirectory(folder);
            File.WriteAllText(path, "[Invalid]\nPos=NaN,0,0\nRot=0,0,0\n[Valid]\nPos=1,2,3\nRot=0,0,0\nFOV=NaN\nFOVFinal=-1\nChance=Infinity\nDuracion=Infinity\n");
            var invalid = CameraStore.Load(folder, float.PositiveInfinity, 20, warnings);
            Check(invalid.Count == 1, "Invalid position ignored");
            Check(invalid[0].Fov == 25, "Invalid default/individual FOV fallback");
            Check(!invalid[0].HasEnd, "Negative end FOV means no endpoint");
            Check(invalid[0].Weight == 1 && invalid[0].Duration == -1, "Invalid weight/duration fallback");
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
        Console.WriteLine("PASS " + assertions + " camera assertions; 33 original cameras retained; weighted sampling 20,000 trials.");
    }
}
