using LastGround.Core.Events;
using LastGround.Core.Net;
using LastGround.Gameplay.Crowd;
using LastGround.Gameplay.Players;
using Unity.Collections;
using Unity.Mathematics;

namespace LastGround.Gameplay.Zombies
{
    /// <summary>
    /// Zombie health, attacks and knockback (TDD_01 §5.5–5.6). Attacks are telegraphed: a zombie that reaches
    /// attack range winds up for <c>AttackWindup</c> seconds and only lands the hit if the player is still within
    /// reach, so moving away dodges it. Main-thread loops over ≤ 512 slots; no allocation.
    /// </summary>
    public sealed partial class ZombieWorld
    {
        const byte PhaseReady = 0;
        const byte PhaseWindup = 1;
        const byte PhaseCooldown = 2;
        const float HitFlagDuration = 0.15f;
        /// <summary>How long a bullet's knockback keeps a zombie from steering.</summary>
        const float HitStagger = 0.12f;
        /// <summary>Respawn push: long enough for friction to carry zombies out of the push radius.</summary>
        const float PushStagger = 0.6f;

        float[] _health;
        float[] _attackTimer;
        byte[] _attackPhase;
        float[] _hitFlagTimer;
        NativeArray<float> _stagger;
        EventReader<PlayerRespawn> _respawnReader;
        IGameEventStream<PlayerRespawn> _respawns;

        /// <summary>Receives zombie attack damage (the host's PlayerHealthSystem).</summary>
        public IPlayerDamageSink DamageSink { get; set; }

        /// <summary>Players getting back up push nearby zombies away.</summary>
        public IGameEventStream<PlayerRespawn> Respawns
        {
            set
            {
                _respawns = value;
                if (value != null) _respawnReader = value.CreateReader();
            }
        }

        public int AttacksLanded { get; private set; }
        public int AttacksDodged { get; private set; }

        public float HealthOf(int slot) => _health[slot];

        void AllocateCombat()
        {
            _health = new float[_capacity];
            _attackTimer = new float[_capacity];
            _attackPhase = new byte[_capacity];
            _hitFlagTimer = new float[_capacity];
            _stagger = new NativeArray<float>(_capacity, Allocator.Persistent);
        }

        void ResetCombat(int slot)
        {
            _health[slot] = _walker.MaxHealth;
            _attackPhase[slot] = PhaseReady;
            _attackTimer[slot] = 0f;
            _hitFlagTimer[slot] = 0f;
            _stagger[slot] = 0f;
        }

        /// <summary>
        /// Applies validated damage. Returns true when it killed the zombie (corpse + death replication follow
        /// through the crowd's Deaths channel).
        /// </summary>
        public bool ApplyDamage(int slot, float amount, float2 direction, float knockback, bool crit, bool local)
        {
            if ((uint)slot >= (uint)_capacity || _alive[slot] == 0) return false;
            float2 p = _position[slot];
            _crowd.Hits.Publish(new CrowdHit
            {
                Slot = slot, X = p.x, Z = p.y, DirX = direction.x, DirZ = direction.y, Damage = amount, Crit = crit, Local = local,
            });
            _health[slot] -= amount;
            if (_health[slot] <= 0f)
            {
                Remove(slot, true);
                return true;
            }
            float push = knockback * _walker.KnockbackScale;
            if (push > 0f)
            {
                _velocity[slot] += direction * push;
                _stagger[slot] = math.max(_stagger[slot], HitStagger);
            }
            _hitFlagTimer[slot] = HitFlagDuration;
            _crowd.Flags[slot] |= CrowdFlags.Hit;
            return false;
        }

        /// <summary>Pushes every zombie within <paramref name="radius"/> outwards (respawn clearance).</summary>
        public void PushAway(float2 centre, float radius, float speed)
        {
            float r2 = radius * radius;
            for (int i = 0; i < _capacity; i++)
            {
                if (_alive[i] == 0) continue;
                float2 away = _position[i] - centre;
                float d2 = math.lengthsq(away);
                if (d2 > r2) continue;
                float d = math.sqrt(d2);
                float2 dir = d > 1e-3f ? away / d : new float2(math.cos(i), math.sin(i));
                _velocity[i] = dir * speed * (1f - d / radius * 0.5f) * _walker.KnockbackScale;
                _stagger[i] = PushStagger;
                _attackPhase[i] = PhaseCooldown;
                _attackTimer[i] = _walker.AttackCooldown;
            }
        }

        void ApplyRespawnPushes()
        {
            if (_respawns == null) return;
            while (_respawns.TryRead(ref _respawnReader, out PlayerRespawn respawn))
                PushAway(new float2(respawn.X, respawn.Z), respawn.PushRadius, respawn.PushSpeed);
        }

        void TickCombat(float dt)
        {
            float reach = _tuning.AttackRange + _walker.AttackReachGrace;
            for (int i = 0; i < _capacity; i++)
            {
                if (_alive[i] == 0) continue;

                if (_stagger[i] > 0f) _stagger[i] -= dt;
                if (_hitFlagTimer[i] > 0f)
                {
                    _hitFlagTimer[i] -= dt;
                    if (_hitFlagTimer[i] <= 0f) _crowd.Flags[i] &= unchecked((byte)~CrowdFlags.Hit);
                }

                switch (_attackPhase[i])
                {
                    case PhaseReady:
                        if (_outState[i] == ZombieSteeringJob.StateAttack && _stagger[i] <= 0f)
                        {
                            _attackPhase[i] = PhaseWindup;
                            _attackTimer[i] = _walker.AttackWindup;
                            _crowd.Anim[i] = ZombieSteeringJob.StateAttack;
                        }
                        break;
                    case PhaseWindup:
                        _crowd.Anim[i] = ZombieSteeringJob.StateAttack;
                        _attackTimer[i] -= dt;
                        if (_attackTimer[i] > 0f) break;
                        LandAttack(i, reach);
                        _attackPhase[i] = PhaseCooldown;
                        _attackTimer[i] = _walker.AttackCooldown;
                        break;
                    default:
                        _attackTimer[i] -= dt;
                        if (_attackTimer[i] <= 0f) _attackPhase[i] = PhaseReady;
                        break;
                }
            }
        }

        void LandAttack(int i, float reach)
        {
            byte t = _target[i];
            if (t == ZombieSteeringJob.NoTarget || _playerActive[t] == 0 || math.distance(_position[i], _playerPosition[t]) > reach)
            {
                AttacksDodged++;
                return;
            }
            AttacksLanded++;
            DamageSink?.Damage(t, _walker.AttackDamage);
        }
    }
}
