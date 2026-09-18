namespace LastGround.Core.Net.Protocol
{
    public static class NetProtocol
    {
        /// <summary>Bump on any wire-format change; mismatching peers are rejected at join (TDD_02 §15.9).</summary>
        public const uint Version = 1;

        public const ushort DefaultGamePort = 7777;
        public const ushort DiscoveryPort = 47777;
        public const int MaxPlayers = 4;

        /// <summary>Unreliable payload ceiling; stays below KCP's unreliable MTU so nothing fragments (TDD_02 §17.4).</summary>
        public const int MaxUnreliablePayload = 1100;
    }
}
