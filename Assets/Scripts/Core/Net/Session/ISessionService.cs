using System;
using LastGround.Core.Net.Protocol;
using LastGround.Core.Run;

namespace LastGround.Core.Net.Session
{
    /// <summary>
    /// App-level multiplayer flow used by menus and the run installer: host/join/solo, run start handshake
    /// (LoadRun → RunReady → RunStart) and leaving. Implemented in the App assembly.
    /// </summary>
    public interface ISessionService
    {
        ISession Session { get; }
        ILanDiscovery Discovery { get; }

        /// <summary>Address to show on the host screen for Join by IP; null when unknown.</summary>
        string LocalAddress { get; }

        /// <summary>Last address used for Join by IP (persisted).</summary>
        string LastJoinAddress { get; }

        RunLaunch CurrentRun { get; }
        bool RunStarted { get; }

        /// <summary>Why the last session ended (for the menu message). Cleared by <see cref="ClearLastDisconnect"/>.</summary>
        DisconnectReason LastDisconnect { get; }
        JoinRejectReason LastReject { get; }

        event Action RunStartedEvent;

        bool HostGame();
        void Join(string address, ushort port);
        void StartSolo();

        /// <summary>Host/offline: load the run on every device.</summary>
        void StartRun();

        /// <summary>Called by the run scene when it has finished building.</summary>
        void NotifyRunSceneReady();

        /// <summary>Leaves the session and returns to the menu.</summary>
        void Leave();

        void ClearLastDisconnect();
    }
}
