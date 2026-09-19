namespace LastGround.Gameplay.Crowd
{
    /// <summary>Snapshot flag bits (5 bits on the wire, TDD_02 §17.2).</summary>
    public static class CrowdFlags
    {
        /// <summary>Took damage in the last ~0.15 s: clients flash it and spray blood for other players' hits.</summary>
        public const byte Hit = 1 << 0;
        public const byte Burning = 1 << 1;
        public const byte Stunned = 1 << 2;
        /// <summary>Elite: the modifier id comes with the enter message (<see cref="ICrowdRenderSource.Elites"/>).</summary>
        public const byte Elite = 1 << 3;
        /// <summary>Exploder fuse lit: blinking telegraph before the blast.</summary>
        public const byte Priming = 1 << 4;
    }
}
