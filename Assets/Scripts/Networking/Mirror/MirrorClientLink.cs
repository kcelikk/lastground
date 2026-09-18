using System;
using KcpTransport = kcp2k.KcpTransport;
using LastGround.Core.Net.Link;
using LastGround.Core.Net.Protocol;
using Mirror;

namespace LastGround.Networking.MirrorLink
{
    /// <summary>Client side over Mirror's NetworkClient. Only one instance may be active (Mirror is static).</summary>
    sealed class MirrorClientLink : IClientLink
    {
        readonly KcpTransport _transport;

        public MirrorClientLink(KcpTransport transport)
        {
            _transport = transport;
        }

        public bool IsConnected => NetworkClient.isConnected;
        public event Action Connected;
        public event Action<ArraySegment<byte>> DataReceived;
        public event Action Disconnected;

        public void Connect(string address, ushort port)
        {
            _transport.Port = port;
            NetworkClient.OnConnectedEvent = () => Connected?.Invoke();
            NetworkClient.OnDisconnectedEvent = () => Disconnected?.Invoke();
            NetworkClient.ReplaceHandler<LgPacket>(packet => DataReceived?.Invoke(packet.Payload), false);
            NetworkClient.Connect(address);
        }

        public void Disconnect()
        {
            NetworkClient.OnConnectedEvent = null;
            NetworkClient.OnDisconnectedEvent = null;
            if (NetworkClient.active) NetworkClient.Disconnect();
            NetworkClient.Shutdown();
        }

        public void Send(ArraySegment<byte> data, NetChannel channel)
        {
            if (NetworkClient.isConnected)
                NetworkClient.Send(new LgPacket { Payload = data }, MirrorLinkFactory.ToMirrorChannel(channel));
        }

        public void Dispose() => Disconnect();
    }
}
