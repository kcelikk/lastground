namespace LastGround.App
{
    /// <summary>
    /// First-launch quality tier from device RAM (D-006). Replaced by DeviceTierDetector + benchmark in M2.
    /// </summary>
    public static class QualityDefaults
    {
        public const int Low = 0;
        public const int Medium = 1;
        public const int High = 2;

        public static int FromSystemMemory(int systemMemoryMb)
        {
            if (systemMemoryMb < 6000) return Low;
            if (systemMemoryMb < 8000) return Medium;
            return High;
        }

        /// <summary>LOW is locked to 30 FPS; other tiers use the player's setting (TDD_02 §21.9).</summary>
        public static int TargetFps(int qualityLevel, int preferredFps)
        {
            return qualityLevel == Low ? 30 : preferredFps;
        }
    }
}
