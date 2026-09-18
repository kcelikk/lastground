using System;
using LastGround.Core.Events;
using LastGround.Core.Ids;
using LastGround.Core.Net.Protocol;
using LastGround.Core.Net.Session;
using LastGround.Core.Net.Wire;
using LastGround.Core.Tick;
using LastGround.Data.Combat;
using LastGround.Gameplay.Projectiles;
using Unity.Mathematics;

namespace LastGround.Networking.Replication
{
    /// <summary>
    /// Projectiles and blasts host → clients (TDD_01 §6.3, TDD_02 §15.5 ProjectileSpawn/Impact, reliable, per event):
    /// a spawn carries the closed-form flight (origin, end, duration) and its age, so the client replays it from the
    /// same point in time (age + half the round trip); an end removes it where the host saw it end; an explosion
    /// drives the blast effect. 15 B, 7 B and 7 B.
    /// </summary>
    public sealed class CombatFxSync : ITickable, IDisposable
    {
        const byte NoOwner = 255;

        readonly ISession _session;
        readonly ProjectileTable _table;
        readonly EventChannel<ExplosionFx> _blasts;
        readonly NetRawHandler _onSpawn, _onEnd, _onExplosion;
        EventReader<ProjectileLaunched> _launched;
        EventReader<ProjectileEnded> _ended;
        EventReader<ExplosionFx> _blastReader;

        public CombatFxSync(ISession session, ProjectileTable table, EventChannel<ExplosionFx> blasts)
        {
            _session = session;
            _table = table;
            _blasts = blasts;
            _onSpawn = OnSpawn;
            _onEnd = OnEnd;
            _onExplosion = OnExplosion;
            _launched = table.Launched.CreateReader();
            _ended = table.Ended.CreateReader();
            _blastReader = blasts.CreateReader();
            if (session.IsAuthority) return;
            session.Subscribe(NetMsgId.ProjectileSpawn, _onSpawn);
            session.Subscribe(NetMsgId.ProjectileEnd, _onEnd);
            session.Subscribe(NetMsgId.Explosion, _onExplosion);
        }

        /// <summary>Host: NetSend phase (forwards what happened this tick). Client: Presentation phase (replays flights).</summary>
        public void Tick(float dt, uint tick)
        {
            if (!_session.IsAuthority)
            {
                _table.AdvanceReplay(dt);
                return;
            }
            while (_table.Launched.TryRead(ref _launched, out ProjectileLaunched launch))
            {
                if (!_table.Active[launch.Id]) continue;
                NetWriter w = _session.Begin(NetMsgId.ProjectileSpawn);
                w.WriteByte((byte)launch.Id);
                w.WriteByte(launch.Definition);
                w.WriteByte(launch.Owner >= 0 ? (byte)launch.Owner : NoOwner);
                w.WriteUShort(Quantize.Position(launch.OriginX));
                w.WriteUShort(Quantize.Position(launch.OriginZ));
                w.WriteUShort(Quantize.Position(launch.EndX));
                w.WriteUShort(Quantize.Position(launch.EndZ));
                w.WriteUShort(Millis(launch.Duration));
                w.WriteUShort(Millis(_table.Age[launch.Id]));
                _session.SendToClients(NetChannel.Reliable);
            }
            while (_table.Ended.TryRead(ref _ended, out ProjectileEnded end))
            {
                NetWriter w = _session.Begin(NetMsgId.ProjectileEnd);
                w.WriteByte((byte)end.Id);
                w.WriteUShort(Quantize.Position(end.X));
                w.WriteUShort(Quantize.Position(end.Z));
                w.WriteByte(end.HitPlayer >= 0 ? (byte)end.HitPlayer : NoOwner);
                _session.SendToClients(NetChannel.Reliable);
            }
            while (_blasts.TryRead(ref _blastReader, out ExplosionFx fx))
            {
                NetWriter w = _session.Begin(NetMsgId.Explosion);
                w.WriteUShort(Quantize.Position(fx.X));
                w.WriteUShort(Quantize.Position(fx.Z));
                w.WriteByte((byte)math.min(255f, fx.Radius * 10f + 0.5f));
                w.WriteByte((byte)fx.Kind);
                _session.SendToClients(NetChannel.Reliable);
            }
        }

        public void Dispose()
        {
            _session.Unsubscribe(NetMsgId.ProjectileSpawn, _onSpawn);
            _session.Unsubscribe(NetMsgId.ProjectileEnd, _onEnd);
            _session.Unsubscribe(NetMsgId.Explosion, _onExplosion);
        }

        static ushort Millis(float seconds) => (ushort)math.clamp(seconds * 1000f + 0.5f, 0f, 65535f);

        void OnSpawn(PlayerId sender, ref NetReader r)
        {
            var launch = new ProjectileLaunched
            {
                Id = r.ReadByte(),
                Definition = r.ReadByte(),
            };
            byte owner = r.ReadByte();
            launch.Owner = owner == NoOwner ? -1 : owner;
            launch.OriginX = Quantize.Position(r.ReadUShort());
            launch.OriginZ = Quantize.Position(r.ReadUShort());
            launch.EndX = Quantize.Position(r.ReadUShort());
            launch.EndZ = Quantize.Position(r.ReadUShort());
            launch.Duration = r.ReadUShort() / 1000f;
            float age = r.ReadUShort() / 1000f;
            if (r.Failed) return;
            _table.Put(launch, age + (float)(_session.Clock.Rtt * 0.5));
        }

        void OnEnd(PlayerId sender, ref NetReader r)
        {
            int id = r.ReadByte();
            float x = Quantize.Position(r.ReadUShort());
            float z = Quantize.Position(r.ReadUShort());
            byte hit = r.ReadByte();
            if (r.Failed) return;
            _table.Remove(id, new float2(x, z), hit == NoOwner ? -1 : hit);
        }

        void OnExplosion(PlayerId sender, ref NetReader r)
        {
            float x = Quantize.Position(r.ReadUShort());
            float z = Quantize.Position(r.ReadUShort());
            float radius = r.ReadByte() / 10f;
            byte kind = r.ReadByte();
            if (r.Failed || kind > (byte)ExplosionKind.Volatile) return;
            _blasts.Publish(new ExplosionFx { X = x, Z = z, Radius = radius, Kind = (ExplosionKind)kind });
        }
    }
}
