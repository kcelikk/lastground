using LastGround.Core.Random;
using LastGround.Core.Tick;
using LastGround.Data.Boss;
using LastGround.Data.Crowd;
using LastGround.Gameplay.Director;
using LastGround.Gameplay.Loot;
using LastGround.Gameplay.Players;
using LastGround.Gameplay.Projectiles;
using LastGround.Gameplay.Zombies;
using Unity.Mathematics;

namespace LastGround.Gameplay.Boss
{
    /// <summary>
    /// Host boss (TDD_01 §10, D-021). Appears at its threat level (and returns later, tougher), spawns off screen near
    /// the team as a driven crowd body, roars through an invulnerable intro, then walks at its aggro target and picks
    /// attacks by phase, range and cooldown (<c>.Attacks</c>). The tumour on its back takes double damage. While it
    /// lives the director spawns less; its death drops a reward pile and lets the extraction controller open a window.
    /// Partials: <c>.Attacks</c> (telegraph → hit → recovery for each attack kind).
    /// </summary>
    public sealed partial class BossController : ITickable, IZombieDamageModifier
    {
        const float SpawnClearance = 1.5f;
        const float AggroRange = 15f;
        const float AggroHalfLife = 8f;

        readonly ZombieWorld _world;
        readonly PlayerStateTable _players;
        readonly BossDefinition _definition;
        readonly RunStatus _status;
        readonly BossState _state;
        readonly float[] _aggro = new float[PlayerStateTable.Max];
        DeterministicRandom _rng;
        int _slot = -1;
        byte _generation;
        float _heading;
        float _phaseTimer;
        float _switchTimer;
        int _target = -1;
        int _lastThreat;
        bool _forced;

        public BossController(ZombieWorld world, PlayerStateTable players, BossDefinition definition, RunStatus status, BossState state,
            uint seed)
        {
            _world = world;
            _players = players;
            _definition = definition;
            _status = status;
            _state = state;
            _rng = DeterministicRandom.ForStream(seed, "boss");
            _cooldowns = new float[definition.Attacks != null ? definition.Attacks.Length : 0];
        }

        /// <summary>Spawn rate is cut while the boss lives (arena, TDD_01 §10).</summary>
        public HordeDirector Director { get; set; }

        /// <summary>Slam, charge and debris hurt players through it.</summary>
        public IPlayerDamageSink Damage { get; set; }

        /// <summary>Reward pile on death.</summary>
        public PickupRegistry Loot { get; set; }

        /// <summary>Charging through barrels and landing debris on them sets them off.</summary>
        public IBlastListener Barrels { get; set; }

        public int WeakPointHits { get; private set; }
        public int Summoned { get; private set; }

        /// <summary>Dev (-lg-boss): appear now instead of at the threat level.</summary>
        public void ForceAppear() => _forced = true;

        public void Tick(float dt, uint tick)
        {
            if (!_state.Active)
            {
                if (IsDue()) Appear();
                return;
            }
            if (!_world.IsAlive(_slot) || _world.Crowd.Generation[_slot] != _generation)
            {
                Defeated();
                return;
            }

            float2 position = _world.PositionOf(_slot);
            float health = _world.HealthOf(_slot);
            UpdatePhase(health);
            UpdateAggro(dt, position);
            byte anim = (byte)CrowdClipId.Idle;
            if (_state.Phase == BossPhase.Intro)
            {
                anim = (byte)CrowdClipId.Crawl;
                _phaseTimer -= dt;
                if (_phaseTimer <= 0f) SetPhase(BossPhase.Phase1);
            }
            else if (_stunTimer > 0f)
            {
                _stunTimer -= dt;
            }
            else if (_attack >= 0)
            {
                anim = TickAttack(dt, ref position);
            }
            else
            {
                TickCooldowns(dt);
                if (!TryStartAttack(position)) anim = Move(dt, ref position);
                else anim = (byte)_definition.Attacks[_attack].Clip;
            }
            _world.Drive(_slot, position, _heading, anim);
            _state.Set(_state.Phase, _slot, math.max(0f, _world.HealthOf(_slot)), _state.MaxHealth, _state.Appearance, _stunTimer > 0f,
                _state.Defeats);
        }

