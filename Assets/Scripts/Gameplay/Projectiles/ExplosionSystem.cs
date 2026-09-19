using LastGround.Core.Events;
using LastGround.Core.Tick;
using LastGround.Data.Combat;
using LastGround.Gameplay.Players;
using LastGround.Gameplay.Zombies;
using Unity.Mathematics;

namespace LastGround.Gameplay.Projectiles
{
    /// <summary>
    /// Host area damage (TDD_01 §5.6 ExplosionSystem): a circle query over zombies and players with linear falloff,
    /// outward knockback and optional stun. Blasts are queued and resolved in the Combat phase; a blast that kills an
    /// Exploder or a Volatile elite queues theirs, so chains resolve in the same tick (capped). Player-thrown blasts
    /// hurt only the thrower (friendly fire off, self-damage scale from the spec); zombie blasts hurt everyone in range.
    /// </summary>
    public sealed class ExplosionSystem : ITickable, IExplosionSink
    {
        struct Blast
        {
            public float2 Center;
            public ExplosionSpec Spec;
            public ExplosionKind Kind;
            public int Source;
        }

        const int QueueCapacity = 32;

        readonly ZombieWorld _world;
        readonly PlayerStateTable _players;
        readonly IPlayerDamageSink _damage;
        readonly Blast[] _queue = new Blast[QueueCapacity];
        int _count;

        /// <summary>Every blast, for presentation and replication.</summary>
        public EventChannel<ExplosionFx> Blasts { get; }

        /// <param name="blasts">Shared blast feed (presentation reads the same channel on every device); null = own.</param>
        public ExplosionSystem(ZombieWorld world, PlayerStateTable players, IPlayerDamageSink damage, EventChannel<ExplosionFx> blasts = null)
        {
            _world = world;
            _players = players;
            _damage = damage;
            Blasts = blasts ?? new EventChannel<ExplosionFx>(32);
        }

        /// <summary>Barrels and tanks caught in a blast (chains). Optional.</summary>
        public IBlastListener Listener { get; set; }

        public int Exploded { get; private set; }
        public int Dropped { get; private set; }

        public void Explode(float2 center, in ExplosionSpec spec, ExplosionKind kind, int sourcePlayer)
        {
            if (!spec.IsValid) return;
            if (_count >= QueueCapacity)
            {
                Dropped++;
                return;
            }
            _queue[_count++] = new Blast { Center = center, Spec = spec, Kind = kind, Source = sourcePlayer };
        }

        public void Tick(float dt, uint tick)
        {
            // Resolving a blast may queue more (chains); they run in this loop.
            for (int i = 0; i < _count; i++) Resolve(in _queue[i]);
            _count = 0;
        }

        void Resolve(in Blast blast)
        {
            Exploded++;
            ExplosionSpec spec = blast.Spec;
            Blasts.Publish(new ExplosionFx { X = blast.Center.x, Z = blast.Center.y, Radius = spec.Radius, Kind = blast.Kind });
            Listener?.OnBlast(blast.Center, spec.Radius, spec.Damage);
            float r2 = spec.Radius * spec.Radius;
            int local = _players.Local.IsValid ? _players.Local.Value : -1;
            bool localSource = blast.Source >= 0 && blast.Source == local;

            for (int slot = 0; slot < _world.Crowd.Capacity; slot++)
            {
                if (!_world.IsAlive(slot)) continue;
                float2 away = _world.PositionOf(slot) - blast.Center;
                float d2 = math.lengthsq(away);
                if (d2 > r2) continue;
                float d = math.sqrt(d2);
                float falloff = Falloff(d, spec);
                float2 dir = d > 1e-3f ? away / d : new float2(0f, 1f);
                bool killed = _world.ApplyDamage(slot, spec.Damage * falloff, dir, spec.Knockback * falloff, false, localSource, blast.Source);
                if (!killed && spec.StunSeconds > 0f) _world.ApplyStun(slot, spec.StunSeconds);
            }

            if (spec.PlayerDamageScale <= 0f || _damage == null) return;
            for (int p = 0; p < PlayerStateTable.Max; p++)
            {
                if (!_players.CanAct(p)) continue;
                // Friendly fire is off: a player's blast can only hurt that player.
                if (blast.Source >= 0 && p != blast.Source) continue;
                float d = math.distance(new float2(_players.X[p], _players.Z[p]), blast.Center);
                if (d > spec.Radius) continue;
                _damage.Damage(p, spec.Damage * Falloff(d, spec) * spec.PlayerDamageScale);
            }
        }

        static float Falloff(float distance, in ExplosionSpec spec) =>
            math.lerp(1f, spec.EdgeDamageScale, math.saturate(distance / spec.Radius));
    }
}
