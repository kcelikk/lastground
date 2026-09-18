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

        /// <summary>Transport connection on the host; -1 for the host's own player and on clients.</summary>
        internal int ConnectionId = -1;
    }
}
