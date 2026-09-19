using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using LastGround.Core.Logging;
using LastGround.Core.Net;
using LastGround.Core.Net.Wire;
using LastGround.Core.Random;
using LastGround.Platform.Net;

namespace LastGround.Networking.Discovery
{
    /// <summary>
    /// UDP broadcast request/response discovery (TDD_02 §18). The browser sends a request every second to
    /// 255.255.255.255 and to each interface's directed broadcast (needed for hotspots); hosts answer unicast.
    /// Non-blocking sockets polled from the main thread — no threads, menu-time only.
    /// </summary>
    public sealed class UdpLanDiscovery : ILanDiscovery
    {
        const double RequestInterval = 1.0;
        const double HostTimeout = 3.0;
        const double InterfaceRefresh = 5.0;

        sealed class KnownHost
        {
            public DiscoveredHost Info;
            public double LastSeen;
        }

        readonly uint _protocolVersion;
        readonly uint _contentHash;
        readonly ushort _port;
        readonly INetworkInterfaces _interfaces;
        readonly IMulticastLock _multicastLock;
        readonly NetWriter _writer = new NetWriter(256);
        readonly byte[] _receiveBuffer = new byte[1500];
        // ReceiveFrom replaces the ref argument with a new endpoint only when a packet arrives: polling an idle socket
        // every frame (the host keeps advertising during a run) must not allocate.
        readonly EndPoint _anyRemote = new IPEndPoint(IPAddress.Any, 0);
        readonly Dictionary<string, KnownHost> _hosts = new Dictionary<string, KnownHost>(StringComparer.Ordinal);
        readonly List<string> _expired = new List<string>();
        readonly List<IPEndPoint> _broadcastTargets = new List<IPEndPoint>();

        Socket _responder;
        Socket _browser;
        DeterministicRandom _nonces;
        double _now;
        double _nextRequest;
        double _nextInterfaceRefresh;

        string _sessionName;
        ushort _gamePort;
        byte _players;
        byte _maxPlayers;
        bool _inRun;

        public UdpLanDiscovery(uint protocolVersion, uint contentHash, ushort discoveryPort,
            INetworkInterfaces interfaces, IMulticastLock multicastLock)
        {
            _protocolVersion = protocolVersion;
            _contentHash = contentHash;
            _port = discoveryPort;
            _interfaces = interfaces;
            _multicastLock = multicastLock;
            _nonces = new DeterministicRandom((uint)Environment.TickCount);
        }

        public bool IsAdvertising => _responder != null;
        public bool IsBrowsing => _browser != null;
        public event Action<DiscoveredHost> HostFound;
        public event Action<string, ushort> HostLost;

        public void StartAdvertising(string sessionName, ushort gamePort, byte players, byte maxPlayers)
        {
            _sessionName = sessionName;
            _gamePort = gamePort;
            _players = players;
            _maxPlayers = maxPlayers;
            _inRun = false;
            if (_responder != null) return;
            try
            {
                _responder = CreateSocket(_port);
                _multicastLock.Acquire();
                Log.Info(LogCategory.Net, "Discovery responder on UDP " + _port);
            }
            catch (SocketException e)
            {
                Log.Warning(LogCategory.Net, "Discovery responder failed: " + e.Message);
                CloseSocket(ref _responder);
            }
        }

        public void UpdateAdvertisement(byte players, byte maxPlayers, bool inRun)
        {
            _players = players;
            _maxPlayers = maxPlayers;
            _inRun = inRun;
        }

        public void StopAdvertising()
        {
            CloseSocket(ref _responder);
            ReleaseLockIfIdle();
        }

        public void StartBrowsing()
        {
            if (_browser != null) return;
            try
            {
                _browser = CreateSocket(0);
                _browser.EnableBroadcast = true;
                _multicastLock.Acquire();
                _nextRequest = _now;
                _nextInterfaceRefresh = _now;
            }
            catch (SocketException e)
            {
                Log.Warning(LogCategory.Net, "Discovery browser failed: " + e.Message);
                CloseSocket(ref _browser);
            }
        }

        public void StopBrowsing()
        {
            CloseSocket(ref _browser);
            _hosts.Clear();
            ReleaseLockIfIdle();
        }

        public void Poll(double now)
        {
            _now = now;
            if (_responder != null) PollResponder();
            if (_browser != null) PollBrowser();
        }

        public void Dispose()
        {
            StopAdvertising();
            StopBrowsing();
        }

