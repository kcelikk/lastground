namespace LastGround.Core.Net.Protocol
{
    public enum JoinRejectReason : byte
    {
        None = 0,
        VersionMismatch = 1,
        Full = 2,
        InProgress = 3,
        Refused = 4,
        Timeout = 5,
    }
}
