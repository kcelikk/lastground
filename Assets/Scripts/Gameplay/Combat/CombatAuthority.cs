using LastGround.Core.Tick;
using LastGround.Data.Combat;
using LastGround.Data.Weapons;
using LastGround.Gameplay.Crowd;
using LastGround.Gameplay.Navigation;
using LastGround.Gameplay.Players;
using LastGround.Gameplay.Upgrades;
using LastGround.Gameplay.Zombies;
using Unity.Mathematics;

namespace LastGround.Gameplay.Combat
{
    /// <summary>
    /// Host damage pipeline (TDD_01 §5.2): hit claims from every player (the host's own weapon included, no special
    /// case) are queued, validated, resolved with the shooter's deterministic RNG and applied to the ZombieWorld in
    /// the Combat sim phase. Kills flow out through the crowd's Deaths channel (corpses, replication).
    /// On-hit upgrades (M6) set the zombie on fire, slow it, or stun it on a deterministic roll.
    /// </summary>
    public sealed class CombatAuthority : ITickable, IHitClaimSink
    {
        const int InboxCapacity = 512;

        readonly ZombieWorld _world;
        readonly PlayerStateTable _players;
        readonly HitClaimValidator _validator;
        readonly uint _runSeed;
        readonly HitClaim[] _inbox = new HitClaim[InboxCapacity];
        readonly int[] _verdicts = new int[(int)HitClaimVerdict.Count];
        int _count;

        public CombatAuthority(ZombieWorld world, PlayerStateTable players, NavGrid nav, WeaponDefinition[] weaponsByNetIndex, uint runSeed)
        {
            _world = world;
            _players = players;
            _runSeed = runSeed;
            _validator = new HitClaimValidator(players, world.Crowd, nav, weaponsByNetIndex);
        }

        /// <summary>Who carries which weapon (claims must use one of them). Null = any weapon.</summary>
        public LoadoutTable Loadouts
        {
            get => _validator.Loadouts;
            set => _validator.Loadouts = value;
        }

        /// <summary>Durations of on-hit status effects. Null = upgrades apply no status effects.</summary>
        public CombatCatalog Catalog { get; set; }

        /// <summary>Team builds (upgraded damage, pierce, fire rate). Null = base weapon values.</summary>
        public TeamBuilds Builds
        {
            get => _validator.Builds;
            set => _validator.Builds = value;
        }

        public int Accepted => _verdicts[(int)HitClaimVerdict.Accepted];
        public int Kills { get; private set; }
        public int Dropped { get; private set; }

        /// <summary>Total rejected claims (all reasons).</summary>
        public int Rejected
        {
            get
            {
                int sum = 0;
                for (int i = 1; i < _verdicts.Length; i++) sum += _verdicts[i];
                return sum;
            }
        }

        public int CountOf(HitClaimVerdict verdict) => _verdicts[(int)verdict];

        public void Submit(in HitClaim claim)
        {
            if (_count >= InboxCapacity)
            {
                Dropped++;
                return;
            }
            _inbox[_count++] = claim;
        }

        public void Tick(float dt, uint tick)
        {
            _validator.Refill(dt);
            for (int i = 0; i < _count; i++) Resolve(in _inbox[i]);
            _count = 0;
        }

        void Resolve(in HitClaim claim)
        {
            HitClaimVerdict verdict = _validator.Check(in claim);
            _verdicts[(int)verdict]++;
            if (verdict != HitClaimVerdict.Accepted) return;

            ref readonly WeaponStats stats = ref _validator.StatsOf(claim.Shooter, claim.Weapon);
            uint seed = ShotRng.Seed(_runSeed, claim.Shooter, claim.ShotSeq);
            float damage = DamageResolver.Resolve(stats, seed, claim.Pellet, out bool crit);
            var shooter = new float2(_players.X[claim.Shooter], _players.Z[claim.Shooter]);
            float2 dir = math.normalizesafe(new float2(claim.HitX, claim.HitZ) - shooter);
            bool local = _players.Local.IsValid && claim.Shooter == _players.Local.Value;
            if (_world.ApplyDamage(claim.Slot, damage, dir, stats.Knockback, crit, local, claim.Shooter))
            {
                Kills++;
                return;
            }
            ApplyOnHit(claim, stats, seed);
        }

        void ApplyOnHit(in HitClaim claim, in WeaponStats stats, uint seed)
        {
            if (Catalog == null) return;
            if (stats.BurnDps > 0f) _world.ApplyBurn(claim.Slot, stats.BurnDps, Catalog.BurnSeconds, claim.Shooter);
            if (stats.SlowPct > 0f) _world.ApplySlow(claim.Slot, 1f - stats.SlowPct / 100f, Catalog.SlowSeconds);
            if (ShotRng.IsStun(seed, claim.Pellet, claim.Pierce, stats.StunChance)) _world.ApplyStun(claim.Slot, Catalog.StunSeconds);
        }
    }
}
