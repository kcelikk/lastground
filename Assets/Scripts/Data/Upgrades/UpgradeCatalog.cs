using UnityEngine;

namespace LastGround.Data.Upgrades
{
    /// <summary>All run upgrades and the offer rules. The array index is the network id (append only).</summary>
    [CreateAssetMenu(menuName = "LastGround/Upgrades/Catalog")]
    public sealed class UpgradeCatalog : ScriptableObject
    {
        public UpgradeDefinition[] Upgrades;

        [Header("Rarity (TDD_01 §7.3)")]
        public float[] RarityWeights = { 60f, 28f, 10f, 2f };
        public Color[] RarityColors =
        {
            new Color(0.8f, 0.8f, 0.8f), new Color(0.3f, 0.6f, 1f), new Color(0.7f, 0.35f, 1f), new Color(1f, 0.7f, 0.15f),
        };
        public string[] RarityKeys = { "upgrade.rarity.common", "upgrade.rarity.rare", "upgrade.rarity.epic", "upgrade.rarity.legendary" };

        public int ChoicesPerOffer = 3;
    }
}