        /// <summary>Weak point (back ×2), intro invulnerability and aggro from damage.</summary>
        public float Scale(int slot, float2 direction, float amount, int sourcePlayer)
        {
            if (slot != _slot || !_state.Active) return 1f;
            if (_state.Phase == BossPhase.Intro) return 0f;
            float2 facing = Facing(_heading);
            bool back = math.dot(direction, facing) >= _definition.WeakPointDot;
            float scale = back ? _definition.WeakPointMultiplier : 1f;
            if (back) WeakPointHits++;
            if ((uint)sourcePlayer < PlayerStateTable.Max) _aggro[sourcePlayer] += amount * scale * _definition.AggroPerDamage;
            return scale;
        }

        // ----- appearance -----

        bool IsDue()
        {
            if (_forced) return HasTargets();
            int due = _state.Appearance == 0 && _state.Defeats == 0
                ? _definition.FirstThreat
                : math.max(_definition.ReturnFromThreat, _lastThreat + 1);
            return _status.Threat >= due && HasTargets();
        }

        void Appear()
        {
            int player = PickPlayer();
            if (player < 0) return;
            var focus = new float2(_players.X[player], _players.Z[player]);
            if (!FindSpawn(focus, out float2 position)) return;
            float heading = HeadingTo(focus - position);
            int slot = _world.Spawn(position, heading, _definition.Zombie.TypeIndex);
            if (slot < 0) return;
            _forced = false;
            _slot = slot;
            _generation = _world.Crowd.Generation[slot];
            _heading = heading;
            _world.SetPinned(slot, true);
            byte appearance = (byte)math.min(255, _state.Defeats);
            float health = _definition.HealthFor(CountPlayers(), appearance);
            _world.SetHealth(slot, health);
            for (int p = 0; p < _aggro.Length; p++) _aggro[p] = 0f;
            _aggro[player] = 1f;
            _target = player;
            _switchTimer = _rng.Range(_definition.AggroSwitch.x, _definition.AggroSwitch.y);
            ResetAttacks();
            _state.Set(BossPhase.Intro, slot, health, health, appearance, false, _state.Defeats);
            _phaseTimer = _definition.IntroSeconds;
            if (Director != null) Director.ExternalRateScale = _definition.DirectorRateScale;
            _status.Announcements.Publish(new DirectorAnnouncement { Kind = AnnouncementKind.BossArrived, ZombieType = _definition.Zombie.TypeIndex });
        }

        bool FindSpawn(float2 focus, out float2 position)
        {
            float start = _rng.Range(-math.PI, math.PI);
            for (int k = 0; k < 24; k++)
            {
                float angle = start + k * (2f * math.PI / 24f);
                for (float distance = _definition.SpawnDistance; distance >= _definition.SpawnDistance * 0.5f; distance -= 4f)
                {
                    position = focus + new float2(math.cos(angle), math.sin(angle)) * distance;
                    if (Clear(position, SpawnClearance)) return true;
                }
            }
            position = focus;
            return false;
        }

        bool Clear(float2 position, float radius)
        {
            if (!_world.Nav.IsWalkable(position)) return false;
            for (int k = 0; k < 4; k++)
            {
                float a = k * math.PI * 0.5f;
                if (!_world.Nav.IsWalkable(position + new float2(math.cos(a), math.sin(a)) * radius)) return false;
            }
            return true;
        }

        void Defeated()
        {
            float2 at = _slot >= 0 ? new float2(_world.Crowd.PosX[_slot], _world.Crowd.PosZ[_slot]) : float2.zero;
            _lastThreat = _status.Threat;
            _attack = -1;
            _stunTimer = 0f;
            int players = CountPlayers();
            Loot?.SpawnReward(at, _definition.RewardCoinsPerPlayer * players, _definition.RewardMedkits);
            if (_definition.RewardWeapon) Loot?.DropWeaponAt(at + new float2(1.5f, 0f));
            for (int g = 0; g < _definition.RewardGrenades; g++) Loot?.DropGrenadeAt(at + new float2(-1.5f, g * 0.6f));
            if (Director != null)
            {
                Director.ExternalRateScale = 1f;
                Director.ForceRelax();
            }
            _state.Set(BossPhase.Dead, -1, 0f, _state.MaxHealth, _state.Appearance, false, _state.Defeats + 1);
            _slot = -1;
        }

        // ----- phases, aggro, movement -----

