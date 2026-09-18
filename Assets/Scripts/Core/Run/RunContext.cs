namespace LastGround.Core.Run
{
    /// <summary>
    /// Immutable facts about the current run. Role checks belong in installers and network adapters only,
    /// never scattered through gameplay code.
    /// </summary>
    public sealed class RunContext
    {
        public RunRole Role { get; }
        public uint RunSeed { get; }
        public string MapId { get; }
        public int PlayerCount { get; }

        /// <summary>Host sim tick at which the run started (from RunStart).</summary>
        public uint StartTick { get; }

        public bool IsAuthority => Role != RunRole.Client;

        public RunContext(RunRole role, uint runSeed, string mapId, int playerCount, uint startTick)
        {
            Role = role;
            RunSeed = runSeed;
            MapId = mapId;
            PlayerCount = playerCount;
            StartTick = startTick;
        }
    }
}
