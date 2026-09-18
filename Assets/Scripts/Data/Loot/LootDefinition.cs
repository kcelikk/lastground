using UnityEngine;

namespace LastGround.Data.Loot
{
    /// <summary>
    /// Drops and pickups (TDD_01 §13). Coins are team loot (anyone's touch pays everyone); medkits are instanced (each
    /// player has their own copy). Coin drops within one cell merge into a single pile over a short window, so a big
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

        [Header("Collecting")]
        /// <summary>Base pickup radius; the Scavenger upgrade scales it.</summary>
        public float PickupRadius = 1.6f;
        /// <summary>Extra distance the host allows (the claimer's position is up to a few frames old).</summary>
        public float ClaimTolerance = 1.5f;
        public int MaxPickups = 256;
    }
}
