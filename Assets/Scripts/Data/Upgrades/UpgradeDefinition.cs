using UnityEngine;

namespace LastGround.Data.Upgrades
{
    /// <summary>
    /// A temporary run upgrade (TDD_01 §7.2, D-005: reset when the run ends). M5 upgrades each change one stat by a
    /// value that depends on the rarity rolled for the offer. Weapon modifiers, abilities and on-hit effects join in M6.
    /// </summary>
    [CreateAssetMenu(menuName = "LastGround/Upgrades/Upgrade")]
    public sealed class UpgradeDefinition : ScriptableObject
    {
        /// <summary>Stable id (save/analytics); the catalog index is the wire id.</summary>
        public string Id;
        public string NameKey;
        /// <summary>Localized format with one {0} for the value, e.g. "+{0}% damage".</summary>
        public string DescriptionKey;
        public StatId Stat;
        /// <summary>Value per rarity: Common, Rare, Epic, Legendary.</summary>
        public float[] Values = { 10f, 15f, 22f, 30f };
        public int MaxStacks = 5;
        public float Weight = 1f;

        public float ValueFor(UpgradeRarity rarity) => Values[Mathf.Clamp((int)rarity, 0, Values.Length - 1)];
    }
}
