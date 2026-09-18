namespace LastGround.Core.Net
{
    /// <summary>A host found on the local network.</summary>
    public readonly struct DiscoveredHost
    {
        public readonly string Address;
        public readonly ushort Port;
        public readonly string SessionName;
        public readonly byte Players;
        public readonly byte MaxPlayers;
        public readonly int PingMs;

        /// <summary>Same protocol version and content hash as this build.</summary>
        public readonly bool VersionMatches;

        /// <summary>The host is in a run and not accepting joins.</summary>
        public readonly bool InRun;

        public DiscoveredHost(string address, ushort port, string sessionName, byte players, byte maxPlayers,
            int pingMs, bool versionMatches, bool inRun)
        {
            Address = address;
            Port = port;
            SessionName = sessionName;
            Players = players;
            MaxPlayers = maxPlayers;
            PingMs = pingMs;
            VersionMatches = versionMatches;
            InRun = inRun;
        }

        public bool CanJoin => VersionMatches && !InRun && Players < MaxPlayers;
    }
}
