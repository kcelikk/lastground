using LastGround.Data.Upgrades;
using UnityEngine;

namespace LastGround.Data.Meta
{
    /// <summary>
    /// Everything the meta progression can unlock (TDD_01 §14.7, D-005) and its rules: Scrap per banked coin and the
    /// per-stat caps that keep perks net zero. Index order is the network index: append, never reorder.
    /// Read-only at runtime.
    /// </summary>
    [CreateAssetMenu(menuName = "LastGround/Meta/Catalog")]
    public sealed class MetaCatalog : ScriptableObject
    {
        public CharacterDefinition[] Characters;
        public PerkDefinition[] Perks;
        public LoadoutDefinition[] Loadouts;
        public WeaponUnlock[] Weapons;
        public EmoteDefinition[] Emotes;
        public TitleDefinition[] Titles;

        /// <summary>Scrap earned per coin banked at the end of a run.</summary>
        public float ScrapPerCoin = 1f;

        [System.Serializable]
        public struct PerkCap
        {
            public StatId Stat;
            /// <summary>Largest allowed |value| for this stat in a perk.</summary>
            public float Max;
        }

        /// <summary>Stats a perk may touch and how far (anything else is not allowed in a perk).</summary>
        public PerkCap[] PerkCaps;

        public static int IndexOf<T>(T[] items, string id) where T : MetaItem
        {
            if (items == null || string.IsNullOrEmpty(id)) return -1;
            for (int i = 0; i < items.Length; i++)
                if (items[i] != null && string.Equals(items[i].Id, id, System.StringComparison.Ordinal)) return i;
            return -1;
        }

        public static T At<T>(T[] items, int index) where T : MetaItem =>
            items != null && (uint)index < (uint)items.Length ? items[index] : null;
    }
}
