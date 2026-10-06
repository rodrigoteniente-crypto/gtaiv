namespace KickChaos
{
    internal static class StreamPolicePolicy
    {
        internal static bool DirectFire(bool nativeAi, bool aggressive, double taskAge,
            double shotAge, double progressAge, bool inCover)
        {
            if (aggressive) return true;
            if (!nativeAi) return taskAge >= 8 && shotAge >= 8;
            return taskAge >= 30 && shotAge >= 30 && progressAge >= 20 && (!inCover || taskAge >= 60);
        }
        internal static bool FinishEntry(double taskAge, bool enteringAnimation)
        {
            return taskAge < (enteringAnimation ? 22 : 16);
        }
    }
}
