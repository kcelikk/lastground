using LastGround.Core.Events;
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
    /// The local player's weapon (TDD_01 §6.2): fire rate, magazine, automatic reload (empty magazine, or 1 s
    /// without firing), deterministic spread, hit detection against what this device shows, and hit claims.
    /// Runs every rendered frame so firing feels immediate; the host resolves damage in its Combat phase.
    /// On clients it also publishes the predicted hit (flash, blood, damage number) right away.
    /// Zombies this weapon has already dealt lethal damage to are "presumed dead" for a moment: bullets pass through
    /// them to the next target instead of being wasted on a corpse-to-be (and its claim rejected as TargetGone).
    /// </summary>
    public sealed class WeaponController : ITickable, IWeaponStatus
    {
        public const float HitRadius = 0.5f;
        const float IdleReloadDelay = 1f;
        const float MuzzleForward = 0.6f;
        const float FiringFlagHold = 0.2f;
        const int MaxHitsPerPellet = 8;
        /// <summary>Longer than a kill takes to come back from the host; afterwards the zombie is targetable again.</summary>
        const float PresumedDeadTime = 0.6f;

        readonly PlayerStateTable _players;
        readonly IPlayerInputSource _input;
        readonly WeaponDefinition _weapon;
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
        /// <param name="zombieHealth">Health of a fresh zombie (M4: walkers only; M6 looks it up per type).</param>
        public WeaponController(PlayerStateTable players, IPlayerInputSource input, WeaponDefinition weapon,
            ICrowdRenderSource targets, NavGrid nav, uint runSeed, IHitClaimSink sink, EventChannel<ShotFired> shots,
            CrowdReplica predictions, float zombieHealth = float.MaxValue)
        {
            _players = players;
            _input = input;
            _weapon = weapon;
            _targets = targets;
            _nav = nav;
            _runSeed = runSeed;
            _sink = sink;
            _shots = shots;
            _predictions = predictions;
            _zombieHealth = zombieHealth;
            _dealt = new float[targets.Capacity];
            _dealtGeneration = new byte[targets.Capacity];
            _presumedDeadUntil = new float[targets.Capacity];
            _presumedDead = new bool[targets.Capacity];
            Ammo = weapon.MagazineSize;
        }

        public int Ammo { get; private set; }
        public int MagazineSize => _weapon.MagazineSize;
        public bool Reloading => _reloadTimer > 0f;
        public float ReloadProgress => Reloading ? 1f - _reloadTimer / _weapon.ReloadTime : 0f;
        public WeaponDefinition Weapon => _weapon;
        public int ShotsFired { get; private set; }
        public int ClaimsSent { get; private set; }

        /// <summary>Zombies currently skipped because this weapon expects them to die.</summary>
        public int PresumedDead => _presumedCount;

        /// <summary>Per slot: true while presumed dead (auto-aim skips these too).</summary>
        public bool[] PresumedDeadMask => _presumedDead;

        public void Tick(float dt, uint tick)
        {
            int me = _players.Local.IsValid ? _players.Local.Value : -1;
            if (me < 0 || !_players.Active[me]) return;
            if (_players.Dead[me])
            {
                // Dying refills the magazine: the player gets back up ready to fight.
                _reloadTimer = 0f;
                Ammo = _weapon.MagazineSize;
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
                    Ammo = _weapon.MagazineSize;
                }
            }

            PlayerInputFrame frame = _input.Current;
            bool trigger = frame.FireHeld && frame.AimActive;
            _cooldown -= dt;
            if (!trigger && _cooldown < 0f) _cooldown = 0f;

            while (trigger && _cooldown <= 0f && Ammo > 0 && _reloadTimer <= 0f)
            {
                Fire(me, new float2(frame.AimX, frame.AimY));
                _cooldown += _weapon.ShotInterval;
                Ammo--;
                _sinceFired = 0f;
            }

            if (_reloadTimer <= 0f && Ammo < _weapon.MagazineSize && (Ammo == 0 || _sinceFired > IdleReloadDelay))
                _reloadTimer = _weapon.ReloadTime;

            _players.Firing[me] = _sinceFired < FiringFlagHold;
        }

        void Fire(int me, float2 aim)
        {
            float2 origin = new float2(_players.X[me], _players.Z[me]);
            float2 baseDir = math.normalizesafe(aim, new float2(0f, 1f));
            ushort seq = ++_shotSeq;
            uint seed = ShotRng.Seed(_runSeed, me, seq);
            int pellets = math.max(1, _weapon.PelletCount);
            int maxHits = math.min(MaxHitsPerPellet, math.max(0, _weapon.Penetration) + 1);
            ShotsFired++;

            for (int pellet = 0; pellet < pellets; pellet++)
            {
                float angle = ShotRng.SpreadRadians(seed, pellet, _weapon.SpreadDeg);
                math.sincos(angle, out float sin, out float cos);
                var dir = new float2(baseDir.x * cos - baseDir.y * sin, baseDir.x * sin + baseDir.y * cos);
                int hits = HitQuery.Cast(_targets, _nav, origin, dir, _weapon.Range, HitRadius, maxHits, _hitSlots, _hitDistances,
                    out float end, _presumedDead);

                for (int k = 0; k < hits; k++)
                {
                    int slot = _hitSlots[k];
                    var claim = new HitClaim
                    {
                        Shooter = (byte)me,
                        ShotSeq = seq,
                        Weapon = _weapon.NetIndex,
                        Pellet = (byte)pellet,
                        Pierce = (byte)k,
                        Slot = (ushort)slot,
                        Generation = _targets.GenerationOf(slot),
                        HitX = _targets.X[slot],
                        HitZ = _targets.Z[slot],
                    };
                    _sink.Submit(claim);
                    ClaimsSent++;
                    float damage = DamageResolver.Resolve(_weapon, seed, pellet, out bool crit);
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

                float2 muzzle = origin + baseDir * MuzzleForward;
                float2 tip = origin + dir * math.max(end, MuzzleForward);
                _shots?.Publish(new ShotFired
                {
                    Shooter = (byte)me, OriginX = muzzle.x, OriginZ = muzzle.y, EndX = tip.x, EndZ = tip.y,
                    FirstPellet = pellet == 0, Local = true,
                });
            }
        }

        void TrackDamage(int slot, byte generation, float damage)
        {
            if (_dealtGeneration[slot] != generation)
            {
                _dealtGeneration[slot] = generation;
                _dealt[slot] = 0f;
            }
            _dealt[slot] += damage;
            if (_dealt[slot] < _zombieHealth || _presumedDead[slot]) return;
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
    }
}
