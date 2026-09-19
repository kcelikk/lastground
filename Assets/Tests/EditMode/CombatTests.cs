using System.Collections.Generic;
using LastGround.Core.Ids;
using LastGround.Core.Input;
using LastGround.Data.Players;
using LastGround.Data.Weapons;
using LastGround.Data.Zombies;
using LastGround.Gameplay.Combat;
using LastGround.Gameplay.Crowd;
using LastGround.Gameplay.Navigation;
using LastGround.Gameplay.Players;
using LastGround.Gameplay.Zombies;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace LastGround.Tests
{
    /// <summary>M4 combat rules: shot RNG, hit query, claim validation, zombie damage and attacks, player health, weapon.</summary>
    public class CombatTests
    {
        const float Dt = 1f / 30f;

        readonly List<Object> _assets = new List<Object>();
        readonly List<System.IDisposable> _disposables = new List<System.IDisposable>();

        [TearDown]
        public void TearDown()
        {
            foreach (var d in _disposables) d.Dispose();
            _disposables.Clear();
            foreach (var a in _assets) Object.DestroyImmediate(a);
            _assets.Clear();
        }

        internal sealed class ScriptedInput : IPlayerInputSource
        {
            public PlayerInputFrame Frame;
            public PlayerInputFrame Current => Frame;

            public void Aim(float x, float y, bool fire)
            {
                Frame.AimX = x;
                Frame.AimY = y;
                Frame.AimActive = true;
                Frame.FireHeld = fire;
            }
        }

        internal sealed class RecordingSink : IHitClaimSink, IPlayerDamageSink
        {
            public readonly List<HitClaim> Claims = new List<HitClaim>();
            public float Damage;
            public int Hits;
            public float SlowMultiplier = 1f;
            public void Submit(in HitClaim claim) => Claims.Add(claim);
            void IPlayerDamageSink.Damage(int player, float amount)
            {
                Damage += amount;
                Hits++;
            }

            void IPlayerDamageSink.Slow(int player, float multiplier, float seconds) => SlowMultiplier = multiplier;
        }

        T Asset<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            _assets.Add(asset);
            return asset;
        }

        NavGrid Track(NavGrid nav)
        {
            _disposables.Add(nav);
            return nav;
        }

        /// <summary>40 × 40 m grid centred on the origin with a wall across z ∈ [2, 3) when requested.</summary>
        NavGrid Grid(bool wall)
        {
            var cells = new byte[40 * 40];
            for (int i = 0; i < cells.Length; i++) cells[i] = 1;
            if (wall) for (int x = 0; x < 40; x++) cells[22 * 40 + x] = 0;
            return Track(new NavGrid(40, 40, 1f, new float2(-20f, -20f), cells));
        }

        static PlayerStateTable PlayerAtOrigin()
        {
            var players = new PlayerStateTable { Local = new PlayerId(0) };
            players.SetLocal(0f, 0f, 0f, 0f, 0f);
            return players;
        }

        [Test]
        public void ShotRng_IsDeterministic_AndStaysInsideTheSpread()
        {
            uint seed = ShotRng.Seed(7u, 1, 42);
            Assert.AreEqual(seed, ShotRng.Seed(7u, 1, 42));
            Assert.AreNotEqual(seed, ShotRng.Seed(7u, 2, 42));
            Assert.AreEqual(ShotRng.SpreadRadians(seed, 0, 3f), ShotRng.SpreadRadians(seed, 0, 3f));

            int crits = 0;
            for (ushort s = 0; s < 10000; s++)
            {
                uint shot = ShotRng.Seed(7u, 0, s);
                Assert.LessOrEqual(math.abs(ShotRng.SpreadRadians(shot, 0, 3f)), math.radians(1.5f) + 1e-6f);
                if (ShotRng.IsCrit(shot, 0, 0.08f)) crits++;
            }
            Assert.That(crits, Is.InRange(650, 950), "crit chance 8 % over 10 000 shots");
        }

        [Test]
        public void HitQuery_ReturnsNearestFirst_AndStopsAfterPenetration()
        {
            var crowd = new CrowdState(16);
            int far = crowd.Spawn(0, 0f, 8f, 0f);
            int near = crowd.Spawn(0, 0f, 3f, 0f);
            int mid = crowd.Spawn(0, 0.2f, 5f, 0f);
            crowd.Spawn(0, 2f, 4f, 0f); // beside the line
            var slots = new int[4];
            var distances = new float[4];

            int hits = HitQuery.Cast(crowd, Grid(false), float2.zero, new float2(0f, 1f), 30f, 0.5f, 2, slots, distances, out float end);
            Assert.AreEqual(2, hits);
            Assert.AreEqual(near, slots[0]);
            Assert.AreEqual(mid, slots[1]);
            Assert.AreEqual(distances[1], end, 1e-4f, "the tracer ends at the last zombie it could not pass");

            hits = HitQuery.Cast(crowd, Grid(false), float2.zero, new float2(0f, 1f), 30f, 0.5f, 4, slots, distances, out end);
            Assert.AreEqual(3, hits);
            Assert.AreEqual(far, slots[2]);
        }

        [Test]
        public void HitQuery_WallsBlockBullets()
        {
            var crowd = new CrowdState(4);
            crowd.Spawn(0, 0f, 6f, 0f);
            var slots = new int[2];
            var distances = new float[2];
            int hits = HitQuery.Cast(crowd, Grid(true), float2.zero, new float2(0f, 1f), 30f, 0.5f, 2, slots, distances, out float end);
            Assert.AreEqual(0, hits);
            Assert.AreEqual(2f, end, 0.01f);
        }

        [Test]
        public void NavGrid_Raycast_MeasuresDistanceToWalls()
        {
            NavGrid nav = Grid(true);
            Assert.AreEqual(2f, nav.Raycast(float2.zero, new float2(0f, 1f), 30f), 0.01f);
            Assert.AreEqual(10f, nav.Raycast(float2.zero, new float2(1f, 0f), 10f), 0.01f);
            Assert.AreEqual(2f * math.SQRT2, nav.Raycast(float2.zero, math.normalize(new float2(1f, 1f)), 30f), 0.02f);
            Assert.IsFalse(nav.HasLineOfSight(float2.zero, new float2(0f, 6f)));
            Assert.IsTrue(nav.HasLineOfSight(float2.zero, new float2(6f, 1.5f)));
        }

        HitClaimValidator Validator(PlayerStateTable players, CrowdState crowd, NavGrid nav, WeaponDefinition rifle)
        {
            return new HitClaimValidator(players, crowd, nav, new[] { rifle });
        }

        static HitClaim ClaimOn(CrowdState crowd, int slot, ushort shot, byte pierce = 0)
        {
            return new HitClaim
            {
                Shooter = 0, ShotSeq = shot, Pierce = pierce, Slot = (ushort)slot, Generation = crowd.Generation[slot],
                HitX = crowd.PosX[slot], HitZ = crowd.PosZ[slot],
            };
        }

        [Test]
        public void Validator_AcceptsFairHits_AndRejectsImpossibleOnes()
        {
            var rifle = Asset<WeaponDefinition>();
            var crowd = new CrowdState(16);
            PlayerStateTable players = PlayerAtOrigin();
            HitClaimValidator validator = Validator(players, crowd, Grid(false), rifle);
            int zombie = crowd.Spawn(0, 0f, 10f, 0f);

            Assert.AreEqual(HitClaimVerdict.Accepted, validator.Check(ClaimOn(crowd, zombie, 1)));

            HitClaim stale = ClaimOn(crowd, zombie, 2);
            stale.Generation++;
            Assert.AreEqual(HitClaimVerdict.TargetGone, validator.Check(stale));

            HitClaim moved = ClaimOn(crowd, zombie, 3);
            moved.HitX += 4f;
            Assert.AreEqual(HitClaimVerdict.FarFromTarget, validator.Check(moved));

            int far = crowd.Spawn(0, 0f, 35f, 0f);
            Assert.AreEqual(HitClaimVerdict.OutOfRange, validator.Check(ClaimOn(crowd, far, 4)));

            HitClaim wrongWeapon = ClaimOn(crowd, zombie, 5);
            wrongWeapon.Weapon = 9;
            Assert.AreEqual(HitClaimVerdict.UnknownWeapon, validator.Check(wrongWeapon));

            players.Life[0] = PlayerLife.Dead;
            Assert.AreEqual(HitClaimVerdict.ShooterDead, validator.Check(ClaimOn(crowd, zombie, 6)));
        }

        [Test]
        public void Validator_BlocksShotsThroughWalls()
        {
            var rifle = Asset<WeaponDefinition>();
            var crowd = new CrowdState(4);
            HitClaimValidator validator = Validator(PlayerAtOrigin(), crowd, Grid(true), rifle);
            int zombie = crowd.Spawn(0, 0f, 6f, 0f);
            Assert.AreEqual(HitClaimVerdict.NoLineOfSight, validator.Check(ClaimOn(crowd, zombie, 1)));
        }

        [Test]
        public void Validator_LimitsFireRate_AndClaimsPerShot()
        {
            var rifle = Asset<WeaponDefinition>(); // 8 shots/s, penetration 1
            var crowd = new CrowdState(8);
            HitClaimValidator validator = Validator(PlayerAtOrigin(), crowd, Grid(false), rifle);
            int zombie = crowd.Spawn(0, 0f, 10f, 0f);

            Assert.AreEqual(HitClaimVerdict.Accepted, validator.Check(ClaimOn(crowd, zombie, 1, 0)));
            Assert.AreEqual(HitClaimVerdict.Accepted, validator.Check(ClaimOn(crowd, zombie, 1, 1)));
            Assert.AreEqual(HitClaimVerdict.TooManyClaims, validator.Check(ClaimOn(crowd, zombie, 1, 2)));

            // A cheat firing 40 shots at once: only the burst allowance passes.
            int accepted = 0;
            for (ushort shot = 2; shot < 42; shot++)
                if (validator.Check(ClaimOn(crowd, zombie, shot)) == HitClaimVerdict.Accepted) accepted++;
            Assert.LessOrEqual(accepted, 6);

            // At the real rate, every shot passes.
            accepted = 0;
            for (ushort shot = 100; shot < 130; shot++)
            {
                validator.Refill(rifle.ShotInterval);
                if (validator.Check(ClaimOn(crowd, zombie, shot)) == HitClaimVerdict.Accepted) accepted++;
            }
            Assert.AreEqual(30, accepted);
        }

        sealed class WorldRig : System.IDisposable
        {
            public readonly CrowdState Crowd = new CrowdState(64);
            public readonly PlayerStateTable Players = PlayerAtOrigin();
            public readonly ZombieWorld World;
            public readonly RecordingSink Sink = new RecordingSink();
            uint _tick;

            public WorldRig(NavGrid nav, ZombieDefinition walker)
            {
                World = new ZombieWorld(Crowd, Players, nav, new ZombieTuning(), walker, 5u) { DamageSink = Sink };
            }

            public void Run(float seconds)
            {
                for (int t = 0; t < seconds * 30f; t++) World.Tick(Dt, _tick++);
            }

            public void Dispose() => World.Dispose();
        }

        [Test]
        public void Zombie_DiesAfterEnoughDamage()
        {
            var walker = Asset<ZombieDefinition>(); // 45 HP
            using (var rig = new WorldRig(Grid(false), walker))
            {
                int slot = rig.World.Spawn(new float2(0f, 10f), 0f);
                var deaths = rig.Crowd.Deaths.CreateReader();
                var hits = rig.Crowd.Hits.CreateReader();
                Assert.IsFalse(rig.World.ApplyDamage(slot, 16f, new float2(0f, 1f), 1f, false, true));
                Assert.IsFalse(rig.World.ApplyDamage(slot, 16f, new float2(0f, 1f), 1f, false, true));
                Assert.AreEqual(13f, rig.World.HealthOf(slot), 1e-4f);
                Assert.AreNotEqual(0, rig.Crowd.Flags[slot] & CrowdFlags.Hit, "hit flag for clients");
                Assert.IsTrue(rig.World.ApplyDamage(slot, 16f, new float2(0f, 1f), 1f, false, true));
                Assert.IsFalse(rig.World.IsAlive(slot));

                int hitCount = 0;
                while (rig.Crowd.Hits.TryRead(ref hits, out _)) hitCount++;
                Assert.AreEqual(3, hitCount);
                Assert.IsTrue(rig.Crowd.Deaths.TryRead(ref deaths, out CrowdDeath death));
                Assert.AreEqual(slot, death.Slot);
                Assert.IsFalse(rig.World.ApplyDamage(slot, 16f, float2.zero, 0f, false, true), "dead zombies take no damage");
            }
        }

        [Test]
        public void Zombie_AttackLandsAfterWindup()
        {
            var walker = Asset<ZombieDefinition>();
            using (var rig = new WorldRig(Grid(false), walker))
            {
                rig.World.Spawn(new float2(1.3f, 0f), 270f);
                rig.Run(0.3f);
                Assert.AreEqual(0, rig.Sink.Hits, "still winding up");
                rig.Run(0.3f);
                Assert.AreEqual(1, rig.Sink.Hits);
                Assert.AreEqual(walker.AttackDamage, rig.Sink.Damage, 1e-4f);
                rig.Run(walker.AttackCooldown + walker.AttackWindup + 0.3f);
                Assert.AreEqual(2, rig.Sink.Hits, "attacks repeat after the cooldown");
            }
        }

        [Test]
        public void Zombie_AttackCanBeDodged()
        {
            var walker = Asset<ZombieDefinition>();
            using (var rig = new WorldRig(Grid(false), walker))
            {
                rig.World.Spawn(new float2(1.3f, 0f), 270f);
                rig.Run(0.2f);
                rig.Players.SetLocal(-6f, 0f, 0f, 0f, 0f); // stepped away during the windup
                rig.Run(0.4f);
                Assert.AreEqual(0, rig.Sink.Hits);
                Assert.AreEqual(1, rig.World.AttacksDodged);
            }
        }

        WeaponController Weapon(PlayerStateTable players, ScriptedInput input, WeaponDefinition rifle, ICrowdRenderSource crowd,
            IHitClaimSink sink, CrowdReplica predictions = null)
        {
            return new WeaponController(players, input, rifle, crowd, Grid(false), 11u, sink, null, predictions);
        }

        [Test]
        public void Weapon_FiresAtItsRate_AndReloadsWhenEmpty()
        {
            var rifle = Asset<WeaponDefinition>(); // 8/s, 30 rounds, 1.8 s reload
            var crowd = new CrowdState(8);
            crowd.Spawn(0, 0f, 10f, 0f);
            PlayerStateTable players = PlayerAtOrigin();
            var input = new ScriptedInput();
            var sink = new RecordingSink();
            WeaponController weapon = Weapon(players, input, rifle, crowd, sink);

            input.Aim(0f, 1f, true);
            for (int f = 0; f < 60; f++) weapon.Tick(1f / 60f, 0);
            Assert.That(weapon.ShotsFired, Is.InRange(8, 9), "one second at 8 shots/s");
            Assert.IsTrue(players.Firing[0]);
            Assert.GreaterOrEqual(sink.Claims.Count, weapon.ShotsFired - 1, "every shot at a zombie in front produces a claim");
            Assert.AreEqual(weapon.ShotsFired, sink.Claims[sink.Claims.Count - 1].ShotSeq);

            for (int f = 0; f < 240; f++) weapon.Tick(1f / 60f, 0);
            Assert.AreEqual(rifle.MagazineSize, weapon.ShotsFired, "stops at an empty magazine");
            Assert.IsTrue(weapon.Reloading);

            input.Aim(0f, 1f, false);
            for (int f = 0; f < 90; f++) weapon.Tick(1f / 60f, 0);
            Assert.IsFalse(weapon.Reloading);
            Assert.AreEqual(rifle.MagazineSize, weapon.Ammo);
            Assert.IsFalse(players.Firing[0]);
        }

        [Test]
        public void Weapon_DoesNotBurstAfterReloadingWithTheTriggerHeld()
        {
            var rifle = Asset<WeaponDefinition>();
            PlayerStateTable players = PlayerAtOrigin();
            var input = new ScriptedInput();
            WeaponController weapon = Weapon(players, input, rifle, new CrowdState(4), new RecordingSink());

            input.Aim(0f, 1f, true);
            // Empty the magazine and reload, trigger held throughout (auto fire does exactly this).
            for (int f = 0; f < 60 * 6; f++) weapon.Tick(1f / 60f, 0);
            int afterFiveSeconds = weapon.ShotsFired;
            Assert.Greater(afterFiveSeconds, rifle.MagazineSize, "the second magazine has started");

            // Over any 0.5 s window the rate stays at the weapon's 8 shots/s (≤ 5 shots), never a volley.
            int max = 0;
            for (int window = 0; window < 8; window++)
            {
                int before = weapon.ShotsFired;
                for (int f = 0; f < 30; f++) weapon.Tick(1f / 60f, 0);
                max = System.Math.Max(max, weapon.ShotsFired - before);
            }
            Assert.LessOrEqual(max, 5);

            // And the first frame after a reload fires one bullet, not the reload time's worth.
            var fresh = Weapon(players, input, rifle, new CrowdState(4), new RecordingSink());
            int shotsBeforeReloadEnds = 0;
            for (int f = 0; f < 60 * 10; f++)
            {
                bool wasReloading = fresh.Reloading;
                int before = fresh.ShotsFired;
                fresh.Tick(1f / 60f, 0);
                if (wasReloading && !fresh.Reloading) shotsBeforeReloadEnds = System.Math.Max(shotsBeforeReloadEnds, fresh.ShotsFired - before);
            }
            Assert.LessOrEqual(shotsBeforeReloadEnds, 1);
        }

        [Test]
        public void Weapon_OnClient_PredictsHitsImmediately()
        {
            var rifle = Asset<WeaponDefinition>();
            var replica = new CrowdReplica(8);
            replica.Enter(3, 1, 0, 0f, 6f, 0f, 0.0);
            PlayerStateTable players = PlayerAtOrigin();
            var input = new ScriptedInput();
            var sink = new RecordingSink();
            WeaponController weapon = Weapon(players, input, rifle, replica, sink, replica);
            var hits = replica.Hits.CreateReader();

            input.Aim(0f, 1f, true);
            weapon.Tick(1f / 60f, 0);
            Assert.AreEqual(1, sink.Claims.Count);
            Assert.AreEqual(3, sink.Claims[0].Slot);
            Assert.AreEqual(1, sink.Claims[0].Generation);
            Assert.IsTrue(replica.Hits.TryRead(ref hits, out CrowdHit hit));
            Assert.IsTrue(hit.Local);
            Assert.That(hit.Damage, Is.EqualTo(rifle.Damage).Or.EqualTo(rifle.Damage * rifle.CritMultiplier));

            // The host's hit flag for the same zombie shortly after is not shown a second time.
            replica.Update(3, 0f, 6f, 0f, 0.1, 1, CrowdFlags.Hit);
            Assert.IsFalse(replica.Hits.TryRead(ref hits, out _));
        }

        [Test]
        public void Weapon_ShootsPastZombiesItHasAlreadyKilled()
        {
            var rifle = Asset<WeaponDefinition>();
            rifle.Penetration = 0;
            rifle.CritChance = 0f;
            var crowd = new CrowdState(8);
            int front = crowd.Spawn(0, 0f, 8f, 0f);
            int behind = crowd.Spawn(0, 0f, 11f, 0f);
            PlayerStateTable players = PlayerAtOrigin();
            var input = new ScriptedInput();
            var sink = new RecordingSink();
            var weapon = new WeaponController(players, input, rifle, crowd, Grid(false), 11u, sink, null, null, zombieHealth: 45f);

            input.Aim(0f, 1f, true);
            for (int f = 0; f < 60 && sink.Claims.Count < 4; f++) weapon.Tick(1f / 60f, 0);
            Assert.AreEqual(4, sink.Claims.Count);
            Assert.AreEqual(front, sink.Claims[2].Slot, "three rifle hits (48) kill a 45 HP walker");
            Assert.AreEqual(behind, sink.Claims[3].Slot, "the fourth bullet passes the presumed-dead zombie");
            Assert.AreEqual(1, weapon.PresumedDead);

            // The host never confirmed the kill: after the grace period the zombie is a target again.
            input.Aim(0f, 1f, false);
            for (int f = 0; f < 60; f++) weapon.Tick(1f / 60f, 0);
            Assert.AreEqual(0, weapon.PresumedDead);
        }

        [Test]
        public void Weapon_DoesNotFireWhileDead()
        {
            var rifle = Asset<WeaponDefinition>();
            PlayerStateTable players = PlayerAtOrigin();
            players.Life[0] = PlayerLife.Dead;
            var input = new ScriptedInput();
            input.Aim(0f, 1f, true);
            WeaponController weapon = Weapon(players, input, rifle, new CrowdState(4), new RecordingSink());
            for (int f = 0; f < 30; f++) weapon.Tick(1f / 60f, 0);
            Assert.AreEqual(0, weapon.ShotsFired);
        }

        [Test]
        public void AimResolver_AutoMode_TargetsTheNearestZombie()
        {
            var rifle = Asset<WeaponDefinition>();
            var crowd = new CrowdState(8);
            crowd.Spawn(0, 3f, 4f, 0f);
            crowd.Spawn(0, -10f, 10f, 0f);
            var raw = new ScriptedInput();
            var aim = new AimResolver(raw, PlayerAtOrigin(), crowd, Grid(false), rifle) { Mode = ControlMode.AutoAimAutoFire };
            aim.Tick(Dt, 0);
            Assert.IsTrue(aim.Current.AimActive);
            Assert.IsTrue(aim.Current.FireHeld);
            Assert.AreEqual(0.6f, aim.Current.AimX, 1e-3f);
            Assert.AreEqual(0.8f, aim.Current.AimY, 1e-3f);

            aim.Mode = ControlMode.Manual;
            aim.Tick(Dt, 1);
            Assert.IsFalse(aim.Current.AimActive, "manual mode never fires on its own");
        }

        [Test]
        public void AimResolver_AssistBendsTowardsZombiesInTheCone()
        {
            var rifle = Asset<WeaponDefinition>();
            var crowd = new CrowdState(8);
            crowd.Spawn(0, 1f, 10f, 0f); // ~5.7° right of straight ahead
            var raw = new ScriptedInput();
            raw.Aim(0f, 1f, true);
            var aim = new AimResolver(raw, PlayerAtOrigin(), crowd, Grid(false), rifle) { AssistLevel = 1f };
            aim.Tick(Dt, 0);
            Assert.Greater(aim.Current.AimX, 0.03f);
            Assert.IsTrue(aim.Current.FireHeld);

            aim.AssistLevel = 0f;
            aim.Tick(Dt, 1);
            Assert.AreEqual(0f, aim.Current.AimX, 1e-5f);
        }
    }
}
