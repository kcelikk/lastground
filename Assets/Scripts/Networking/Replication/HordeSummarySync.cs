using System;
using LastGround.Core.Ids;
using LastGround.Core.Net.Protocol;
using LastGround.Core.Net.Session;
using LastGround.Core.Net.Wire;
using LastGround.Core.Tick;
using LastGround.Gameplay.Director;

namespace LastGround.Networking.Replication
{
    /// <summary>
    /// Far horde groups host → clients (TDD_02 §17.7 <c>HordeGroupSummary</c>): sent when the 1 Hz summary changes,
    /// unreliable (the next one replaces it). 1 + 5 B per group.
    /// </summary>
    public sealed class HordeSummarySync : ITickable, IDisposable
    {
        readonly ISession _session;
        readonly HordeSummary _summary;
        readonly NetRawHandler _onSummary;
        int _sentVersion = -1;

        public HordeSummarySync(ISession session, HordeSummary summary)
        {
            _session = session;
            _summary = summary;
            _onSummary = OnSummary;
            if (!session.IsAuthority) session.Subscribe(NetMsgId.HordeSummary, _onSummary);
        }

        /// <summary>Host: NetSend phase.</summary>
        public void Tick(float dt, uint tick)
        {
            if (!_session.IsAuthority || _summary.Version == _sentVersion) return;
            _sentVersion = _summary.Version;
            NetWriter w = _session.Begin(NetMsgId.HordeSummary);
            w.WriteByte((byte)_summary.Count);
            for (int i = 0; i < _summary.Count; i++)
            {
                w.WriteUShort(Quantize.Position(_summary.X[i]));
                w.WriteUShort(Quantize.Position(_summary.Z[i]));
                w.WriteByte((byte)Math.Min(255, _summary.Size[i]));
            }
            _session.SendToClients(NetChannel.Unreliable);
        }

        public void Dispose()
        {
            _session.Unsubscribe(NetMsgId.HordeSummary, _onSummary);
        }

        void OnSummary(PlayerId sender, ref NetReader r)
        {
            int count = r.ReadByte();
            if (r.Failed || count > HordeSummary.MaxGroups) return;
            _summary.Clear();
            for (int i = 0; i < count; i++)
            {
                float x = Quantize.Position(r.ReadUShort());
                float z = Quantize.Position(r.ReadUShort());
                int size = r.ReadByte();
                if (r.Failed) return;
                _summary.Add(x, z, size);
            }
            _summary.Commit();
        }
    }
}
