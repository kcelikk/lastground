using System;
using LastGround.Core.Ids;
using LastGround.Core.Net.Protocol;
using LastGround.Core.Net.Session;
using LastGround.Core.Net.Wire;
using LastGround.Core.Tick;
using LastGround.Gameplay.Extraction;

namespace LastGround.Networking.Replication
{
    /// <summary>
    /// Extraction window host → clients (TDD_02 §15 EventState: LZ, window time, hold progress), reliable, on change,
    /// merged to ≤ 4 Hz; phase changes go out at once. 16 B.
    /// </summary>
    public sealed class ExtractionSync : ITickable, IDisposable
    {
        const float MinInterval = 0.25f;

        readonly ISession _session;
        readonly ExtractionState _state;
        readonly NetRawHandler _onState;
        int _sentVersion = -1;
        ExtractionPhase _sentPhase;
        float _timer;

        public ExtractionSync(ISession session, ExtractionState state)
        {
            _session = session;
            _state = state;
            _onState = OnState;
            if (!session.IsAuthority) session.Subscribe(NetMsgId.ExtractionState, _onState);
        }

        /// <summary>Host: NetSend phase.</summary>
        public void Tick(float dt, uint tick)
        {
            if (!_session.IsAuthority) return;
            _timer -= dt;
            if (_state.Version == _sentVersion) return;
            if (_timer > 0f && _state.Phase == _sentPhase) return;
            _timer = MinInterval;
            _sentVersion = _state.Version;
            _sentPhase = _state.Phase;
            NetWriter w = _session.Begin(NetMsgId.ExtractionState);
            w.WriteByte((byte)_state.Phase);
            w.WriteUShort(_state.Instance);
            w.WriteUShort(Quantize.Position(_state.X));
            w.WriteUShort(Quantize.Position(_state.Z));
            w.WriteByte((byte)Math.Min(255f, _state.Radius * 10f));
            w.WriteByte((byte)Math.Min(255, Math.Max(0, _state.Region + 1)));
            w.WriteUShort((ushort)Math.Min(ushort.MaxValue, Math.Max(0, _state.SecondsLeft)));
            w.WriteUShort((ushort)Math.Min(ushort.MaxValue, _state.Hold));
            w.WriteUShort((ushort)Math.Min(ushort.MaxValue, _state.HoldTarget));
            w.WriteBool(_state.Holding);
            _session.SendToClients(NetChannel.Reliable);
        }

        public void Dispose()
        {
            _session.Unsubscribe(NetMsgId.ExtractionState, _onState);
        }

        void OnState(PlayerId sender, ref NetReader r)
        {
            var phase = (ExtractionPhase)r.ReadByte();
            ushort instance = r.ReadUShort();
            float x = Quantize.Position(r.ReadUShort());
            float z = Quantize.Position(r.ReadUShort());
            float radius = r.ReadByte() / 10f;
            int region = r.ReadByte() - 1;
            int seconds = r.ReadUShort();
            int hold = r.ReadUShort();
            int target = r.ReadUShort();
            bool holding = r.ReadBool();
            if (r.Failed || phase > ExtractionPhase.Missed) return;
            _state.Set(phase, instance, x, z, radius, region, seconds, hold, target, holding);
        }
    }
}
