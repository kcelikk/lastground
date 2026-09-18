namespace LastGround.Networking.Replication
{
    /// <summary>
    /// Interest-management and bandwidth constants (TDD_02 §17.3–17.4, §32). Moves into NetworkTuningProfile (SO) in M3.
    /// </summary>
    public sealed class ReplicationTuning
    {
        public float TierADistance = 20f;
        public float TierBDistance = 35f;
        public float TierCDistance = 60f;

        /// <summary>Entities leave relevance only beyond this (hysteresis against enter/exit flapping).</summary>
        public float ExitDistance = 65f;

        public float TierARate = 15f;
        public float TierBRate = 6f;
        public float TierCRate = 2f;

        /// <summary>Snapshot budget per client per 30 Hz tick (700 B ≈ 21 KB/s ceiling).</summary>
        public int SnapshotBytesPerTick = 700;

        public const int SnapshotEntryBits = 56;
        public const int SlotBits = 10;
        public const int YawBits = 6;
        public const int AnimBits = 3;
        public const int FlagBits = 5;
        public const int MaxEntriesPerReliableMessage = 100;

        /// <summary>Host tick rate used to stamp samples (matches the sim step).</summary>
        public const int TickRate = 30;
    }
}
