using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace LastGround.Gameplay.Navigation
{
    /// <summary>Breadth-first integration field from one target cell (4-neighbour steps), TDD_01 §8.3.</summary>
    [BurstCompile]
    public struct IntegrationFieldJob : IJob
    {
        public const ushort Unreached = ushort.MaxValue;

        [ReadOnly] public NativeArray<byte> Walkable;
        public NativeArray<ushort> Distance;
        public NativeArray<int> Queue;
        public int Width;
        public int Height;
        public int TargetIndex;

        public void Execute()
        {
            for (int i = 0; i < Distance.Length; i++) Distance[i] = Unreached;
            if (TargetIndex < 0 || TargetIndex >= Distance.Length) return;

            int head = 0, tail = 0;
            Distance[TargetIndex] = 0;
            Queue[tail++] = TargetIndex;
            while (head < tail)
            {
                int index = Queue[head++];
                int x = index % Width;
                int y = index / Width;
                ushort next = (ushort)(Distance[index] + 1);
                if (x > 0) Visit(index - 1, next, ref tail);
                if (x < Width - 1) Visit(index + 1, next, ref tail);
                if (y > 0) Visit(index - Width, next, ref tail);
                if (y < Height - 1) Visit(index + Width, next, ref tail);
            }
        }

        void Visit(int index, ushort distance, ref int tail)
        {
            if (Walkable[index] == 0 || Distance[index] != Unreached) return;
            Distance[index] = distance;
            Queue[tail++] = index;
        }
    }

    /// <summary>
    /// Per cell, the unit direction towards the lowest-distance neighbour (8 directions, no corner cutting).
    /// Stored as float2 so steering can read it without decoding.
    /// </summary>
    [BurstCompile]
    public struct FlowDirectionJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<byte> Walkable;
        [ReadOnly] public NativeArray<ushort> Distance;
        [WriteOnly] public NativeArray<float2> Direction;
        public int Width;
        public int Height;

        public void Execute(int index)
        {
            ushort best = Distance[index];
            if (best == IntegrationFieldJob.Unreached || best == 0)
            {
                Direction[index] = float2.zero;
                return;
            }
            int x = index % Width;
            int y = index / Width;
            int2 bestOffset = int2.zero;
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= Width || ny >= Height) continue;
                    int n = ny * Width + nx;
                    if (Walkable[n] == 0) continue;
                    if (dx != 0 && dy != 0 && (Walkable[y * Width + nx] == 0 || Walkable[ny * Width + x] == 0)) continue;
                    ushort d = Distance[n];
                    if (d < best)
                    {
                        best = d;
                        bestOffset = new int2(dx, dy);
                    }
                }
            }
            Direction[index] = math.normalizesafe((float2)bestOffset);
        }
    }
}
