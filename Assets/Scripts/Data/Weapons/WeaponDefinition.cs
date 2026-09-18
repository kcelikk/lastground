using UnityEngine;

namespace LastGround.Data.Weapons
{
    /// <summary>
    /// Weapon balance and behaviour (TDD_01 §6.1). Read-only at runtime; <see cref="NetIndex"/> is the wire id.
    /// Values are the §6.4 placeholders. Reserve ammo, projectiles and visual profiles arrive with M6.
    /// </summary>
    [CreateAssetMenu(menuName = "LastGround/Weapons/Weapon")]
    public sealed class WeaponDefinition : ScriptableObject
    {
        public string Id = "assault_rifle";
        public string DisplayNameKey = "weapon.assault_rifle";

        /// <summary>Stable wire id (HitClaim). Never reuse a number.</summary>
        public byte NetIndex;

        public WeaponFireMode FireMode = WeaponFireMode.Hitscan;

        [Header("Damage")]
        public float Damage = 16f;
        [Range(0f, 1f)] public float CritChance = 0.08f;
        public float CritMultiplier = 2f;
        /// <summary>Extra zombies a bullet passes through (0 = stops at the first).</summary>
        public int Penetration = 1;
        /// <summary>Velocity impulse given to a hit zombie, m/s.</summary>
        public float Knockback = 1.2f;

        [Header("Handling")]
        /// <summary>Shots per second.</summary>
        public float FireRate = 8f;
        public int MagazineSize = 30;
        public float ReloadTime = 1.8f;
        public float Range = 30f;
        public float SpreadDeg = 3f;
        public int PelletCount = 1;
        [Range(0f, 1f)] public float MoveSpeedMultiplierWhileFiring = 1f;

        [Header("Aim assist")]
        /// <summary>Half-angle of the soft aim assist cone in degrees (TDD_01 §3.3).</summary>
        public float AimAssistConeDeg = 10f;
        [Range(0f, 1f)] public float AimAssistStrength = 0.6f;

        /// <summary>Seconds between shots.</summary>
        public float ShotInterval => FireRate > 0f ? 1f / FireRate : 1f;

        /// <summary>Most claims one shot can produce (pellets × pierced targets).</summary>
        public int MaxClaimsPerShot => Mathf.Max(1, PelletCount) * (Mathf.Max(0, Penetration) + 1);
    }
}
