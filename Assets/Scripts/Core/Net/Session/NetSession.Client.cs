using System;
using System.Collections.Generic;
using LastGround.Core.Ids;
using LastGround.Core.Logging;
using LastGround.Core.Net.Link;
using LastGround.Core.Net.Protocol;
using LastGround.Core.Net.Wire;
using LastGround.Core.Run;

namespace LastGround.Core.Net.Session
{
    /// <summary>
    /// Framework-independent session: handshake (version/content hash), roster, clock sync, message routing and stats
    /// over any <see cref="INetLinkFactory"/> (Mirror in the app, loopback in tests). Single-threaded; call
    /// <see cref="Tick"/> once per frame. Steady-state sends and receives do not allocate.
    /// </summary>
    /// <summary>Client side: join, roster mirror, clock samples.</summary>
    public sealed partial class NetSession
    {
        // ------------------------------------------------------------------ client side

        void OnClientConnected()
        {
            _transportConnected = true;
            SendToHost(new JoinRequest
            {
                ProtocolVersion = _config.ProtocolVersion,
                ContentHash = _config.ContentHash,
                PlayerName = SanitizeName(_config.PlayerName),
                PlayerGuid = _config.PlayerGuid,
            });
        }

        void OnClientData(ArraySegment<byte> data)
        {
            if (data.Count == 0) return;
            var reader = new NetReader(data);
            byte id = reader.ReadByte();
            _stats.CountIn(id, data.Count);

            switch (id)
            {
                case NetMsgId.JoinAccepted:
                {
                    var accepted = default(JoinAccepted);
                    accepted.Read(ref reader);
                    if (reader.Failed) return;
                    LocalPlayer = new PlayerId(accepted.PlayerId);
                    SessionName = accepted.SessionName;
                    _nextPing = _now;
                    SetState(SessionState.Connected);
                    return;
                }
                case NetMsgId.JoinRejected:
                {
                    var rejected = default(JoinRejected);
                    rejected.Read(ref reader);
                    LastRejectReason = rejected.Reason;
                    EndClient(DisconnectReason.Rejected);
                    return;
                }
                case NetMsgId.LobbyRoster:
                {
                    var roster = default(LobbyRoster);
                    roster.Read(ref reader);
                    if (!reader.Failed) ApplyRoster(roster);
                    return;
                }
                case NetMsgId.Pong:
                {
                    var pong = default(Pong);
                    pong.Read(ref reader);
                    if (!reader.Failed) _clock.AddSample(pong.ClientTime, _now, pong.HostTime);
                    return;
                }
            }

            if (State == SessionState.Connected)
                Dispatch(new PlayerId(0), id, ref reader);
        }

        void ApplyRoster(in LobbyRoster roster)
        {
            var previous = new List<PlayerId>(_players.Count);
            for (int i = 0; i < _players.Count; i++) previous.Add(_players[i].Id);

            _players.Clear();
            for (int i = 0; i < roster.Count; i++)
            {
                LobbyMember m = roster.Members[i];
                _players.Add(new LobbyPlayer { Id = new PlayerId(m.PlayerId), Name = m.Name, IsHost = m.IsHost, RttMs = m.RttMs });
            }

            for (int i = 0; i < previous.Count; i++)
            {
                if (Find(previous[i]) == null) PlayerLeft?.Invoke(previous[i]);
            }
            RosterChanged?.Invoke();
        }

        void OnClientDisconnected()
        {
            DisconnectReason reason;
            if (LastRejectReason != JoinRejectReason.None) reason = DisconnectReason.Rejected;
            else if (!_transportConnected) reason = DisconnectReason.ConnectFailed;
            else reason = DisconnectReason.HostLost;
            EndClient(reason);
        }

        void EndClient(DisconnectReason reason)
        {
            if (Role != RunRole.Client || State == SessionState.Idle) return;
            DisposeClient();
            _players.Clear();
            LocalPlayer = PlayerId.Invalid;
            SetState(SessionState.Idle);
            Log.Info(LogCategory.Net, "Disconnected: " + reason);
            Disconnected?.Invoke(reason);
        }
    }
}
