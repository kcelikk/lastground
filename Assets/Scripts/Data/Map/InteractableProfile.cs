using LastGround.Data.Combat;
using UnityEngine;

namespace LastGround.Data.Map
{
    /// <summary>Balance of the map interactables (TDD_01 §12.3). Read-only at runtime.</summary>
    [CreateAssetMenu(menuName = "LastGround/Map/Interactables")]
    public sealed class InteractableProfile : ScriptableObject
    {
        [Header("Explosive barrel")]
        public float BarrelHealth = 30f;
        public ExplosionSpec BarrelBlast = new ExplosionSpec
        {
            Radius = 4.5f, Damage = 130f, EdgeDamageScale = 0.3f, Knockback = 7f, StunSeconds = 0.8f, PlayerDamageScale = 0.35f,
        };

        [Header("Fuel tank")]
        public float TankHealth = 60f;
        public ExplosionSpec TankBlast = new ExplosionSpec
        {
            Radius = 6.5f, Damage = 220f, EdgeDamageScale = 0.3f, Knockback = 9f, StunSeconds = 1.2f, PlayerDamageScale = 0.35f,
        };
        /// <summary>Destroyed barrels and tanks come back after this long (away from the players' eyes is not checked).</summary>
        public float RespawnSeconds = 120f;
        /// <summary>Bullets hit a barrel within this distance of its centre.</summary>
        public float HitRadius = 0.45f;

        [Header("Ammo crate (instanced cooldown per player)")]
        public float AmmoRadius = 2f;
        public float AmmoCooldown = 45f;

        [Header("Medical station")]
        public float MedRadius = 2.2f;
        public float MedHealPerSecond = 12f;
        /// <summary>Healing the station can give before it runs dry.</summary>
        public float MedCharge = 150f;
        public float MedRechargePerSecond = 2f;
    }
}
