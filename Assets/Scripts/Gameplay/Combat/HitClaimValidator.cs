using LastGround.Data.Weapons;
using LastGround.Gameplay.Crowd;
using LastGround.Gameplay.Navigation;
using LastGround.Gameplay.Players;
using LastGround.Gameplay.Upgrades;
using Unity.Mathematics;

namespace LastGround.Gameplay.Combat
{
    /// <summary>
    /// Host-side sanity checks for hit claims (TDD_01 §5.2, TDD_02 §16): the shooter is alive and carries the claimed
    /// weapon (or dropped it a moment ago), the zombie is the same generation and still alive, it is within that
    /// weapon's range, near where the shooter saw it, visible (no wall), and the shooter does not fire faster than the
    /// weapon allows. Tolerances are generous: this is LAN co-op, the goal is to stop impossible hits, not to catch
    /// skilled players.
    /// </summary>
    public sealed class HitClaimValidator
    {
        /// <summary>Extra distance allowed beyond weapon range (movement between frames, interpolation).</summary>
        public const float RangeSlack = 2f;
        /// <summary>How far the shooter's view of a zombie may differ from the host's (interp delay × speed + knockback).</summary>
        public const float PositionTolerance = 2.5f;
        /// <summary>Allowed shot rate over the weapon's nominal rate.</summary>
        public const float RateSlack = 1.25f;
        /// <summary>Claims with a weapon swapped out this recently still count (they were fired before the swap arrived).</summary>
        public const float SwapGrace = 1.5f;

        readonly PlayerStateTable _players;
        readonly CrowdState _crowd;
        readonly NavGrid _nav;
        readonly WeaponDefinition[] _weapons;
        readonly float[] _tokens = new float[PlayerStateTable.Max];
        readonly ushort[] _lastShot = new ushort[PlayerStateTable.Max];
        readonly bool[] _hasShot = new bool[PlayerStateTable.Max];
        readonly int[] _claimsThisShot = new int[PlayerStateTable.Max];
        readonly WeaponStats[] _stats;
        readonly int[] _statsVersion;
        readonly byte[] _previousPrimary = { LoadoutTable.NoWeapon, LoadoutTable.NoWeapon, LoadoutTable.NoWeapon, LoadoutTable.NoWeapon };
        readonly byte[] _knownPrimary = { LoadoutTable.NoWeapon, LoadoutTable.NoWeapon, LoadoutTable.NoWeapon, LoadoutTable.NoWeapon };
        readonly float[] _swapAge = new float[PlayerStateTable.Max];

        public HitClaimValidator(PlayerStateTable players, CrowdState crowd, NavGrid nav, WeaponDefinition[] weaponsByNetIndex)
        {
            _players = players;
            _crowd = crowd;
            _nav = nav;
            _weapons = weaponsByNetIndex;
            _stats = new WeaponStats[PlayerStateTable.Max * weaponsByNetIndex.Length];
            _statsVersion = new int[_stats.Length];
            for (int i = 0; i < _statsVersion.Length; i++) _statsVersion[i] = -1;
            for (int p = 0; p < _tokens.Length; p++) _tokens[p] = 4f;
        }

        public WeaponDefinition WeaponOf(byte netIndex) => netIndex < _weapons.Length ? _weapons[netIndex] : null;

        /// <summary>Team builds: fire rate, pierce and damage follow each shooter's upgrades. Null = base values.</summary>
        public TeamBuilds Builds { get; set; }

        /// <summary>Who carries what. Null = everyone may use every weapon (M4–M5 tests).</summary>
        public LoadoutTable Loadouts { get; set; }

        /// <summary>A shooter's numbers for one weapon after their upgrades (cached per player and weapon).</summary>
        public ref readonly WeaponStats StatsOf(int player, byte weapon)
        {
            int index = player * _weapons.Length + weapon;
            PlayerBuild build = Builds?.Of(player);
            int version = build != null ? build.Version : 0;
            if (_statsVersion[index] != version)
            {
                _statsVersion[index] = version;
                _stats[index] = WeaponStats.From(_weapons[weapon], build);
            }
            return ref _stats[index];
        }

        /// <summary>Refills each player's shot budget at the rate of the fastest weapon they carry (call once per sim tick).</summary>
        public void Refill(float dt)
        {
            for (int p = 0; p < _tokens.Length; p++)
            {
                TrackSwap(p, dt);
                float fireRate = FastestRate(p);
                if (fireRate <= 0f) continue;
                _tokens[p] = math.min(Burst(fireRate), _tokens[p] + fireRate * RateSlack * dt);
            }
        }

        public HitClaimVerdict Check(in HitClaim claim)
        {
            int p = claim.Shooter;
            if (p >= PlayerStateTable.Max || !_players.Active[p]) return HitClaimVerdict.UnknownShooter;
            if (!_players.CanAct(p)) return HitClaimVerdict.ShooterDead;
            WeaponDefinition weapon = WeaponOf(claim.Weapon);
            if (weapon == null || !Carries(p, claim.Weapon)) return HitClaimVerdict.UnknownWeapon;

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
            if (++_claimsThisShot[p] > StatsOf(p, claim.Weapon).MaxClaimsPerShot) return HitClaimVerdict.TooManyClaims;
            return HitClaimVerdict.Accepted;
        }

        bool Carries(int p, byte weapon)
        {
            if (Loadouts == null) return true;
            return Loadouts.Holds(p, weapon) || (weapon == _previousPrimary[p] && _swapAge[p] < SwapGrace);
        }

        void TrackSwap(int p, float dt)
        {
            _swapAge[p] += dt;
            if (Loadouts == null || Loadouts.Primary[p] == _knownPrimary[p]) return;
            _previousPrimary[p] = _knownPrimary[p];
            _knownPrimary[p] = Loadouts.Primary[p];
            _swapAge[p] = 0f;
        }

        float FastestRate(int p)
        {
            if (Loadouts == null) return _weapons.Length > 0 && _weapons[0] != null ? StatsOf(p, 0).FireRate : 0f;
            float rate = 0f;
            byte primary = Loadouts.Primary[p], sidearm = Loadouts.Sidearm[p];
            if (primary < _weapons.Length) rate = math.max(rate, StatsOf(p, primary).FireRate);
            if (sidearm < _weapons.Length) rate = math.max(rate, StatsOf(p, sidearm).FireRate);
            if (_swapAge[p] < SwapGrace && _previousPrimary[p] < _weapons.Length) rate = math.max(rate, StatsOf(p, _previousPrimary[p]).FireRate);
            return rate;
        }

        /// <summary>Burst allowance: network batching can deliver several shots at once.</summary>
        static float Burst(float fireRate) => math.max(4f, fireRate * 0.75f);
    }
}
