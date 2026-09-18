namespace LastGround.Save
{
    /// <summary>Device-specific settings (settings.json). Never shared over the network.</summary>
    public sealed class SettingsData
    {
        public const int CurrentVersion = 1;

        public int Version = CurrentVersion;

        /// <summary>Language code; null until the first launch picks one.</summary>
        public string Language;

        /// <summary>Quality level index (0 LOW, 1 MEDIUM, 2 HIGH); -1 = auto-detect on next launch.</summary>
        public int QualityLevel = -1;

        /// <summary>30 or 60.</summary>
        public int TargetFps = 60;
    }
}
