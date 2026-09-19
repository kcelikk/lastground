using LastGround.Core.Tick;
using LastGround.Data.Combat;
using LastGround.Gameplay.Navigation;
using LastGround.Gameplay.Players;
using LastGround.Gameplay.Zombies;
using Unity.Mathematics;

namespace LastGround.Gameplay.Projectiles
{
    /// <summary>
    /// Host projectile simulation (TDD_01 §6.3): grenades are lobbed to a point (stopped short of walls), land, and blow
    /// up after a short fuse; spit flies straight until it hits a player (damage + slow), a wall or its range. The
    /// flights live in the shared <see cref="ProjectileTable"/> so presentation and replication read one source.
    /// Struct data, no allocation.
    /// </summary>
    public sealed class ProjectileSystem : ITickable, IProjectileLauncher
    {
        /// <summary>Straight projectiles hit players within HitRadius plus this body radius.</summary>
        const float PlayerRadius = 0.4f;
        /// <summary>Grenades stop this far in front of a wall.</summary>
        const float WallMargin = 0.4f;

        readonly ProjectileTable _table;
        readonly ProjectileDefinition[] _definitions;
        readonly PlayerStateTable _players;
        readonly NavGrid _nav;
        readonly IPlayerDamageSink _damage;
        readonly IExplosionSink _explosions;

        public ProjectileSystem(ProjectileTable table, ProjectileDefinition[] definitions, PlayerStateTable players, NavGrid nav,
            IPlayerDamageSink damage, IExplosionSink explosions)
        {
            _table = table;
            _definitions = definitions;
            _players = players;
            _nav = nav;
            _damage = damage;
            _explosions = explosions;
        }

        public int Launches { get; private set; }
        public int PlayerHits { get; private set; }
        public int Dropped { get; private set; }

        /// <summary>Launches a projectile from <paramref name="origin"/> towards <paramref name="target"/>. Returns its id or -1.</summary>
        public int Fire(ProjectileDefinition definition, float2 origin, float2 target, int ownerPlayer)
        {
            int id = _table.FreeSlot();
            if (id < 0 || definition == null || definition.NetIndex >= _definitions.Length)
            {
                Dropped++;
                return -1;
            }
            float2 to = target - origin;
            float length = math.length(to);
            float2 dir = length > 1e-3f ? to / length : new float2(0f, 1f);
            float distance;
            float duration;
            if (definition.Motion == ProjectileMotion.Arc)
            {
                distance = math.min(length, definition.MaxRange);
                float wall = _nav != null ? _nav.Raycast(origin, dir, distance) : distance;
                if (wall < distance) distance = math.max(0f, wall - WallMargin);
                float k = definition.MaxRange > 0f ? distance / definition.MaxRange : 1f;
                duration = math.lerp(definition.FlightTime.x, definition.FlightTime.y, k);
            }
            else
            {
                distance = definition.MaxRange;
                duration = distance / math.max(0.1f, definition.Speed);
            }
            float2 end = origin + dir * distance;
            _table.Put(new ProjectileLaunched
            {
                Id = id, Definition = definition.NetIndex, Owner = ownerPlayer,
                OriginX = origin.x, OriginZ = origin.y, EndX = end.x, EndZ = end.y, Duration = duration,
            }, 0f);
            Launches++;
            return id;
        }

        void IProjectileLauncher.Launch(ProjectileDefinition projectile, float2 origin, float2 target, int ownerPlayer) =>
            Fire(projectile, origin, target, ownerPlayer);

        public void Tick(float dt, uint tick)
        {
            for (int id = 0; id < _table.Capacity; id++)
            {
                if (!_table.Active[id]) continue;
                ProjectileDefinition definition = _table.DefinitionOf(id);
                if (definition == null)
                {
                    _table.Remove(id, _table.PositionOf(id), -1);
                    continue;
                }
                float2 before = _table.PositionOf(id);
                _table.Age[id] += dt;
                if (definition.Motion == ProjectileMotion.Arc) TickArc(id, definition);
                else TickStraight(id, definition, before);
            }
        }

        void TickArc(int id, ProjectileDefinition definition)
        {
            if (_table.Age[id] < _table.Duration[id] + definition.FuseAfterLanding) return;
            float2 at = _table.End[id];
            _explosions?.Explode(at, definition.Explosion, ExplosionKind.Grenade, _table.Owner[id]);
            _table.Remove(id, at, -1);
        }

        void TickStraight(int id, ProjectileDefinition definition, float2 before)
        {
            float2 now = _table.PositionOf(id);
            float reach = definition.HitRadius + PlayerRadius;
            for (int p = 0; p < PlayerStateTable.Max; p++)
            {
                if (!_players.CanAct(p)) continue;
                float2 player = new float2(_players.X[p], _players.Z[p]);
                if (DistanceToSegment(player, before, now) > reach) continue;
                PlayerHits++;
                _damage?.Damage(p, definition.ImpactDamage);
                _damage?.Slow(p, definition.SlowMultiplier, definition.SlowSeconds);
                _table.Remove(id, player, p);
                return;
            }
            bool wall = _nav != null && !_nav.IsWalkable(now);
            if (wall || _table.Age[id] >= _table.Duration[id]) _table.Remove(id, now, -1);
        }

        static float DistanceToSegment(float2 p, float2 a, float2 b)
        {
            float2 ab = b - a;
            float len2 = math.lengthsq(ab);
            float t = len2 > 1e-6f ? math.saturate(math.dot(p - a, ab) / len2) : 0f;
            return math.distance(p, a + ab * t);
        }
    }
}
