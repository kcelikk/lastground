using LastGround.Data.Boss;
using LastGround.Data.Crowd;
using LastGround.Gameplay.Players;
using Unity.Mathematics;

namespace LastGround.Gameplay.Boss
{
    /// <summary>
    /// Boss attacks (TDD_01 §10): every attack telegraphs on the ground for at least 0.8 s, then hits, then recovers.
    /// Ground Slam hits inside its circle and sends a shock ring outwards; Charge dashes along its line (walls stun
    /// the boss, barrels on the way blow up); Prop Throw lands debris on where the target stood; Summon Scream brings
    /// a Runner pack. Enraged, a slam is followed by a charge at once (Frenzy).
    /// </summary>
    public sealed partial class BossController
    {
        enum Stage : byte
        {
            Telegraph,
            Active,
            Recovery,
        }

        const float PlayerRadius = 0.4f;

        readonly float[] _cooldowns;
        int _attack = -1;
        Stage _stage;
        float _stageTimer;
        float2 _origin;
        float2 _goal;
        float2 _dir;
        float _ring;
        float _travelled;
        float _stunTimer;
        int _hitMask;
        bool _frenzyNext;

        public int AttacksStarted { get; private set; }
        public int PlayerHits { get; private set; }

        void ResetAttacks()
        {
            _attack = -1;
            _stunTimer = 0f;
            _frenzyNext = false;
            // A short grace after the intro: the first attack is never instant.
            for (int i = 0; i < _cooldowns.Length; i++) _cooldowns[i] = _definition.Attacks[i].Cooldown * 0.5f;
        }

        void TickCooldowns(float dt)
        {
            for (int i = 0; i < _cooldowns.Length; i++) _cooldowns[i] -= dt;
        }

        bool TryStartAttack(float2 position)
        {
            if (_target < 0 || _cooldowns.Length == 0) return false;
            float2 goal = new float2(_players.X[_target], _players.Z[_target]);
            float distance = math.distance(position, goal);
            int phaseBit = _state.Phase == BossPhase.Enraged ? 4 : _state.Phase == BossPhase.Phase2 ? 2 : 1;

            int chosen = -1;
            if (_frenzyNext)
            {
                _frenzyNext = false;
                for (int i = 0; i < _cooldowns.Length && chosen < 0; i++)
                    if (_definition.Attacks[i].Kind == BossAttackKind.Charge) chosen = i;
            }
            if (chosen < 0)
            {
                float total = 0f;
                for (int i = 0; i < _cooldowns.Length; i++) total += Eligible(i, phaseBit, distance) ? _definition.Attacks[i].Weight : 0f;
                if (total <= 0f) return false;
                float roll = _rng.NextFloat() * total;
                for (int i = 0; i < _cooldowns.Length && chosen < 0; i++)
                {
                    if (!Eligible(i, phaseBit, distance)) continue;
                    roll -= _definition.Attacks[i].Weight;
                    if (roll <= 0f) chosen = i;
                }
                if (chosen < 0) return false;
            }
            Begin(chosen, position, goal);
            return true;
        }

        bool Eligible(int i, int phaseBit, float distance)
        {
            BossAttackDefinition a = _definition.Attacks[i];
            return _cooldowns[i] <= 0f && (a.PhaseMask & phaseBit) != 0 && distance >= a.Range.x && distance <= a.Range.y;
        }

        void Begin(int index, float2 position, float2 goal)
        {
            BossAttackDefinition a = _definition.Attacks[index];
            _attack = index;
            _stage = Stage.Telegraph;
            _stageTimer = a.TelegraphSeconds;
            _origin = position;
            _goal = goal;
            float2 to = goal - position;
            _dir = math.lengthsq(to) > 1e-4f ? math.normalize(to) : Facing(_heading);
            _heading = HeadingTo(_dir);
            _hitMask = 0;
            _travelled = 0f;
            _ring = a.Radius;
            AttacksStarted++;
            _state.Attacks.Publish(new BossAttackStarted
            {
                Attack = (byte)index, Kind = a.Kind, OriginX = position.x, OriginZ = position.y, TargetX = goal.x, TargetZ = goal.y,
                DirX = _dir.x, DirZ = _dir.y, Elapsed = 0f,
            });
        }

        byte TickAttack(float dt, ref float2 position)
        {
            BossAttackDefinition a = _definition.Attacks[_attack];
            _stageTimer -= dt;
            switch (_stage)
            {
                case Stage.Telegraph:
                    if (_stageTimer > 0f) return (byte)a.Clip;
                    Land(a, ref position);
                    break;
                case Stage.Active:
                    if (a.Kind == BossAttackKind.GroundSlam) TickRing(a, dt);
                    else if (a.Kind == BossAttackKind.Charge) return TickCharge(a, dt, ref position);
                    break;
                default:
                    if (_stageTimer <= 0f) Finish(a);
                    return (byte)CrowdClipId.Idle;
            }
            return (byte)a.Clip;
        }

