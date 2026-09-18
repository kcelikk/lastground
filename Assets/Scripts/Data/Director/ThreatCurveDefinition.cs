using UnityEngine;

namespace LastGround.Data.Director
{
    /// <summary>
    /// Long-term escalation (TDD_01 §2.2): Threat I at 00:00, then II/III/IV/V at the listed minutes and one more
    /// level every <see cref="RepeatMinutes"/> after the last. Each level scales the director's budget.
    /// </summary>
    [CreateAssetMenu(menuName = "LastGround/Director/Threat Curve")]
    public sealed class ThreatCurveDefinition : ScriptableObject
    {
        /// <summary>Minute at which Threat II, III, IV, V… begin.</summary>
        public float[] LevelStartMinutes = { 3f, 7f, 12f, 15f, 22f };
        public float RepeatMinutes = 7f;
        /// <summary>Spawn rate and max alive multiplier per level above I.</summary>
        public float BudgetPerLevel = 0.15f;

        /// <summary>Threat level (1 = I) at the given run time.</summary>
        public int LevelAt(float seconds)
        {
            float minutes = seconds / 60f;
            int level = 1;
            for (int i = 0; i < LevelStartMinutes.Length; i++)
            {
                if (minutes < LevelStartMinutes[i]) return level;
                level++;
            }
            float last = LevelStartMinutes.Length > 0 ? LevelStartMinutes[LevelStartMinutes.Length - 1] : 0f;
            if (RepeatMinutes > 0f) level += (int)((minutes - last) / RepeatMinutes);
            return level;
        }
    }
}
