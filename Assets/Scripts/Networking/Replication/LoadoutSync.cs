using System;
using LastGround.Core.Events;
using LastGround.Core.Ids;
using LastGround.Core.Net.Protocol;
using LastGround.Core.Net.Session;
using LastGround.Core.Net.Wire;
using LastGround.Core.Tick;
using LastGround.Gameplay.Combat;
using LastGround.Gameplay.Players;
using Unity.Mathematics;

namespace LastGround.Networking.Replication
{
    /// <summary>
    /// Loadouts over the network (TDD_02 §15.5): LoadoutState host → all (reliable, on change: primary, sidearm and
    /// grenades of every player, 1 + 4 B each) and ThrowGrenade player → host (reliable, 4 B target). On the host the
    /// throws of clients go to the <see cref="LoadoutAuthority"/>; on clients this is the local player's grenade sink.
    /// </summary>
    public sealed class LoadoutSync : ITickable, IGrenadeSink, IDisposable
    {
        readonly ISession _session;
        readonly LoadoutTable _table;
        readonly IGrenadeSink _authority;
        readonly NetRawHandler _onState, _onThrow;
        EventReader<LoadoutChanged> _changed;
        bool _dirty;

        /// <param name="authority">Host only (the LoadoutAuthority); null on clients.</param>
        public LoadoutSync(ISession session, LoadoutTable table, IGrenadeSink authority)
        {
            _session = session;
            _table = table;
            _authority = authority;
            _onState = OnState;
            _onThrow = OnThrow;
            _changed = table.Changed.CreateReader();
            if (session.IsAuthority)
            {
                session.Subscribe(NetMsgId.ThrowGrenade, _onThrow);
                session.RosterChanged += OnRosterChanged;
            }
            else
            {
                session.Subscribe(NetMsgId.LoadoutState, _onState);
            }
        }

        /// <summary>Host: NetSend phase.</summary>
        public void Tick(float dt, uint tick)
        {
            if (!_session.IsAuthority) return;
            while (_table.Changed.TryRead(ref _changed, out _)) _dirty = true;
            if (!_dirty) return;
            _dirty = false;
            NetWriter w = _session.Begin(NetMsgId.LoadoutState);
            int count = 0;
            for (int p = 0; p < PlayerStateTable.Max; p++) if (_table.HasLoadout(p)) count++;
            w.WriteByte((byte)count);
            for (int p = 0; p < PlayerStateTable.Max; p++)
            {
                if (!_table.HasLoadout(p)) continue;
                w.WriteByte((byte)p);
                w.WriteByte(_table.Primary[p]);
                w.WriteByte(_table.Sidearm[p]);
                w.WriteByte(_table.Grenades[p]);
            }
            _session.SendToClients(NetChannel.Reliable);
        }

        /// <summary>Client: the local player's throw goes to the host.</summary>
        public void Throw(int player, float2 target)
        {
            if (_session.IsAuthority)
            {
                _authority?.Throw(player, target);
                return;
            }
            NetWriter w = _session.Begin(NetMsgId.ThrowGrenade);
            w.WriteUShort(Quantize.Position(target.x));
            w.WriteUShort(Quantize.Position(target.y));
            _session.SendToHost(NetChannel.Reliable);
        }

        public void Dispose()
        {
            _session.Unsubscribe(NetMsgId.LoadoutState, _onState);
            _session.Unsubscribe(NetMsgId.ThrowGrenade, _onThrow);
            if (_session.IsAuthority) _session.RosterChanged -= OnRosterChanged;
        }

        /// <summary>Someone joined or left: resend everyone's loadout, not only changes.</summary>
        void OnRosterChanged() => _dirty = true;

        void OnThrow(PlayerId sender, ref NetReader r)
        {
            float x = Quantize.Position(r.ReadUShort());
            float z = Quantize.Position(r.ReadUShort());
            if (r.Failed || !sender.IsValid || sender.Value >= PlayerStateTable.Max) return;
            _authority?.Throw(sender.Value, new float2(x, z));
        }

        void OnState(PlayerId sender, ref NetReader r)
        {
            int count = r.ReadByte();
            for (int k = 0; k < count && !r.Failed; k++)
            {
                int p = r.ReadByte();
                byte primary = r.ReadByte(), sidearm = r.ReadByte(), grenades = r.ReadByte();
                if (r.Failed || p >= PlayerStateTable.Max) return;
                _table.Set(p, primary, sidearm, grenades);
            }
        }
    }
}
