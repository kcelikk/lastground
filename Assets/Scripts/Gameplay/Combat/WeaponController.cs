using LastGround.Core.Events;
using LastGround.Core.Input;
using LastGround.Core.Tick;
using LastGround.Data.Combat;
using LastGround.Data.Weapons;
using LastGround.Gameplay.Crowd;
using LastGround.Gameplay.Loot;
using LastGround.Gameplay.Navigation;
using LastGround.Gameplay.Players;
using LastGround.Gameplay.Upgrades;
using Unity.Mathematics;

namespace LastGround.Gameplay.Combat
{
    /// <summary>
    /// The local player's weapons (TDD_01 §6.2): two slots from the loadout (primary + sidearm), fire rate, magazine and
    /// reserve ammo, automatic reload (empty magazine, or 1 s without firing), deterministic spread, hit detection
    /// against what this device shows, and hit claims. Runs every rendered frame so firing feels immediate; the host
    /// resolves damage in its Combat phase. On clients it also publishes the predicted hit (flash, blood, damage number).
    /// Zombies this weapon has already dealt lethal damage to are "presumed dead" for a moment: bullets pass through
    /// them to the next target instead of being wasted on a corpse-to-be (and its claim rejected as TargetGone).
    /// Partial <c>.Slots</c>: loadout sync, ammo, swapping, grenades.
    /// </summary>
    public sealed partial class WeaponController : ITickable, IWeaponStatus
    {
        public const float HitRadius = 0.5f;
        const float BarrelHitRadius = 0.45f;
        const float IdleReloadDelay = 1f;
        const float MuzzleForward = 0.6f;
        const float FiringFlagHold = 0.2f;
        const int MaxHitsPerPellet = 8;
        /// <summary>Longer than a kill takes to come back from the host; afterwards the zombie is targetable again.</summary>
        const float PresumedDeadTime = 0.6f;

        readonly PlayerStateTable _players;
        readonly IPlayerInputSource _input;
        readonly ICrowdRenderSource _targets;
        readonly NavGrid _nav;
        readonly uint _runSeed;
        readonly IHitClaimSink _sink;
        readonly EventChannel<ShotFired> _shots;
        readonly CrowdReplica _predictions;
        readonly int[] _hitSlots = new int[MaxHitsPerPellet];
        readonly float[] _hitDistances = new float[MaxHitsPerPellet];
        readonly float _zombieHealth;
        readonly float[] _dealt;
        readonly byte[] _dealtGeneration;
        readonly float[] _presumedDeadUntil;
        readonly bool[] _presumedDead;
        int _presumedCount;
        float _time;

        float _cooldown;
        float _reloadTimer;
        float _sinceFired = float.MaxValue;
        ushort _shotSeq;

        /// <param name="predictions">Client replica for predicted hits; null on the host (it resolves at once).</param>
        /// <param name="catalog">Zombie health per type and elite (presumed-dead tracking); null uses <paramref name="zombieHealth"/>.</param>
        public WeaponController(PlayerStateTable players, IPlayerInputSource input, LoadoutTable loadouts, ICrowdRenderSource targets,
            NavGrid nav, uint runSeed, IHitClaimSink sink, EventChannel<ShotFired> shots, CrowdReplica predictions,
            CombatCatalog catalog = null, float zombieHealth = float.MaxValue)
        {
            _players = players;
            _input = input;
            _loadouts = loadouts;
            _targets = targets;
            _nav = nav;
            _runSeed = runSeed;
            _sink = sink;
            _shots = shots;
            _predictions = predictions;
            _catalog = catalog;
            _zombieHealth = zombieHealth;
            _dealt = new float[targets.Capacity];
            _dealtGeneration = new byte[targets.Capacity];
            _presumedDeadUntil = new float[targets.Capacity];
            _presumedDead = new bool[targets.Capacity];
            _changedReader = loadouts.Changed.CreateReader();
        }

        /// <summary>One weapon, no sidearm (M4–M5 tests and benchmarks).</summary>
        public WeaponController(PlayerStateTable players, IPlayerInputSource input, WeaponDefinition weapon, ICrowdRenderSource targets,
            NavGrid nav, uint runSeed, IHitClaimSink sink, EventChannel<ShotFired> shots, CrowdReplica predictions,
            float zombieHealth = float.MaxValue)
            : this(players, input, SingleWeapon(weapon), targets, nav, runSeed, sink, shots, predictions, null, zombieHealth)
        {
            _standaloneWeapon = weapon;
        }

