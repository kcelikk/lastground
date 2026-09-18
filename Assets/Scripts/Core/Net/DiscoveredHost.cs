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
        public readonly bool VersionMatches;

        public DiscoveredHost(string address, ushort port, string sessionName, byte players, byte maxPlayers,
            int pingMs, bool versionMatches)
        {
            Address = address;
            Port = port;
            SessionName = sessionName;
            Players = players;
            MaxPlayers = maxPlayers;
            PingMs = pingMs;
            VersionMatches = versionMatches;
        }
    }
}
