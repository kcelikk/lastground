using LastGround.Core.Net.Protocol;

namespace LastGround.Core.Net.Session
{
    public sealed class SessionConfig
    {
        public uint ProtocolVersion = NetProtocol.Version;

        /// <summary>Hash of content ids and app version; peers must match (TDD_02 §15.9).</summary>
        public uint ContentHash;

        public string PlayerName = "Player";
        public string PlayerGuid = "";
        public string SessionName = "";
        public ushort Port = NetProtocol.DefaultGamePort;
        public int MaxPlayers = NetProtocol.MaxPlayers;

        /// <summary>Seconds a client waits for JoinAccepted before giving up (TDD_02 §19.2).</summary>
        public double JoinTimeout = 5.0;

        /// <summary>Seconds between client clock pings.</summary>
        public double PingInterval = 1.0;
    }
}
