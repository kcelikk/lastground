using LastGround.Core.Random;
using LastGround.Core.Tick;
using LastGround.Gameplay.Players;
using Unity.Mathematics;

namespace LastGround.Gameplay.Zombies
{
    /// <summary>
    /// M3 stand-in for the Horde Director (M5): keeps a fixed population alive, spawning 22–45 m from a random
    /// player on walkable cells (TDD_01 §9.7), recycles zombies that drift beyond 70 m from everyone, and optionally
    /// kills a few per second so corpse/blood/death replication are exercised.
    /// </summary>
    public sealed class TestHordeSpawner : ITickable
    {
        const float MinSpawnDistance = 22f;
        const float MaxSpawnDistance = 45f;
        const float RecycleDistance = 70f;
        const int SpawnsPerTick = 12;

        readonly ZombieWorld _world;
        readonly PlayerStateTable _players;
        DeterministicRandom _rng;
        float _recycleTimer;
        float _killAccumulator;

        public TestHordeSpawner(ZombieWorld world, PlayerStateTable players, uint seed)
        {
            _world = world;
            _players = players;
            _rng = DeterministicRandom.ForStream(seed, "test-spawner");
        }

        public int Population { get; set; } = 300;
        public float KillsPerSecond { get; set; } = 1f;

        public void Tick(float dt, uint tick)
        {
            // Deaths first so the same tick refills the population.
            _killAccumulator += KillsPerSecond * dt;
            while (_killAccumulator >= 1f)
            {
                _killAccumulator -= 1f;
                KillNearest();
            }

            _recycleTimer -= dt;
            if (_recycleTimer <= 0f)
            {
                _recycleTimer = 1f;
                Recycle();
            }

            for (int n = 0; n < SpawnsPerTick && _world.Crowd.ActiveCount < Population; n++)
            {
                if (!TryFindSpawn(out float2 position)) break;
                _world.Spawn(position, _rng.Range(0f, 360f));
            }
        }

        bool TryFindSpawn(out float2 position)
        {
            position = float2.zero;
            int player = RandomActivePlayer();
            if (player < 0) return false;
            var centre = new float2(_players.X[player], _players.Z[player]);
            for (int attempt = 0; attempt < 16; attempt++)
            {
                float angle = _rng.Range(-math.PI, math.PI);
                float distance = _rng.Range(MinSpawnDistance, MaxSpawnDistance);
                float2 candidate = centre + new float2(math.cos(angle), math.sin(angle)) * distance;
                if (!_world.Nav.IsWalkable(candidate) || NearAnyPlayer(candidate, MinSpawnDistance)) continue;
                position = candidate;
                return true;
            }
            return false;
        }

        void Recycle()
        {
            for (int i = 0; i < _world.Crowd.Capacity; i++)
            {
                if (!_world.IsAlive(i) || NearAnyPlayer(_world.PositionOf(i), RecycleDistance)) continue;
                if (TryFindSpawn(out float2 position)) _world.Teleport(i, position);
            }
        }

        /// <summary>Test deaths close to players (where the camera sees corpses), standing in for combat until M4.</summary>
        void KillNearest()
        {
            int player = RandomActivePlayer();
            if (player < 0) return;
            var centre = new float2(_players.X[player], _players.Z[player]);
            int best = -1;
            float bestDistance = 12f * 12f;
            int start = _rng.Range(0, _world.Crowd.Capacity);
            for (int n = 0; n < _world.Crowd.Capacity; n++)
            {
                int i = (start + n) % _world.Crowd.Capacity;
                if (!_world.IsAlive(i)) continue;
                float d = math.distancesq(_world.PositionOf(i), centre);
                if (d < bestDistance)
                {
                    best = i;
                    break;
                }
            }
            if (best >= 0) _world.Remove(best, true);
        }

        bool NearAnyPlayer(float2 position, float distance)
        {
            for (int p = 0; p < PlayerStateTable.Max; p++)
            {
                if (_players.Active[p] && math.distance(position, new float2(_players.X[p], _players.Z[p])) < distance) return true;
            }
            return false;
        }

        int RandomActivePlayer()
        {
            int count = 0;
            for (int p = 0; p < PlayerStateTable.Max; p++) if (_players.Active[p]) count++;
            if (count == 0) return -1;
            int pick = _rng.Range(0, count);
            for (int p = 0; p < PlayerStateTable.Max; p++)
            {
                if (!_players.Active[p]) continue;
                if (pick-- == 0) return p;
            }
            return -1;
        }
    }
}
