using UnityEngine;

namespace LastGround.Data.Upgrades
{
    /// <summary>
    /// Team XP needed per level (TDD_01 §7.5): xp(n) = A·n^Exponent + B to go from level n to n+1. XP is shared and
    /// equal for everyone (D-002), so more players (more kills) need proportionally more XP per level.
    /// </summary>
    [CreateAssetMenu(menuName = "LastGround/Upgrades/Level Curve")]
    public sealed class LevelCurveDefinition : ScriptableObject
    {
        public float A = 6f;
        public float Exponent = 1.5f;
        public float B = 6f;
        /// <summary>Multiplier on the XP per level by player count (index 0 = 1 player).</summary>
        public float[] PlayerCountXpScale = { 1f, 1.35f, 1.65f, 1.9f };
        public int MaxLevel = 99;

        public int XpForLevel(int level, int players)
        {
            float scale = PlayerCountXpScale[Mathf.Clamp(players, 1, PlayerCountXpScale.Length) - 1];
            return Mathf.Max(1, Mathf.RoundToInt((A * Mathf.Pow(level, Exponent) + B) * scale));
        }
    }
}
