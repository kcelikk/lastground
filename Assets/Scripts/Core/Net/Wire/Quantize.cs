using System.Runtime.CompilerServices;

namespace LastGround.Core.Net.Wire
{
    /// <summary>
    /// Fixed-point encodings shared by all snapshot streams (TDD_02 §17.2).
    /// Positions: 16 bit over [-256, 256) m → 7.8 mm steps. Yaw: 6 or 8 bit over 360°.
    /// </summary>
    public static class Quantize
    {
        public const float WorldMin = -256f;
        public const float WorldSize = 512f;
        const float PositionScale = 65535f / WorldSize;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort Position(float value)
        {
            float t = (value - WorldMin) * PositionScale;
            if (t <= 0f) return 0;
            if (t >= 65535f) return 65535;
            return (ushort)(t + 0.5f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Position(ushort value) => value / PositionScale + WorldMin;

        /// <summary>Yaw in degrees → n-bit step count.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Yaw(float degrees, int bits)
        {
            int steps = 1 << bits;
            float normalized = degrees / 360f;
            normalized -= (float)System.Math.Floor(normalized);
            return (uint)((int)(normalized * steps + 0.5f) & (steps - 1));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Yaw(uint value, int bits) => value * (360f / (1 << bits));

        /// <summary>Velocity in m/s → centimetres per second, clamped to ±327 m/s.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short Velocity(float metresPerSecond)
        {
            float cm = metresPerSecond * 100f;
            if (cm > short.MaxValue) return short.MaxValue;
            if (cm < short.MinValue) return short.MinValue;
            return (short)(cm >= 0f ? cm + 0.5f : cm - 0.5f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Velocity(short centimetresPerSecond) => centimetresPerSecond * 0.01f;
    }
}
