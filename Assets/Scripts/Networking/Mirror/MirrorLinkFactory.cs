using kcp2k;
using LastGround.Core.Net.Link;
using Mirror;
using UnityEngine;

namespace LastGround.Networking.MirrorLink
{
    /// <summary>
    /// Creates Mirror-backed links over KCP (UDP). The transport component lives on the app root so it
    /// survives scene loads.
    /// </summary>
    public sealed class MirrorLinkFactory : INetLinkFactory
    {
        readonly KcpTransport _transport;

        public MirrorLinkFactory(GameObject root)
        {
            _transport = root.GetComponent<KcpTransport>() ?? root.AddComponent<KcpTransport>();
            Transport.active = _transport;
        }

        public IServerLink CreateServer() => new MirrorServerLink(_transport);
        public IClientLink CreateClient() => new MirrorClientLink(_transport);

        internal static int ToMirrorChannel(Core.Net.Protocol.NetChannel channel)
        {
            return channel == Core.Net.Protocol.NetChannel.Reliable ? Channels.Reliable : Channels.Unreliable;
        }
    }
}
