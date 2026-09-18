namespace LastGround.Networking.Replication
{
    /// <summary>Converts host time to the tick stamp carried by snapshots and back.</summary>
    public static class NetTime
    {
        public static uint ToTick(double hostTime) => hostTime <= 0 ? 0u : (uint)(hostTime * ReplicationTuning.TickRate);
        public static double FromTick(uint tick) => (double)tick / ReplicationTuning.TickRate;
    }
}
