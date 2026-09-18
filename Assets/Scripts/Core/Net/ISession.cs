using System;
using LastGround.Core.Ids;

namespace LastGround.Core.Net
{
    /// <summary>
    /// The network boundary seen by gameplay (TDD_02 §15.11). Implemented by MirrorSession and LoopbackSession;
    /// gameplay code never references a networking framework directly.
    /// </summary>
    public interface ISession
    {
        SessionState State { get; }
        bool IsHost { get; }
        PlayerId LocalPlayer { get; }
        INetClock Clock { get; }
        ICommandSink Commands { get; }

        event Action<PlayerId> PlayerJoined;
        event Action<PlayerId> PlayerLeft;
        event Action<SessionState> StateChanged;
    }
}
