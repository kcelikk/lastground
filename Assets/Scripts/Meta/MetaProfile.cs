using System.Collections.Generic;
using LastGround.Data.Meta;
using LastGround.Save;

namespace LastGround.Meta
{
    /// <summary>
    /// The saved profile read through the catalog (TDD_01 §14.7): what is owned (bought or owned by default), what is
    /// equipped (with fallbacks when a stored id is unknown or not owned), and the equip actions. Persisting is the
    /// caller's job (ISaveService.RequestSave).
    /// </summary>
    public sealed class MetaProfile
    {
        public const int EmoteSlots = 3;

        readonly MetaCatalog _catalog;

        public MetaProfile(ProfileData data, MetaCatalog catalog)
        {
            Data = data;
            _catalog = catalog;
        }

        public ProfileData Data { get; }
        public MetaCatalog Catalog => _catalog;
        public int Scrap => Data.Scrap;

        public bool Owns(MetaItem item) =>
            item != null && (item.OwnedByDefault || Data.Owned.Contains(item.Id) || (item is TitleDefinition && Data.Badges.Contains(item.Id)));

        // ----- equipped (index into the catalog arrays) -----

        public int CharacterIndex => Equipped(_catalog.Characters, Data.Character);

        public CharacterDefinition Character => MetaCatalog.At(_catalog.Characters, CharacterIndex);

        public int PerkIndex
        {
            get
            {
                int index = MetaCatalog.IndexOf(_catalog.Perks, Data.Perk);
                if (index >= 0 && Owns(_catalog.Perks[index])) return index;
                CharacterDefinition character = Character;
                return character != null && character.DefaultPerk != null ? MetaCatalog.IndexOf(_catalog.Perks, character.DefaultPerk.Id) : -1;
            }
        }

        public int LoadoutIndex
        {
            get
            {
                int index = MetaCatalog.IndexOf(_catalog.Loadouts, Data.Loadout);
                if (index >= 0 && CanUse(_catalog.Loadouts[index])) return index;
                for (int i = 0; i < _catalog.Loadouts.Length; i++) if (CanUse(_catalog.Loadouts[i])) return i;
                return -1;
            }
        }

        /// <summary>-1 = no title.</summary>
        public int TitleIndex
        {
            get
            {
                int index = MetaCatalog.IndexOf(_catalog.Titles, Data.Title);
                return index >= 0 && Owns(_catalog.Titles[index]) ? index : -1;
            }
        }

        public int OutfitIndex(CharacterDefinition character)
        {
            if (character == null || character.Outfits == null || character.Outfits.Length == 0) return 0;
            string chosen = null;
            foreach (OutfitChoice choice in Data.Outfits)
                if (string.Equals(choice.Character, character.Id, System.StringComparison.Ordinal)) chosen = choice.Outfit;
            int index = MetaCatalog.IndexOf(character.Outfits, chosen);
            return index >= 0 && Owns(character.Outfits[index]) ? index : 0;
        }

        /// <summary>Bit per catalog weapon owned (joins the run's drop pool).</summary>
        public ushort OwnedWeaponMask
        {
            get
            {
                int mask = 0;
                for (int i = 0; i < _catalog.Weapons.Length && i < 16; i++) if (Owns(_catalog.Weapons[i])) mask |= 1 << i;
                return (ushort)mask;
            }
        }

        /// <summary>Equipped emotes that are owned, in wheel order (defaults fill empty slots).</summary>
        public void EquippedEmotes(List<EmoteDefinition> result)
        {
            result.Clear();
            foreach (string id in Data.Emotes)
            {
                int index = MetaCatalog.IndexOf(_catalog.Emotes, id);
                if (index >= 0 && Owns(_catalog.Emotes[index]) && !result.Contains(_catalog.Emotes[index])) result.Add(_catalog.Emotes[index]);
            }
            for (int i = 0; i < _catalog.Emotes.Length && result.Count < EmoteSlots; i++)
                if (Owns(_catalog.Emotes[i]) && !result.Contains(_catalog.Emotes[i])) result.Add(_catalog.Emotes[i]);
        }

        /// <summary>A loadout is usable when owned and its weapon is owned.</summary>
        public bool CanUse(LoadoutDefinition loadout)
        {
            if (!Owns(loadout)) return false;
            if (loadout.Primary == null) return true;
            foreach (WeaponUnlock weapon in _catalog.Weapons)
                if (weapon != null && weapon.Weapon == loadout.Primary) return Owns(weapon);
            return true;
        }

        // ----- equip -----

        public bool Equip(MetaItem item)
        {
            if (!Owns(item)) return false;
            switch (item)
            {
                case CharacterDefinition character: Data.Character = character.Id; return true;
                case PerkDefinition perk: Data.Perk = perk.Id; return true;
                case LoadoutDefinition loadout when CanUse(loadout): Data.Loadout = loadout.Id; return true;
                case TitleDefinition title: Data.Title = title.Id; return true;
                case EmoteDefinition emote:
                    if (Data.Emotes.Contains(emote.Id)) return true;
                    if (Data.Emotes.Count >= EmoteSlots) Data.Emotes.RemoveAt(0);
                    Data.Emotes.Add(emote.Id);
                    return true;
                default: return false;
            }
        }

        public bool EquipOutfit(CharacterDefinition character, OutfitDefinition outfit)
        {
            if (character == null || !Owns(outfit)) return false;
            foreach (OutfitChoice choice in Data.Outfits)
            {
                if (!string.Equals(choice.Character, character.Id, System.StringComparison.Ordinal)) continue;
                choice.Outfit = outfit.Id;
                return true;
            }
            Data.Outfits.Add(new OutfitChoice { Character = character.Id, Outfit = outfit.Id });
            return true;
        }

        int Equipped<T>(T[] items, string id) where T : MetaItem
        {
            int index = MetaCatalog.IndexOf(items, id);
            if (index >= 0 && Owns(items[index])) return index;
            for (int i = 0; i < items.Length; i++) if (Owns(items[i])) return i;
            return 0;
        }
    }
}
