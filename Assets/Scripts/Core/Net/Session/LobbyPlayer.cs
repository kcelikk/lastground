using LastGround.Core.Ids;

namespace LastGround.Core.Net.Session
{
    /// <summary>One entry in the session roster, identical on every device.</summary>
    public sealed class LobbyPlayer
    {
        public PlayerId Id;
        public string Name;
        public bool IsHost;
        public int RttMs;
        /// <summary>Opaque meta selection (M9, decoded by the game).</summary>
        public ulong Meta;

        /// <summary>True on the host for remote players. Transport ids can be any int (kcp2k uses negative ones).</summary>
        internal bool HasConnection;

        /// <summary>Transport connection id on the host; only meaningful when <see cref="HasConnection"/>.</summary>
        internal int ConnectionId;
    }
}
