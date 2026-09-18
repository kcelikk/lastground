using LastGround.Core.Net.Wire;

namespace LastGround.Core.Net.Protocol
{
    /// <summary>Full lobby state, re-sent by the host whenever it changes (small, reliable, menu-time only).</summary>
    public struct LobbyRoster : INetMessage
    {
        public LobbyMember[] Members;
        public int Count;

        public byte Id => NetMsgId.LobbyRoster;
        public NetChannel Channel => NetChannel.Reliable;

        public void Write(NetWriter w)
        {
            w.WriteByte((byte)Count);
            for (int i = 0; i < Count; i++)
            {
                w.WriteByte(Members[i].PlayerId);
                w.WriteString(Members[i].Name);
                w.WriteBool(Members[i].IsHost);
                w.WriteUShort(Members[i].RttMs);
            }
        }

        public void Read(ref NetReader r)
        {
            Count = r.ReadByte();
            if (Count > NetProtocol.MaxPlayers) Count = NetProtocol.MaxPlayers;
            Members = new LobbyMember[Count];
            for (int i = 0; i < Count; i++)
            {
                Members[i].PlayerId = r.ReadByte();
                Members[i].Name = r.ReadString();
                Members[i].IsHost = r.ReadBool();
                Members[i].RttMs = r.ReadUShort();
            }
        }
    }

    public struct LobbyMember
    {
        public byte PlayerId;
        public string Name;
        public bool IsHost;
        public ushort RttMs;
    }
}