        /// <summary>Team builds (M5 upgrades); null = the weapons' base values.</summary>
        public TeamBuilds Builds { get; set; }

        /// <summary>The active weapon's current numbers after upgrades.</summary>
        public WeaponStats Stats => _stats[_active];

        public int Ammo => _ammo[_active];
        public int MagazineSize => _stats[_active].MagazineSize;
        public bool Reloading => _reloadTimer > 0f;
        public float ReloadProgress => Reloading ? 1f - _reloadTimer / _stats[_active].ReloadTime : 0f;
        public WeaponDefinition Weapon => _weapon[_active];
        public int ShotsFired { get; private set; }
        public int ClaimsSent { get; private set; }

        /// <summary>Zombies currently skipped because this weapon expects them to die.</summary>
        public int PresumedDead => _presumedCount;

        /// <summary>Per slot: true while presumed dead (auto-aim skips these too).</summary>
        public bool[] PresumedDeadMask => _presumedDead;

        public float MoveMultiplier =>
            _weapon[_active] != null && _sinceFired < FiringFlagHold ? _weapon[_active].MoveSpeedMultiplierWhileFiring : 1f;

        public void Tick(float dt, uint tick)
        {
            int me = _players.Local.IsValid ? _players.Local.Value : -1;
            if (me < 0 || !_players.Active[me]) return;
            SyncLoadout(me);
            ReadPickups(me);
            RefreshStats(me);
            if (_weapon[_active] == null) return;
            if (!_players.CanAct(me))
            {
                // Going down reloads from reserve: the player gets back up ready to fight.
                _reloadTimer = 0f;
                FinishReload(_active);
                _players.Firing[me] = false;
                return;
            }

            _sinceFired += dt;
            _time += dt;
            if (_presumedCount > 0) ExpirePresumedDead();
            if (_reloadTimer > 0f)
            {
                _reloadTimer -= dt;
                if (_reloadTimer <= 0f)
                {
                    _reloadTimer = 0f;
                    FinishReload(_active);
                }
            }

            PlayerInputFrame frame = _input.Current;
            HandleSwitchAndGrenade(me, frame);
            ref readonly WeaponStats stats = ref _stats[_active];
            bool trigger = frame.FireHeld && frame.AimActive;
            _cooldown -= dt;
            if (!trigger && _cooldown < 0f) _cooldown = 0f;

            while (trigger && _cooldown <= 0f && _ammo[_active] > 0 && _reloadTimer <= 0f)
            {
                Fire(me, new float2(frame.AimX, frame.AimY), stats);
                _cooldown += stats.ShotInterval;
                _ammo[_active]--;
                _sinceFired = 0f;
            }
            // Time spent unable to fire (reloading, empty) must not bank shots: that fired ~14 bullets in one frame
            // after every reload. Carry-over below zero only matters inside the loop above.
            if (_cooldown < 0f) _cooldown = 0f;

            if (_reloadTimer <= 0f && _ammo[_active] < stats.MagazineSize && HasReserve(_active)
                && (_ammo[_active] == 0 || _sinceFired > IdleReloadDelay))
                _reloadTimer = stats.ReloadTime;
            AutoSwitchWhenDry();

            _players.Firing[me] = _sinceFired < FiringFlagHold;
        }

