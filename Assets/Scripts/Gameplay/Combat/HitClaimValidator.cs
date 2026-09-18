using LastGround.Data.Weapons;
using LastGround.Gameplay.Crowd;
using LastGround.Gameplay.Navigation;
using LastGround.Gameplay.Players;
using Unity.Mathematics;

namespace LastGround.Gameplay.Combat
{
    /// <summary>
    /// Host-side sanity checks for hit claims (TDD_01 §5.2, TDD_02 §16): the shooter is alive, the zombie is the
    /// same generation and still alive, it is within weapon range, near where the shooter saw it, visible (no wall),
    /// and the shooter does not fire faster than the weapon allows. Tolerances are generous: this is LAN co-op,
    /// the goal is to stop impossible hits, not to catch skilled players.
    /// </summary>
    public sealed class HitClaimValidator
    {
        /// <summary>Extra distance allowed beyond weapon range (movement between frames, interpolation).</summary>
        public const float RangeSlack = 2f;
        /// <summary>How far the shooter's view of a zombie may differ from the host's (interp delay × speed + knockback).</summary>
        public const float PositionTolerance = 2.5f;
        /// <summary>Allowed shot rate over the weapon's nominal rate.</summary>
        public const float RateSlack = 1.25f;

        readonly PlayerStateTable _players;
        readonly CrowdState _crowd;
        readonly NavGrid _nav;
        readonly WeaponDefinition[] _weapons;
        readonly float[] _tokens = new float[PlayerStateTable.Max];
        readonly ushort[] _lastShot = new ushort[PlayerStateTable.Max];
        readonly bool[] _hasShot = new bool[PlayerStateTable.Max];
        readonly int[] _claimsThisShot = new int[PlayerStateTable.Max];

        public HitClaimValidator(PlayerStateTable players, CrowdState crowd, NavGrid nav, WeaponDefinition[] weaponsByNetIndex)
        {
            _players = players;
            _crowd = crowd;
            _nav = nav;
            _weapons = weaponsByNetIndex;
            for (int p = 0; p < _tokens.Length; p++) _tokens[p] = 4f;
        }

        public WeaponDefinition WeaponOf(byte netIndex) => netIndex < _weapons.Length ? _weapons[netIndex] : null;

        /// <summary>Refills each player's shot budget (call once per sim tick).</summary>
        public void Refill(float dt)
        {
            for (int p = 0; p < _tokens.Length; p++)
            {
                // M4: one weapon for everyone; M6 tracks the equipped weapon per player.
                WeaponDefinition weapon = _weapons.Length > 0 ? _weapons[0] : null;
                if (weapon == null) continue;
                float rate = weapon.FireRate * RateSlack;
                _tokens[p] = math.min(Burst(weapon), _tokens[p] + rate * dt);
            }
        }

        public HitClaimVerdict Check(in HitClaim claim)
        {
            int p = claim.Shooter;
            if (p >= PlayerStateTable.Max || !_players.Active[p]) return HitClaimVerdict.UnknownShooter;
            if (!_players.CanAct(p)) return HitClaimVerdict.ShooterDead;
            WeaponDefinition weapon = WeaponOf(claim.Weapon);
            if (weapon == null) return HitClaimVerdict.UnknownWeapon;

            int slot = claim.Slot;
            if (slot >= _crowd.Capacity || !_crowd.AliveSlots[slot] || _crowd.Generation[slot] != claim.Generation)
                return HitClaimVerdict.TargetGone;

            var shooter = new float2(_players.X[p], _players.Z[p]);
            var seen = new float2(claim.HitX, claim.HitZ);
            if (math.distance(shooter, seen) > weapon.Range + RangeSlack) return HitClaimVerdict.OutOfRange;
            if (math.distance(seen, new float2(_crowd.PosX[slot], _crowd.PosZ[slot])) > PositionTolerance)
                return HitClaimVerdict.FarFromTarget;
            if (_nav != null && !_nav.HasLineOfSight(shooter, seen)) return HitClaimVerdict.NoLineOfSight;

            // Rate: each new shot costs a token; claims of the same shot are capped by pellets × pierce.
            if (!_hasShot[p] || (short)(claim.ShotSeq - _lastShot[p]) > 0)
            {
                if (_tokens[p] < 1f) return HitClaimVerdict.RateLimited;
                _tokens[p] -= 1f;
                _lastShot[p] = claim.ShotSeq;
                _hasShot[p] = true;
                _claimsThisShot[p] = 0;
            }
            else if (claim.ShotSeq != _lastShot[p])
            {
                // Claims of an older shot arriving late (reordering cannot happen on the reliable channel).
                return HitClaimVerdict.RateLimited;
            }
            if (++_claimsThisShot[p] > weapon.MaxClaimsPerShot) return HitClaimVerdict.TooManyClaims;
            return HitClaimVerdict.Accepted;
        }

        /// <summary>Burst allowance: network batching can deliver several shots at once.</summary>
        static float Burst(WeaponDefinition weapon) => math.max(4f, weapon.FireRate * 0.75f);
    }
}
