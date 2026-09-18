using LastGround.Core.Net.Wire;

namespace LastGround.Core.Net.Protocol
{
    /// <summary>
    /// A typed message for low-rate traffic (lobby, run flow, pings). High-rate streams (snapshots) use codecs
    /// that write straight into the session writer instead, to avoid per-message arrays.
    /// </summary>
    public interface INetMessage
    {
        byte Id { get; }
        NetChannel Channel { get; }
        void Write(NetWriter writer);
        void Read(ref NetReader reader);
    }
}
