using UnityEngine;

namespace LastGround.Data.Combat
{
    /// <summary>
    /// Elite = zombie type + one modifier (TDD_01 §9.8): more health, a glow and one extra trait. Read-only at runtime.
    /// </summary>
    [CreateAssetMenu(menuName = "LastGround/Combat/Elite Modifier")]
    public sealed class EliteModifierDefinition : ScriptableObject
    {
        public string Id = "armored";
        public string DisplayNameKey = "elite.armored";

        /// <summary>Wire id, 1-based (0 = not elite). Never reuse a number.</summary>
        public byte NetIndex = 1;

        public float HealthMultiplier = 4f;
        public float SpeedMultiplier = 1f;
        /// <summary>Incoming damage multiplier (Armored &lt; 1).</summary>
        [Range(0.1f, 1f)] public float DamageTakenMultiplier = 1f;
        public bool KnockbackImmune;

        [Header("Toxic")]
        /// <summary>Its hits slow the player (1 = no slow).</summary>
        [Range(0.1f, 1f)] public float AttackSlowMultiplier = 1f;
        public float AttackSlowSeconds;
        public float AttackDamageMultiplier = 1f;

        [Header("Volatile")]
        public ExplosionSpec DeathExplosion;

        [Header("Presentation")]
        public Color Glow = new Color(1f, 0.8f, 0.3f);
    }
}
