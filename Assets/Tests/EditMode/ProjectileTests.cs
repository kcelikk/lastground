using System.Collections.Generic;
using LastGround.Core.Ids;
using LastGround.Data.Combat;
using LastGround.Data.Zombies;
using LastGround.Gameplay.Crowd;
using LastGround.Gameplay.Navigation;
using LastGround.Gameplay.Players;
using LastGround.Gameplay.Projectiles;
using LastGround.Gameplay.Zombies;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace LastGround.Tests
{
    /// <summary>M6 projectiles and explosions (host).</summary>
    public class ProjectileTests
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

        T Asset<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            _assets.Add(asset);
            return asset;
        }

        /// <summary>40 × 40 m open grid; optional wall across z ∈ [5, 6).</summary>
        NavGrid Grid(bool wall = false)
        {
            var cells = new byte[40 * 40];
            for (int i = 0; i < cells.Length; i++) cells[i] = 1;
            if (wall) for (int x = 0; x < 40; x++) cells[25 * 40 + x] = 0;
            var nav = new NavGrid(40, 40, 1f, new float2(-20f, -20f), cells);
            _disposables.Add(nav);
            return nav;
        }

        sealed class Rig : System.IDisposable
        {
            public readonly CrowdState Crowd = new CrowdState(64);
            public readonly PlayerStateTable Players = new PlayerStateTable { Local = new PlayerId(0) };
            public readonly ZombieWorld World;
            public readonly ZombieTypeTests.Recorder Rec = new ZombieTypeTests.Recorder();
            public readonly ExplosionSystem Explosions;
            public readonly ProjectileTable Table;
            public readonly ProjectileSystem Projectiles;
            uint _tick;

            public Rig(NavGrid nav, ZombieDefinition[] types, ProjectileDefinition[] projectiles)
            {
                Players.SetLocal(0f, 0f, 0f, 0f, 0f);
                Players.Active[1] = true;
                Players.X[1] = 2f;
                World = new ZombieWorld(Crowd, Players, nav, new ZombieTuning(), types, 4u) { DamageSink = Rec, KillSink = Rec };
                Explosions = new ExplosionSystem(World, Players, Rec);
                World.ExplosionSink = Explosions;
                Table = new ProjectileTable(projectiles);
                Projectiles = new ProjectileSystem(Table, projectiles, Players, nav, Rec, Explosions);
            }

            public void Run(float seconds)
            {
                for (int t = 0; t < seconds * 30f; t++)
                {
                    World.Tick(Dt, _tick);
                    Projectiles.Tick(Dt, _tick);
                    Explosions.Tick(Dt, _tick);
                    _tick++;
                }
            }

            public void Dispose() => World.Dispose();
        }

        ProjectileDefinition Grenade()
        {
            var grenade = Asset<ProjectileDefinition>();
            grenade.NetIndex = 0;
            grenade.Explosion = new ExplosionSpec
            {
                Radius = 4.5f, Damage = 150f, EdgeDamageScale = 0.3f, Knockback = 7f, StunSeconds = 1.2f, PlayerDamageScale = 0.25f,
            };
            return grenade;
        }

        ProjectileDefinition Spit()
        {
            var spit = Asset<ProjectileDefinition>();
            spit.NetIndex = 1;
            spit.Motion = ProjectileMotion.Straight;
            spit.Speed = 11f;
            spit.MaxRange = 14f;
            spit.ImpactDamage = 8f;
            spit.SlowMultiplier = 0.6f;
            return spit;
        }

        [Test]
        public void Grenade_LandsAtTarget_KillsInRadius_HurtsOnlyTheThrower()
        {
            var walker = Asset<ZombieDefinition>();
            walker.AttackWindup = 10f; // only the blast may hurt the thrower here
            ProjectileDefinition grenade = Grenade();
            using (var rig = new Rig(Grid(), new[] { walker }, new[] { grenade, Spit() }))
            {
                rig.Players.SetLocal(0f, 6f, 0f, 0f, 0f); // thrower stands near the blast
                int near = rig.World.Spawn(new float2(0f, 8f), 0f);
                int edge = rig.World.Spawn(new float2(4f, 8f), 0f);
                int outside = rig.World.Spawn(new float2(0f, 16f), 0f);
                var blasts = rig.Explosions.Blasts.CreateReader();
                int id = rig.Projectiles.Fire(grenade, float2.zero, new float2(0f, 8f), 0);
                Assert.GreaterOrEqual(id, 0);
                Assert.AreEqual(8f, rig.Table.End[id].y, 1e-3f);
                rig.Run(grenade.FlightTime.y * 0.5f);
                Assert.IsTrue(rig.Table.Active[id], "still in the air");
                rig.Run(1f);
                Assert.IsFalse(rig.Table.Active[id]);
                Assert.IsTrue(rig.Explosions.Blasts.TryRead(ref blasts, out ExplosionFx fx));
                Assert.AreEqual(ExplosionKind.Grenade, fx.Kind);
                Assert.IsFalse(rig.World.IsAlive(near));
                Assert.IsTrue(rig.World.IsAlive(outside));
                Assert.IsTrue(!rig.World.IsAlive(edge) || rig.World.IsStunned(edge), "the edge takes less damage but is stunned");
                Assert.AreEqual(1, rig.Rec.Hits, "only the thrower is hurt (friendly fire off)");
                Assert.Less(rig.Rec.Damage, 150f * 0.25f + 0.01f);
                Assert.Contains(0, rig.Rec.Kills);
            }
        }

        [Test]
        public void Grenade_StopsShortOfWalls()
        {
            ProjectileDefinition grenade = Grenade();
            using (var rig = new Rig(Grid(wall: true), new[] { Asset<ZombieDefinition>() }, new[] { grenade }))
            {
                int id = rig.Projectiles.Fire(grenade, float2.zero, new float2(0f, 10f), 0);
                Assert.Less(rig.Table.End[id].y, 5f);
            }
        }

        [Test]
        public void Spit_HitsPlayer_AndSlows_WallsStopIt()
        {
            ProjectileDefinition spit = Spit();
            using (var rig = new Rig(Grid(wall: true), new[] { Asset<ZombieDefinition>() }, new[] { Grenade(), spit }))
            {
                var ended = rig.Table.Ended.CreateReader();
                rig.Projectiles.Fire(spit, new float2(0f, -10f), float2.zero, -1);
                rig.Run(1.2f);
                Assert.AreEqual(1, rig.Projectiles.PlayerHits);
                Assert.AreEqual(8f, rig.Rec.Damage, 1e-3f);
                Assert.AreEqual(0.6f, rig.Rec.SlowMultiplier, 1e-3f);
                Assert.IsTrue(rig.Table.Ended.TryRead(ref ended, out ProjectileEnded end));
                Assert.AreEqual(0, end.HitPlayer);

                rig.Players.SetLocal(0f, 10f, 0f, 0f, 0f); // behind the wall
                rig.Players.Active[1] = false;
                int id = rig.Projectiles.Fire(spit, float2.zero, new float2(0f, 10f), -1);
                rig.Run(1f);
                Assert.IsFalse(rig.Table.Active[id]);
                Assert.AreEqual(1, rig.Projectiles.PlayerHits, "the wall stopped it");
            }
        }

        [Test]
        public void Explosions_Chain_ThroughExploders()
        {
            var walker = Asset<ZombieDefinition>();
            var exploder = Asset<ZombieDefinition>();
            exploder.TypeIndex = 1;
            exploder.Behaviour = ZombieBehaviour.Exploder;
            exploder.Explosion = new ExplosionSpec { Radius = 3.5f, Damage = 60f, EdgeDamageScale = 1f, PlayerDamageScale = 0.5f };
            using (var rig = new Rig(Grid(), new[] { walker, exploder }, new[] { Grenade() }))
            {
                rig.Players.SetLocal(-15f, -15f, 0f, 0f, 0f);
                rig.Players.Active[1] = false;
                int a = rig.World.Spawn(new float2(0f, 10f), 0f, 1);
                int b = rig.World.Spawn(new float2(3f, 10f), 0f, 1);
                int c = rig.World.Spawn(new float2(6f, 10f), 0f, 1);
                rig.World.ApplyDamage(a, 100f, float2.zero, 0f, false, false);
                rig.Explosions.Tick(Dt, 0);
                Assert.IsFalse(rig.World.IsAlive(b));
                Assert.IsFalse(rig.World.IsAlive(c));
                Assert.AreEqual(3, rig.Explosions.Exploded);
                Assert.AreEqual(0, rig.Rec.Hits, "players far away");
            }
        }
    }
}
