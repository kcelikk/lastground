namespace LastGround.Core.Net
{
    /// <summary>Per-second traffic counters of the local device (TDD_02 §15.10). Payload bytes, excluding transport headers.</summary>
    public interface INetStats
    {
        float InBytesPerSecond { get; }
        float OutBytesPerSecond { get; }
        int InMessagesPerSecond { get; }
        int OutMessagesPerSecond { get; }

        /// <summary>Bytes received last second for a message id.</summary>
        int InBytesFor(byte messageId);

        /// <summary>Bytes sent last second for a message id.</summary>
        int OutBytesFor(byte messageId);
    }
}
