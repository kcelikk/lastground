using System.Runtime.CompilerServices;

namespace LastGround.Core.Random
{
    /// <summary>
    /// Deterministic 32-bit hashing used to derive RNG seeds, e.g. hash(runSeed, playerId, shotSeq) (TDD_01 §5.2).
    /// Same result on every platform and culture.
    /// </summary>
    public static class Hash32
    {
        const uint Prime2 = 2246822519u;
        const uint Prime3 = 3266489917u;
        const uint Prime4 = 668265263u;
        const uint Prime5 = 374761393u;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Combine(uint a, uint b)
        {
            uint h = Prime5 + a * Prime3;
            h = Rotl(h, 17) * Prime4;
            h += b * Prime3;
            h = Rotl(h, 17) * Prime4;
            return Avalanche(h);
        }

        public static uint Combine(uint a, uint b, uint c)
        {
            return Combine(Combine(a, b), c);
        }

        /// <summary>FNV-1a over UTF-16 code units. Culture-independent; use for string ids and stream names.</summary>
        public static uint Of(string text)
        {
            uint h = 2166136261u;
            if (text == null) return h;
            for (int i = 0; i < text.Length; i++)
            {
                h ^= text[i];
                h *= 16777619u;
            }
            return Avalanche(h);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static uint Rotl(uint x, int r) => (x << r) | (x >> (32 - r));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static uint Avalanche(uint h)
        {
            h ^= h >> 15;
            h *= Prime2;
            h ^= h >> 13;
            h *= Prime3;
            h ^= h >> 16;
            return h;
        }
    }
}
