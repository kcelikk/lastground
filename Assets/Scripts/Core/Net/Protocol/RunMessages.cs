using LastGround.Core.Net.Wire;

namespace LastGround.Core.Net.Protocol
{
    /// <summary>Host → all: load the run scene (TDD_02 §19.4).</summary>
    public struct LoadRun : INetMessage
    {
        public uint RunSeed;
        public string MapId;

        public byte Id => NetMsgId.LoadRun;
        public NetChannel Channel => NetChannel.Reliable;

        public void Write(NetWriter w)
        {
            w.WriteUInt(RunSeed);
            w.WriteString(MapId);
        }

        public void Read(ref NetReader r)
        {
            RunSeed = r.ReadUInt();
            MapId = r.ReadString();
        }
    }

    /// <summary>Client → host: run scene loaded.</summary>
    public struct RunReady : INetMessage
    {
        public byte Id => NetMsgId.RunReady;
        public NetChannel Channel => NetChannel.Reliable;
        public void Write(NetWriter w) { }
        public void Read(ref NetReader r) { }
    }

    /// <summary>Host → all: everyone is loaded; simulation starts at this host tick.</summary>
    public struct RunStart : INetMessage
    {
        public uint StartTick;

        public byte Id => NetMsgId.RunStart;
        public NetChannel Channel => NetChannel.Reliable;
        public void Write(NetWriter w) => w.WriteUInt(StartTick);
        public void Read(ref NetReader r) => StartTick = r.ReadUInt();
    }
}
