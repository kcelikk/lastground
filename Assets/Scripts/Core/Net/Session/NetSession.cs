using System;
using System.Collections.Generic;
using LastGround.Core.Ids;
using LastGround.Core.Logging;
using LastGround.Core.Net.Link;
using LastGround.Core.Net.Protocol;
using LastGround.Core.Net.Wire;
using LastGround.Core.Run;
using Unity.Profiling;

namespace LastGround.Core.Net.Session
{
    /// <summary>
    /// Framework-independent session: handshake (version/content hash), roster, clock sync, message routing and stats
    /// over any <see cref="INetLinkFactory"/> (Mirror in the app, loopback in tests). Single-threaded; call
    /// <see cref="Tick"/> once per frame. Steady-state sends and receives do not allocate.
    /// </summary>
    public sealed partial class NetSession : ISession, IDisposable
    {
        const float RosterRefreshInterval = 2f;
        const double KickDelay = 0.5;
        const int MaxNameLength = 16;

        readonly INetLinkFactory _factory;
        readonly SessionConfig _config;
        readonly NetWriter _writer = new NetWriter(1400);
        readonly NetClock _clock = new NetClock();
        readonly NetStats _stats = new NetStats();
        readonly List<LobbyPlayer> _players = new List<LobbyPlayer>(NetProtocol.MaxPlayers);
        readonly List<NetRawHandler>[] _handlers = new List<NetRawHandler>[NetMsgId.Count];
        readonly List<(int connection, double at)> _pendingKicks = new List<(int, double)>();
        readonly LobbyMember[] _rosterBuffer = new LobbyMember[NetProtocol.MaxPlayers];

        IServerLink _server;
        IClientLink _client;
        byte[] _localBuffer = new byte[1400];
        int _localDepth;
        double _now;
        double _joinDeadline;
        double _nextPing;
        double _nextRosterRefresh;
        bool _rosterDirty;
        bool _transportConnected;

        public NetSession(INetLinkFactory factory, SessionConfig config)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            LocalPlayer = PlayerId.Invalid;
        }

        public SessionState State { get; private set; }
        public RunRole Role { get; private set; }
        public bool IsAuthority => Role != RunRole.Client;
        public PlayerId LocalPlayer { get; private set; }
        public string SessionName { get; private set; }
        public INetClock Clock => _clock;
        public INetStats Stats => _stats;
        public IReadOnlyList<LobbyPlayer> Players => _players;
        public bool AcceptingPlayers { get; set; }
        public JoinRejectReason LastRejectReason { get; private set; }
        public SessionConfig Config => _config;

        public event Action<SessionState> StateChanged;
        public event Action RosterChanged;
        public event Action<PlayerId> PlayerLeft;
        public event Action<DisconnectReason> Disconnected;

        // ------------------------------------------------------------------ lifecycle

        /// <summary>Solo: host logic without a listening transport (TDD_02 §15.3).</summary>
        public void StartOffline()
        {
            Leave(raise: false);
            Role = RunRole.Offline;
            BecomeHost(listening: false);
        }

        /// <summary>Starts listening. Returns false when the port is unavailable.</summary>
        public bool StartHost()
        {
            Leave(raise: false);
            _server = _factory.CreateServer();
            _server.Connected += OnServerConnected;
            _server.DataReceived += OnServerData;
            _server.Disconnected += OnServerDisconnected;
            if (!_server.Start(_config.Port, _config.MaxPlayers - 1))
            {
                Log.Warning(LogCategory.Net, "Could not listen on port " + _config.Port);
                DisposeServer();
                return false;
            }
            Role = RunRole.Host;
            BecomeHost(listening: true);
            return true;
        }

        public void Join(string address, ushort port)
        {
            Leave(raise: false);
            Role = RunRole.Client;
            LastRejectReason = JoinRejectReason.None;
            _clock.Reset();
            _client = _factory.CreateClient();
            _client.Connected += OnClientConnected;
            _client.DataReceived += OnClientData;
            _client.Disconnected += OnClientDisconnected;
            _joinDeadline = _now + _config.JoinTimeout;
            SetState(SessionState.Connecting);
            _client.Connect(address, port);
        }

