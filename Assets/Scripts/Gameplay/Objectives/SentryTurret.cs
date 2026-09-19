using LastGround.Core.Events;
using LastGround.Gameplay.Combat;
using LastGround.Gameplay.Navigation;
using LastGround.Gameplay.Zombies;
using Unity.Mathematics;

namespace LastGround.Gameplay.Objectives
{
    /// <summary>
    /// Generator reward (TDD_01 §12.2 Power Generator: "60 s otomatik turret"): a host-simulated hitscan sentry at the
    /// generator that shoots the nearest visible zombie in range at a fixed rate. Shots are published as ShotFired
    /// (shooter 255) for tracers and sound on the host; clients replay them from the objective state
    /// (<see cref="RemoteTurretEmitter"/>). No GameObject, no allocation.
    /// </summary>
    public sealed class SentryTurret
    {
        public const byte ShooterId = 255;
        public const float Range = 16f;
        public const float FireRate = 5f;
        public const float Damage = 22f;
        const float Knockback = 1f;
        const float MuzzleHeightOffset = 0.8f;

        readonly ZombieWorld _world;
        readonly NavGrid _nav;
        readonly EventChannel<ShotFired> _shots;
        float2 _position;
        float _timeLeft;
        float _cooldown;

        public SentryTurret(ZombieWorld world, NavGrid nav, EventChannel<ShotFired> shots)
        {
            _world = world;
            _nav = nav;
            _shots = shots;
        }

        public bool Active => _timeLeft > 0f;
        public float TimeLeft => _timeLeft;
        public float2 Position => _position;
        public int Kills { get; private set; }

        public void Activate(float2 position, float seconds)
        {
            _position = position;
            _timeLeft = seconds;
            _cooldown = 0f;
        }

        public void Tick(float dt)
        {
            if (_timeLeft <= 0f) return;
            _timeLeft -= dt;
            _cooldown -= dt;
            if (_cooldown > 0f) return;
            int target = Nearest();
            if (target < 0) return;
            _cooldown = 1f / FireRate;
            float2 to = _world.PositionOf(target) - _position;
            float2 dir = math.normalizesafe(to);
            if (_world.ApplyDamage(target, Damage, dir, Knockback, false, false)) Kills++;
            float2 muzzle = _position + dir * MuzzleHeightOffset;
            float2 end = _position + to;
            _shots?.Publish(new ShotFired
            {
                Shooter = ShooterId, OriginX = muzzle.x, OriginZ = muzzle.y, EndX = end.x, EndZ = end.y, FirstPellet = true,
            });
        }

        int Nearest()
        {
            int best = -1;
            float bestDistance = Range * Range;
            for (int i = 0; i < _world.Crowd.Capacity; i++)
            {
                if (!_world.IsAlive(i)) continue;
                float d2 = math.distancesq(_world.PositionOf(i), _position);
                if (d2 >= bestDistance || (_nav != null && !_nav.HasLineOfSight(_position, _world.PositionOf(i)))) continue;
                bestDistance = d2;
                best = i;
            }
            return best;
        }
    }
}
