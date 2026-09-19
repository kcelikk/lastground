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
    /// Objective state host → clients (TDD_01 §12.1 EventState + §12.4 ObjectiveState, reliable, on change, counter
    /// updates merged to ≤ 4 Hz; phase changes go out at once): kind, region, anchor, radius, stage, counter, seconds
    /// left and the generator sentry. 23 B.
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
            w.WriteByte((byte)_state.Kind);
            w.WriteByte(_state.Stage);
            w.WriteUShort(Quantize.Position(_state.AnchorX));
            w.WriteUShort(Quantize.Position(_state.AnchorZ));
            w.WriteByte((byte)Math.Min(255f, _state.Radius * 10f));
            w.WriteUShort((ushort)Math.Min(ushort.MaxValue, Math.Max(0, _state.SecondsLeft)));
            w.WriteUShort(Quantize.Position(_state.TurretX));
            w.WriteUShort(Quantize.Position(_state.TurretZ));
            w.WriteByte((byte)Math.Min(255, _state.TurretSeconds));
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
            var kind = (Data.Objectives.ObjectiveKind)r.ReadByte();
            byte stage = r.ReadByte();
            float x = Quantize.Position(r.ReadUShort());
            float z = Quantize.Position(r.ReadUShort());
            float radius = r.ReadByte() / 10f;
            int seconds = r.ReadUShort();
            float tx = Quantize.Position(r.ReadUShort());
            float tz = Quantize.Position(r.ReadUShort());
            int turret = r.ReadByte();
            if (r.Failed) return;
            if (instance != _state.Instance || kind != _state.Kind) _state.SetEvent(kind, x, z, radius);
            else _state.MoveAnchor(x, z);
            _state.SetStage(stage);
            _state.SetSeconds(seconds);
            _state.SetTurret(tx, tz, turret);
            _state.Set(instance, zone, current, target, phase);
        }
    }
}
