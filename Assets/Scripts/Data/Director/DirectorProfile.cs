using UnityEngine;

namespace LastGround.Data.Director
{
    /// <summary>
    /// Horde director tuning (TDD_01 §9). No waves (D-003): spawn points accrue continuously; a state machine adds
    /// tension → peak → breather on top of the long-term Threat climb. Every number is a starting hypothesis.
    /// </summary>
    [CreateAssetMenu(menuName = "LastGround/Director/Profile")]
    public sealed class DirectorProfile : ScriptableObject
    {
        [Header("Budget (1P, Threat I)")]
        /// <summary>Spawn points per second at run start (a walker costs 1).</summary>
        public float SpawnRateStart = 1.2f;
        public float SpawnRatePerMinute = 0.25f;
        public float SpawnRateCap = 8f;
        public float MaxAliveStart = 45f;
        public float MaxAlivePerMinute = 14f;
        /// <summary>Host device cap (D-007: 300 simulated).</summary>
        public int MaxAliveCap = 300;
        /// <summary>Unspent points never exceed this (no huge bursts after a quiet stretch).</summary>
        public float BudgetCarryCap = 30f;

        [Header("State machine (TDD_01 §9.4)")]
        public float CalmAtStart = 8f;
        public Vector2 BuildUpDuration = new Vector2(35f, 70f);
        public Vector2 PeakHoldDuration = new Vector2(15f, 30f);
        public Vector2 RelaxMinDuration = new Vector2(10f, 20f);
        public float PeakIntensity = 0.75f;
        public float RelaxIntensity = 0.3f;
        public float CalmRate = 0.35f;
        public float BuildUpRate = 1f;
        public float PeakRate = 1.9f;
        public float PeakHoldRate = 1.4f;
        public float RelaxRate = 0.15f;

        [Header("Run personality (rolled once per run from the seed)")]
        /// <summary>± fraction applied to the spawn rate for the whole run.</summary>
        [Range(0f, 0.5f)] public float RateVariance = 0.15f;
        /// <summary>BuildUp durations are scaled by a factor in [1 − v, 1 + v].</summary>
        [Range(0f, 0.5f)] public float BuildUpVariance = 0.3f;
        /// <summary>± added to the peak intensity threshold.</summary>
        [Range(0f, 0.2f)] public float PeakThresholdVariance = 0.08f;

        [Header("Stress (TDD_01 §9.3)")]
        /// <summary>Stress per max-health fraction of damage taken.</summary>
        public float DamageStress = 2.2f;
        public float NearbyRadius = 6f;
        /// <summary>Nearby zombies at which the crowd term saturates.</summary>
        public int NearbySaturation = 25;
        public float NearbyStress = 0.55f;
        public float LowHealthStress = 0.35f;
        /// <summary>Stress lost per second.</summary>
        public float StressDecay = 0.12f;

        [Header("Spawning (TDD_01 §9.6–9.7)")]
        public float MinSpawnDistance = 22f;
        public float MaxSpawnDistance = 45f;
        /// <summary>Zombies further than this from every player are removed (no corpse).</summary>
        public float DespawnDistance = 70f;
        public int SpawnsPerTick = 12;
        public Vector2Int TrickleSize = new Vector2Int(1, 3);
        public Vector2Int PackSize = new Vector2Int(6, 12);
        public Vector2Int PincerSideSize = new Vector2Int(4, 8);
        public Vector2Int SurroundSectors = new Vector2Int(4, 6);
        public Vector2Int SurroundSectorSize = new Vector2Int(4, 6);
        public float PackSpreadDeg = 18f;

        [Header("Anti-camping (TDD_01 §9.8)")]
        /// <summary>The team's centre counts as "in one spot" while it stays within this radius.</summary>
        public float CampRadius = 12f;
        public float CampSeconds = 45f;
        public float CampRateMultiplier = 1.5f;
        /// <summary>Spitter card weight multiplier while camping.</summary>
        public float CampSpitterWeight = 3f;

        [Header("HUD HORDE label thresholds (CALM · LOW · MEDIUM · HIGH · EXTREME)")]
        public float LowThreshold = 0.15f;
        public float MediumThreshold = 0.35f;
        public float HighThreshold = 0.55f;
        public float ExtremeThreshold = 0.8f;
        /// <summary>A new label must hold this long before it shows (no flicker).</summary>
        public float LabelHysteresis = 2f;
    }
}
