using System;
using LastGround.Core.Net.Protocol;

namespace LastGround.Core.Net.Link
{
    /// <summary>Client side of a transport. Data segments are only valid during the callback.</summary>
    public interface IClientLink : IDisposable
    {
        bool IsConnected { get; }
        void Connect(string address, ushort port);
        void Disconnect();
        void Send(ArraySegment<byte> data, NetChannel channel);

        event Action Connected;
        event Action<ArraySegment<byte>> DataReceived;
        event Action Disconnected;
    }
}