        void Fire(int me, float2 aim, in WeaponStats stats)
        {
            float2 origin = new float2(_players.X[me], _players.Z[me]);
            float2 baseDir = math.normalizesafe(aim, new float2(0f, 1f));
            ushort seq = ++_shotSeq;
            uint seed = ShotRng.Seed(_runSeed, me, seq);
            int pellets = math.max(1, stats.PelletCount);
            int maxHits = math.min(MaxHitsPerPellet, math.max(0, stats.Penetration) + 1);
            ShotsFired++;

            for (int pellet = 0; pellet < pellets; pellet++)
            {
                float angle = ShotRng.SpreadRadians(seed, pellet, stats.SpreadDeg);
                math.sincos(angle, out float sin, out float cos);
                var dir = new float2(baseDir.x * cos - baseDir.y * sin, baseDir.x * sin + baseDir.y * cos);
                int hits = HitQuery.Cast(_targets, _nav, origin, dir, stats.Range, HitRadius, maxHits, _hitSlots, _hitDistances,
                    out float end, _presumedDead);

                for (int k = 0; k < hits; k++)
                {
                    int slot = _hitSlots[k];
                    var claim = new HitClaim
                    {
                        Shooter = (byte)me,
                        ShotSeq = seq,
                        Weapon = stats.NetIndex,
                        Pellet = (byte)pellet,
                        Pierce = (byte)k,
                        Slot = (ushort)slot,
                        Generation = _targets.GenerationOf(slot),
                        HitX = _targets.X[slot],
                        HitZ = _targets.Z[slot],
                    };
                    _sink.Submit(claim);
                    ClaimsSent++;
                    float damage = DamageResolver.Resolve(stats, seed, pellet, out bool crit) * DamageTakenOf(slot);
                    TrackDamage(slot, claim.Generation, damage);
                    if (_predictions != null)
                    {
                        _predictions.PublishLocalHit(new CrowdHit
                        {
                            Slot = slot, X = claim.HitX, Z = claim.HitZ, DirX = dir.x, DirZ = dir.y,
                            Damage = damage, Crit = crit, Local = true,
                        });
                    }
                }

                end = HitBarrel(me, origin, dir, end, stats, seed, pellet);
                float2 muzzle = origin + baseDir * MuzzleForward;
                float2 tip = origin + dir * math.max(end, MuzzleForward);
                _shots?.Publish(new ShotFired
                {
                    Shooter = (byte)me, Weapon = stats.NetIndex, OriginX = muzzle.x, OriginZ = muzzle.y, EndX = tip.x, EndZ = tip.y,
                    FirstPellet = pellet == 0, Local = true,
                });
            }
        }

        /// <summary>
        /// The pellet's first intact barrel or fuel tank before <paramref name="end"/> takes its damage (host checks it);
        /// the tracer then stops there. Returns the new end distance.
        /// </summary>
        float HitBarrel(int me, float2 origin, float2 dir, float end, in WeaponStats stats, uint seed, int pellet)
        {
            if (Interactables == null || InteractableHits == null) return end;
            int best = -1;
            float bestDistance = end;
            for (int i = 0; i < Interactables.Count; i++)
            {
                if (!Interactables.Intact[i] || !Interactables.IsExplosive(i)) continue;
                float2 to = Interactables.Position[i] - origin;
                float along = math.dot(to, dir);
                if (along <= 0f || along >= bestDistance) continue;
                if (math.lengthsq(to - dir * along) > BarrelHitRadius * BarrelHitRadius) continue;
                best = i;
                bestDistance = along;
            }
            if (best < 0) return end;
            InteractableHits.HitInteractable(me, best, DamageResolver.Resolve(stats, seed, pellet, out _));
            return bestDistance;
        }

        void TrackDamage(int slot, byte generation, float damage)
        {
            if (_dealtGeneration[slot] != generation)
            {
                _dealtGeneration[slot] = generation;
                _dealt[slot] = 0f;
            }
            _dealt[slot] += damage;
            if (_dealt[slot] < HealthOf(slot) || _presumedDead[slot]) return;
            _presumedDead[slot] = true;
            _presumedDeadUntil[slot] = _time + PresumedDeadTime;
            _presumedCount++;
        }

        void ExpirePresumedDead()
        {
            for (int i = 0; i < _presumedDead.Length; i++)
            {
                if (!_presumedDead[i]) continue;
                // Gone (confirmed), reused by a new zombie, or the host disagreed: stop skipping it.
                if (_time < _presumedDeadUntil[i] && _targets.Alive[i] && _targets.GenerationOf(i) == _dealtGeneration[i]) continue;
                _presumedDead[i] = false;
                _dealt[i] = 0f;
                _presumedCount--;
            }
        }

        /// <summary>Full health of the zombie in this slot: its type × elite multiplier (burn damage is not tracked).</summary>
        float HealthOf(int slot)
        {
            if (_catalog == null) return _zombieHealth;
            var zombie = _catalog.Zombie(_targets.Types[slot]);
            if (zombie == null) return _zombieHealth;
            var elite = _catalog.Elite(_targets.Elites[slot]);
            return zombie.MaxHealth * (elite != null ? elite.HealthMultiplier : 1f);
        }

        /// <summary>Armored elites take less damage; the predicted damage number matches the host's.</summary>
        float DamageTakenOf(int slot)
        {
            var elite = _catalog?.Elite(_targets.Elites[slot]);
            return elite != null ? elite.DamageTakenMultiplier : 1f;
        }
    }
}
