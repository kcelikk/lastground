namespace LastGround.Gameplay.Zombies
{
    /// <summary>
    /// Walker behaviour numbers (TDD_01 §8). Starting hypotheses; become ZombieDefinition / DirectorProfile SO
    /// fields when more types arrive (M6).
    /// </summary>
    public sealed class ZombieTuning
    {
        public float MinSpeed = 1.3f;
        public float MaxSpeed = 2.1f;
        public float Acceleration = 6f;
        public float Radius = 0.45f;
        public float SeparationStrength = 2.5f;
        public float AttackRange = 1.3f;
        public float SurroundRange = 10f;
        public float RingMax = 6f;
        public float RingMin = 1.15f;
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
