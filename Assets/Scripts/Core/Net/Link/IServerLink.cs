using System;
using LastGround.Core.Net.Protocol;

namespace LastGround.Core.Net.Link
{
    public delegate void ServerDataHandler(int connectionId, ArraySegment<byte> data);

    /// <summary>
    /// Host side of a transport: raw bytes per connection. Implemented by MirrorServerLink and LoopbackNetwork.
    /// Data segments are only valid during the callback.
    /// </summary>
    public interface IServerLink : IDisposable
    {
        bool IsActive { get; }
        bool Start(ushort port, int maxConnections);
        void Stop();
        void Send(int connectionId, ArraySegment<byte> data, NetChannel channel);
        void Disconnect(int connectionId);
        string GetAddress(int connectionId);

        event Action<int> Connected;
        event ServerDataHandler DataReceived;
        event Action<int> Disconnected;
    }
}
