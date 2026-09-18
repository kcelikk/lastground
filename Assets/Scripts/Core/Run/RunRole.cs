namespace LastGround.Core.Run
{
    /// <summary>Which side of the session this device plays (TDD_02 §15.3).</summary>
    public enum RunRole : byte
    {
        /// <summary>Solo: host mode with listening disabled. Same code path as co-op host.</summary>
        Offline = 0,
        /// <summary>Authoritative host + local player.</summary>
        Host = 1,
        /// <summary>Replica systems + local player.</summary>
        Client = 2,
    }
}
