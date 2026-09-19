using LastGround.Core.Random;
using Unity.Mathematics;

namespace LastGround.Gameplay.Combat
{
    /// <summary>
    /// Deterministic per-shot randomness (TDD_01 §5.2): spread and crit come from hash(runSeed, player, shotSeq,
    /// pellet), so the shooter's predicted damage number and the host's resolved damage are identical without
    /// sending either over the network.
    /// </summary>
    public static class ShotRng
    {
        const uint SpreadSalt = 0x5EED5u;
        const uint CritSalt = 0xC417u;
        const uint StunSalt = 0x57u;

        public static uint Seed(uint runSeed, int player, ushort shotSeq)
        {
            return Hash32.Combine(runSeed, (uint)player, shotSeq);
        }

        /// <summary>Spread offset in radians, uniform in ±spreadDeg/2.</summary>
        public static float SpreadRadians(uint shotSeed, int pellet, float spreadDeg)
        {
            if (spreadDeg <= 0f) return 0f;
            return (Unit(Hash32.Combine(shotSeed, SpreadSalt, (uint)pellet)) - 0.5f) * math.radians(spreadDeg);
        }

        public static bool IsCrit(uint shotSeed, int pellet, float chance)
        {
            return chance > 0f && Unit(Hash32.Combine(shotSeed, CritSalt, (uint)pellet)) < chance;
        }

        /// <summary>On-hit stun roll (Concussive upgrade), same on the shooter and the host.</summary>
        public static bool IsStun(uint shotSeed, int pellet, int pierce, float chance)
        {
            return chance > 0f && Unit(Hash32.Combine(shotSeed, StunSalt + (uint)pierce, (uint)pellet)) < chance;
        }

        static float Unit(uint hash) => (hash >> 8) * (1f / 16777216f);
    }
}
