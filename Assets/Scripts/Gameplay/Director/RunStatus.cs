namespace LastGround.Gameplay.Director
{
    /// <summary>
    /// What the HUD shows about the run (TDD_01 §9.11): survival time, HORDE label, THREAT level. Written by the
    /// host's director and, on clients, by DirectorInfo replication (the clock advances locally between updates).
    /// Intensity and state are host-only diagnostics.
    /// </summary>
    public sealed class RunStatus
    {
        public float RunSeconds;
        public HordeLevel Horde;
        public int Threat = 1;

        public float Intensity;
        public DirectorState State;
        public int Alive;
        public float SpawnRate;
        public int MaxAlive;
    }
}
