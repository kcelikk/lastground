namespace LastGround.Core.Net.Protocol
{
    /// <summary>
    /// First byte of every packet. Values are part of the wire protocol: never renumber, only append,
    /// and bump <see cref="NetProtocol.Version"/> when a message layout changes.
    /// </summary>
    public static class NetMsgId
    {
        // Session (1–9)
        public const byte JoinRequest = 1;
        public const byte JoinAccepted = 2;
        public const byte JoinRejected = 3;
        public const byte LobbyRoster = 4;
        public const byte Ping = 5;
        public const byte Pong = 6;

        // Run flow (10–19)
        public const byte LoadRun = 10;
        public const byte RunReady = 11;
        public const byte RunStart = 12;

        // Players (20–29)
        public const byte PlayerInput = 20;
        public const byte PlayerStates = 21;

        // Zombie replication (30–39)
        public const byte ZombieEnter = 30;
        public const byte ZombieSnapshot = 31;
        public const byte ZombieExit = 32;
        public const byte ZombieDeath = 33;

        // Combat (40–49)
        public const byte HitClaimBatch = 40;
        public const byte PlayerVitals = 41;

        // Director / run status (50–59)
        public const byte DirectorInfo = 50;

        public const int Count = 256;
    }
}
