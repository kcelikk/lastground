using UnityEngine;

namespace LastGround.Data.Objectives
{
    /// <summary>
    /// A map objective (TDD_01 §12.4, D-019). M5: "Clear the area" — kill zombies inside a zone. The counter is scoped
    /// to the objective (never a global "zombies left"); the director keeps spawning regardless of it.
    /// </summary>
    [CreateAssetMenu(menuName = "LastGround/Objectives/Objective")]
    public sealed class ObjectiveDefinition : ScriptableObject
    {
        public string Id = "clear_area";
        /// <summary>Title format with {0} = zone name, e.g. "Clear the area — {0}".</summary>
        public string TitleKey = "objective.clear_area.title";
        /// <summary>Counter format with {0}/{1}, e.g. "Kill zombies: {0}/{1}".</summary>
        public string CounterKey = "objective.clear_area.counter";

        [Header("Target (scales with players and threat)")]
        public int TargetBase = 30;
        public float TargetPerExtraPlayer = 0.5f;
        public int TargetPerThreat = 10;

        [Header("Flow")]
        public float FirstDelay = 20f;
        public float Cooldown = 25f;
        /// <summary>How long a finished objective stays on the panel as "completed".</summary>
        public float CompletedShowTime = 4f;

        [Header("Reward (TDD_01 §12.4: breather + loot)")]
        public int RewardCoinsPerPlayer = 15;
        public int RewardMedkits = 1;
    }
}
