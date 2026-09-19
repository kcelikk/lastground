using LastGround.Data.Meta;

namespace LastGround.Meta
{
    /// <summary>
    /// Buys meta items with Scrap (TDD_01 §14.7): price, prerequisite, already owned. Titles are earned, never bought.
    /// The caller saves afterwards.
    /// </summary>
    public sealed class UnlockService
    {
        readonly MetaProfile _profile;

        public UnlockService(MetaProfile profile)
        {
            _profile = profile;
        }

        public UnlockResult Check(MetaItem item)
        {
            if (item == null || item is TitleDefinition) return UnlockResult.NotForSale;
            if (_profile.Owns(item)) return UnlockResult.AlreadyOwned;
            if (!string.IsNullOrEmpty(item.Requires) && !OwnsId(item.Requires)) return UnlockResult.MissingRequirement;
            if (_profile.Scrap < item.Price) return UnlockResult.NotEnoughScrap;
            return UnlockResult.Unlocked;
        }

        public UnlockResult Unlock(MetaItem item)
        {
            UnlockResult result = Check(item);
            if (result != UnlockResult.Unlocked) return result;
            _profile.Data.Scrap -= item.Price;
            _profile.Data.Owned.Add(item.Id);
            return UnlockResult.Unlocked;
        }

        bool OwnsId(string id)
        {
            MetaCatalog c = _profile.Catalog;
            return Owned(c.Characters, id) || Owned(c.Perks, id) || Owned(c.Loadouts, id) || Owned(c.Weapons, id) || Owned(c.Emotes, id)
                   || OwnedOutfit(id);
        }

        bool Owned<T>(T[] items, string id) where T : MetaItem
        {
            int index = MetaCatalog.IndexOf(items, id);
            return index >= 0 && _profile.Owns(items[index]);
        }

        bool OwnedOutfit(string id)
        {
            foreach (CharacterDefinition character in _profile.Catalog.Characters)
                if (character != null && Owned(character.Outfits, id)) return true;
            return false;
        }
    }
}
