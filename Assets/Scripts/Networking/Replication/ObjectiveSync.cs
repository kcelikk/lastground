using System;
using LastGround.Core.Ids;
using LastGround.Core.Net.Protocol;
using LastGround.Core.Net.Session;
using LastGround.Core.Net.Wire;
using LastGround.Core.Tick;
using LastGround.Gameplay.Objectives;

namespace LastGround.Networking.Replication
{
    /// <summary>
    /// Objective state host → clients (TDD_01 §12.4: ObjectiveState, reliable, on change, counter updates merged to
    /// ≤ 4 Hz; phase changes go out at once). 8 B.
    /// </summary>
    public sealed class ObjectiveSync : ITickable, IDisposable
    {
        const float MinInterval = 0.25f;

        readonly ISession _session;
        readonly ObjectiveState _state;
        readonly NetRawHandler _onState;
        int _sentVersion = -1;
        ObjectivePhase _sentPhase;
        float _timer;

        public ObjectiveSync(ISession session, ObjectiveState state)
        {
            _session = session;
            _state = state;
            _onState = OnState;
            if (!session.IsAuthority) session.Subscribe(NetMsgId.ObjectiveState, _onState);
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
            NetWriter w = _session.Begin(NetMsgId.ObjectiveState);
            w.WriteUShort(_state.Instance);
            w.WriteByte((byte)Math.Min(255, Math.Max(0, _state.Zone)));
            w.WriteUShort((ushort)Math.Min(ushort.MaxValue, _state.Current));
            w.WriteUShort((ushort)Math.Min(ushort.MaxValue, _state.Target));
            w.WriteByte((byte)_state.Phase);
            _session.SendToClients(NetChannel.Reliable);
        }

        public void Dispose()
        {
            _session.Unsubscribe(NetMsgId.ObjectiveState, _onState);
        }

        void OnState(PlayerId sender, ref NetReader r)
        {
            ushort instance = r.ReadUShort();
            int zone = r.ReadByte();
            int current = r.ReadUShort();
            int target = r.ReadUShort();
            var phase = (ObjectivePhase)r.ReadByte();
            if (!r.Failed) _state.Set(instance, zone, current, target, phase);
        }
    }
}
