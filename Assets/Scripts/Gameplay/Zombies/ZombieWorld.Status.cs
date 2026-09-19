using LastGround.Data.Combat;
using LastGround.Data.Zombies;
using LastGround.Gameplay.Crowd;
using Unity.Mathematics;

namespace LastGround.Gameplay.Zombies
{
    /// <summary>
    /// Status effects and elites (TDD_01 §5.6, §9.8). Burn, slow and stun are timers per slot, ticked on the main thread
    /// before steering: burn deals its damage in 0.5 s pulses credited to the player who lit it, slow scales the steering
    /// speed, stun reuses the stagger (no steering, no attack) and interrupts windups. Elites multiply health, speed and
    /// attack, and may take less damage, ignore knockback, slow on hit or blow up on death.
    /// </summary>
    public sealed partial class ZombieWorld
    {
        const float BurnPulse = 0.5f;

        float[] _damageTaken, _knockbackScale, _attackDamageScale, _attackSlow, _attackSlowSeconds;
        float[] _burnTimer, _burnDps, _burnAccum, _burnPulse, _slowTimer, _slowMultiplier, _stunTimer;
        int[] _burnSource;

        /// <summary>Elite modifiers by NetIndex − 1 (CombatCatalog.Elites). Null = no elites.</summary>
        public EliteModifierDefinition[] Elites { get; set; }

        /// <summary>Stacked slows never push a zombie below this speed multiplier.</summary>
        public float MinSlowMultiplier { get; set; } = 0.4f;

        public EliteModifierDefinition EliteOf(byte netIndex) =>
            Elites != null && netIndex >= 1 && netIndex <= Elites.Length ? Elites[netIndex - 1] : null;

        public byte EliteAt(int slot) => _crowd.Elite[slot];
        public bool IsBurning(int slot) => _burnTimer[slot] > 0f;
        public bool IsStunned(int slot) => _stunTimer[slot] > 0f;
        public bool IsSlowed(int slot) => _slowTimer[slot] > 0f;

        void AllocateStatus()
        {
            _damageTaken = new float[_capacity];
            _knockbackScale = new float[_capacity];
            _attackDamageScale = new float[_capacity];
            _attackSlow = new float[_capacity];
            _attackSlowSeconds = new float[_capacity];
            _burnTimer = new float[_capacity];
            _burnDps = new float[_capacity];
            _burnAccum = new float[_capacity];
            _burnPulse = new float[_capacity];
            _burnSource = new int[_capacity];
            _slowTimer = new float[_capacity];
            _slowMultiplier = new float[_capacity];
            _stunTimer = new float[_capacity];
            AllocateTypes();
        }

        void ResetStatus(int slot, ZombieDefinition definition, byte elite)
        {
            _burnTimer[slot] = 0f;
            _burnAccum[slot] = 0f;
            _slowTimer[slot] = 0f;
            _stunTimer[slot] = 0f;
            _speedScale[slot] = 1f;
            _lunge[slot] = 0f;
            _damageTaken[slot] = 1f;
            _knockbackScale[slot] = definition.KnockbackScale;
            _attackDamageScale[slot] = 1f;
            _attackSlow[slot] = 1f;
            _attackSlowSeconds[slot] = 0f;
            ResetTypes(slot);

            EliteModifierDefinition modifier = EliteOf(elite);
            if (modifier == null) return;
            _health[slot] *= modifier.HealthMultiplier;
            _speed[slot] *= modifier.SpeedMultiplier;
            _damageTaken[slot] = modifier.DamageTakenMultiplier;
            if (modifier.KnockbackImmune) _knockbackScale[slot] = 0f;
            _attackDamageScale[slot] = modifier.AttackDamageMultiplier;
            _attackSlow[slot] = modifier.AttackSlowMultiplier;
            _attackSlowSeconds[slot] = modifier.AttackSlowSeconds;
        }

