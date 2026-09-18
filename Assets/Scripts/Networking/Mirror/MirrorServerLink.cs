using System;
using KcpTransport = kcp2k.KcpTransport;
using LastGround.Core.Logging;
using LastGround.Core.Net.Link;
using LastGround.Core.Net.Protocol;
using Mirror;

namespace LastGround.Networking.MirrorLink
{
    /// <summary>Host side over Mirror's NetworkServer. Only one instance may be active (Mirror is static).</summary>
    sealed class MirrorServerLink : IServerLink
    {
        const int TraceCount = 5;

        readonly KcpTransport _transport;
        int _sent;

        public MirrorServerLink(KcpTransport transport)
        {
            _transport = transport;
        }

        public bool IsActive => NetworkServer.active;
        public event Action<int> Connected;
        public event ServerDataHandler DataReceived;
        public event Action<int> Disconnected;

        public bool Start(ushort port, int maxConnections)
        {
            _transport.Port = port;
            NetworkServer.OnConnectedEvent = conn => Connected?.Invoke(conn.connectionId);
            NetworkServer.OnDisconnectedEvent = OnDisconnected;
            NetworkServer.ReplaceHandler<LgPacket>((conn, packet) => DataReceived?.Invoke(conn.connectionId, packet.Payload), false);
            try
            {
                NetworkServer.Listen(maxConnections);
            }
            catch (Exception e)
            {
                Log.Warning(LogCategory.Net, "Listen failed: " + e.Message);
                Stop();
                return false;
            }
            return NetworkServer.active;
        }

        public void Stop()
        {
            if (NetworkServer.active) NetworkServer.Shutdown();
            NetworkServer.OnConnectedEvent = null;
            NetworkServer.OnDisconnectedEvent = null;
        }

        public void Send(int connectionId, ArraySegment<byte> data, NetChannel channel)
        {
            if (!NetworkServer.connections.TryGetValue(connectionId, out NetworkConnectionToClient conn))
            {
                Log.Warning(LogCategory.Net, "Send to unknown connection " + connectionId);
                return;
            }
            if (_sent++ < TraceCount) Log.Info(LogCategory.Net, "Server send #" + _sent + " to " + connectionId + " id " + data.Array[data.Offset] + " (" + data.Count + " B)");
            conn.Send(new LgPacket { Payload = data }, MirrorLinkFactory.ToMirrorChannel(channel));
        }

        public void Disconnect(int connectionId)
        {
            if (NetworkServer.connections.TryGetValue(connectionId, out NetworkConnectionToClient conn))
                conn.Disconnect();
        }

        public string GetAddress(int connectionId)
        {
            return NetworkServer.connections.TryGetValue(connectionId, out NetworkConnectionToClient conn) ? conn.address : "?";
        }

        public void Dispose() => Stop();

        void OnDisconnected(NetworkConnectionToClient conn)
        {
            // Mirror expects the default cleanup (destroy owned objects) when the event is overridden.
            NetworkServer.DestroyPlayerForConnection(conn);
            Disconnected?.Invoke(conn.connectionId);
        }
    }
}
