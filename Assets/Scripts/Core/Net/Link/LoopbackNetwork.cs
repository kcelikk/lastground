using System;
using System.Collections.Generic;
using LastGround.Core.Net.Protocol;
using LastGround.Core.Random;

namespace LastGround.Core.Net.Link
{
    /// <summary>
    /// In-process transport for tests and editor tools (TDD_02 §15.11 rule 5). Packets are copied and delivered on
    /// <see cref="Pump"/>, optionally with simulated latency and unreliable-channel loss. Proves that session logic
    /// does not depend on Mirror.
    /// </summary>
    public sealed class LoopbackNetwork : INetLinkFactory
    {
        struct Packet
        {
            public double DeliverAt;
            public bool ToServer;
            public int ConnectionId;
            public byte[] Data;
            public bool IsDisconnect;
        }

        readonly Dictionary<ushort, Server> _servers = new Dictionary<ushort, Server>();
        readonly List<Packet> _inFlight = new List<Packet>();
        DeterministicRandom _rng = new DeterministicRandom(1234u);
        double _now;
        int _nextConnectionId = 1;

        /// <summary>One-way delay in seconds applied to every packet.</summary>
        public double Latency { get; set; }

        /// <summary>
        /// Hands out negative connection ids like kcp2k does for some endpoints (hash-based ids).
        /// Keeps code from treating the sign of an id as meaningful.
        /// </summary>
        public bool NegativeConnectionIds { get; set; }

        /// <summary>0..1 drop probability for unreliable packets.</summary>
        public float UnreliableLoss { get; set; }

        public IServerLink CreateServer() => new Server(this);
        public IClientLink CreateClient() => new Client(this);

        /// <summary>Advances simulated time and delivers due packets in order.</summary>
        public void Pump(double now)
        {
            _now = now;
            for (int i = 0; i < _inFlight.Count;)
            {
                Packet p = _inFlight[i];
                if (p.DeliverAt > now)
                {
                    i++;
                    continue;
                }
                _inFlight.RemoveAt(i);
                Deliver(p);
            }
        }

        void Enqueue(bool toServer, int connectionId, ArraySegment<byte> data, NetChannel channel)
        {
            if (channel == NetChannel.Unreliable && UnreliableLoss > 0f && _rng.Chance(UnreliableLoss))
                return;
            var copy = new byte[data.Count];
            Buffer.BlockCopy(data.Array, data.Offset, copy, 0, data.Count);
            _inFlight.Add(new Packet { DeliverAt = _now + Latency, ToServer = toServer, ConnectionId = connectionId, Data = copy });
        }

        void Deliver(Packet p)
        {
            foreach (Server server in _servers.Values)
            {
                if (!server.Clients.TryGetValue(p.ConnectionId, out Client client)) continue;
                if (p.IsDisconnect)
                {
                    server.Clients.Remove(p.ConnectionId);
                    if (p.ToServer) server.RaiseDisconnected(p.ConnectionId);
                    client.RaiseDisconnected();
                    return;
                }
                if (p.ToServer) server.RaiseData(p.ConnectionId, new ArraySegment<byte>(p.Data));
                else client.RaiseData(new ArraySegment<byte>(p.Data));
                return;
            }
        }

        sealed class Server : IServerLink
        {
            readonly LoopbackNetwork _net;
            ushort _port;
            int _max;
            public readonly Dictionary<int, Client> Clients = new Dictionary<int, Client>();

            public Server(LoopbackNetwork net) => _net = net;

            public bool IsActive { get; private set; }
            public event Action<int> Connected;
            public event ServerDataHandler DataReceived;
            public event Action<int> Disconnected;

            public bool Start(ushort port, int maxConnections)
            {
                if (_net._servers.ContainsKey(port)) return false;
                _port = port;
                _max = maxConnections;
                _net._servers[port] = this;
                IsActive = true;
                return true;
            }

            public void Stop()
            {
                if (!IsActive) return;
                foreach (Client c in Clients.Values) c.RaiseDisconnected();
                Clients.Clear();
                _net._servers.Remove(_port);
                IsActive = false;
            }

            public void Send(int connectionId, ArraySegment<byte> data, NetChannel channel)
            {
                if (Clients.ContainsKey(connectionId)) _net.Enqueue(false, connectionId, data, channel);
            }

            public void Disconnect(int connectionId)
            {
                if (!Clients.TryGetValue(connectionId, out Client client)) return;
                Clients.Remove(connectionId);
                client.RaiseDisconnected();
                Disconnected?.Invoke(connectionId);
            }

            public string GetAddress(int connectionId) => "loopback:" + connectionId;

            public bool Accept(Client client, out int connectionId)
            {
                connectionId = 0;
                if (!IsActive || Clients.Count >= _max) return false;
                connectionId = _net.NegativeConnectionIds ? -(_net._nextConnectionId++) : _net._nextConnectionId++;
                Clients[connectionId] = client;
                Connected?.Invoke(connectionId);
                return true;
            }

            public void RaiseData(int id, ArraySegment<byte> data) => DataReceived?.Invoke(id, data);
            public void RaiseDisconnected(int id) => Disconnected?.Invoke(id);
            public void Dispose() => Stop();
        }

        sealed class Client : IClientLink
        {
            readonly LoopbackNetwork _net;
            int _connectionId;

            public Client(LoopbackNetwork net) => _net = net;

            public bool IsConnected { get; private set; }
            public event Action Connected;
            public event Action<ArraySegment<byte>> DataReceived;
            public event Action Disconnected;

            public void Connect(string address, ushort port)
            {
                if (_net._servers.TryGetValue(port, out Server server) && server.Accept(this, out _connectionId))
                {
                    IsConnected = true;
                    Connected?.Invoke();
                }
                else
                {
                    Disconnected?.Invoke();
                }
            }

            public void Disconnect()
            {
                if (!IsConnected) return;
                IsConnected = false;
                _net._inFlight.Add(new Packet { DeliverAt = _net._now, ToServer = true, ConnectionId = _connectionId, IsDisconnect = true });
            }

            public void Send(ArraySegment<byte> data, NetChannel channel)
            {
                if (IsConnected) _net.Enqueue(true, _connectionId, data, channel);
            }

            public void RaiseData(ArraySegment<byte> data) => DataReceived?.Invoke(data);

            public void RaiseDisconnected()
            {
                bool was = IsConnected;
                IsConnected = false;
                if (was) Disconnected?.Invoke();
            }

            public void Dispose() => Disconnect();
        }
    }
}
