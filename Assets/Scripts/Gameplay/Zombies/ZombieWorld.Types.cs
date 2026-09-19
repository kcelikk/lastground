using LastGround.Core.Random;
using LastGround.Data.Zombies;
using LastGround.Gameplay.Crowd;
using Unity.Mathematics;

namespace LastGround.Gameplay.Zombies
{
    /// <summary>
    /// Type actions decided on the main thread (TDD_01 §8.5); the steering job then carries them out:
    /// Runner — leaps at a target 4–6 m away (a velocity the job keeps for the lunge duration);
    /// Spitter — with line of sight inside its band it stops, winds up and spits a projectile led at the target;
    /// Exploder — near its target it lights a fuse (Priming flag, 0.8 s telegraph) and blows up where it stands.
    /// </summary>
    public sealed partial class ZombieWorld
    {
        float[] _lungeCooldown, _rangedCooldown, _rangedWindup, _fuse;

        /// <summary>Spitter projectiles (host ProjectileSystem).</summary>
        public IProjectileLauncher ProjectileLauncher { get; set; }

        /// <summary>Death blasts of Exploders and Volatile elites (host ExplosionSystem).</summary>
        public IExplosionSink ExplosionSink { get; set; }

        public int Lunges { get; private set; }
        public int Spits { get; private set; }
        public int Detonations { get; private set; }

        public bool IsPriming(int slot) => _fuse[slot] > 0f;

        void AllocateTypes()
        {
            _lungeCooldown = new float[_capacity];
            _rangedCooldown = new float[_capacity];
            _rangedWindup = new float[_capacity];
            _fuse = new float[_capacity];
        }

        void ResetTypes(int slot)
        {
            // Spits start staggered so a fresh pack does not fire in unison. A hash of (slot, generation) keeps the
            // world's random stream untouched by type bookkeeping.
            float stagger = (Hash32.Combine((uint)slot, _crowd.Generation[slot], 0x5717u) >> 8) * (1f / 16777216f);
            _lungeCooldown[slot] = 0f;
            _rangedCooldown[slot] = 1f + 2f * stagger;
            _rangedWindup[slot] = 0f;
            _fuse[slot] = 0f;
        }

        /// <summary>Spitters stand still while winding up a spit.</summary>
        bool IsHolding(int slot) => _rangedWindup[slot] > 0f;

        void InterruptTypeAction(int slot)
        {
            if (_rangedWindup[slot] <= 0f) return;
            _rangedWindup[slot] = 0f;
            _rangedCooldown[slot] = math.max(_rangedCooldown[slot], 1f);
        }

        void TickTypes(float dt)
        {
            for (int i = 0; i < _capacity; i++)
            {
                if (_alive[i] == 0) continue;
                if (_lunge[i] > 0f) _lunge[i] -= dt;
                ZombieDefinition definition = _types[_type[i]];
                switch (definition.Behaviour)
                {
                    case ZombieBehaviour.Runner: TickRunner(i, dt, definition); break;
                    case ZombieBehaviour.Spitter: TickSpitter(i, dt, definition); break;
                    case ZombieBehaviour.Exploder: TickExploder(i, dt, definition); break;
                }
            }
        }

        void TickRunner(int i, float dt, ZombieDefinition definition)
        {
            _lungeCooldown[i] -= dt;
            if (_lungeCooldown[i] > 0f || _stagger[i] > 0f || _attackPhase[i] != PhaseReady || !TargetDistance(i, out float2 toTarget, out float d))
                return;
            if (d < definition.LungeRange.x || d > definition.LungeRange.y) return;
            if (!_nav.HasLineOfSight(_position[i], _position[i] + toTarget)) return;
            _lunge[i] = definition.LungeDuration;
            _velocity[i] = toTarget / d * definition.LungeSpeed;
            _lungeCooldown[i] = definition.LungeCooldown;
            Lunges++;
        }

        void TickSpitter(int i, float dt, ZombieDefinition definition)
        {
            if (_rangedWindup[i] > 0f)
            {
                _crowd.Anim[i] = ZombieSteeringJob.StateAttack;
                _rangedWindup[i] -= dt;
                if (_rangedWindup[i] > 0f) return;
                _rangedWindup[i] = 0f;
                _rangedCooldown[i] = definition.RangedCooldown;
                Spit(i, definition);
                return;
            }
            _rangedCooldown[i] -= dt;
            if (_rangedCooldown[i] > 0f || _stagger[i] > 0f || _attackPhase[i] != PhaseReady || definition.Projectile == null
                || !TargetDistance(i, out float2 toTarget, out float d))
                return;
            if (d < definition.PreferredRange.x * 0.7f || d > definition.PreferredRange.y * 1.25f) return;
            if (!_nav.HasLineOfSight(_position[i], _position[i] + toTarget)) return;
            _rangedWindup[i] = definition.RangedWindup;
            _crowd.Anim[i] = ZombieSteeringJob.StateAttack;
        }

        void Spit(int i, ZombieDefinition definition)
        {
            byte t = _target[i];
            if (ProjectileLauncher == null || t == ZombieSteeringJob.NoTarget || _playerActive[t] == 0) return;
            float2 target = _playerPosition[t];
            // Lead the target by half its travel during the flight: dodgeable, but standing still is punished.
            float flight = math.distance(_position[i], target) / math.max(1f, definition.Projectile.Speed);
            target += new float2(_players.VelX[t], _players.VelZ[t]) * flight * 0.5f;
            ProjectileLauncher.Launch(definition.Projectile, _position[i], target, -1);
            Spits++;
        }

        void TickExploder(int i, float dt, ZombieDefinition definition)
        {
            if (_fuse[i] > 0f)
            {
                _fuse[i] -= dt;
                if (_fuse[i] > 0f) return;
                Detonations++;
                Remove(i, true);
                return;
            }
            if (_stagger[i] > 0f || !TargetDistance(i, out _, out float d) || d > definition.FuseRange) return;
            _fuse[i] = definition.FuseSeconds;
            _crowd.Flags[i] |= CrowdFlags.Priming;
        }

        bool TargetDistance(int i, out float2 toTarget, out float distance)
        {
            byte t = _target[i];
            if (t == ZombieSteeringJob.NoTarget || _playerActive[t] == 0)
            {
                toTarget = float2.zero;
                distance = float.MaxValue;
                return false;
            }
            toTarget = _playerPosition[t] - _position[i];
            distance = math.length(toTarget);
            return distance > 1e-3f;
        }
    }
}
