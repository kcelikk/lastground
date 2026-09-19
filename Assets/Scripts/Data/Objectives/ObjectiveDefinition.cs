using LastGround.Data.Map;
using UnityEngine;

namespace LastGround.Data.Objectives
{
    /// <summary>
    /// A map event with its objective (TDD_01 §12.2–12.4, D-019): kind, when and how often it may start, its
    /// parameters and its reward. The counter is scoped to the event (never a global "zombies left"); the director
    /// keeps spawning regardless of it. Read-only at runtime.
    /// </summary>
    [CreateAssetMenu(menuName = "LastGround/Objectives/Objective")]
    public sealed class ObjectiveDefinition : ScriptableObject
    {
        public string Id = "clear_area";
        public ObjectiveKind Kind = ObjectiveKind.ClearArea;
        /// <summary>Title format with {0} = region name, e.g. "Clear the area — {0}".</summary>
        public string TitleKey = "objective.clear_area.title";
        /// <summary>Counter format: {0}/{1} for counts, {0} for seconds.</summary>
        public string CounterKey = "objective.clear_area.counter";

        [Header("Scheduling")]
        public float MinRunSeconds;
        [Min(0f)] public float Weight = 1f;
        /// <summary>Anchor kind the event needs (ignored for Clear Area and Elite Hunt).</summary>
        public MapAnchorKind Anchor = MapAnchorKind.SupplyDrop;
        /// <summary>Seconds before the event fails if unfinished (0 = no limit).</summary>
        public float TimeLimit;

        [Header("Target (Clear Area: scales with players and threat)")]
        public int TargetBase = 30;
        public float TargetPerExtraPlayer = 0.5f;
        public int TargetPerThreat = 10;

        [Header("Proximity events (supply, cache, generator, rescue)")]
        /// <summary>Seconds of warning before the crate lands (Supply Drop).</summary>
        public float ArriveSeconds = 15f;
        public float Radius = 2.5f;
        /// <summary>Seconds someone must stand inside (open, repair) or defend (Rescue Signal).</summary>
        public float HoldSeconds = 3f;
        /// <summary>Holding raises the horde pressure (TDD_01 §12.2 "under pressure").</summary>
        public bool PressureWhileHolding;

        [Header("Flow")]
        public float FirstDelay = 20f;
        public float Cooldown = 25f;
        /// <summary>How long a finished objective stays on the panel as "completed".</summary>
        public float CompletedShowTime = 4f;

        [Header("Reward (TDD_01 §12.2)")]
        public int RewardCoinsPerPlayer = 15;
        public int RewardMedkits = 1;
        /// <summary>Instanced weapon pickup for everyone.</summary>
        public bool RewardWeapon;
        public int RewardGrenades;
        /// <summary>Bonus level-up offer of at least this rarity (0 Common … 3 Legendary; -1 = none).</summary>
        public int RewardOfferRarity = -1;
        /// <summary>Generator: seconds the sentry turret fights after completion.</summary>
        public float TurretSeconds;
        /// <summary>Rescue Signal: dead players come back.</summary>
        public bool RewardRespawn;
    }
}