        public void SetLocalMeta(ulong meta)
        {
            _config.LocalMeta = meta;
            LobbyPlayer me = State == SessionState.Idle ? null : Find(LocalPlayer);
            if (me == null || me.Meta == meta) return;
            me.Meta = meta;
            if (Role == RunRole.Client)
            {
                if (State == SessionState.Connected) SendToHost(new PlayerMetaUpdate { Meta = meta });
                return;
            }
            BroadcastRoster();
            RosterChanged?.Invoke();
        }

        public void Leave()
        {
            Leave(raise: true);
        }

        public void Dispose()
        {
            Leave(raise: false);
        }

        public void Tick(double now)
        {
            _now = now;
            _clock.SetLocalTime(now);
            _stats.Tick(now);

            for (int i = _pendingKicks.Count - 1; i >= 0; i--)
            {
                if (_pendingKicks[i].at > now) continue;
                _server?.Disconnect(_pendingKicks[i].connection);
                _pendingKicks.RemoveAt(i);
            }

            if (Role == RunRole.Client)
            {
                if (State == SessionState.Connecting && now >= _joinDeadline)
                {
                    LastRejectReason = JoinRejectReason.Timeout;
                    EndClient(DisconnectReason.Timeout);
                    return;
                }
                if (State == SessionState.Connected && now >= _nextPing)
                {
                    _nextPing = now + _config.PingInterval;
                    SendToHost(new Ping { ClientTime = now, LastRttMs = (ushort)Math.Min(65535.0, _clock.Rtt * 1000.0) });
                }
            }
            else if (Role == RunRole.Host && _rosterDirty && AcceptingPlayers && now >= _nextRosterRefresh)
            {
                _nextRosterRefresh = now + RosterRefreshInterval;
                BroadcastRoster();
            }
        }

        void BecomeHost(bool listening)
        {
            _clock.StartAsHost(Now());
            LocalPlayer = new PlayerId(0);
            SessionName = string.IsNullOrEmpty(_config.SessionName) ? _config.PlayerName : _config.SessionName;
            _players.Clear();
            _players.Add(new LobbyPlayer { Id = LocalPlayer, Name = SanitizeName(_config.PlayerName), IsHost = true, Meta = _config.LocalMeta });
            AcceptingPlayers = listening;
            SetState(SessionState.Hosting);
            RosterChanged?.Invoke();
        }

        void Leave(bool raise)
        {
            bool wasActive = State != SessionState.Idle;
            DisposeServer();
            DisposeClient();
            _players.Clear();
            _pendingKicks.Clear();
            LocalPlayer = PlayerId.Invalid;
            AcceptingPlayers = false;
            SetState(SessionState.Idle);
            if (raise && wasActive) Disconnected?.Invoke(DisconnectReason.Left);
        }

        void DisposeServer()
        {
            if (_server == null) return;
            _server.Connected -= OnServerConnected;
            _server.DataReceived -= OnServerData;
            _server.Disconnected -= OnServerDisconnected;
            _server.Stop();
            _server.Dispose();
            _server = null;
        }

        void DisposeClient()
        {
            if (_client == null) return;
            _client.Connected -= OnClientConnected;
            _client.DataReceived -= OnClientData;
            _client.Disconnected -= OnClientDisconnected;
            _client.Disconnect();
            _client.Dispose();
            _client = null;
            _transportConnected = false;
        }

        void SetState(SessionState state)
        {
            if (State == state) return;
            Log.Info(LogCategory.Net, "Session " + State + " -> " + state + " (" + Role + ")");
            State = state;
            StateChanged?.Invoke(state);
        }

        // ------------------------------------------------------------------ sending

        public NetWriter Begin(byte messageId)
        {
            _writer.Reset();
            _writer.WriteByte(messageId);
            return _writer;
        }

        public void SendTo(PlayerId player, NetChannel channel)
        {
            if (IsAuthority && player == LocalPlayer)
            {
                DispatchLocal(LocalPlayer);
                return;
            }
            if (_server == null) return;
            LobbyPlayer target = Find(player);
            if (target == null || !target.HasConnection) return;
            SendRaw(target.ConnectionId, channel);
        }

        public void SendToClients(NetChannel channel)
        {
            if (_server == null) return;
            for (int i = 0; i < _players.Count; i++)
            {
                if (_players[i].HasConnection)
                    SendRaw(_players[i].ConnectionId, channel);
            }
        }

