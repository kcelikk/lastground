using System.Collections.Generic;

namespace LastGround.Save
{
    /// <summary>
    /// Permanent meta profile (profile.json, TDD_02 §24): Scrap, bought unlocks, earned badges, equipped choices and
    /// lifetime statistics. Ids are the stable MetaItem ids. Nothing run-scoped is stored here (D-005).
    /// Lists only (no dictionaries) so IL2CPP needs no extra AOT generics for Newtonsoft.
    /// </summary>
    public sealed class ProfileData
    {
        public const int CurrentVersion = 1;

        public int Version = CurrentVersion;
        public int Scrap;
        /// <summary>Bought items (defaults are owned without being listed).</summary>
        public List<string> Owned = new List<string>();
        /// <summary>Earned badges (title ids).</summary>
        public List<string> Badges = new List<string>();

        public string Character;
        public string Perk;
        public string Loadout;
        public string Title;
        /// <summary>Outfit chosen per character.</summary>
        public List<OutfitChoice> Outfits = new List<OutfitChoice>();
        /// <summary>Equipped emotes (emote wheel order).</summary>
        public List<string> Emotes = new List<string>();

        public ProfileStats Stats = new ProfileStats();
    }
}
