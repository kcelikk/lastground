using UnityEngine;

namespace LastGround.Data.Loot
{
    /// <summary>
    /// Drops and pickups (TDD_01 §13). Coins are team loot (anyone's touch pays everyone); medkits, ammo, grenades and
    /// weapons are instanced (each player has their own copy, D-002). Coin drops within one cell merge into a single pile over a short window, so a big
    /// fight makes a few piles, not hundreds of objects.
    /// </summary>
    [CreateAssetMenu(menuName = "LastGround/Loot/Loot")]
    public sealed class LootDefinition : ScriptableObject
    {
        [Header("Coins (team, shared equally)")]
        public float CoinCellSize = 2f;
        public float CoinMergeWindow = 0.5f;
        public float CoinLifetime = 60f;

        [Header("Medkits (instanced per player)")]
        [Range(0f, 1f)] public float MedkitChance = 0.015f;
        public float MedkitHeal = 30f;
        public float MedkitLifetime = 45f;

        [Header("Ammo, grenades, weapons (instanced per player, M6)")]
        [Range(0f, 1f)] public float AmmoChance = 0.05f;
        public float AmmoLifetime = 45f;
        [Range(0f, 1f)] public float GrenadeChance = 0.008f;
        public float GrenadeLifetime = 45f;
        /// <summary>Chance an ordinary kill drops a weapon; elites always drop one.</summary>
        [Range(0f, 1f)] public float WeaponChance = 0.002f;
        public float WeaponLifetime = 60f;

        [Header("Collecting")]
        /// <summary>Base pickup radius; the Scavenger upgrade scales it.</summary>
        public float PickupRadius = 1.6f;
        /// <summary>Extra distance the host allows (the claimer's position is up to a few frames old).</summary>
        public float ClaimTolerance = 1.5f;
        public int MaxPickups = 256;
    }
}