        void UpdatePhase(float health)
        {
            if (_state.Phase == BossPhase.Intro) return;
            float fraction = _state.MaxHealth > 0f ? health / _state.MaxHealth : 0f;
            BossPhase phase = fraction <= _definition.EnragedAt ? BossPhase.Enraged
                : fraction <= _definition.Phase2At ? BossPhase.Phase2 : BossPhase.Phase1;
            // Phases only go forward.
            if (phase > _state.Phase) SetPhase(phase);
        }

        void SetPhase(BossPhase phase) =>
            _state.Set(phase, _slot, _state.Health, _state.MaxHealth, _state.Appearance, _state.Stunned, _state.Defeats);

        void UpdateAggro(float dt, float2 position)
        {
            float decay = math.exp(-dt * 0.693f / AggroHalfLife);
            int best = -1;
            float bestAggro = -1f;
            for (int p = 0; p < PlayerStateTable.Max; p++)
            {
                if (!_players.CanAct(p))
                {
                    _aggro[p] = 0f;
                    continue;
                }
                float d = math.distance(position, new float2(_players.X[p], _players.Z[p]));
                _aggro[p] = _aggro[p] * decay + _definition.AggroProximity * math.max(0f, 1f - d / AggroRange) * dt;
                if (_aggro[p] > bestAggro)
                {
                    bestAggro = _aggro[p];
                    best = p;
                }
            }
            _switchTimer -= dt;
            if (_switchTimer <= 0f && _attack < 0)
            {
                // Forced switch (TDD_01 §10): the most hated player other than the current target takes over.
                _switchTimer = _rng.Range(_definition.AggroSwitch.x, _definition.AggroSwitch.y);
                int next = -1;
                float nextAggro = -1f;
                for (int p = 0; p < PlayerStateTable.Max; p++)
                {
                    if (p == _target || !_players.CanAct(p) || _aggro[p] <= nextAggro) continue;
                    next = p;
                    nextAggro = _aggro[p];
                }
                if (next >= 0)
                {
                    _aggro[next] = bestAggro + 1f;
                    best = next;
                }
            }
            if (_target < 0 || !_players.CanAct(_target) || _aggro[best] > _aggro[_target] * 1.2f) _target = best;
        }

        byte Move(float dt, ref float2 position)
        {
            if (_target < 0) return (byte)CrowdClipId.Idle;
            float2 goal = new float2(_players.X[_target], _players.Z[_target]);
            float2 to = goal - position;
            float distance = math.length(to);
            Turn(HeadingTo(to), dt);
            if (distance <= _definition.StopDistance) return (byte)CrowdClipId.Idle;
            float speed = PhaseValue(_definition.MoveSpeed);
            float2 step = to / distance * math.min(speed * dt, distance - _definition.StopDistance);
            if (Clear(position + step, SpawnClearance * 0.6f)) position += step;
            else if (Clear(position + new float2(step.x, 0f), SpawnClearance * 0.6f)) position += new float2(step.x, 0f);
            else if (Clear(position + new float2(0f, step.y), SpawnClearance * 0.6f)) position += new float2(0f, step.y);
            return (byte)CrowdClipId.Walk;
        }

        void Turn(float goal, float dt)
        {
            float delta = math.fmod(goal - _heading + 540f, 360f) - 180f;
            float maxStep = _definition.TurnDegreesPerSecond * dt;
            _heading += math.clamp(delta, -maxStep, maxStep);
        }

        float PhaseValue(float3 values) =>
            _state.Phase == BossPhase.Enraged ? values.z : _state.Phase == BossPhase.Phase2 ? values.y : values.x;

        static float HeadingTo(float2 v) => math.degrees(math.atan2(v.x, v.y));

        static float2 Facing(float heading)
        {
            math.sincos(math.radians(heading), out float s, out float c);
            return new float2(s, c);
        }

        bool HasTargets()
        {
            for (int p = 0; p < PlayerStateTable.Max; p++) if (_players.CanAct(p)) return true;
            return false;
        }

        int PickPlayer()
        {
            int count = 0;
            for (int p = 0; p < PlayerStateTable.Max; p++) if (_players.CanAct(p)) count++;
            if (count == 0) return -1;
            int pick = _rng.Range(0, count);
            for (int p = 0; p < PlayerStateTable.Max; p++)
            {
                if (!_players.CanAct(p)) continue;
                if (pick-- == 0) return p;
            }
            return -1;
        }

        int CountPlayers()
        {
            int count = 0;
            for (int p = 0; p < PlayerStateTable.Max; p++) if (_players.Active[p]) count++;
            return math.max(1, count);
        }
    }
}
