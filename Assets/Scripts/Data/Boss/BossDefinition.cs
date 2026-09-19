using LastGround.Data.Zombies;
using UnityEngine;

namespace LastGround.Data.Boss
{
    /// <summary>
    /// A boss (TDD_01 §10 <c>BossDefinition</c>, D-021): body and base health, player-count scaling (§9.10), phase
    /// thresholds, movement, aggro, weak point, attack pool, when it appears and how it grows on every return.
    /// Read-only at runtime.
    /// </summary>
    [CreateAssetMenu(menuName = "LastGround/Boss/Boss")]
    public sealed class BossDefinition : ScriptableObject
    {
        public string Id = "mutant_brute";
        public string NameKey = "boss.mutant_brute";
        /// <summary>Crowd type of the body (Behaviour = Boss); its MaxHealth is ignored.</summary>
        public ZombieDefinition Zombie;

        [Header("Health (TDD_01 §9.10)")]
        public float BaseHealth = 4000f;
        /// <summary>Health multiplier by player count (1–4).</summary>
        public float[] HealthByPlayers = { 1f, 1.7f, 2.3f, 2.9f };
        /// <summary>Extra health per earlier appearance this run (returns are tougher).</summary>
        public float HealthPerReturn = 0.35f;

        [Header("Phases")]
        [Range(0f, 1f)] public float Phase2At = 0.6f;
        [Range(0f, 1f)] public float EnragedAt = 0.25f;
        /// <summary>Walk speed in Phase 1, Phase 2, Enraged.</summary>
        public Vector3 MoveSpeed = new Vector3(1.6f, 2f, 2.6f);
        /// <summary>Cooldown multiplier in Phase 1, Phase 2, Enraged.</summary>
        public Vector3 CooldownScale = new Vector3(1f, 0.85f, 0.65f);
        /// <summary>Enraged: a Ground Slam is followed at once by a Charge (Frenzy).</summary>
        public bool FrenzyInEnraged = true;
        public float IntroSeconds = 3f;
        /// <summary>Walks up to this distance from its target, then waits for an attack.</summary>
        public float StopDistance = 3f;
        public float TurnDegreesPerSecond = 120f;

        [Header("Player count (TDD_01 §9.10)")]
        /// <summary>Extra summoned zombies by player count (1–4).</summary>
        public int[] ExtraSummonByPlayers = { 0, 1, 1, 2 };
        /// <summary>Attack cooldown multiplier by player count (1–4).</summary>
        public float[] CooldownByPlayers = { 1f, 1f, 0.9f, 0.85f };

        [Header("Aggro")]
        /// <summary>Forced target change every x..y seconds so every player has to move.</summary>
        public Vector2 AggroSwitch = new Vector2(8f, 12f);
        /// <summary>Aggro from damage dealt, per point.</summary>
        public float AggroPerDamage = 1f;
        /// <summary>Aggro for being close, per second at 0 m (falls to 0 at 15 m).</summary>
        public float AggroProximity = 40f;

        [Header("Weak point: tumour on the back")]
        public float WeakPointMultiplier = 2f;
        /// <summary>Shots whose direction points along the boss's facing (dot ≥ this) hit the back.</summary>
        [Range(-1f, 1f)] public float WeakPointDot = 0.35f;

        public BossAttackDefinition[] Attacks;

        [Header("Appearance")]
        /// <summary>First appearance at this threat level (TDD_01 §2.2: IV ≈ 12:00).</summary>
        public int FirstThreat = 4;
        /// <summary>Returns at this threat level and every level after it (§2.2: 22:00+).</summary>
        public int ReturnFromThreat = 6;
        public float SpawnDistance = 24f;
        /// <summary>Director spawn rate while the boss lives (§10 arena: 30–50 %).</summary>
        [Range(0f, 1f)] public float DirectorRateScale = 0.4f;

        [Header("Reward")]
        public int RewardCoinsPerPlayer = 40;
        public int RewardMedkits = 2;
        public bool RewardWeapon = true;
        public int RewardGrenades = 2;

        public float HealthFor(int players, int appearance)
        {
            int index = Mathf.Clamp(players, 1, HealthByPlayers.Length) - 1;
            return BaseHealth * HealthByPlayers[index] * (1f + HealthPerReturn * appearance);
        }

        public static int ForPlayers(int[] table, int players) => table[Mathf.Clamp(players, 1, table.Length) - 1];
        public static float ForPlayers(float[] table, int players) => table[Mathf.Clamp(players, 1, table.Length) - 1];
    }
}
