using System;

namespace KickChaos
{
    // Scheduling and task leases stay independent of GTA native calls.
    public static class AmbientRules
    {
        public static bool Roll(double now, double lastStarted, float cooldown, float probabilityPercent, double roll)
        {
            return now - lastStarted >= cooldown && probabilityPercent > 0 &&
                roll >= 0 && roll < Math.Min(100f, probabilityPercent) / 100.0;
        }

        public static bool Quiet(double now, double lastHit, double lastShot, double created, bool order, bool offer, bool busy)
        {
            return !order && !offer && !busy && now - lastHit >= 20 && now - lastShot >= 20 && now - created >= 15;
        }

        public static bool CompatibleDrivers(bool aAlive, bool bAlive, bool aDriving, bool bDriving,
            bool aBusy, bool bBusy, bool combat, float distance, float heightDifference, float radius)
        {
            return aAlive && bAlive && aDriving && bDriving && !aBusy && !bBusy && !combat &&
                distance >= 12 && distance <= radius && Math.Abs(heightDifference) <= 8;
        }

        public static bool RefreshDrive(bool first, double now, double lastTask, double stillSince,
            float destinationMoved, float driftThreshold = 65f)
        {
            if (first) return true;
            if (now - lastTask < 12) return false;
            return (stillSince >= 0 && now - stillSince >= 10) || destinationMoved >= driftThreshold;
        }

        public static bool EndAlliance(bool participantAlive, bool stillDriving, bool recentOrder,
            bool attacked, float separation, double now, double ends, double stationarySince = -1)
        {
            return !participantAlive || !stillDriving || recentOrder || attacked || separation > 300 || now >= ends ||
                stationarySince >= 0 && now - stationarySince > 35;
        }

        public static uint RestoredRelationship(bool sameGroup, bool sameFollowerCohort, bool bothFighters,
            bool duel, bool stillRaceAllies, bool stillConvoyAllies)
        {
            return sameGroup || sameFollowerCohort || stillRaceAllies || stillConvoyAllies || !bothFighters || !duel ? 3u : 5u;
        }
    }
}
