using System.Collections.Generic;
using LastGround.Core.Ids;
using LastGround.Data.Director;
using LastGround.Data.Players;
using LastGround.Data.Presentation;
using LastGround.Data.Zombies;
using LastGround.Gameplay.Crowd;
using LastGround.Gameplay.Director;
using LastGround.Gameplay.Navigation;
using LastGround.Gameplay.Players;
using LastGround.Gameplay.Zombies;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace LastGround.Tests
{
    /// <summary>M5 horde director: threat curve, camera footprint, spawn rules, and whole simulated runs.</summary>
    public class DirectorTests
    {
        const float Dt = 1f / 30f;
        readonly List<Object> _assets = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object a in _assets) Object.DestroyImmediate(a);
            _assets.Clear();
        }

        T Asset<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            _assets.Add(asset);
            return asset;
        }

        [Test]
        public void ThreatCurve_ClimbsAtTheListedMinutes_ThenEverySevenMinutes()
        {
            var curve = Asset<ThreatCurveDefinition>();
            Assert.AreEqual(1, curve.LevelAt(0f));
            Assert.AreEqual(1, curve.LevelAt(179f));
            Assert.AreEqual(2, curve.LevelAt(180f));
            Assert.AreEqual(3, curve.LevelAt(7 * 60f));
            Assert.AreEqual(5, curve.LevelAt(15 * 60f));
            Assert.AreEqual(6, curve.LevelAt(22 * 60f));
            Assert.AreEqual(7, curve.LevelAt(29 * 60f));
        }

        [Test]
        public void CameraFootprint_MatchesTheTopDownView()
        {
            var footprint = new CameraFootprint(Asset<CameraProfile>(), 0f);
            Assert.AreEqual(-6.9f, footprint.NearZ, 0.2f, "pitch 55°, FOV 35°, 22 m: ~7 m behind the player");
            Assert.AreEqual(10.9f, footprint.FarZ, 0.2f);
            Assert.IsTrue(footprint.Contains(float2.zero, new float2(10f, 5f)));
            Assert.IsFalse(footprint.Contains(float2.zero, new float2(0f, 15f)));
            Assert.IsFalse(footprint.Contains(float2.zero, new float2(22f, 0f)), "spawn distance is always off screen");
        }

        sealed class SimRun : System.IDisposable
        {
            public readonly CrowdState Crowd = new CrowdState(512);
            public readonly PlayerStateTable Players = new PlayerStateTable { Local = new PlayerId(0) };
            public readonly NavGrid Nav = NavGrid.Open(120);
            public readonly RunStatus Status = new RunStatus();
            public readonly ZombieWorld World;
            public readonly HordeDirector Director;
            public readonly PlayerHealthSystem Health;
            public readonly List<float> Intensity = new List<float>();
            public readonly HashSet<DirectorState> StatesSeen = new HashSet<DirectorState>();
            public int OnScreenSpawns;
            public int CloseSpawns;
            public int MaxAlive;
            readonly bool[] _wasAlive = new bool[512];
            readonly CameraFootprint _footprint;
            uint _tick;
            float _killTimer;

            public SimRun(uint seed, DirectorProfile profile, ThreatCurveDefinition threat, PlayerCountScalingProfile scaling,
                ZombieDefinition walker, PlayerDefinition player, CameraProfile camera)
            {
                Players.SetLocal(0f, 0f, 0f, 0f, 0f);
                World = new ZombieWorld(Crowd, Players, Nav, new ZombieTuning(), walker, seed);
                Health = new PlayerHealthSystem(Players, player);
                World.DamageSink = Health;
                World.Respawns = Health.Respawns;
                _footprint = new CameraFootprint(camera, 0f);
                Director = new HordeDirector(World, Players, profile, threat, scaling, new CameraFootprint(camera, 3f),
                    player.MaxHealth, Status, seed);
            }

            /// <summary>A bot that stands in the middle and kills the nearest zombie within 8 m ~3 times a second.</summary>
            public void Run(float seconds)
            {
                for (int t = 0; t < seconds * 30f; t++)
                {
                    Director.Tick(Dt, _tick);
                    CheckNewSpawns();
                    World.Tick(Dt, _tick);
                    Health.Tick(Dt, _tick);
                    BotShoots();
                    StatesSeen.Add(Status.State);
                    MaxAlive = math.max(MaxAlive, Crowd.ActiveCount);
                    if (_tick % 30 == 0) Intensity.Add(Status.Intensity);
                    _tick++;
                }
            }

            void CheckNewSpawns()
            {
                for (int i = 0; i < Crowd.Capacity; i++)
                {
                    bool alive = Crowd.AliveSlots[i];
                    if (alive && !_wasAlive[i])
                    {
                        var p = new float2(Crowd.PosX[i], Crowd.PosZ[i]);
                        if (_footprint.Contains(float2.zero, p)) OnScreenSpawns++;
                        if (math.length(p) < 21.9f) CloseSpawns++;
                    }
                    _wasAlive[i] = alive;
                }
            }

            void BotShoots()
            {
                if (!Players.CanAct(0)) return;
                _killTimer += Dt;
                if (_killTimer < 0.33f) return;
                _killTimer = 0f;
                int best = -1;
                float bestDistance = 64f;
                for (int i = 0; i < Crowd.Capacity; i++)
                {
                    if (!Crowd.AliveSlots[i]) continue;
                    float d = Crowd.PosX[i] * Crowd.PosX[i] + Crowd.PosZ[i] * Crowd.PosZ[i];
                    if (d < bestDistance)
                    {
                        bestDistance = d;
                        best = i;
                    }
                }
                if (best >= 0) World.ApplyDamage(best, 100f, new float2(0f, 1f), 0f, false, true);
            }

            public void Dispose()
            {
                World.Dispose();
                Nav.Dispose();
            }
        }

        SimRun Sim(uint seed)
        {
            // The bot always gets back up (plenty of adrenaline) so a whole run can be observed.
            var player = Asset<PlayerDefinition>();
            player.SoloAdrenaline = 1000;
            return new SimRun(seed, Asset<DirectorProfile>(), Asset<ThreatCurveDefinition>(), Asset<PlayerCountScalingProfile>(),
                Asset<ZombieDefinition>(), player, Asset<CameraProfile>());
        }

        [Test]
        public void SimulatedRun_BreathesThroughTheTensionCycle_AndNeverSpawnsOnScreen()
        {
            using (SimRun run = Sim(11u))
            {
                run.Run(360f);
                Debug.Log($"[Test] director 6 min: spawned {run.Director.Spawned}, patterns {run.Director.Patterns}, max alive {run.MaxAlive}, " +
                          $"threat {run.Status.Threat}, downs {run.Health.Downs}, states {string.Join(",", run.StatesSeen)}");
                Assert.IsTrue(run.StatesSeen.Contains(DirectorState.BuildUp));
                Assert.IsTrue(run.StatesSeen.Contains(DirectorState.Peak));
                Assert.IsTrue(run.StatesSeen.Contains(DirectorState.PeakHold));
                Assert.IsTrue(run.StatesSeen.Contains(DirectorState.Relax), "breathers happen (no endless pressure)");
                Assert.AreEqual(2, run.Status.Threat, "Threat II after 3 minutes");
                Assert.AreEqual(0, run.OnScreenSpawns, "nobody sees a zombie pop in (TDD_01 §9.7)");
                Assert.AreEqual(0, run.CloseSpawns);
                Assert.Greater(run.Director.Spawned, 150);
                Assert.LessOrEqual(run.MaxAlive, 300);
            }
        }

        [Test]
        public void ThreeRuns_HaveDifferentIntensityCurves()
        {
            var curves = new List<List<float>>();
            var peaks = new List<List<float>>();
            var spawned = new List<int>();
            foreach (uint seed in new[] { 1u, 2u, 3u })
            {
                using (SimRun run = Sim(seed))
                {
                    run.Run(300f);
                    curves.Add(new List<float>(run.Intensity));
                    peaks.Add(new List<float>(run.Director.PeakTimes));
                    spawned.Add(run.Director.Spawned);
                    Debug.Log($"[Test] run {seed}: peaks at {string.Join(", ", run.Director.PeakTimes.ConvertAll(t => t.ToString("0")))} s, spawned {run.Director.Spawned}");
                }
            }
            for (int a = 0; a < curves.Count; a++)
            {
                for (int b = a + 1; b < curves.Count; b++)
                {
                    int n = math.min(curves[a].Count, curves[b].Count);
                    float diff = 0f;
                    for (int i = 0; i < n; i++) diff += math.abs(curves[a][i] - curves[b][i]);
                    diff /= n;
                    Debug.Log($"[Test] intensity curves {a + 1} vs {b + 1}: mean |Δ| {diff:0.000}");
                    Assert.Greater(diff, 0.01f, "runs are not copies of each other");
                    // The shape differs, not just the noise: the tension peaks land at different times.
                    int k = math.min(3, math.min(peaks[a].Count, peaks[b].Count));
                    float shift = 0f;
                    for (int i = 0; i < k; i++) shift = math.max(shift, math.abs(peaks[a][i] - peaks[b][i]));
                    Assert.Greater(shift, 10f, "TDD_03 §36 M5: tension peaks of runs " + (a + 1) + " and " + (b + 1) + " land at different times");
                }
            }
            spawned.Sort();
            Assert.Greater(spawned[2], spawned[0] * 1.05f, "the run personality changes how much horde each run throws");
        }

        [Test]
        public void Governor_ScalesDownWhenTheHostIsSlow_AndRecovers()
        {
            var governor = new PerformanceGovernor(1f / 60f);
            for (int i = 0; i < 600; i++) governor.Report(1f / 40f);
            Assert.Less(governor.Multiplier, 0.8f);
            Assert.GreaterOrEqual(governor.Multiplier, 0.5f);
            for (int i = 0; i < 6000; i++) governor.Report(1f / 60f);
            Assert.AreEqual(1f, governor.Multiplier, 1e-4f);
        }
    }
}
