using LastGround.Core.Events;
using LastGround.Core.Tick;
using LastGround.Gameplay.Combat;
using LastGround.Gameplay.Crowd;
using LastGround.Gameplay.Navigation;
using Unity.Mathematics;

namespace LastGround.Gameplay.Objectives
{
    /// <summary>
    /// The generator sentry on clients, for presentation only: while the replicated objective state says the turret is
    /// on, publishes ShotFired from its position towards the nearest visible replica zombie at the sentry's rate. Kills
    /// arrive through normal death replication.
    /// </summary>
    public sealed class RemoteTurretEmitter : ITickable
    {
        readonly ObjectiveState _state;
        readonly ICrowdRenderSource _crowd;
        readonly NavGrid _nav;
        readonly EventChannel<ShotFired> _shots;
        float _cooldown;

        public RemoteTurretEmitter(ObjectiveState state, ICrowdRenderSource crowd, NavGrid nav, EventChannel<ShotFired> shots)
        {
            _state = state;
            _crowd = crowd;
            _nav = nav;
            _shots = shots;
        }

        public void Tick(float dt, uint tick)
        {
            if (_state.TurretSeconds <= 0) return;
            _cooldown -= dt;
            if (_cooldown > 0f) return;
            var origin = new float2(_state.TurretX, _state.TurretZ);
            int best = -1;
            float bestDistance = SentryTurret.Range * SentryTurret.Range;
            for (int i = 0; i < _crowd.Capacity; i++)
            {
                if (!_crowd.Alive[i]) continue;
                var p = new float2(_crowd.X[i], _crowd.Z[i]);
                float d2 = math.distancesq(p, origin);
                if (d2 >= bestDistance || (_nav != null && !_nav.HasLineOfSight(origin, p))) continue;
                bestDistance = d2;
                best = i;
            }
            if (best < 0) return;
            _cooldown = 1f / SentryTurret.FireRate;
            _shots.Publish(new ShotFired
            {
                Shooter = SentryTurret.ShooterId, OriginX = origin.x, OriginZ = origin.y, EndX = _crowd.X[best], EndZ = _crowd.Z[best],
                FirstPellet = true,
            });
        }
    }
}
