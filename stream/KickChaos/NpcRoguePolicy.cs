using System;
using System.Globalization;

namespace KickChaos
{
    public enum RogueBuild { Agresivo, Cazador, Superviviente }

    /// <summary>Pure decisions shared by live NPCs and the policy checks. No native calls.</summary>
    public static class NpcRoguePolicy
    {
        public static string BuildName(RogueBuild build)
        {
            return build == RogueBuild.Agresivo ? "AGRESIVO" : build == RogueBuild.Cazador ? "CAZADOR" : "SUPERVIVIENTE";
        }

        public static bool ParseBuild(string text, out RogueBuild build)
        {
            build = RogueBuild.Superviviente;
            string value = (text ?? "").Trim().ToLowerInvariant().Replace(" ", "").TrimStart('!');
            if (value == "agresivo" || value == "build1") { build = RogueBuild.Agresivo; return true; }
            if (value == "cazador" || value == "build2") { build = RogueBuild.Cazador; return true; }
            return value == "superviviente" || value == "supervivencia" || value == "build3";
        }

        public static bool IsFollowSource(string source)
        {
            string value = (source ?? "").Trim();
            return value.Equals("Follow", StringComparison.OrdinalIgnoreCase) || value.Equals("Follower", StringComparison.OrdinalIgnoreCase);
        }

        public static bool ClaimId(string text, out int id)
        {
            id = 0;
            string value = (text ?? "").Trim();
            if (!value.StartsWith("!unirme", StringComparison.OrdinalIgnoreCase)) return false;
            string digits = value.Substring(7).Trim();
            return int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out id) && id > 0;
        }

        public static uint InitialHealth(bool follower, bool clone, uint baseHealth, float mainScale, float cloneScale, uint followHealth)
        {
            if (float.IsNaN(mainScale) || float.IsInfinity(mainScale)) mainScale = 1.35f;
            if (float.IsNaN(cloneScale) || float.IsInfinity(cloneScale)) cloneScale = 0.65f;
            // The clone starts below its principal even with a malformed scale in a config file.
            float principal = Math.Min(2500, Math.Max(120, baseHealth * Math.Max(1, mainScale)));
            if (follower) return (uint)Math.Max(60, Math.Min(followHealth, principal * 0.65f));
            float value = clone ? principal * Math.Max(0.2f, Math.Min(0.85f, cloneScale)) : principal;
            return (uint)Math.Max(60, Math.Min(2500, value));
        }

        public static bool CanDuplicate(bool follower, int aliveInBand, int bandLimit, int totalAlive, int totalLimit)
        {
            return !follower && aliveInBand < bandLimit && totalAlive < totalLimit;
        }

        public static uint CloneHealth(uint persistentPrincipalHealth, uint proposedHealth)
        {
            return Math.Max(1u, Math.Min(proposedHealth, (uint)(persistentPrincipalHealth * 0.85f)));
        }

        public static bool ClaimAllowed(bool follower, bool primary, bool alive, bool alreadyOwned, bool viewerHasClone)
        {
            return !follower && !primary && alive && !alreadyOwned && !viewerHasClone;
        }

        public static float FleeThreshold(RogueBuild build, float configured)
        {
            return build == RogueBuild.Agresivo ? Math.Min(configured, 0.22f) :
                build == RogueBuild.Superviviente ? Math.Max(configured, 0.55f) : Math.Max(configured, 0.35f);
        }

        public static float SearchRadius(RogueBuild build)
        {
            return build == RogueBuild.Agresivo ? 95 : build == RogueBuild.Cazador ? 80 : 55;
        }

        public static float TargetScore(RogueBuild build, float distance, float health01, int nearbyAllies)
        {
            health01 = Math.Max(0, Math.Min(1, health01));
            if (build == RogueBuild.Cazador) return distance + health01 * 28 + Math.Max(0, nearbyAllies - 1) * 12;
            if (build == RogueBuild.Superviviente) return distance + Math.Max(0, nearbyAllies - 1) * 20;
            return distance;
        }

        public static int Stance(RogueBuild build, float health, int allies, int enemies, bool justHit, int level)
        {
            if (health < FleeThreshold(build, 0.35f) || (justHit && health < 0.5f)) return 0;
            if (build == RogueBuild.Superviviente) return enemies >= allies || health < 0.7f ? 0 : 1;
            if (build == RogueBuild.Agresivo) return enemies <= allies + 2 && health >= 0.35f ? 2 : 1;
            return enemies > allies + 1 ? 0 : health > 0.7f && (allies >= enemies || level >= 4) ? 2 : 1;
        }
    }
}
