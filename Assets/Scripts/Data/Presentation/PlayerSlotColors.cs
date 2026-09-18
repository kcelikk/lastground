using UnityEngine;

namespace LastGround.Data.Presentation
{
    /// <summary>Team colours by player slot (TDD_01 §0.12: P1 blue, P2 green, P3 yellow, P4 purple).</summary>
    public static class PlayerSlotColors
    {
        static readonly Color[] Colors =
        {
            new Color(0.2f, 0.6f, 1f),
            new Color(0.3f, 0.9f, 0.3f),
            new Color(1f, 0.8f, 0.2f),
            new Color(0.8f, 0.4f, 1f),
        };

        public static Color Of(int slot) => Colors[(uint)slot % (uint)Colors.Length];
    }
}
