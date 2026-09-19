using System;
using LastGround.Core.Net.Session;
using LastGround.Data.Meta;
using LastGround.Gameplay.Meta;
using LastGround.Gameplay.Run;
using LastGround.Meta;
using LastGround.Save;

namespace LastGround.App
{
    /// <summary>
    /// App-lifetime meta progression (M9): the profile read through the catalog, unlocks, run banking, and the packed
    /// selection this player shows in lobbies. Every change is saved (debounced) and pushed to the session.
    /// </summary>
    public sealed class MetaService : IMetaStore
    {
        readonly ISaveService _save;
        ISession _session;

        public MetaService(ISaveService save, MetaCatalog catalog)
        {
            _save = save;
            Catalog = catalog;
            Profile = new MetaProfile(save.Profile, catalog);
            Unlocks = new UnlockService(Profile);
            Progression = new MetaProgressionService(Profile);
        }

        public MetaCatalog Catalog { get; }
        public MetaProfile Profile { get; }
        public UnlockService Unlocks { get; }
        public MetaProgressionService Progression { get; }

        /// <summary>Scrap, ownership or equipment changed (menus refresh).</summary>
        public event Action Changed;

        /// <summary>The last run's banking, for the results screen (null before the first run).</summary>
        public BankReport LastReport { get; private set; }

        /// <summary>Dev (-lg-character): owns and equips a character without Scrap.</summary>
        public void DevEquipCharacter(string id)
        {
            int index = MetaCatalog.IndexOf(Catalog.Characters, id);
            if (index < 0) return;
            CharacterDefinition character = Catalog.Characters[index];
            if (!Profile.Owns(character)) Profile.Data.Owned.Add(character.Id);
            Equip(character);
        }

        public void Attach(ISession session)
        {
            _session = session;
            _session.SetLocalMeta(Packed());
        }

        public PlayerMeta Selection()
        {
            CharacterDefinition character = Profile.Character;
            int perk = Profile.PerkIndex, loadout = Profile.LoadoutIndex, title = Profile.TitleIndex;
            return new PlayerMeta
            {
                Character = (byte)Math.Max(0, Profile.CharacterIndex),
                Outfit = (byte)Profile.OutfitIndex(character),
                Perk = perk >= 0 ? (byte)perk : PlayerMeta.None,
                Loadout = loadout >= 0 ? (byte)loadout : PlayerMeta.None,
                Title = title >= 0 ? (byte)title : PlayerMeta.None,
                OwnedWeapons = Profile.OwnedWeaponMask,
            };
        }

        public ulong Packed() => Selection().Pack();

        public UnlockResult Unlock(MetaItem item)
        {
            UnlockResult result = Unlocks.Unlock(item);
            if (result == UnlockResult.Unlocked) Commit();
            return result;
        }

        public bool Equip(MetaItem item)
        {
            if (!Profile.Equip(item)) return false;
            Commit();
            return true;
        }

        public bool EquipOutfit(CharacterDefinition character, OutfitDefinition outfit)
        {
            if (!Profile.EquipOutfit(character, outfit)) return false;
            Commit();
            return true;
        }

        /// <summary>Banks this player's share of a finished run once (results screen).</summary>
        public BankReport Bank(in RunResult result, int localPlayer)
        {
            LastReport = Progression.Bank(new RunSummary
            {
                BankedCoins = result.BankedFor(localPlayer),
                Extracted = result.Extracted,
                MaxThreat = result.MaxThreat,
                SurvivalSeconds = result.SurvivalSeconds,
                Kills = result.Kills,
                Revives = result.Revives,
                BossKills = result.BossKills,
            });
            _save.SaveNow();
            Changed?.Invoke();
            return LastReport;
        }

        void Commit()
        {
            _save.RequestSave();
            _session?.SetLocalMeta(Packed());
            Changed?.Invoke();
        }
    }
}
