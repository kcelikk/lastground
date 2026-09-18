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

        public void Dispose()
        {
            if (Walkable.IsCreated) Walkable.Dispose();
        }
    }
}
