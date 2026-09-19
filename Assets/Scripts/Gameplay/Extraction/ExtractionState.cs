namespace LastGround.Gameplay.Extraction
{
    /// <summary>
    /// Every device's view of the extraction window (TDD_01 §2.2, §9.11 "EXTRACTION 01:12 → GAS STATION"): landing
    /// zone, region, window time left, hold progress. The host's <see cref="ExtractionController"/> writes it; clients
    /// receive it through replication.
    /// </summary>
    public sealed class ExtractionState
    {
        public ExtractionPhase Phase;
        public ushort Instance;
        public float X;
        public float Z;
        public float Radius;
        /// <summary>Map region of the landing zone (-1 = none).</summary>
        public int Region = -1;
        /// <summary>Window seconds left (whole seconds, for the HUD).</summary>
        public int SecondsLeft;
        /// <summary>Hold progress in tenths of a second, and the target.</summary>
        public int Hold;
        public int HoldTarget;
        /// <summary>Someone is standing in the zone right now.</summary>
        public bool Holding;
        public int Version;

        public bool IsOpen => Phase == ExtractionPhase.Open;
        public float Progress => HoldTarget > 0 ? (float)Hold / HoldTarget : 0f;

        public void Set(ExtractionPhase phase, ushort instance, float x, float z, float radius, int region, int secondsLeft, int hold,
            int holdTarget, bool holding)
        {
            if (Phase == phase && Instance == instance && X == x && Z == z && Radius == radius && Region == region
                && SecondsLeft == secondsLeft && Hold == hold && HoldTarget == holdTarget && Holding == holding) return;
            Phase = phase;
            Instance = instance;
            X = x;
            Z = z;
            Radius = radius;
            Region = region;
            SecondsLeft = secondsLeft;
            Hold = hold;
            HoldTarget = holdTarget;
            Holding = holding;
            Version++;
        }
    }
}
