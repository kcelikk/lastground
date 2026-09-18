using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace LastGround.Gameplay.Navigation
{
    /// <summary>
    /// One flow field per player slot (max 4), rebuilt only when that player's cell changes (TDD_01 §8.3).
    /// Cost depends on the number of targets, not on the number of zombies.
    /// </summary>
    public sealed class FlowFieldSet : IDisposable
    {
        public const int MaxTargets = 4;

        readonly NavGrid _grid;
        readonly NativeArray<ushort>[] _distance = new NativeArray<ushort>[MaxTargets];
        readonly int2[] _targetCell = new int2[MaxTargets];
        readonly bool[] _valid = new bool[MaxTargets];
        NativeArray<int> _queue;

        /// <summary>Directions for all targets: [target * cellCount + cell].</summary>
        public NativeArray<float2> Directions;

        public FlowFieldSet(NavGrid grid)
        {
            _grid = grid;
            int cells = grid.CellCount;
            for (int i = 0; i < MaxTargets; i++)
            {
                _distance[i] = new NativeArray<ushort>(cells, Allocator.Persistent);
                _targetCell[i] = new int2(int.MinValue, int.MinValue);
            }
            _queue = new NativeArray<int>(cells, Allocator.Persistent);
            Directions = new NativeArray<float2>(cells * MaxTargets, Allocator.Persistent);
        }

        public int Rebuilds { get; private set; }

        public bool IsValid(int target) => _valid[target];

        /// <summary>Distance in cells from a position to the target; ushort.MaxValue when unreachable.</summary>
        public ushort DistanceAt(int target, float2 position)
        {
            int2 c = _grid.CellOf(position);
            return _grid.InBounds(c) ? _distance[target][c.y * _grid.Width + c.x] : IntegrationFieldJob.Unreached;
        }

        /// <summary>Rebuilds the field for a target if it moved to another cell. Runs synchronously (Burst).</summary>
        public void SetTarget(int target, float2 position)
        {
            int2 cell = _grid.CellOf(position);
            if (!_grid.InBounds(cell))
            {
                _valid[target] = false;
                return;
            }
            if (_valid[target] && cell.Equals(_targetCell[target])) return;
            _targetCell[target] = cell;
            _valid[target] = true;
            Rebuilds++;

            new IntegrationFieldJob
            {
                Walkable = _grid.Walkable,
                Distance = _distance[target],
                Queue = _queue,
                Width = _grid.Width,
                Height = _grid.Height,
                TargetIndex = cell.y * _grid.Width + cell.x,
            }.Run();

            new FlowDirectionJob
            {
                Walkable = _grid.Walkable,
                Distance = _distance[target],
                Direction = Directions.GetSubArray(target * _grid.CellCount, _grid.CellCount),
                Width = _grid.Width,
                Height = _grid.Height,
            }.Schedule(_grid.CellCount, 256).Complete();
        }

        public void Clear(int target)
        {
            _valid[target] = false;
            _targetCell[target] = new int2(int.MinValue, int.MinValue);
        }

        public void Dispose()
        {
            for (int i = 0; i < MaxTargets; i++) if (_distance[i].IsCreated) _distance[i].Dispose();
            if (_queue.IsCreated) _queue.Dispose();
            if (Directions.IsCreated) Directions.Dispose();
        }
    }
}
