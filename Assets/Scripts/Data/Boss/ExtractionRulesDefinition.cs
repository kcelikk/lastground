using UnityEngine;

namespace LastGround.Data.Boss
{
    /// <summary>
    /// Extraction windows and the end-of-run reward conversion (TDD_01 §2.2, TDD_02 <c>ExtractionRulesDefinition</c>):
    /// a landing zone opens after each boss kill and then periodically; holding it long enough ends the run with all
    /// coins plus a threat-based bonus, a team wipe converts only part of them. Read-only at runtime.
    /// </summary>
    [CreateAssetMenu(menuName = "LastGround/Boss/Extraction Rules")]
    public sealed class ExtractionRulesDefinition : ScriptableObject
    {
        /// <summary>Seconds the landing zone stays open unless the team is holding it.</summary>
        public float WindowSeconds = 90f;
        /// <summary>Seconds of holding (at least one standing player inside) to extract.</summary>
        public float HoldSeconds = 45f;
        public float Radius = 6f;
        /// <summary>After the first window, a new one every this many seconds from the previous close.</summary>
        public float IntervalSeconds = 420f;
        /// <summary>Delay between a boss kill and its landing zone opening.</summary>
        public float AfterBossDelay = 6f;
        /// <summary>Holding the zone draws the horde (director hold pressure).</summary>
        public bool PressureWhileHolding = true;

        [Header("Reward conversion")]
        /// <summary>Extraction bonus per player per threat level, in coins.</summary>
        public int BonusPerThreat = 15;
        /// <summary>Share of run coins kept when the whole team falls.</summary>
        [Range(0f, 1f)] public float WipeConversion = 0.6f;
        /// <summary>Share for players who are down or dead when the others extract (they get the base, no bonus).</summary>
        [Range(0f, 1f)] public float LeftBehindConversion = 1f;

        /// <summary>Banked coins for a run (per player).</summary>
        public int Convert(int runCoins, int threat, bool extracted, bool standing)
        {
            if (!extracted) return Mathf.FloorToInt(runCoins * WipeConversion);
            if (!standing) return Mathf.FloorToInt(runCoins * LeftBehindConversion);
            return runCoins + BonusPerThreat * Mathf.Max(1, threat);
        }
    }
}
