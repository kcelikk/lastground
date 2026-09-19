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
    /// <summary>Host side: join handshake, roster, pings.</summary>
    public sealed partial class NetSession
    {
        // ------------------------------------------------------------------ host side

        void OnServerConnected(int connectionId)
        {
            Log.Info(LogCategory.Net, "Transport connected: " + _server.GetAddress(connectionId));
        }

        void OnServerData(int connectionId, ArraySegment<byte> data)
        {
            if (data.Count == 0) return;
            var reader = new NetReader(data);
            byte id = reader.ReadByte();
            _stats.CountIn(id, data.Count);

            if (id == NetMsgId.JoinRequest)
            {
                var request = default(JoinRequest);
                request.Read(ref reader);
                if (!reader.Failed) HandleJoin(connectionId, request);
                return;
            }

            LobbyPlayer player = FindByConnection(connectionId);
            if (player == null) return; // not joined yet

            if (id == NetMsgId.Ping)
            {
                var ping = default(Ping);
                ping.Read(ref reader);
                if (reader.Failed) return;
                if (Math.Abs(player.RttMs - ping.LastRttMs) >= 5)
                {
                    player.RttMs = ping.LastRttMs;
                    _rosterDirty = true;
                }
                _clock.SetLocalTime(Now());
                Send(player.Id, new Pong { ClientTime = ping.ClientTime, HostTime = _clock.HostTime });
                return;
            }

            if (id == NetMsgId.PlayerMetaUpdate)
            {
                var update = default(PlayerMetaUpdate);
                update.Read(ref reader);
                if (reader.Failed || player.Meta == update.Meta) return;
                player.Meta = update.Meta;
                BroadcastRoster();
                RosterChanged?.Invoke();
                return;
            }

            Dispatch(player.Id, id, ref reader);
        }

        void HandleJoin(int connectionId, in JoinRequest request)
        {
            if (FindByConnection(connectionId) != null) return;

            JoinRejectReason reason = JoinRejectReason.None;
            if (request.ProtocolVersion != _config.ProtocolVersion || request.ContentHash != _config.ContentHash)
                reason = JoinRejectReason.VersionMismatch;
            else if (!AcceptingPlayers)
                reason = JoinRejectReason.InProgress;
            else if (_players.Count >= _config.MaxPlayers)
                reason = JoinRejectReason.Full;

            if (reason != JoinRejectReason.None)
            {
                Begin(NetMsgId.JoinRejected);
                new JoinRejected { Reason = reason }.Write(_writer);
                SendRaw(connectionId, NetChannel.Reliable);
                _pendingKicks.Add((connectionId, _now + KickDelay));
                Log.Info(LogCategory.Net, "Rejected join: " + reason);
                return;
            }

            var player = new LobbyPlayer
            {
                Id = FreePlayerId(),
                Name = SanitizeName(request.PlayerName),
                Meta = request.Meta,
                HasConnection = true,
                ConnectionId = connectionId,
            };
            _players.Add(player);
            _players.Sort((a, b) => a.Id.Value.CompareTo(b.Id.Value));

            Send(player.Id, new JoinAccepted { PlayerId = player.Id.Value, SessionName = SessionName });
            BroadcastRoster();
            Log.Info(LogCategory.Net, "Player joined: " + player.Name + " as " + player.Id);
            RosterChanged?.Invoke();
        }

        void OnServerDisconnected(int connectionId)
        {
            for (int i = 0; i < _pendingKicks.Count; i++)
            {
                if (_pendingKicks[i].connection == connectionId)
                {
                    _pendingKicks.RemoveAt(i);
                    break;
                }
            }

            LobbyPlayer player = FindByConnection(connectionId);
            if (player == null) return;
            _players.Remove(player);
            Log.Info(LogCategory.Net, "Player left: " + player.Name);
            BroadcastRoster();
            PlayerLeft?.Invoke(player.Id);
            RosterChanged?.Invoke();
        }

        void BroadcastRoster()
        {
            _rosterDirty = false;
            for (int i = 0; i < _players.Count; i++)
            {
                _rosterBuffer[i] = new LobbyMember
                {
                    PlayerId = _players[i].Id.Value,
                    Name = _players[i].Name,
                    IsHost = _players[i].IsHost,
                    RttMs = (ushort)Math.Min(65535, _players[i].RttMs),
                    Meta = _players[i].Meta,
                };
            }
            SendToClients(new LobbyRoster { Members = _rosterBuffer, Count = _players.Count });
        }

        PlayerId FreePlayerId()
        {
            for (byte id = 1; id < _config.MaxPlayers; id++)
            {
                if (Find(new PlayerId(id)) == null) return new PlayerId(id);
            }
            return PlayerId.Invalid;
        }
    }
}
