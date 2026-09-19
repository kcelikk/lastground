using System;
using LastGround.Data.Meta;

namespace LastGround.Meta
{
    /// <summary>What menus need from the meta progression (implemented by the app, which also saves and syncs).</summary>
    public interface IMetaStore
    {
        MetaCatalog Catalog { get; }
        MetaProfile Profile { get; }
        /// <summary>Scrap gained and badges earned by the last run (null before one).</summary>
        BankReport LastReport { get; }
        event Action Changed;
        UnlockResult Unlock(MetaItem item);
        bool Equip(MetaItem item);
        bool EquipOutfit(CharacterDefinition character, OutfitDefinition outfit);
    }
}
