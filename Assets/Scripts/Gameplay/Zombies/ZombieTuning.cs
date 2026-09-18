namespace LastGround.Gameplay.Zombies
{
    /// <summary>
    /// Crowd-movement technical constants (TDD_01 §8). Per-type balance (health, speed, attack) lives in
    /// <see cref="LastGround.Data.Zombies.ZombieDefinition"/>.
    /// </summary>
    public sealed class ZombieTuning
    {
        public float Acceleration = 6f;
        public float Radius = 0.45f;
        public float SeparationStrength = 2.5f;
        /// <summary>Arm reach plus both body radii; the inner surround ring sits inside it.</summary>
        public float AttackRange = 1.85f;
        /// <summary>Zombies closer than this get a surround slot (must cover the outer queue rings of a full horde).</summary>
        public float SurroundRange = 14f;
        /// <summary>Inner ring radius: 12 attackers at 0.85 m spacing (2π·1.6/12 ≈ 0.84).</summary>
        public float RingMin = 1.6f;
        public float RingSpacing = 0.85f;
        public float TierADistance = 18f;
        public float TierBDistance = 35f;
        public float TierCDistance = 60f;
        public float TargetInterval = 0.5f;
        public float SurroundInterval = 0.5f;
        public int Sectors = 12;
        public float StuckCheckInterval = 1f;
        public float StuckDistance = 0.25f;
    }
}