        /// <summary>Sets the zombie on fire; a stronger burn replaces a weaker one, the longer duration wins.</summary>
        public void ApplyBurn(int slot, float damagePerSecond, float seconds, int sourcePlayer)
        {
            if ((uint)slot >= (uint)_capacity || _alive[slot] == 0 || damagePerSecond <= 0f) return;
            if (damagePerSecond >= _burnDps[slot] || _burnTimer[slot] <= 0f)
            {
                _burnDps[slot] = damagePerSecond;
                _burnSource[slot] = sourcePlayer;
            }
            if (_burnTimer[slot] <= 0f) _burnPulse[slot] = BurnPulse;
            _burnTimer[slot] = math.max(_burnTimer[slot], seconds);
            _crowd.Flags[slot] |= CrowdFlags.Burning;
        }

        /// <summary>Slows the zombie: the strongest slow and the longest duration win.</summary>
        public void ApplySlow(int slot, float multiplier, float seconds)
        {
            if ((uint)slot >= (uint)_capacity || _alive[slot] == 0 || multiplier >= 1f || IsDriven(slot)) return;
            _slowMultiplier[slot] = _slowTimer[slot] > 0f ? math.min(_slowMultiplier[slot], multiplier) : multiplier;
            _slowTimer[slot] = math.max(_slowTimer[slot], seconds);
        }

        /// <summary>Stuns: no steering and no attacks; windups, lunges and spits in progress are cancelled.</summary>
        public void ApplyStun(int slot, float seconds)
        {
            if ((uint)slot >= (uint)_capacity || _alive[slot] == 0 || seconds <= 0f || IsDriven(slot)) return;
            _stunTimer[slot] = math.max(_stunTimer[slot], seconds);
            _stagger[slot] = math.max(_stagger[slot], seconds);
            _lunge[slot] = 0f;
            InterruptAttack(slot);
            InterruptTypeAction(slot);
            _crowd.Flags[slot] |= CrowdFlags.Stunned;
        }

        void TickStatus(float dt)
        {
            int local = _players.Local.IsValid ? _players.Local.Value : -1;
            for (int i = 0; i < _capacity; i++)
            {
                if (_alive[i] == 0) continue;
                if (_stunTimer[i] > 0f)
                {
                    _stunTimer[i] -= dt;
                    if (_stunTimer[i] <= 0f) _crowd.Flags[i] &= unchecked((byte)~CrowdFlags.Stunned);
                }
                float scale = 1f;
                if (_slowTimer[i] > 0f)
                {
                    _slowTimer[i] -= dt;
                    scale = math.max(MinSlowMultiplier, _slowMultiplier[i]);
                }
                _speedScale[i] = IsHolding(i) ? 0f : scale;
                if (_burnTimer[i] > 0f) TickBurn(i, dt, local);
            }
        }

        void TickBurn(int i, float dt, int local)
        {
            float step = math.min(dt, _burnTimer[i]);
            _burnTimer[i] -= dt;
            _burnAccum[i] += _burnDps[i] * step;
            _burnPulse[i] -= dt;
            bool over = _burnTimer[i] <= 0f;
            if (_burnPulse[i] > 0f && !over) return;
            _burnPulse[i] = BurnPulse;
            float amount = _burnAccum[i];
            _burnAccum[i] = 0f;
            if (over) _crowd.Flags[i] &= unchecked((byte)~CrowdFlags.Burning);
            int source = _burnSource[i];
            if (amount > 0f) ApplyDamage(i, amount, float2.zero, 0f, false, source >= 0 && source == local, source);
        }

        /// <summary>A death blast (Exploder, Volatile elite) goes off where the zombie died.</summary>
        void OnDied(int slot)
        {
            if (ExplosionSink == null) return;
            ZombieDefinition definition = _types[_type[slot]];
            if (definition.ExplodesOnDeath) ExplosionSink.Explode(_position[slot], definition.Explosion, ExplosionKind.Exploder, -1);
            EliteModifierDefinition modifier = EliteOf(_crowd.Elite[slot]);
            if (modifier != null && modifier.DeathExplosion.IsValid)
                ExplosionSink.Explode(_position[slot], modifier.DeathExplosion, ExplosionKind.Volatile, -1);
        }
    }
}
