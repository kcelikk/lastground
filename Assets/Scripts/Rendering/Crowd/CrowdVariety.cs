using System.Runtime.CompilerServices;
using LastGround.Core.Random;

namespace LastGround.Rendering.Crowd
{
    /// <summary>
    /// Deterministic per-slot look (TDD_02 §21.3): body, tint, scale, animation speed and phase from one hash,
    /// so the same zombie looks the same on every device without sending visual data.
    /// </summary>
    public static class CrowdVariety
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Hash(int slot) => Hash32.Combine((uint)slot, 0x5EEDu);

        public static int Body(uint hash, int bodyCount) => (int)(hash % (uint)bodyCount);
        public static int Tint(uint hash) => (int)((hash >> 8) & 7u);
        public static float Scale(uint hash, float variation) => 1f + (((hash >> 16) & 255u) / 255f * 2f - 1f) * variation;
        public static float Speed(uint hash) => 0.9f + ((hash >> 4) & 15u) / 15f * 0.2f;
        public static float Phase(uint hash) => (hash >> 24) / 255f;
    }
}
