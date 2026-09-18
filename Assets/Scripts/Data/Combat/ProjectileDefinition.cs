using UnityEngine;

namespace LastGround.Data.Combat
{
    /// <summary>
    /// A simulated projectile (TDD_01 §6.3, TDD_02 ProjectileDefinition): grenade or zombie spit. Host simulates,
    /// clients replay the same closed-form flight from the spawn message. Read-only at runtime.
    /// </summary>
    [CreateAssetMenu(menuName = "LastGround/Combat/Projectile")]
    public sealed class ProjectileDefinition : ScriptableObject
    {
        public string Id = "frag_grenade";
        public string DisplayNameKey = "projectile.frag_grenade";

        /// <summary>Stable wire id (ProjectileSpawn). Never reuse a number.</summary>
        public byte NetIndex;

        public ProjectileMotion Motion = ProjectileMotion.Arc;

        [Header("Flight")]
        /// <summary>Straight: metres per second. Arc: horizontal speed used to derive the flight time.</summary>
        public float Speed = 14f;
        /// <summary>Arc: shortest and longest flight time.</summary>
        public Vector2 FlightTime = new Vector2(0.35f, 0.8f);
        /// <summary>Arc: apex height in metres at the longest throw (presentation only).</summary>
        public float ArcHeight = 2.5f;
        public float MaxRange = 12f;
        /// <summary>Straight: hits a player within this distance of its path.</summary>
        public float HitRadius = 0.5f;
        /// <summary>Arc: seconds between landing and the blast.</summary>
        public float FuseAfterLanding = 0.3f;

        [Header("Impact")]
        /// <summary>Straight: damage to the player it hits.</summary>
        public float ImpactDamage = 8f;
        /// <summary>Straight: player move speed multiplier after a hit.</summary>
        [Range(0.1f, 1f)] public float SlowMultiplier = 0.6f;
        public float SlowSeconds = 2f;
        public ExplosionSpec Explosion;

        [Header("Presentation")]
        public Color Color = new Color(0.25f, 0.28f, 0.2f);
        public float Size = 0.25f;
    }
}
