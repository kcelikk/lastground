using System.Diagnostics;
using LastGround.Gameplay.Crowd;
using LastGround.Gameplay.Navigation;
using LastGround.Gameplay.Players;
using LastGround.Gameplay.Zombies;
using LastGround.Core.Ids;
using NUnit.Framework;
using Unity.Mathematics;

namespace LastGround.Tests
{
    public class HordeSimulationTests
    {
        const float Dt = 1f / 30f;

        static byte[] Grid(int size, System.Func<int, int, bool> blocked)
        {
            var cells = new byte[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    cells[y * size + x] = blocked(x, y) ? (byte)0 : (byte)1;
            return cells;
        }

        [Test]
        public void FlowField_RoutesThroughGap_AndMarksPocketsUnreachable()
        {
            // 20x20, wall at x = 10 with a gap at y = 18; pocket at (2,2) enclosed.
            byte[] cells = Grid(20, (x, y) => (x == 10 && y != 18) || (x == 1 && y <= 3) || (y == 3 && x <= 3) || (x == 3 && y <= 3));
            using (var nav = new NavGrid(20, 20, 1f, float2.zero, cells))
            using (var flow = new FlowFieldSet(nav))
            {
                flow.SetTarget(0, new float2(15.5f, 5.5f));
                float2 dir = flow.Directions[5 * 20 + 5];
                Assert.Greater(dir.y, 0.5f, "west of the wall the path leads north to the gap");
                Assert.AreEqual(ushort.MaxValue, flow.DistanceAt(0, new float2(2.5f, 1.5f)));
                Assert.Less(flow.DistanceAt(0, new float2(5.5f, 5.5f)), ushort.MaxValue);
                int rebuilds = flow.Rebuilds;
                flow.SetTarget(0, new float2(15.9f, 5.1f)); // same cell: no rebuild
                Assert.AreEqual(rebuilds, flow.Rebuilds);
            }
        }

        sealed class Rig : System.IDisposable
        {
            public NavGrid Nav;
            public CrowdState Crowd = new CrowdState(512);
            public PlayerStateTable Players = new PlayerStateTable { Local = new PlayerId(0) };
            public ZombieWorld World;
            public TestHordeSpawner Spawner;
            public LastGround.Data.Zombies.ZombieDefinition Walker = UnityEngine.ScriptableObject.CreateInstance<LastGround.Data.Zombies.ZombieDefinition>();

            public Rig(NavGrid nav, int population, float kills = 0f)
            {
                Nav = nav;
                World = new ZombieWorld(Crowd, Players, nav, new ZombieTuning(), Walker, 3u);
                Spawner = new TestHordeSpawner(World, Players, 3u) { Population = population, KillsPerSecond = kills };
            }

            public void Run(float seconds)
            {
                for (int t = 0; t < seconds * 30f; t++)
                {
                    Spawner.Tick(Dt, (uint)t);
                    World.Tick(Dt, (uint)t);
                }
            }

            public void Dispose()
            {
                World.Dispose();
                Nav.Dispose();
                UnityEngine.Object.DestroyImmediate(Walker);
            }
        }

        [Test]
        public void ThreeHundred_SurroundThePlayer()
        {
            using (var rig = new Rig(NavGrid.Open(100), 300))
            {
                rig.Players.SetLocal(0f, 0f, 0f, 0f, 0f);
                var watch = Stopwatch.StartNew();
                rig.Run(25f);
                watch.Stop();
                UnityEngine.Debug.Log($"[Test] 300 zombies, 750 ticks: {watch.Elapsed.TotalMilliseconds / 750.0:0.000} ms/tick (editor, Burst JIT)");

                Assert.AreEqual(300, rig.Crowd.ActiveCount);
                int near = 0, attacking = 0;
                var sectors = new bool[12];
                for (int i = 0; i < rig.Crowd.Capacity; i++)
                {
                    if (!rig.Crowd.AliveSlots[i]) continue;
                    float x = rig.Crowd.PosX[i], z = rig.Crowd.PosZ[i];
                    float d = math.sqrt(x * x + z * z);
                    if (d < 8f)
                    {
                        near++;
                        int s = (int)math.floor((math.atan2(z, x) + math.PI) / (2f * math.PI / 12f));
                        sectors[math.clamp(s, 0, 11)] = true;
                    }
                    if (rig.Crowd.Anim[i] == ZombieSteeringJob.StateAttack) attacking++;
                    Assert.Greater(d, 0.6f, "no zombie stands inside the player");
                }
                int covered = 0;
                foreach (bool s in sectors) if (s) covered++;

                // Queueing, not piling: near zombies keep roughly a body width from their nearest neighbour.
                double spacing = 0;
                int measured = 0;
                for (int i = 0; i < rig.Crowd.Capacity; i++)
                {
                    if (!rig.Crowd.AliveSlots[i]) continue;
                    float xi = rig.Crowd.PosX[i], zi = rig.Crowd.PosZ[i];
                    if (xi * xi + zi * zi > 64f) continue;
                    float nearest = float.MaxValue;
                    for (int j = 0; j < rig.Crowd.Capacity; j++)
                    {
                        if (j == i || !rig.Crowd.AliveSlots[j]) continue;
                        float dx = rig.Crowd.PosX[j] - xi, dz = rig.Crowd.PosZ[j] - zi;
                        nearest = math.min(nearest, math.sqrt(dx * dx + dz * dz));
                    }
                    spacing += nearest;
                    measured++;
                }
                double averageSpacing = spacing / math.max(1, measured);
                UnityEngine.Debug.Log($"[Test] near zombies {measured}, average nearest-neighbour spacing {averageSpacing:0.00} m, unstuck {rig.World.Unstuck}");
                Assert.Greater(averageSpacing, 0.55, "zombies pile on top of each other");
                Assert.Greater(near, 120, "the horde reaches the player");
                Assert.GreaterOrEqual(covered, 11, "the ring surrounds instead of queueing");
                Assert.Greater(attacking, 5);
            }
        }

        [Test]
        public void Zombies_NeverEnterWalls()
        {
            // Arena 80 m with an L-shaped wall between the spawn ring and the player.
            byte[] cells = Grid(80, (x, y) => (x >= 30 && x <= 50 && y == 46) || (x == 50 && y >= 30 && y <= 46) || x == 0 || y == 0 || x == 79 || y == 79);
            using (var rig = new Rig(new NavGrid(80, 80, 1f, new float2(-40f, -40f), cells), 150))
            {
                rig.Players.SetLocal(0f, 0f, 0f, 0f, 0f);
                for (int step = 0; step < 20; step++)
                {
                    rig.Run(1f);
                    for (int i = 0; i < rig.Crowd.Capacity; i++)
                    {
                        if (!rig.Crowd.AliveSlots[i]) continue;
                        Assert.IsTrue(rig.Nav.IsWalkable(new float2(rig.Crowd.PosX[i], rig.Crowd.PosZ[i])), "zombie inside a wall");
                    }
                }
            }
        }

        [Test]
        public void TwoPlayers_ShareTheHorde()
        {
            using (var rig = new Rig(NavGrid.Open(120), 200))
            {
                rig.Players.SetLocal(-20f, 0f, 0f, 0f, 0f);
                rig.Players.PushRemote(new PlayerId(1), 20f, 0f, 0f, 0f, 0f, 0.0, 0.0);
                rig.Run(15f);
                int left = 0, right = 0;
                for (int i = 0; i < rig.Crowd.Capacity; i++)
                {
                    if (!rig.Crowd.AliveSlots[i]) continue;
                    float x = rig.Crowd.PosX[i];
                    if (x < -10f) left++;
                    else if (x > 10f) right++;
                }
                Assert.Greater(left, 50);
                Assert.Greater(right, 50);
            }
        }

        [Test]
        public void Kills_PublishDeaths_AndPopulationRefills()
        {
            using (var rig = new Rig(NavGrid.Open(100), 100, kills: 3f))
            {
                rig.Players.SetLocal(0f, 0f, 0f, 0f, 0f);
                var reader = rig.Crowd.Deaths.CreateReader();
                rig.Run(15f);
                int deaths = 0;
                while (rig.Crowd.Deaths.TryRead(ref reader, out _)) deaths++;
                Assert.Greater(deaths, 10);
                Assert.AreEqual(100, rig.Crowd.ActiveCount);
            }
        }
    }
}
