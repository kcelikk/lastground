using LastGround.Core.Events;
using LastGround.Core.Net;
using LastGround.Data.Zombies;
using LastGround.Gameplay.Crowd;
using LastGround.Gameplay.Players;
using Unity.Collections;
using Unity.Mathematics;

namespace LastGround.Gameplay.Zombies
{
    /// <summary>
    /// Zombie health, melee attacks and knockback (TDD_01 §5.5–5.6). Attacks are telegraphed: a zombie that reaches
    /// attack range winds up for <c>AttackWindup</c> seconds and only lands the hit if the player is still within
    /// reach, so moving away dodges it. Exploders never melee (their fuse is in <c>.Types</c>). Main-thread loops over
    /// ≤ 512 slots; no allocation.
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

        /// <summary>Kills caused by a player (bullets, burn, grenades): heal-on-kill upgrades.</summary>
        public IKillCreditSink KillSink { get; set; }

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

        void ResetCombat(int slot, ZombieDefinition definition)
        {
            _health[slot] = definition.MaxHealth;
            _attackPhase[slot] = PhaseReady;
            _attackTimer[slot] = 0f;
            _hitFlagTimer[slot] = 0f;
            _stagger[slot] = 0f;
        }

        /// <summary>
        /// Applies validated damage (after the elite's damage-taken multiplier). Returns true when it killed the zombie
        /// (corpse + death replication follow through the crowd's Deaths channel; the source player gets kill credit).
        /// </summary>
        /// <param name="sourcePlayer">Player credited with a kill, -1 for none.</param>
        public bool ApplyDamage(int slot, float amount, float2 direction, float knockback, bool crit, bool local, int sourcePlayer = -1)
        {
            if ((uint)slot >= (uint)_capacity || _alive[slot] == 0) return false;
            amount *= _damageTaken[slot];
            float2 p = _position[slot];
            _crowd.Hits.Publish(new CrowdHit
            {
                Slot = slot, X = p.x, Z = p.y, DirX = direction.x, DirZ = direction.y, Damage = amount, Crit = crit, Local = local,
            });
            _health[slot] -= amount;
            if (_health[slot] <= 0f)
            {
                Remove(slot, true);
                if (sourcePlayer >= 0) KillSink?.OnKill(sourcePlayer);
                return true;
            }
            float push = knockback * _knockbackScale[slot];
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
                _velocity[i] = dir * speed * (1f - d / radius * 0.5f) * _knockbackScale[i];
                _stagger[i] = PushStagger;
                _attackPhase[i] = PhaseCooldown;
                _attackTimer[i] = _types[_type[i]].AttackCooldown;
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
            for (int i = 0; i < _capacity; i++)
            {
                if (_alive[i] == 0) continue;

                if (_stagger[i] > 0f) _stagger[i] -= dt;
                if (_hitFlagTimer[i] > 0f)
                {
                    _hitFlagTimer[i] -= dt;
                    if (_hitFlagTimer[i] <= 0f) _crowd.Flags[i] &= unchecked((byte)~CrowdFlags.Hit);
                }

                ZombieDefinition definition = _types[_type[i]];
                if (definition.Behaviour == ZombieBehaviour.Exploder) continue;
                switch (_attackPhase[i])
                {
                    case PhaseReady:
                        if (_outState[i] == ZombieSteeringJob.StateAttack && _stagger[i] <= 0f && _lunge[i] <= 0f)
                        {
                            _attackPhase[i] = PhaseWindup;
                            _attackTimer[i] = definition.AttackWindup;
                            _crowd.Anim[i] = ZombieSteeringJob.StateAttack;
                        }
                        break;
                    case PhaseWindup:
                        _crowd.Anim[i] = ZombieSteeringJob.StateAttack;
                        _attackTimer[i] -= dt;
                        if (_attackTimer[i] > 0f) break;
                        LandAttack(i, _typeParams[_type[i]].AttackRange + definition.AttackReachGrace, definition);
                        _attackPhase[i] = PhaseCooldown;
                        _attackTimer[i] = definition.AttackCooldown;
                        break;
                    default:
                        _attackTimer[i] -= dt;
                        if (_attackTimer[i] <= 0f) _attackPhase[i] = PhaseReady;
                        break;
                }
            }
        }

        void LandAttack(int i, float reach, ZombieDefinition definition)
        {
            byte t = _target[i];
            if (t == ZombieSteeringJob.NoTarget || _playerActive[t] == 0 || math.distance(_position[i], _playerPosition[t]) > reach)
            {
                AttacksDodged++;
                return;
            }
            AttacksLanded++;
            DamageSink?.Damage(t, definition.AttackDamage * _attackDamageScale[i]);
            if (_attackSlowSeconds[i] > 0f) DamageSink?.Slow(t, _attackSlow[i], _attackSlowSeconds[i]);
        }

        /// <summary>Cancels a melee windup (stun).</summary>
        void InterruptAttack(int slot)
        {
            if (_attackPhase[slot] != PhaseWindup) return;
            _attackPhase[slot] = PhaseCooldown;
            _attackTimer[slot] = 0.3f;
        }
    }
}
