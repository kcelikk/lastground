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

        /// <summary>Name shown in lobbies; generated on first launch.</summary>
        public string PlayerName;

        /// <summary>Stable device identity for future reconnects (TDD_01 §14.5).</summary>
        public string PlayerGuid;

        /// <summary>Last address typed in Join by IP.</summary>
        public string LastJoinAddress;

        /// <summary>Core.Input.ControlMode: 0 manual twin-stick, 1 auto aim + fire.</summary>
        public int ControlMode;

        /// <summary>Soft aim assist in manual mode: 0 off, 1 low, 2 high (TDD_01 §3.3).</summary>
        public int AimAssist = 1;
    }
}