        void PollResponder()
        {
            EndPoint remote = _anyRemote;
            while (TryReceive(_responder, ref remote, out int length))
            {
                if (DiscoveryPacket.ReadKind(new ArraySegment<byte>(_receiveBuffer, 0, length), out NetReader reader) != DiscoveryPacket.KindRequest)
                    continue;
                if (!DiscoveryPacket.TryRead(ref reader, out DiscoveryPacket.Request request)) continue;

                DiscoveryPacket.Write(_writer, new DiscoveryPacket.Response
                {
                    ProtocolVersion = _protocolVersion,
                    ContentHash = _contentHash,
                    SessionName = _sessionName,
                    Players = _players,
                    MaxPlayers = _maxPlayers,
                    GamePort = _gamePort,
                    InRun = _inRun,
                    Nonce = request.Nonce,
                    EchoSentAt = request.SentAt,
                });
                SendTo(_responder, remote);
            }
        }

        void PollBrowser()
        {
            if (_now >= _nextInterfaceRefresh)
            {
                _nextInterfaceRefresh = _now + InterfaceRefresh;
                RefreshBroadcastTargets();
            }

            if (_now >= _nextRequest)
            {
                _nextRequest = _now + RequestInterval;
                DiscoveryPacket.Write(_writer, new DiscoveryPacket.Request
                {
                    ProtocolVersion = _protocolVersion,
                    Nonce = _nonces.NextUInt(),
                    SentAt = _now,
                });
                for (int i = 0; i < _broadcastTargets.Count; i++)
                    SendTo(_browser, _broadcastTargets[i]);
            }

            EndPoint remote = _anyRemote;
            while (TryReceive(_browser, ref remote, out int length))
            {
                if (DiscoveryPacket.ReadKind(new ArraySegment<byte>(_receiveBuffer, 0, length), out NetReader reader) != DiscoveryPacket.KindResponse)
                    continue;
                if (!DiscoveryPacket.TryRead(ref reader, out DiscoveryPacket.Response response)) continue;
                OnResponse(((IPEndPoint)remote).Address.ToString(), response);
            }

            ExpireHosts();
        }

        void OnResponse(string address, in DiscoveryPacket.Response response)
        {
            bool compatible = response.ProtocolVersion == _protocolVersion && response.ContentHash == _contentHash;
            int ping = (int)Math.Max(0, (_now - response.EchoSentAt) * 1000.0);
            var info = new DiscoveredHost(address, response.GamePort, response.SessionName, response.Players,
                response.MaxPlayers, ping, compatible, response.InRun);

            string key = address + ":" + response.GamePort;
            if (!_hosts.TryGetValue(key, out KnownHost known))
            {
                known = new KnownHost();
                _hosts.Add(key, known);
            }
            known.Info = info;
            known.LastSeen = _now;
            HostFound?.Invoke(info);
        }

        void ExpireHosts()
        {
            _expired.Clear();
            foreach (var pair in _hosts)
            {
                if (_now - pair.Value.LastSeen > HostTimeout) _expired.Add(pair.Key);
            }
            for (int i = 0; i < _expired.Count; i++)
            {
                KnownHost host = _hosts[_expired[i]];
                _hosts.Remove(_expired[i]);
                HostLost?.Invoke(host.Info.Address, host.Info.Port);
            }
        }

        void RefreshBroadcastTargets()
        {
            _broadcastTargets.Clear();
            _broadcastTargets.Add(new IPEndPoint(IPAddress.Broadcast, _port));
            IReadOnlyList<LocalInterface> interfaces = _interfaces.GetIPv4();
            for (int i = 0; i < interfaces.Count; i++)
            {
                if (IPAddress.TryParse(interfaces[i].Broadcast, out IPAddress broadcast) && !broadcast.Equals(IPAddress.Broadcast))
                    _broadcastTargets.Add(new IPEndPoint(broadcast, _port));
            }
        }

        bool TryReceive(Socket socket, ref EndPoint remote, out int length)
        {
            length = 0;
            try
            {
                if (socket.Available <= 0) return false;
                length = socket.ReceiveFrom(_receiveBuffer, ref remote);
                return length > 0;
            }
            catch (SocketException)
            {
                return false;
            }
        }

        void SendTo(Socket socket, EndPoint target)
        {
            try
            {
                socket.SendTo(_writer.Buffer, 0, _writer.Length, SocketFlags.None, target);
            }
            catch (SocketException e)
            {
                // Unreachable interface targets are expected (e.g. a disconnected hotspot); keep trying the rest.
                Log.Info(LogCategory.Net, "Discovery send to " + target + " failed: " + e.SocketErrorCode);
            }
        }

        static Socket CreateSocket(ushort port)
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            socket.Blocking = false;
            socket.Bind(new IPEndPoint(IPAddress.Any, port));
            return socket;
        }

        static void CloseSocket(ref Socket socket)
        {
            if (socket == null) return;
            try
            {
                socket.Close();
            }
            catch (SocketException)
            {
            }
            socket = null;
        }

        void ReleaseLockIfIdle()
        {
            if (_responder == null && _browser == null) _multicastLock.Release();
        }
    }
}
