using System;
using System.Collections.Generic;
using LastGround.Core.Ids;
using LastGround.Core.Net.Protocol;
using LastGround.Core.Net.Wire;
using LastGround.Core.Run;

namespace LastGround.Core.Net.Session
{
    /// <summary>Handler for a raw message; the reader is positioned after the message id.</summary>
    public delegate void NetRawHandler(PlayerId sender, ref NetReader reader);

    /// <summary>Handler for a typed message.</summary>
    public delegate void NetHandler<T>(PlayerId sender, in T message) where T : struct, INetMessage;

    /// <summary>
    /// The network boundary seen by gameplay and UI (TDD_02 §15.11). Implemented by <see cref="NetSession"/> over any
    /// <see cref="Link.INetLinkFactory"/>; nothing here references a networking framework.
    /// Host: the local player uses the same send paths (loopback), so there is no special host case in gameplay.
    /// </summary>
    public interface ISession
    {
        SessionState State { get; }
        RunRole Role { get; }
        bool IsAuthority { get; }
        PlayerId LocalPlayer { get; }
        string SessionName { get; }
        INetClock Clock { get; }
        INetStats Stats { get; }
        IReadOnlyList<LobbyPlayer> Players { get; }

        /// <summary>Client: why the host refused the last join (None otherwise).</summary>
        JoinRejectReason LastRejectReason { get; }

        /// <summary>Host only: whether new joins are accepted (false once a run starts).</summary>
        bool AcceptingPlayers { get; set; }

        event Action<SessionState> StateChanged;
        event Action RosterChanged;
        event Action<PlayerId> PlayerLeft;
        event Action<DisconnectReason> Disconnected;

        /// <summary>Starts a message; write the payload into the returned writer, then call one of the Send methods.</summary>
        NetWriter Begin(byte messageId);

        /// <summary>Host → one player. Sending to the local player dispatches locally.</summary>
        void SendTo(PlayerId player, NetChannel channel);

        /// <summary>Host → every remote client (not the local player).</summary>
        void SendToClients(NetChannel channel);

        /// <summary>Any role → host. On the host this dispatches locally.</summary>
        void SendToHost(NetChannel channel);

        void Send<T>(PlayerId player, in T message) where T : struct, INetMessage;
        void SendToClients<T>(in T message) where T : struct, INetMessage;
        void SendToHost<T>(in T message) where T : struct, INetMessage;

        void Subscribe(byte messageId, NetRawHandler handler);
        void Unsubscribe(byte messageId, NetRawHandler handler);
        /// <summary>Subscribes a typed handler; keep the returned raw handler to unsubscribe.</summary>
        NetRawHandler Subscribe<T>(NetHandler<T> handler) where T : struct, INetMessage;
    }
}
