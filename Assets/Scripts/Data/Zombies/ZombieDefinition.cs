using UnityEngine;

namespace LastGround.Data.Zombies
{
    /// <summary>
    /// Balance of one zombie type (TDD_01 §8.5). M4 has only the Walker; Runner, Tank, Spitter and Exploder
    /// arrive in M6. Read-only at runtime.
    /// </summary>
    [CreateAssetMenu(menuName = "LastGround/Zombies/Zombie")]
    public sealed class ZombieDefinition : ScriptableObject
    {
        public string Id = "walker";

        /// <summary>Wire/type id (CrowdState.Type).</summary>
        public byte TypeIndex;

        public float MaxHealth = 45f;
        /// <summary>Team XP for a kill (TDD_01 §13.1: shared equally).</summary>
        public int Xp = 1;
        /// <summary>Chance a kill drops coin (aggregated into piles, TDD_01 §13.1).</summary>
        [Range(0f, 1f)] public float CoinChance = 0.35f;
        public int CoinValue = 1;
        public float MinSpeed = 1.3f;
        public float MaxSpeed = 2.1f;

        [Header("Attack (TDD_01 §5.5)")]
        public float AttackDamage = 5f;
        /// <summary>Telegraph before the hit lands; moving out of reach during it dodges the attack.</summary>
        public float AttackWindup = 0.45f;
        public float AttackCooldown = 1.6f;
        /// <summary>Extra distance beyond the attack start range within which the hit still lands.</summary>
        public float AttackReachGrace = 0.35f;

        [Header("Knockback")]
        /// <summary>1 = full weapon knockback, 0 = immune (Tank).</summary>
        [Range(0f, 1f)] public float KnockbackScale = 1f;
    }
}
