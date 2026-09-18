namespace LastGround.Core.Net.Protocol
{
    public enum NetChannel : byte
    {
        /// <summary>Ordered, retransmitted. Events, lobby, batches of enter/exit/death.</summary>
        Reliable = 0,
        /// <summary>Unordered, may drop. Snapshots and pings; the next one supersedes a lost one.</summary>
        Unreliable = 1,
    }
}
