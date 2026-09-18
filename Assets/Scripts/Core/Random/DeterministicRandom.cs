using System.Runtime.CompilerServices;

namespace LastGround.Core.Random
{
    /// <summary>
    /// PCG32 random number generator as a mutable struct. Never use UnityEngine.Random or System.Random in
    /// simulation code: host and clients must derive identical results from the same seed (TDD_03 §B).
    /// </summary>
    public struct DeterministicRandom
    {
        const ulong Multiplier = 6364136223846793005UL;

        ulong _state;
        ulong _inc;

        public DeterministicRandom(uint seed, uint stream = 0)
        {
            _state = 0UL;
            _inc = ((ulong)stream << 1) | 1UL;
            NextUInt();
            _state += seed;
            NextUInt();
        }

        /// <summary>Independent sub-stream, e.g. ForStream(runSeed, "upgrade", playerId).</summary>
        public static DeterministicRandom ForStream(uint runSeed, string streamName, uint index = 0)
        {
            uint stream = Hash32.Combine(Hash32.Of(streamName), index);
            return new DeterministicRandom(Hash32.Combine(runSeed, stream), stream);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint NextUInt()
        {
            ulong old = _state;
            _state = old * Multiplier + _inc;
            uint xorShifted = (uint)(((old >> 18) ^ old) >> 27);
            int rot = (int)(old >> 59);
            return (xorShifted >> rot) | (xorShifted << ((-rot) & 31));
        }

        /// <summary>Uniform float in [0, 1).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float NextFloat()
        {
            return (NextUInt() >> 8) * (1f / 16777216f);
        }

        /// <summary>Uniform float in [min, max).</summary>
        public float Range(float min, float max)
        {
            return min + (max - min) * NextFloat();
        }

        /// <summary>Uniform int in [min, max). Returns min when the range is empty.</summary>
        public int Range(int min, int max)
        {
            if (max <= min) return min;
            uint span = (uint)(max - min);
            // Lemire's method without the rejection step: bias < 2^-32 * span, fine for gameplay.
            return min + (int)(((ulong)NextUInt() * span) >> 32);
        }

        /// <summary>True with the given probability (0..1).</summary>
        public bool Chance(float probability)
        {
            return NextFloat() < probability;
        }
    }
}
