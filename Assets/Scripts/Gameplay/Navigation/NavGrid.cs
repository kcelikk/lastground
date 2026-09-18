using System;
using System.Runtime.CompilerServices;
using LastGround.Data.Map;
using Unity.Collections;
using Unity.Mathematics;

namespace LastGround.Gameplay.Navigation
{
    /// <summary>Runtime walkability grid in native memory so Burst jobs can read it.</summary>
    public sealed class NavGrid : IDisposable
    {
        public readonly int Width;
        public readonly int Height;
        public readonly float CellSize;
        public readonly float2 Origin;
        public NativeArray<byte> Walkable;

        public NavGrid(int width, int height, float cellSize, float2 origin, byte[] cells)
        {
            Width = width;
            Height = height;
            CellSize = cellSize;
            Origin = origin;
            Walkable = new NativeArray<byte>(width * height, Allocator.Persistent);
            if (cells != null) Walkable.CopyFrom(cells);
            else for (int i = 0; i < Walkable.Length; i++) Walkable[i] = 1;
        }

        public static NavGrid FromAsset(NavGridAsset asset)
        {
            return new NavGrid(asset.Width, asset.Height, asset.CellSize, new float2(asset.Origin.x, asset.Origin.y), asset.Cells);
        }

        /// <summary>An open field of the given size centred on the origin (tests, benchmark).</summary>
        public static NavGrid Open(int size)
        {
            return new NavGrid(size, size, 1f, new float2(-size * 0.5f, -size * 0.5f), null);
        }

        public int CellCount => Width * Height;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int2 CellOf(float2 position) => (int2)math.floor((position - Origin) / CellSize);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool InBounds(int2 cell) => cell.x >= 0 && cell.y >= 0 && cell.x < Width && cell.y < Height;

        public bool IsWalkable(float2 position)
        {
            int2 c = CellOf(position);
            return InBounds(c) && Walkable[c.y * Width + c.x] != 0;
        }

        public float2 CellCentre(int2 cell) => Origin + ((float2)cell + 0.5f) * CellSize;

        /// <summary>
        /// Distance along the segment <paramref name="from"/> → <paramref name="from"/> + dir × maxDistance to the first
        /// blocked (or out-of-bounds) cell, or maxDistance when clear. Grid DDA: exact for axis-aligned cells, used as
        /// the line-of-sight test for shots (TDD_01 §5.1) on host and clients alike.
        /// </summary>
        public float Raycast(float2 from, float2 dir, float maxDistance)
        {
            int2 cell = CellOf(from);
            if (!InBounds(cell) || Walkable[cell.y * Width + cell.x] == 0) return 0f;
            int2 step = new int2(dir.x > 0f ? 1 : -1, dir.y > 0f ? 1 : -1);
            float2 local = (from - Origin) / CellSize;
            float2 inv = new float2(math.abs(dir.x) > 1e-6f ? 1f / math.abs(dir.x) : float.MaxValue,
                math.abs(dir.y) > 1e-6f ? 1f / math.abs(dir.y) : float.MaxValue);
            float2 next = new float2(dir.x > 0f ? cell.x + 1 - local.x : local.x - cell.x,
                dir.y > 0f ? cell.y + 1 - local.y : local.y - cell.y) * inv * CellSize;
            float2 delta = inv * CellSize;
            while (true)
            {
                float travelled;
                if (next.x < next.y)
                {
                    travelled = next.x;
                    next.x += delta.x;
                    cell.x += step.x;
                }
                else
                {
                    travelled = next.y;
                    next.y += delta.y;
                    cell.y += step.y;
                }
                if (travelled >= maxDistance) return maxDistance;
                if (!InBounds(cell) || Walkable[cell.y * Width + cell.x] == 0) return travelled;
            }
        }

        /// <summary>True when nothing blocks the straight line between two points.</summary>
        public bool HasLineOfSight(float2 from, float2 to)
        {
            float2 d = to - from;
            float length = math.length(d);
            if (length < 1e-4f) return true;
            return Raycast(from, d / length, length) >= length - 1e-3f;
        }

        public void Dispose()
        {
            if (Walkable.IsCreated) Walkable.Dispose();
        }
    }
}
