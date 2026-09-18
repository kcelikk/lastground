using UnityEngine;

namespace LastGround.Data.Director
{
    /// <summary>
    /// Horde scaling by player count (TDD_01 §9.10). Index 0 = 1 player. Zombie health does not scale (power
    /// fantasy); only how many and how fast.
    /// </summary>
    [CreateAssetMenu(menuName = "LastGround/Director/Player Count Scaling")]
    public sealed class PlayerCountScalingProfile : ScriptableObject
    {
        public float[] HordeCount = { 1f, 1.6f, 2.05f, 2.5f };
        public float[] SpawnRate = { 1f, 1.35f, 1.65f, 1.9f };

        public float HordeCountFor(int players) => HordeCount[Mathf.Clamp(players, 1, HordeCount.Length) - 1];
        public float SpawnRateFor(int players) => SpawnRate[Mathf.Clamp(players, 1, SpawnRate.Length) - 1];
    }
}
