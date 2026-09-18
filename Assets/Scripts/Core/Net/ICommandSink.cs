namespace LastGround.Core.Net
{
    /// <summary>
    /// Client-to-host command path. The host's own player uses the same path (loopback), so there is no
    /// special case for the local host player.
    /// </summary>
    public interface ICommandSink
    {
        void Send<T>(in T command) where T : struct, INetCommand;
    }
}
