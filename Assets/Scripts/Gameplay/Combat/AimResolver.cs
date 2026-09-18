using LastGround.Core.Input;
using LastGround.Core.Tick;
using LastGround.Data.Weapons;
using LastGround.Gameplay.Crowd;
using LastGround.Gameplay.Navigation;
using LastGround.Gameplay.Players;
using Unity.Mathematics;

namespace LastGround.Gameplay.Combat
{
    /// <summary>
    /// Turns raw stick input into the final aim (TDD_01 §3.3–3.4) and is itself the input source for the motor and
    /// the weapon. Manual: soft aim assist bends the stick direction towards the best zombie inside a small cone.
    /// Auto: picks the nearest visible zombie in range at 8 Hz, tracks it every frame and holds the trigger; touching
    /// the right stick overrides. Runs in the Input phase after the device input.
    /// </summary>
    public sealed class AimResolver : ITickable, IPlayerInputSource
    {
        const float AutoSelectInterval = 1f / 8f;

        readonly IPlayerInputSource _raw;
        readonly PlayerStateTable _players;
        readonly ICrowdRenderSource _crowd;
        readonly NavGrid _nav;
        readonly WeaponDefinition _weapon;
        PlayerInputFrame _frame;
        float _selectTimer;
        int _autoTarget = -1;

        public AimResolver(IPlayerInputSource raw, PlayerStateTable players, ICrowdRenderSource crowd, NavGrid nav, WeaponDefinition weapon)
        {
            _raw = raw;
            _players = players;
            _crowd = crowd;
            _nav = nav;
            _weapon = weapon;
        }

        public ControlMode Mode { get; set; }

        /// <summary>0 = off, 0.5 = low, 1 = high (Settings → aim assist).</summary>
        public float AssistLevel { get; set; } = 0.5f;

        public PlayerInputFrame Current => _frame;

        /// <summary>Slots to leave alone (the weapon's presumed-dead zombies). Optional.</summary>
        public bool[] Ignore { get; set; }

        /// <summary>Slot the auto mode is shooting at, or -1 (HUD reticle).</summary>
        public int AutoTarget => _autoTarget;

        public void Tick(float dt, uint tick)
        {
            _frame = _raw.Current;
            int me = _players.Local.IsValid ? _players.Local.Value : -1;
            if (me < 0 || !_players.Active[me]) return;
            var origin = new float2(_players.X[me], _players.Z[me]);

            if (_frame.AimActive)
            {
                _autoTarget = -1;
                if (AssistLevel > 0f) Assist(origin);
                return;
            }
            if (Mode != ControlMode.AutoAimAutoFire) return;

            _selectTimer -= dt;
            if (_selectTimer <= 0f || !Valid(_autoTarget, origin))
            {
                _selectTimer = AutoSelectInterval;
                _autoTarget = SelectNearest(origin);
            }
            if (_autoTarget < 0) return;
            float2 dir = math.normalizesafe(new float2(_crowd.X[_autoTarget], _crowd.Z[_autoTarget]) - origin);
            _frame.AimX = dir.x;
            _frame.AimY = dir.y;
            _frame.AimActive = true;
            _frame.FireHeld = true;
        }

        void Assist(float2 origin)
        {
            float2 aim = math.normalizesafe(new float2(_frame.AimX, _frame.AimY));
            if (math.lengthsq(aim) < 0.5f) return;
            float cone = math.cos(math.radians(_weapon.AimAssistConeDeg));
            float range2 = _weapon.Range * _weapon.Range;
            int best = -1;
            float bestScore = float.MaxValue;
            for (int i = 0; i < _crowd.Capacity; i++)
            {
                if (!_crowd.Alive[i] || (Ignore != null && Ignore[i])) continue;
                float2 to = new float2(_crowd.X[i], _crowd.Z[i]) - origin;
                float d2 = math.lengthsq(to);
                if (d2 > range2 || d2 < 1e-4f) continue;
                float d = math.sqrt(d2);
                float cos = math.dot(to / d, aim);
                if (cos < cone) continue;
                // Angle error dominates, distance breaks ties.
                float score = (1f - cos) * 50f + d * 0.02f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }
            if (best < 0) return;
            float2 target = math.normalizesafe(new float2(_crowd.X[best], _crowd.Z[best]) - origin);
            float strength = _weapon.AimAssistStrength * AssistLevel;
            float2 blended = math.normalizesafe(math.lerp(aim, target, strength), aim);
            _frame.AimX = blended.x;
            _frame.AimY = blended.y;
        }

        int SelectNearest(float2 origin)
        {
            float range2 = _weapon.Range * _weapon.Range;
            int best = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < _crowd.Capacity; i++)
            {
                if (!_crowd.Alive[i] || (Ignore != null && Ignore[i])) continue;
                float d2 = math.distancesq(new float2(_crowd.X[i], _crowd.Z[i]), origin);
                if (d2 >= bestDistance || d2 > range2) continue;
                if (_nav != null && !_nav.HasLineOfSight(origin, new float2(_crowd.X[i], _crowd.Z[i]))) continue;
                bestDistance = d2;
                best = i;
            }
            return best;
        }

        bool Valid(int slot, float2 origin)
        {
            if (slot < 0 || !_crowd.Alive[slot] || (Ignore != null && Ignore[slot])) return false;
            return math.distancesq(new float2(_crowd.X[slot], _crowd.Z[slot]), origin) <= _weapon.Range * _weapon.Range;
        }
    }
}
