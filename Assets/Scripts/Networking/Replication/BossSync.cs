using System;
using LastGround.Core.Events;
using LastGround.Core.Ids;
using LastGround.Core.Net.Protocol;
using LastGround.Core.Net.Session;
using LastGround.Core.Net.Wire;
using LastGround.Core.Tick;
using LastGround.Data.Boss;
using LastGround.Gameplay.Boss;

namespace LastGround.Networking.Replication
{
    /// <summary>
    /// Boss host → clients (TDD_02 §15 BossState / BossAttackStarted, TDD_01 §10): state on change, merged to ≤ 10 Hz
    /// (phase changes at once; 17 B), and every attack windup as a reliable event (20 B) so each device draws the same
    /// telegraph. The body itself moves through ordinary crowd snapshots.
    /// </summary>
    public sealed class BossSync : ITickable, IDisposable
    {
        const float MinInterval = 0.1f;

        readonly ISession _session;
        readonly BossState _state;
        readonly NetRawHandler _onState;
        readonly NetRawHandler _onAttack;
        EventReader<BossAttackStarted> _attacks;
        int _sentVersion = -1;
        BossPhase _sentPhase;
        float _timer;

        public BossSync(ISession session, BossState state)
        {
            _session = session;
            _state = state;
            _onState = OnState;
            _onAttack = OnAttack;
            _attacks = state.Attacks.CreateReader();
            if (session.IsAuthority) return;
            session.Subscribe(NetMsgId.BossState, _onState);
            session.Subscribe(NetMsgId.BossAttack, _onAttack);
        }

        /// <summary>Host: NetSend phase.</summary>
        public void Tick(float dt, uint tick)
        {
            if (!_session.IsAuthority) return;
            while (_state.Attacks.TryRead(ref _attacks, out BossAttackStarted a))
            {
                NetWriter w = _session.Begin(NetMsgId.BossAttack);
                w.WriteByte(a.Attack);
                w.WriteByte((byte)a.Kind);
                w.WriteUShort(Quantize.Position(a.OriginX));
                w.WriteUShort(Quantize.Position(a.OriginZ));
                w.WriteUShort(Quantize.Position(a.TargetX));
                w.WriteUShort(Quantize.Position(a.TargetZ));
                w.WriteShort((short)Math.Round(a.DirX * 32000f));
                w.WriteShort((short)Math.Round(a.DirZ * 32000f));
                _session.SendToClients(NetChannel.Reliable);
            }

            _timer -= dt;
            if (_state.Version == _sentVersion) return;
            if (_timer > 0f && _state.Phase == _sentPhase) return;
            _timer = MinInterval;
            _sentVersion = _state.Version;
            _sentPhase = _state.Phase;
            NetWriter s = _session.Begin(NetMsgId.BossState);
            s.WriteByte((byte)_state.Phase);
            s.WriteShort((short)_state.Slot);
            s.WriteFloat(_state.Health);
            s.WriteFloat(_state.MaxHealth);
            s.WriteByte(_state.Appearance);
            s.WriteBool(_state.Stunned);
            s.WriteUShort((ushort)Math.Min(ushort.MaxValue, _state.Defeats));
            _session.SendToClients(NetChannel.Reliable);
        }

        public void Dispose()
        {
            _session.Unsubscribe(NetMsgId.BossState, _onState);
            _session.Unsubscribe(NetMsgId.BossAttack, _onAttack);
        }

        void OnState(PlayerId sender, ref NetReader r)
        {
            var phase = (BossPhase)r.ReadByte();
            int slot = r.ReadShort();
            float health = r.ReadFloat();
            float maxHealth = r.ReadFloat();
            byte appearance = r.ReadByte();
            bool stunned = r.ReadBool();
            int defeats = r.ReadUShort();
            if (r.Failed || phase > BossPhase.Dead) return;
            _state.Set(phase, slot, health, maxHealth, appearance, stunned, defeats);
        }

        void OnAttack(PlayerId sender, ref NetReader r)
        {
            var a = new BossAttackStarted
            {
                Attack = r.ReadByte(),
                Kind = (BossAttackKind)r.ReadByte(),
                OriginX = Quantize.Position(r.ReadUShort()),
                OriginZ = Quantize.Position(r.ReadUShort()),
                TargetX = Quantize.Position(r.ReadUShort()),
                TargetZ = Quantize.Position(r.ReadUShort()),
                DirX = r.ReadShort() / 32000f,
                DirZ = r.ReadShort() / 32000f,
            };
            if (r.Failed || a.Kind > BossAttackKind.SummonScream) return;
            _state.Attacks.Publish(a);
        }
    }
}
