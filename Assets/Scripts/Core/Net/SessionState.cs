namespace LastGround.Core.Net
{
    /// <summary>Connection lifecycle of the local device.</summary>
    public enum SessionState : byte
    {
        Idle = 0,
        Hosting = 1,
        Connecting = 2,
        Connected = 3,
        Disconnected = 4,
    }
}
