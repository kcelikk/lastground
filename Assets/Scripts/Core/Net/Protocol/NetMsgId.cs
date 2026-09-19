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
        public const byte RunEnd = 13;

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
        public const byte ProjectileSpawn = 42;
        public const byte ProjectileEnd = 43;
        public const byte Explosion = 44;
        public const byte ThrowGrenade = 45;
        public const byte LoadoutState = 46;
        public const byte InteractableHit = 47;
        public const byte InteractableState = 48;

        // Director / run status (50–59)
        public const byte DirectorInfo = 50;
        public const byte DirectorAnnouncement = 51;
        public const byte HordeSummary = 52;

        // Progression (60–69)
        public const byte TeamXp = 60;
        public const byte UpgradeOffer = 61;
        public const byte SelectUpgrade = 62;
        public const byte BuildChanged = 63;

        // Loot (70–79)
        public const byte PickupSpawnBatch = 70;
        public const byte PickupClaim = 71;
        public const byte PickupTaken = 72;
        public const byte TeamWallet = 73;

        // Objectives (80–89)
        public const byte ObjectiveState = 80;
        public const byte ExtractionState = 81;

        // Boss (90–99)
        public const byte BossState = 90;
        public const byte BossAttack = 91;

        public const int Count = 256;
    }
}
