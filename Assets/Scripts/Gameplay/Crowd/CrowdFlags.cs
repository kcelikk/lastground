namespace LastGround.Gameplay.Crowd
{
    /// <summary>Snapshot flag bits (5 bits on the wire, TDD_02 §17.2).</summary>
    public static class CrowdFlags
    {
        /// <summary>Took damage in the last ~0.15 s: clients flash it and spray blood for other players' hits.</summary>
        public const byte Hit = 1 << 0;
    }
}