        public void SendToHost(NetChannel channel)
        {
            if (IsAuthority)
            {
                if (State != SessionState.Idle) DispatchLocal(LocalPlayer);
                return;
            }
            if (_client == null || !_client.IsConnected) return;
            _client.Send(_writer.Segment, channel);
            _stats.CountOut(_writer.Buffer[0], _writer.Length);
        }

        public void Send<T>(PlayerId player, in T message) where T : struct, INetMessage
        {
            Begin(message.Id);
            message.Write(_writer);
            SendTo(player, message.Channel);
        }

        public void SendToClients<T>(in T message) where T : struct, INetMessage
        {
            Begin(message.Id);
            message.Write(_writer);
            SendToClients(message.Channel);
        }

        public void SendToHost<T>(in T message) where T : struct, INetMessage
        {
            Begin(message.Id);
            message.Write(_writer);
            SendToHost(message.Channel);
        }

        void SendRaw(int connectionId, NetChannel channel)
        {
            _server.Send(connectionId, _writer.Segment, channel);
            _stats.CountOut(_writer.Buffer[0], _writer.Length);
        }

        /// <summary>
        /// Host-local delivery. The payload is copied first because handlers may start new messages
        /// (which reuse the shared writer) while reading.
        /// </summary>
        void DispatchLocal(PlayerId sender)
        {
            int length = _writer.Length;
            byte[] buffer = _localDepth == 0 ? EnsureLocalBuffer(length) : new byte[length];
            Buffer.BlockCopy(_writer.Buffer, 0, buffer, 0, length);
            _localDepth++;
            try
            {
                var reader = new NetReader(new ArraySegment<byte>(buffer, 0, length));
                byte id = reader.ReadByte();
                Dispatch(sender, id, ref reader);
            }
            finally
            {
                _localDepth--;
            }
        }

        byte[] EnsureLocalBuffer(int length)
        {
            if (_localBuffer.Length < length) _localBuffer = new byte[Math.Max(length, _localBuffer.Length * 2)];
            return _localBuffer;
        }

        // ------------------------------------------------------------------ subscriptions

        public void Subscribe(byte messageId, NetRawHandler handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            var list = _handlers[messageId] ?? (_handlers[messageId] = new List<NetRawHandler>(2));
            list.Add(handler);
        }

        public void Unsubscribe(byte messageId, NetRawHandler handler)
        {
            _handlers[messageId]?.Remove(handler);
        }

        public NetRawHandler Subscribe<T>(NetHandler<T> handler) where T : struct, INetMessage
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            NetRawHandler raw = (PlayerId sender, ref NetReader reader) =>
            {
                T message = default;
                message.Read(ref reader);
                if (!reader.Failed) handler(sender, in message);
            };
            Subscribe(default(T).Id, raw);
            return raw;
        }

        /// <summary>One profiler marker per message id ("LG.Msg.21"): allocation captures show which handler allocates.</summary>
        static readonly ProfilerMarker[] DispatchMarkers = CreateDispatchMarkers();

        static ProfilerMarker[] CreateDispatchMarkers()
        {
            var markers = new ProfilerMarker[256];
            for (int i = 0; i < markers.Length; i++)
                markers[i] = new ProfilerMarker("LG.Msg." + i.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return markers;
        }

        void Dispatch(PlayerId sender, byte id, ref NetReader reader)
        {
            var list = _handlers[id];
            if (list == null) return;
            using ProfilerMarker.AutoScope scope = DispatchMarkers[id].Auto();
            for (int i = 0; i < list.Count; i++)
            {
                NetReader copy = reader;
                list[i](sender, ref copy);
            }
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>Current local time: the configured clock when available, else the last tick time.</summary>
        double Now() => _config.TimeSource != null ? _config.TimeSource() : _now;

        LobbyPlayer Find(PlayerId id)
        {
            for (int i = 0; i < _players.Count; i++)
            {
                if (_players[i].Id == id) return _players[i];
            }
            return null;
        }

        LobbyPlayer FindByConnection(int connectionId)
        {
            for (int i = 0; i < _players.Count; i++)
            {
                if (_players[i].HasConnection && _players[i].ConnectionId == connectionId) return _players[i];
            }
            return null;
        }

        static string SanitizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Player";
            name = name.Trim();
            return name.Length > MaxNameLength ? name.Substring(0, MaxNameLength) : name;
        }
    }
}
