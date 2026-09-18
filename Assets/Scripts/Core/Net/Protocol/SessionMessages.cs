using LastGround.Core.Net.Wire;

namespace LastGround.Core.Net.Protocol
{
    public struct JoinRequest : INetMessage
    {
        public uint ProtocolVersion;
        public uint ContentHash;
        public string PlayerName;
        public string PlayerGuid;

        public byte Id => NetMsgId.JoinRequest;
        public NetChannel Channel => NetChannel.Reliable;

        public void Write(NetWriter w)
        {
            w.WriteUInt(ProtocolVersion);
            w.WriteUInt(ContentHash);
            w.WriteString(PlayerName);
            w.WriteString(PlayerGuid);
        }

        public void Read(ref NetReader r)
        {
            ProtocolVersion = r.ReadUInt();
            ContentHash = r.ReadUInt();
            PlayerName = r.ReadString();
            PlayerGuid = r.ReadString();
        }
    }

    public struct JoinAccepted : INetMessage
    {
        public byte PlayerId;
        public string SessionName;

        public byte Id => NetMsgId.JoinAccepted;
        public NetChannel Channel => NetChannel.Reliable;

        public void Write(NetWriter w)
        {
            w.WriteByte(PlayerId);
            w.WriteString(SessionName);
        }

        public void Read(ref NetReader r)
        {
            PlayerId = r.ReadByte();
            SessionName = r.ReadString();
        }
    }

    public struct JoinRejected : INetMessage
    {
        public JoinRejectReason Reason;

        public byte Id => NetMsgId.JoinRejected;
        public NetChannel Channel => NetChannel.Reliable;

        public void Write(NetWriter w) => w.WriteByte((byte)Reason);
        public void Read(ref NetReader r) => Reason = (JoinRejectReason)r.ReadByte();
    }

    public struct Ping : INetMessage
    {
        public double ClientTime;
        public ushort LastRttMs;

        public byte Id => NetMsgId.Ping;
        public NetChannel Channel => NetChannel.Unreliable;

        public void Write(NetWriter w)
        {
            w.WriteDouble(ClientTime);
            w.WriteUShort(LastRttMs);
        }

        public void Read(ref NetReader r)
        {
            ClientTime = r.ReadDouble();
            LastRttMs = r.ReadUShort();
        }
    }

    public struct Pong : INetMessage
    {
        public double ClientTime;
        public double HostTime;

        public byte Id => NetMsgId.Pong;
        public NetChannel Channel => NetChannel.Unreliable;

        public void Write(NetWriter w)
        {
            w.WriteDouble(ClientTime);
            w.WriteDouble(HostTime);
        }

        public void Read(ref NetReader r)
        {
            ClientTime = r.ReadDouble();
            HostTime = r.ReadDouble();
        }
    }
}
