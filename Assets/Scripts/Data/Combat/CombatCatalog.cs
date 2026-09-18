using LastGround.Data.Weapons;
using LastGround.Data.Zombies;
using UnityEngine;

namespace LastGround.Data.Combat
{
    /// <summary>
    /// Every combat definition by wire id (M6): weapons by NetIndex, zombies by TypeIndex, projectiles by NetIndex,
    /// elite modifiers by NetIndex − 1, plus the starting loadout. One reference for installers and tests.
    /// </summary>
    [CreateAssetMenu(menuName = "LastGround/Combat/Catalog")]
    public sealed class CombatCatalog : ScriptableObject
    {
        public WeaponDefinition[] Weapons;
        public ZombieDefinition[] Zombies;
        public ProjectileDefinition[] Projectiles;
        public EliteModifierDefinition[] Elites;

        [Header("Starting loadout (TDD_01 §3.5)")]
        public WeaponDefinition StartPrimary;
        public WeaponDefinition StartSidearm;
        public ProjectileDefinition Grenade;
        public int StartGrenades = 2;
        public int MaxGrenades = 4;
        /// <summary>Grenade button tap: throw this far along the aim direction.</summary>
        public float TapThrowDistance = 8f;
        public float GrenadeCooldown = 0.8f;

        [Header("On-hit status effects from upgrades (TDD_01 §5.6)")]
        public float BurnSeconds = 3f;
        public float SlowSeconds = 1.5f;
        public float StunSeconds = 0.6f;
        /// <summary>Slows never go below this speed multiplier, however they stack.</summary>
        [Range(0.1f, 1f)] public float MinSlowMultiplier = 0.4f;

        public WeaponDefinition Weapon(int netIndex) => (uint)netIndex < (uint)Weapons.Length ? Weapons[netIndex] : null;
        public ZombieDefinition Zombie(int typeIndex) => (uint)typeIndex < (uint)Zombies.Length ? Zombies[typeIndex] : null;
        public ProjectileDefinition Projectile(int netIndex) => (uint)netIndex < (uint)Projectiles.Length ? Projectiles[netIndex] : null;

        /// <summary>Elite modifier by wire id (1-based); null for 0 or unknown.</summary>
        public EliteModifierDefinition Elite(int netIndex) => netIndex >= 1 && netIndex <= Elites.Length ? Elites[netIndex - 1] : null;
    }
}
