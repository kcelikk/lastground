using UnityEngine;

namespace LastGround.Data.Meta
{
    /// <summary>
    /// Common part of every permanent unlock (TDD_01 §14.7, TDD_02 <c>UnlockDefinition</c>): stable id (the save key),
    /// display name, Scrap price and whether a new profile owns it. Nothing here grants power (D-005).
    /// </summary>
    public abstract class MetaItem : ScriptableObject
    {
        /// <summary>Stable id stored in profile.json: never rename.</summary>
        public string Id;
        public string NameKey;
        public string DescriptionKey;
        [Min(0)] public int Price;
        public bool OwnedByDefault;
        /// <summary>Id of an item that must be owned first (empty = none).</summary>
        public string Requires;
    }
}
