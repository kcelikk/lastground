namespace LastGround.Gameplay.Director
{
    /// <summary>What the HUD banner announces (TDD_01 §9.8, §9.11).</summary>
    public enum AnnouncementKind : byte
    {
        /// <summary>A zombie type appears for the first time this run ("Runners detected").</summary>
        NewZombieType = 0,
        EliteSpawned = 1,
    }

    /// <summary>A director announcement. Host publishes; clients receive it replicated.</summary>
    public struct DirectorAnnouncement
    {
        public AnnouncementKind Kind;
        public byte ZombieType;
        /// <summary>Elite modifier id (EliteSpawned only).</summary>
        public byte Elite;
    }
}
