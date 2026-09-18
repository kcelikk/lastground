using System;
using LastGround.Core.Random;
using LastGround.Core.Tick;
using LastGround.Gameplay.Crowd;
using LastGround.Gameplay.Navigation;
using LastGround.Gameplay.Players;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace LastGround.Gameplay.Zombies
{
    /// <summary>
    /// Host-authoritative horde simulation (TDD_01 §8): structure-of-arrays in native memory, Burst jobs for the
    /// spatial grid and steering, flow fields per player, surround slots, AI LOD and anti-stuck. Results are
    /// mirrored into <see cref="CrowdState"/>, which replication and rendering already consume.
    /// No GameObject, NavMeshAgent, Rigidbody or Collider per zombie.
    /// </summary>
    public sealed class ZombieWorld : ITickable, IDisposable
    {
        const float GridCellSize = 2f;

        readonly CrowdState _crowd;
        readonly PlayerStateTable _players;
        readonly NavGrid _nav;
        readonly ZombieTuning _tuning;
        readonly FlowFieldSet _flow;
        readonly SurroundSlotSolver _surround;
        readonly int _capacity;
        readonly int _gridWidth;
        readonly int _gridHeight;

        NativeArray<float2> _position, _velocity, _outPosition, _outVelocity;
        NativeArray<float> _heading, _outHeading, _speed, _slotAngle, _slotRing;
        NativeArray<byte> _alive, _target, _outState;
        NativeArray<int> _cellStart, _cellCount, _sorted, _cellOf;
        NativeArray<float2> _playerPosition;
        NativeArray<byte> _playerActive;

        readonly float2[] _stuckAnchor;
        DeterministicRandom _rng;
        float _targetTimer;
        float _surroundTimer;
        float _stuckTimer;

        public ZombieWorld(CrowdState crowd, PlayerStateTable players, NavGrid nav, ZombieTuning tuning, uint seed)
        {
            _crowd = crowd;
            _players = players;
            _nav = nav;
            _tuning = tuning;
            _capacity = crowd.Capacity;
            _flow = new FlowFieldSet(nav);
            _surround = new SurroundSlotSolver(tuning.Sectors, _capacity, tuning.RingMin, tuning.RingSpacing);
            _rng = DeterministicRandom.ForStream(seed, "zombie-world");

            _position = Alloc<float2>(); _velocity = Alloc<float2>(); _outPosition = Alloc<float2>(); _outVelocity = Alloc<float2>();
            _heading = Alloc<float>(); _outHeading = Alloc<float>(); _speed = Alloc<float>(); _slotAngle = Alloc<float>(); _slotRing = Alloc<float>();
            _alive = Alloc<byte>(); _target = Alloc<byte>(); _outState = Alloc<byte>();
            _cellOf = Alloc<int>(); _sorted = Alloc<int>();
            _stuckAnchor = new float2[_capacity];

            _gridWidth = math.max(1, (int)math.ceil(nav.Width * nav.CellSize / GridCellSize));
            _gridHeight = math.max(1, (int)math.ceil(nav.Height * nav.CellSize / GridCellSize));
            _cellStart = new NativeArray<int>(_gridWidth * _gridHeight, Allocator.Persistent);
            _cellCount = new NativeArray<int>(_gridWidth * _gridHeight, Allocator.Persistent);
            _playerPosition = new NativeArray<float2>(PlayerStateTable.Max, Allocator.Persistent);
            _playerActive = new NativeArray<byte>(PlayerStateTable.Max, Allocator.Persistent);
            for (int i = 0; i < _capacity; i++) _target[i] = ZombieSteeringJob.NoTarget;
        }

        public CrowdState Crowd => _crowd;
        public NavGrid Nav => _nav;
        public FlowFieldSet Flow => _flow;
        public int Unstuck { get; private set; }

        /// <summary>Duration of the last Tick in milliseconds (jobs included), for telemetry.</summary>
        public float LastTickMs { get; private set; }

        NativeArray<T> Alloc<T>() where T : struct => new NativeArray<T>(_capacity, Allocator.Persistent);

        /// <summary>Spawns a walker at a walkable position. Returns the slot or -1.</summary>
        public int Spawn(float2 position, float heading)
        {
            int slot = _crowd.Spawn(0, position.x, position.y, heading);
            if (slot < 0) return -1;
            _position[slot] = position;
            _velocity[slot] = float2.zero;
            _heading[slot] = heading;
            _speed[slot] = _rng.Range(_tuning.MinSpeed, _tuning.MaxSpeed);
            _slotAngle[slot] = _rng.Range(-math.PI, math.PI);
            _slotRing[slot] = _tuning.SurroundRange;
            _target[slot] = ZombieSteeringJob.NoTarget;
            _alive[slot] = 1;
            _stuckAnchor[slot] = position;
            _crowd.Anim[slot] = ZombieSteeringJob.StateWalk;
            return slot;
        }

        /// <summary>Removes a zombie; <paramref name="died"/> produces a corpse/blood event.</summary>
        public void Remove(int slot, bool died)
        {
            if (_alive[slot] == 0) return;
            _alive[slot] = 0;
            _crowd.Despawn(slot, died);
        }

        /// <summary>Moves a live zombie elsewhere (recycling far or stuck zombies, TDD_01 §8.4).</summary>
        public void Teleport(int slot, float2 position)
        {
            if (_alive[slot] == 0) return;
            // Recycling is a despawn + spawn so clients see a new generation instead of a 60 m slide.
            float heading = _heading[slot];
            Remove(slot, false);
            Spawn(position, heading);
        }

        public float2 PositionOf(int slot) => _position[slot];
        public bool IsAlive(int slot) => _alive[slot] != 0;

        public void Tick(float dt, uint tick)
        {
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            SyncPlayers();

            _targetTimer -= dt;
            if (_targetTimer <= 0f)
            {
                _targetTimer = _tuning.TargetInterval;
                SelectTargets();
            }
            _surroundTimer -= dt;
            if (_surroundTimer <= 0f)
            {
                _surroundTimer = _tuning.SurroundInterval;
                for (int p = 0; p < PlayerStateTable.Max; p++)
                {
                    if (_playerActive[p] == 0) continue;
                    _surround.Solve(p, _playerPosition[p], _tuning.SurroundRange, _position, _alive, _target, _slotAngle, _slotRing);
                }
            }

            JobHandle grid = new SpatialGridBuildJob
            {
                Position = _position,
                Alive = _alive,
                CellStart = _cellStart,
                CellCount = _cellCount,
                Sorted = _sorted,
                CellOf = _cellOf,
                Origin = _nav.Origin,
                InvCellSize = 1f / GridCellSize,
                GridWidth = _gridWidth,
                GridHeight = _gridHeight,
            }.Schedule();

            new ZombieSteeringJob
            {
                Position = _position,
                Velocity = _velocity,
                Heading = _heading,
                Speed = _speed,
                SlotAngle = _slotAngle,
                SlotRing = _slotRing,
                Target = _target,
                Alive = _alive,
                PlayerPosition = _playerPosition,
                PlayerActive = _playerActive,
                CellStart = _cellStart,
                CellCount = _cellCount,
                Sorted = _sorted,
                CellOf = _cellOf,
                GridWidth = _gridWidth,
                GridHeight = _gridHeight,
                Walkable = _nav.Walkable,
                FlowDirections = _flow.Directions,
                NavWidth = _nav.Width,
                NavHeight = _nav.Height,
                NavOrigin = _nav.Origin,
                NavCellSize = _nav.CellSize,
                Dt = dt,
                Tick = tick,
                Acceleration = _tuning.Acceleration,
                Radius = _tuning.Radius,
                SeparationStrength = _tuning.SeparationStrength,
                AttackRange = _tuning.AttackRange,
                SurroundRange = _tuning.SurroundRange,
                TierA = _tuning.TierADistance,
                TierB = _tuning.TierBDistance,
                OutPosition = _outPosition,
                OutVelocity = _outVelocity,
                OutHeading = _outHeading,
                OutState = _outState,
            }.Schedule(_capacity, 32, grid).Complete();

            Swap(ref _position, ref _outPosition);
            Swap(ref _velocity, ref _outVelocity);
            Swap(ref _heading, ref _outHeading);
            WriteBack();

            _stuckTimer -= dt;
            if (_stuckTimer <= 0f)
            {
                _stuckTimer = _tuning.StuckCheckInterval;
                ResolveStuck();
            }
            LastTickMs = (float)((System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
        }

        public void Dispose()
        {
            _flow.Dispose();
            _position.Dispose(); _velocity.Dispose(); _outPosition.Dispose(); _outVelocity.Dispose();
            _heading.Dispose(); _outHeading.Dispose(); _speed.Dispose(); _slotAngle.Dispose(); _slotRing.Dispose();
            _alive.Dispose(); _target.Dispose(); _outState.Dispose();
            _cellStart.Dispose(); _cellCount.Dispose(); _sorted.Dispose(); _cellOf.Dispose();
            _playerPosition.Dispose(); _playerActive.Dispose();
        }

        void SyncPlayers()
        {
            for (int p = 0; p < PlayerStateTable.Max; p++)
            {
                bool active = _players.Active[p];
                _playerActive[p] = active ? (byte)1 : (byte)0;
                if (!active)
                {
                    _flow.Clear(p);
                    continue;
                }
                var position = new float2(_players.X[p], _players.Z[p]);
                _playerPosition[p] = position;
                _flow.SetTarget(p, position);
            }
        }

        /// <summary>Nearest active player, with hysteresis so zombies do not flip between two players.</summary>
        void SelectTargets()
        {
            for (int i = 0; i < _capacity; i++)
            {
                if (_alive[i] == 0) continue;
                float2 p = _position[i];
                int best = ZombieSteeringJob.NoTarget;
                float bestDistance = float.MaxValue;
                for (int k = 0; k < PlayerStateTable.Max; k++)
                {
                    if (_playerActive[k] == 0) continue;
                    float d = math.distancesq(p, _playerPosition[k]);
                    if (d < bestDistance)
                    {
                        bestDistance = d;
                        best = k;
                    }
                }
                byte current = _target[i];
                if (current != ZombieSteeringJob.NoTarget && _playerActive[current] != 0 && best != current)
                {
                    float currentDistance = math.distancesq(p, _playerPosition[current]);
                    if (bestDistance > currentDistance * 0.64f) best = current; // switch only when 20 % closer
                }
                _target[i] = (byte)best;
            }
        }

        void WriteBack()
        {
            for (int i = 0; i < _capacity; i++)
            {
                if (_alive[i] == 0) continue;
                _crowd.PosX[i] = _position[i].x;
                _crowd.PosZ[i] = _position[i].y;
                _crowd.Heading[i] = _heading[i];
                _crowd.Anim[i] = _outState[i];
            }
        }

        /// <summary>Zombies that made no progress while walking get a sideways nudge (TDD_01 §8.4 anti-stuck).</summary>
        void ResolveStuck()
        {
            for (int i = 0; i < _capacity; i++)
            {
                if (_alive[i] == 0) continue;
                float2 p = _position[i];
                bool walking = _outState[i] == ZombieSteeringJob.StateWalk;
                // Only zombies on their way count: those queueing in the surround rings are supposed to wait.
                if (walking && math.distance(p, _stuckAnchor[i]) < _tuning.StuckDistance && IsFarFromTarget(i, _tuning.SurroundRange))
                {
                    float angle = _rng.Range(-math.PI, math.PI);
                    _velocity[i] = new float2(math.cos(angle), math.sin(angle)) * _speed[i] * 1.5f;
                    Unstuck++;
                }
                _stuckAnchor[i] = p;
            }
        }

        bool IsFarFromTarget(int i, float distance)
        {
            byte t = _target[i];
            return t == ZombieSteeringJob.NoTarget || math.distance(_position[i], _playerPosition[t]) > distance;
        }

        static void Swap<T>(ref NativeArray<T> a, ref NativeArray<T> b) where T : struct
        {
            NativeArray<T> tmp = a;
            a = b;
            b = tmp;
        }
    }
}