        /// <summary>End of the telegraph: the hit lands.</summary>
        void Land(BossAttackDefinition a, ref float2 position)
        {
            switch (a.Kind)
            {
                case BossAttackKind.GroundSlam:
                    HitCircle(_origin, a.Radius, a.Damage);
                    _ring = a.Radius;
                    _hitMask = 0;
                    _stage = Stage.Active;
                    break;
                case BossAttackKind.Charge:
                    _stage = Stage.Active;
                    break;
                case BossAttackKind.PropThrow:
                    HitCircle(_goal, a.Radius, a.Damage);
                    Barrels?.OnBlast(_goal, a.Radius, a.Damage);
                    Recover(a);
                    break;
                default:
                    Summon(a, position);
                    Recover(a);
                    break;
            }
        }

        void TickRing(BossAttackDefinition a, float dt)
        {
            _ring += a.RingSpeed * dt;
            float half = a.RingWidth * 0.5f;
            for (int p = 0; p < PlayerStateTable.Max; p++)
            {
                if ((_hitMask & (1 << p)) != 0 || !_players.CanAct(p)) continue;
                float d = math.distance(_origin, new float2(_players.X[p], _players.Z[p]));
                if (math.abs(d - _ring) > half + PlayerRadius) continue;
                _hitMask |= 1 << p;
                HitPlayer(p, a.RingDamage);
            }
            if (_ring >= a.RingMaxRadius) Recover(a);
        }

        byte TickCharge(BossAttackDefinition a, float dt, ref float2 position)
        {
            float step = a.ChargeSpeed * dt;
            float2 next = position + _dir * step;
            if (!Clear(next, SpawnClearance * 0.6f))
            {
                // Head first into a wall: dazed and open (TDD_01 §10).
                _stunTimer = a.WallStunSeconds;
                Recover(a);
                return (byte)CrowdClipId.Idle;
            }
            position = next;
            _travelled += step;
            for (int p = 0; p < PlayerStateTable.Max; p++)
            {
                if ((_hitMask & (1 << p)) != 0 || !_players.CanAct(p)) continue;
                if (math.distance(position, new float2(_players.X[p], _players.Z[p])) > a.Radius + PlayerRadius) continue;
                _hitMask |= 1 << p;
                HitPlayer(p, a.Damage);
            }
            Barrels?.OnBlast(position, a.Radius + 0.5f, a.Damage);
            if (_travelled >= a.Length) Recover(a);
            return (byte)CrowdClipId.Run;
        }

        void Summon(BossAttackDefinition a, float2 position)
        {
            int count = a.SummonCount + BossDefinition.ForPlayers(_definition.ExtraSummonByPlayers, CountPlayers()) + _state.Appearance;
            for (int i = 0; i < count; i++)
            {
                float angle = _rng.Range(-math.PI, math.PI);
                float2 p = position + new float2(math.cos(angle), math.sin(angle)) * _rng.Range(3f, 6f);
                if (!_world.Nav.IsWalkable(p)) continue;
                if (_world.Spawn(p, math.degrees(angle), a.SummonType) >= 0) Summoned++;
            }
        }

        void Recover(BossAttackDefinition a)
        {
            _stage = Stage.Recovery;
            _stageTimer = a.RecoverySeconds;
        }

        void Finish(BossAttackDefinition a)
        {
            float scale = PhaseValue(_definition.CooldownScale) * BossDefinition.ForPlayers(_definition.CooldownByPlayers, CountPlayers())
                          * math.pow(0.9f, _state.Appearance);
            _cooldowns[_attack] = a.Cooldown * scale;
            if (_state.Phase == BossPhase.Enraged && _definition.FrenzyInEnraged && a.Kind == BossAttackKind.GroundSlam) _frenzyNext = true;
            _attack = -1;
        }

        void HitCircle(float2 centre, float radius, float damage)
        {
            for (int p = 0; p < PlayerStateTable.Max; p++)
            {
                if (!_players.CanAct(p)) continue;
                if (math.distance(centre, new float2(_players.X[p], _players.Z[p])) > radius + PlayerRadius) continue;
                HitPlayer(p, damage);
            }
        }

        void HitPlayer(int player, float damage)
        {
            PlayerHits++;
            Damage?.Damage(player, damage);
        }
    }
}
