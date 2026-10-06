using System;
using KickChaos;

class Program
{
    static int checks;
    static void Check(bool pass, string why)
    {
        checks++;
        if (!pass) throw new Exception(why);
    }

    static void Main()
    {
        RogueBuild build;
        Check(NpcRoguePolicy.ParseBuild("!AGRESIVO", out build) && build == RogueBuild.Agresivo, "case insensitive aggressive command");
        Check(NpcRoguePolicy.ParseBuild(" !build2 ", out build) && build == RogueBuild.Cazador, "hunter option command");
        Check(NpcRoguePolicy.ParseBuild("!superviviente", out build) && build == RogueBuild.Superviviente, "survivor command");
        Check(!NpcRoguePolicy.ParseBuild("!1", out build), "build picks do not hijack existing perk voting");
        Check(!NpcRoguePolicy.ParseBuild("!atacar", out build), "existing commands retain their meaning");
        int id;
        Check(NpcRoguePolicy.ClaimId("!unirme34", out id) && id == 34, "unique exact character claim");
        Check(NpcRoguePolicy.ClaimId(" !UNIRME 34 ", out id) && id == 34, "spacing and case accepted");
        foreach (string text in new[] { "!unirme", "!unirme-1", "!unirme0", "!unirme34x", "!unirme2147483648", "unirme34", "!unirme34 hola", "!unirme+34", "!unirme3.4" })
            Check(!NpcRoguePolicy.ClaimId(text, out id), "invalid claim rejected: " + text);
        Check(NpcRoguePolicy.IsFollowSource("follow") && NpcRoguePolicy.IsFollowSource("Follower"), "follower source recognized");
        Check(!NpcRoguePolicy.IsFollowSource("Suscripcion") && !NpcRoguePolicy.IsFollowSource("Regalos"), "subscriber gifts are not weak follows");
        for (int flags = 0; flags < 32; flags++)
        {
            bool follower = (flags & 1) != 0, primary = (flags & 2) != 0, alive = (flags & 4) != 0;
            bool claimed = (flags & 8) != 0, viewerHasClone = (flags & 16) != 0;
            bool allowed = NpcRoguePolicy.ClaimAllowed(follower, primary, alive, claimed, viewerHasClone);
            Check(allowed == (flags == 4), "only a living, unclaimed clone can be claimed once");
        }
        for (int total = 0; total < 100; total++)
            for (int alive = 0; alive < 8; alive++)
            {
                Check(!NpcRoguePolicy.CanDuplicate(true, alive, 4, total, 48), "followers never duplicate");
                Check(NpcRoguePolicy.CanDuplicate(false, alive, 4, total, 48) == (alive < 4 && total < 48), "clone capacity bounded without evicting live NPCs");
            }
        foreach (uint health in new uint[] { 0, 60, 120, 200, 350, 700, 2500, 4000 })
            foreach (float mainScale in new[] { 0f, 1f, 1.35f, 4f, float.NaN, float.PositiveInfinity })
                foreach (float cloneScale in new[] { 0f, 0.2f, 0.65f, 0.85f, 3f, float.NaN })
                {
                    uint principal = NpcRoguePolicy.InitialHealth(false, false, health, mainScale, cloneScale, 500);
                    uint clone = NpcRoguePolicy.InitialHealth(false, true, health, mainScale, cloneScale, 500);
                    uint follow = NpcRoguePolicy.InitialHealth(true, false, health, mainScale, cloneScale, 500);
                    Check(principal >= 120 && principal <= 2500, "principal health bounded");
                    Check(clone >= 60 && clone < principal, "principal always stronger than clone, including malformed settings");
                    Check(follow >= 60 && follow < principal, "followers remain weaker than principal");
                }
        Check(NpcRoguePolicy.TargetScore(RogueBuild.Cazador, 30, 0.2f, 1) < NpcRoguePolicy.TargetScore(RogueBuild.Cazador, 20, 1, 1), "hunter can prefer wounded enemy farther away");
        Check(NpcRoguePolicy.TargetScore(RogueBuild.Cazador, 25, 0.5f, 1) < NpcRoguePolicy.TargetScore(RogueBuild.Cazador, 25, 0.5f, 3), "hunter prefers isolated enemies");
        Check(NpcRoguePolicy.TargetScore(RogueBuild.Agresivo, 15, 1, 4) < NpcRoguePolicy.TargetScore(RogueBuild.Agresivo, 20, 0.2f, 1), "aggressive prioritizes reaching the fight");
        Check(NpcRoguePolicy.Stance(RogueBuild.Superviviente, 0.9f, 1, 2, false, 1) == 0, "survivor takes cover when outnumbered");
        Check(NpcRoguePolicy.Stance(RogueBuild.Agresivo, 0.9f, 1, 2, false, 1) == 2, "aggressive accepts greater risk");
        Check(NpcRoguePolicy.Stance(RogueBuild.Superviviente, 0.9f, 3, 1, false, 1) == 1, "survivor fights with advantage");
        Check(NpcRoguePolicy.FleeThreshold(RogueBuild.Superviviente, 0.35f) > NpcRoguePolicy.FleeThreshold(RogueBuild.Agresivo, 0.35f), "different builds make distinct survival decisions");
        Check(NpcRoguePolicy.SearchRadius(RogueBuild.Agresivo) > NpcRoguePolicy.SearchRadius(RogueBuild.Superviviente), "aggressive searches farther");
        Check(NpcRoguePolicy.CloneHealth(472, 919) < 472, "raising live settings never creates a clone stronger than its persistent principal");
        Check(NpcRoguePolicy.CloneHealth(472, 200) == 200, "clone cap does not increase weak clones");
        Console.WriteLine("NPC policy: " + checks + " checks passed");
    }
}
