using System;
using LastGround.Core.Ids;
using LastGround.Core.Net.Protocol;
using LastGround.Core.Net.Session;
using LastGround.Core.Net.Wire;
using LastGround.Core.Tick;
using LastGround.Gameplay.Interactables;
using LastGround.Gameplay.Players;

namespace LastGround.Networking.Replication
{
    /// <summary>
    /// Interactables over the network (TDD_01 §12.3: "küçük reliable mesajlar", no NetworkObject): InteractableState
    /// host → all on change (≤ 4 Hz: intact bits + one charge byte per station) and InteractableHit player → host
    /// (id + damage, 4 B) for bullets on barrels and tanks. On clients this is the local player's hit sink.
    /// </summary>
    public sealed class InteractableSync : ITickable, IInteractableHitSink, IDisposable
    {
        const float MinInterval = 0.25f;

        readonly ISession _session;
        readonly InteractableTable _table;
        readonly IInteractableHitSink _authority;
        readonly NetRawHandler _onHit, _onState;
        int _sentVersion = -1;
        float _timer;

        /// <param name="authority">Host only (InteractableSystem); null on clients.</param>
        public InteractableSync(ISession session, InteractableTable table, IInteractableHitSink authority)
        {
            _session = session;
            _table = table;
            _authority = authority;
            _onHit = OnHit;
            _onState = OnState;
            if (session.IsAuthority)
            {
                session.Subscribe(NetMsgId.InteractableHit, _onHit);
                session.RosterChanged += OnRosterChanged;
            }
            else
            {
                session.Subscribe(NetMsgId.InteractableState, _onState);
            }
        }

        /// <summary>Host: NetSend phase.</summary>
        public void Tick(float dt, uint tick)
        {
            if (!_session.IsAuthority || _table.Count == 0) return;
            _timer -= dt;
            if (_table.Version == _sentVersion || _timer > 0f) return;
            _timer = MinInterval;
            _sentVersion = _table.Version;
            NetWriter w = _session.Begin(NetMsgId.InteractableState);
            w.WriteVarUInt((uint)_table.Count);
            for (int i = 0; i < _table.Count; i++)
            {
                w.WriteBits(_table.Intact[i] ? 1u : 0u, 1);
                w.WriteBits((uint)(_table.Charge[i] * 15f + 0.5f), 4);
            }
            w.FlushBits();
            _session.SendToClients(NetChannel.Reliable);
        }

        public void HitInteractable(int shooter, int id, float damage)
        {
            if (_session.IsAuthority)
            {
                _authority?.HitInteractable(shooter, id, damage);
                return;
            }
            NetWriter w = _session.Begin(NetMsgId.InteractableHit);
            w.WriteUShort((ushort)id);
            w.WriteUShort((ushort)Math.Min(ushort.MaxValue, damage * 10f + 0.5f));
            _session.SendToHost(NetChannel.Reliable);
        }

        public void Dispose()
        {
            _session.Unsubscribe(NetMsgId.InteractableHit, _onHit);
            _session.Unsubscribe(NetMsgId.InteractableState, _onState);
            if (_session.IsAuthority) _session.RosterChanged -= OnRosterChanged;
        }

        void OnRosterChanged() => _sentVersion = -1;

        void OnHit(PlayerId sender, ref NetReader r)
        {
            int id = r.ReadUShort();
            float damage = r.ReadUShort() / 10f;
            if (r.Failed || !sender.IsValid || sender.Value >= PlayerStateTable.Max) return;
            _authority?.HitInteractable(sender.Value, id, damage);
        }

        void OnState(PlayerId sender, ref NetReader r)
        {
            int count = (int)r.ReadVarUInt();
            for (int i = 0; i < count && !r.Failed; i++)
            {
                bool intact = r.ReadBits(1) != 0;
                float charge = r.ReadBits(4) / 15f;
                if (r.Failed || i >= _table.Count) return;
                _table.SetIntact(i, intact);
                _table.SetCharge(i, charge);
            }
        }
    }
}
