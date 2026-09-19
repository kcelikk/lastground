using System.Collections.Generic;
using LastGround.Core.Ids;
using LastGround.Data.Combat;
using LastGround.Data.Zombies;
using LastGround.Gameplay.Crowd;
using LastGround.Gameplay.Navigation;
using LastGround.Gameplay.Players;
using LastGround.Gameplay.Zombies;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace LastGround.Tests
{
    /// <summary>M6 zombie types, status effects and elites on the host simulation.</summary>
    public class ZombieTypeTests
    {
        const float Dt = 1f / 30f;
        const byte Walker = 0, Runner = 1, Tank = 2, Spitter = 3, Exploder = 4;

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

        internal sealed class Recorder : IPlayerDamageSink, IProjectileLauncher, IExplosionSink, IKillCreditSink
        {
            public float Damage;
            public int Hits;
            public float SlowMultiplier = 1f;
            public int Spits;
            public readonly List<ExplosionKind> Blasts = new List<ExplosionKind>();
            public readonly List<int> Kills = new List<int>();

            void IPlayerDamageSink.Damage(int player, float amount)
            {
                Damage += amount;
                Hits++;
            }

            void IPlayerDamageSink.Slow(int player, float multiplier, float seconds) => SlowMultiplier = multiplier;
            public void Launch(ProjectileDefinition projectile, float2 origin, float2 target, int ownerPlayer) => Spits++;
            public void Explode(float2 center, in ExplosionSpec spec, ExplosionKind kind, int sourcePlayer) => Blasts.Add(kind);
            public void OnKill(int player) => Kills.Add(player);
        }

        sealed class Rig : System.IDisposable
        {
            public readonly CrowdState Crowd = new CrowdState(64);
            public readonly PlayerStateTable Players = new PlayerStateTable { Local = new PlayerId(0) };
            public readonly ZombieWorld World;
            public readonly Recorder Rec = new Recorder();
            uint _tick;

            public Rig(NavGrid nav, ZombieDefinition[] types, EliteModifierDefinition[] elites = null)
            {
                Players.SetLocal(0f, 0f, 0f, 0f, 0f);
                World = new ZombieWorld(Crowd, Players, nav, new ZombieTuning(), types, 9u)
                {
                    DamageSink = Rec, ProjectileLauncher = Rec, ExplosionSink = Rec, KillSink = Rec, Elites = elites,
                };
            }

            public void Run(float seconds)
            {
                for (int t = 0; t < seconds * 30f; t++) World.Tick(Dt, _tick++);
            }

            public float DistanceToPlayer(int slot) => math.length(World.PositionOf(slot));

            public void Dispose() => World.Dispose();
        }

        T Asset<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            _assets.Add(asset);
            return asset;
        }

        NavGrid OpenGrid()
        {
            var cells = new byte[60 * 60];
            for (int i = 0; i < cells.Length; i++) cells[i] = 1;
            var nav = new NavGrid(60, 60, 1f, new float2(-30f, -30f), cells);
            _disposables.Add(nav);
            return nav;
        }

        ZombieDefinition[] Types()
        {
            var walker = Asset<ZombieDefinition>();
            var runner = Asset<ZombieDefinition>();
            runner.TypeIndex = Runner;
            runner.Behaviour = ZombieBehaviour.Runner;
            runner.MinSpeed = runner.MaxSpeed = 3.5f;
            var tank = Asset<ZombieDefinition>();
            tank.TypeIndex = Tank;
            tank.Behaviour = ZombieBehaviour.Tank;
            tank.MaxHealth = 600f;
            tank.KnockbackScale = 0f;
            tank.Mass = 6f;
            tank.Radius = 0.8f;
            var spitter = Asset<ZombieDefinition>();
            spitter.TypeIndex = Spitter;
            spitter.Behaviour = ZombieBehaviour.Spitter;
            spitter.Projectile = Asset<ProjectileDefinition>();
            var exploder = Asset<ZombieDefinition>();
            exploder.TypeIndex = Exploder;
            exploder.Behaviour = ZombieBehaviour.Exploder;
            exploder.Explosion = new ExplosionSpec { Radius = 3.5f, Damage = 60f, PlayerDamageScale = 0.5f };
            return new[] { walker, runner, tank, spitter, exploder };
        }

        [Test]
        public void Runner_LungesFromFourToSixMetres()
        {
            using (var rig = new Rig(OpenGrid(), Types()))
            {
                int slot = rig.World.Spawn(new float2(0f, 8f), 180f, Runner);
                rig.Run(3f);
                Assert.GreaterOrEqual(rig.World.Lunges, 1);
                Assert.Less(rig.DistanceToPlayer(slot), 2.5f, "the lunge closes the gap");
            }
        }

        [Test]
        public void Tank_IgnoresKnockback_AndShovesWalkers()
        {
            using (var rig = new Rig(OpenGrid(), Types()))
            {
                rig.Players.Remove(new PlayerId(0)); // no target: the tank stands still
                int tank = rig.World.Spawn(new float2(0f, 0f), 0f, Tank);
                rig.World.ApplyDamage(tank, 10f, new float2(1f, 0f), 20f, false, false);
                rig.Run(0.2f);
                Assert.Less(math.abs(rig.World.PositionOf(tank).x), 0.3f, "no knockback on a Tank");

            }
            using (var rig = new Rig(OpenGrid(), Types()))
            {
                // Walking side by side towards a player in the east, overlapping along z: separation pushes the
                // walker aside much further than the tank.
                rig.Players.SetLocal(25f, 0f, 0f, 0f, 0f);
                int walker = rig.World.Spawn(new float2(0f, 0f), 90f, Walker);
                int tank = rig.World.Spawn(new float2(0f, 0.6f), 90f, Tank);
                rig.Run(0.5f);
                float walkerAside = math.abs(rig.World.PositionOf(walker).y - 0f);
                float tankAside = math.abs(rig.World.PositionOf(tank).y - 0.6f);
                Assert.Greater(walkerAside, 0.1f);
                Assert.Greater(walkerAside, tankAside * 2f);
            }
        }

        [Test]
        public void Spitter_HoldsItsBand_AndSpits()
        {
            using (var rig = new Rig(OpenGrid(), Types()))
            {
                int slot = rig.World.Spawn(new float2(0f, 20f), 180f, Spitter);
                rig.Run(15f);
                float d = rig.DistanceToPlayer(slot);
                Assert.That(d, Is.InRange(6f, 13f), "keeps 8–12 m");
                Assert.GreaterOrEqual(rig.Rec.Spits, 2);
                Assert.AreEqual(0, rig.Rec.Hits, "never came close enough to claw");
            }
        }

        [Test]
        public void Exploder_LightsItsFuse_AndBlowsUp()
        {
            using (var rig = new Rig(OpenGrid(), Types()))
            {
                int slot = rig.World.Spawn(new float2(0f, 5f), 180f, Exploder);
                bool primed = false;
                for (int i = 0; i < 90 && rig.World.IsAlive(slot); i++)
                {
                    rig.Run(Dt);
                    primed |= (rig.Crowd.Flags[slot] & CrowdFlags.Priming) != 0;
                }
                Assert.IsTrue(primed, "blinking telegraph before the blast");
                Assert.IsFalse(rig.World.IsAlive(slot));
                Assert.AreEqual(1, rig.World.Detonations);
                CollectionAssert.AreEqual(new[] { ExplosionKind.Exploder }, rig.Rec.Blasts);
                Assert.AreEqual(0, rig.Rec.Hits, "Exploders never claw; the blast does the damage");
            }
        }

        [Test]
        public void Exploder_ShotDead_StillExplodes_AndCreditsTheShooter()
        {
            using (var rig = new Rig(OpenGrid(), Types()))
            {
                int slot = rig.World.Spawn(new float2(0f, 15f), 180f, Exploder);
                Assert.IsTrue(rig.World.ApplyDamage(slot, 100f, new float2(0f, 1f), 0f, false, true, 2));
                CollectionAssert.AreEqual(new[] { ExplosionKind.Exploder }, rig.Rec.Blasts);
                CollectionAssert.AreEqual(new[] { 2 }, rig.Rec.Kills);
            }
        }

        [Test]
        public void Burn_DealsDamageOverTime_AndCreditsTheKill()
        {
            using (var rig = new Rig(OpenGrid(), Types()))
            {
                rig.Players.SetLocal(25f, 25f, 0f, 0f, 0f);
                int slot = rig.World.Spawn(new float2(0f, 0f), 0f, Walker); // 45 HP
                rig.World.ApplyBurn(slot, 10f, 3f, 1);
                Assert.IsTrue(rig.World.IsBurning(slot));
                Assert.AreNotEqual(0, rig.Crowd.Flags[slot] & CrowdFlags.Burning);
                rig.Run(3.1f);
                Assert.AreEqual(15f, rig.World.HealthOf(slot), 1.5f, "10 dps × 3 s");
                Assert.IsFalse(rig.World.IsBurning(slot));
                Assert.AreEqual(0, rig.Crowd.Flags[slot] & CrowdFlags.Burning);

                rig.World.ApplyBurn(slot, 20f, 2f, 3);
                rig.Run(1.5f);
                Assert.IsFalse(rig.World.IsAlive(slot));
                CollectionAssert.AreEqual(new[] { 3 }, rig.Rec.Kills);
            }
        }

        [Test]
        public void Stun_StopsAttacks_AndSlowReducesSpeed()
        {
            using (var rig = new Rig(OpenGrid(), Types()))
            {
                int slot = rig.World.Spawn(new float2(1.3f, 0f), 270f, Walker);
                rig.World.ApplyStun(slot, 1f);
                Assert.AreNotEqual(0, rig.Crowd.Flags[slot] & CrowdFlags.Stunned);
                rig.Run(0.9f);
                Assert.AreEqual(0, rig.Rec.Hits, "stunned zombies do not attack");
                rig.Run(1f);
                Assert.AreEqual(1, rig.Rec.Hits, "and resume afterwards");
                Assert.AreEqual(0, rig.Crowd.Flags[slot] & CrowdFlags.Stunned);
            }
            using (var rig = new Rig(OpenGrid(), Types()))
            {
                int free = rig.World.Spawn(new float2(-3f, 25f), 180f, Walker);
                int slowed = rig.World.Spawn(new float2(3f, 25f), 180f, Walker);
                rig.World.ApplySlow(slowed, 0.5f, 10f);
                float2 a = rig.World.PositionOf(free), b = rig.World.PositionOf(slowed);
                rig.Run(3f);
                float freeMoved = math.distance(a, rig.World.PositionOf(free));
                float slowMoved = math.distance(b, rig.World.PositionOf(slowed));
                Assert.Less(slowMoved, freeMoved * 0.8f);
            }
        }

        [Test]
        public void Elites_MultiplyHealth_ArmorReducesDamage_VolatileExplodes()
        {
            var armored = Asset<EliteModifierDefinition>();
            armored.NetIndex = 1;
            armored.HealthMultiplier = 4f;
            armored.DamageTakenMultiplier = 0.5f;
            armored.KnockbackImmune = true;
            var volatileElite = Asset<EliteModifierDefinition>();
            volatileElite.NetIndex = 2;
            volatileElite.HealthMultiplier = 1f;
            volatileElite.DeathExplosion = new ExplosionSpec { Radius = 4f, Damage = 40f };
            using (var rig = new Rig(OpenGrid(), Types(), new[] { armored, volatileElite }))
            {
                int slot = rig.World.Spawn(new float2(0f, 20f), 0f, Walker, 1);
                Assert.AreEqual(180f, rig.World.HealthOf(slot), 1e-3f);
                Assert.AreEqual(1, rig.Crowd.Elite[slot]);
                Assert.AreNotEqual(0, rig.Crowd.Flags[slot] & CrowdFlags.Elite);
                rig.World.ApplyDamage(slot, 20f, new float2(0f, 1f), 0f, false, false);
                Assert.AreEqual(170f, rig.World.HealthOf(slot), 1e-3f);

                int boom = rig.World.Spawn(new float2(5f, 20f), 0f, Walker, 2);
                rig.World.ApplyDamage(boom, 100f, new float2(0f, 1f), 0f, false, false);
                CollectionAssert.AreEqual(new[] { ExplosionKind.Volatile }, rig.Rec.Blasts);

                int plain = rig.World.Spawn(new float2(-5f, 20f), 0f, Walker, 9);
                Assert.AreEqual(0, rig.Crowd.Elite[plain], "unknown modifier ids spawn ordinary zombies");
            }
        }
    }
}
