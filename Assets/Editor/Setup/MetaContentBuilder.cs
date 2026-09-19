using LastGround.Data.Meta;
using LastGround.Data.Upgrades;
using LastGround.Data.Weapons;
using UnityEditor;
using UnityEngine;

namespace LastGround.EditorTools.Setup
{
    /// <summary>
    /// M9 meta content (TDD_01 §14.7, TDD_03 §35, D-022): 2 characters with outfits, 5 net-zero perks, 5 loadouts,
    /// Arsenal weapons (2 to unlock), 3 emotes, 7 titles, and the catalog. Prices are hypotheses (Scrap = banked coins).
    /// Only missing assets are created; the catalog order is the network index — append only.
    /// </summary>
    static class MetaContentBuilder
    {
        const string Folder = "Assets/ScriptableObjects/Meta";
        /// <summary>In Resources: the app loads it at startup (the composition root has no scene fields).</summary>
        public const string CatalogPath = "Assets/Resources/Meta/META_Catalog.asset";
        const string Weapons = "Assets/ScriptableObjects/Weapons/";

        public static MetaCatalog Build()
        {
            PerkDefinition medic = Perk("field_medic", 0, true, StatId.ReviveSpeedPct, 30f, StatId.MaxHealth, -5f);
            PerkDefinition scavenger = Perk("scavenger", 0, true, StatId.PickupRadiusPct, 40f, StatId.ReloadSpeedPct, -8f);
            PerkDefinition grenadier = Perk("grenadier", 1000, false, StatId.BonusGrenades, 1f, StatId.ReserveAmmoPct, -20f);
            PerkDefinition sprinter = Perk("sprinter", 1000, false, StatId.MoveSpeedPct, 8f, StatId.DamageReductionPct, -8f);
            PerkDefinition sharpshooter = Perk("sharpshooter", 1200, false, StatId.CritChance, 5f, StatId.FireRatePct, -6f);

            CharacterDefinition ranger = Character("ranger", 0, true, "player_ranger", medic, new[]
            {
                Outfit("ranger_standard", 0, true, Color.white),
                Outfit("ranger_urban", 600, false, new Color(0.72f, 0.76f, 0.82f)),
                Outfit("ranger_desert", 600, false, new Color(1f, 0.9f, 0.72f)),
                Outfit("ranger_night", 800, false, new Color(0.5f, 0.52f, 0.58f)),
            });
            CharacterDefinition survivor = Character("survivor", 3000, false, "player_survivor", scavenger, new[]
            {
                Outfit("survivor_standard", 0, true, Color.white),
                Outfit("survivor_rust", 600, false, new Color(1f, 0.78f, 0.66f)),
                Outfit("survivor_forest", 800, false, new Color(0.72f, 0.86f, 0.7f)),
            });

            WeaponUnlock smg = WeaponItem("smg", "WPN_Smg", 0, true);
            WeaponUnlock rifle = WeaponItem("assault_rifle", "WPN_AssaultRifle", 0, true);
            WeaponUnlock shotgun = WeaponItem("shotgun", "WPN_Shotgun", 0, true);
            WeaponUnlock sniper = WeaponItem("sniper", "WPN_Sniper", 1500, false);
            WeaponUnlock machineGun = WeaponItem("machine_gun", "WPN_MachineGun", 2500, false);

            LoadoutDefinition assault = Loadout("assault", rifle.Weapon, 2);
            LoadoutDefinition breacher = Loadout("breacher", shotgun.Weapon, 2);
            LoadoutDefinition suppressor = Loadout("suppressor", smg.Weapon, 3);
            LoadoutDefinition marksman = Loadout("marksman", sniper.Weapon, 2);
            LoadoutDefinition heavy = Loadout("heavy", machineGun.Weapon, 1);

            EmoteDefinition wave = Emote("wave", 0, true, Data.Crowd.CrowdClipId.EmoteWave);
            EmoteDefinition salute = Emote("salute", 400, false, Data.Crowd.CrowdClipId.EmoteSalute);
            EmoteDefinition cheer = Emote("cheer", 600, false, Data.Crowd.CrowdClipId.EmoteCheer);

            var catalog = WeaponContentBuilder.Ensure<MetaCatalog>(CatalogPath, null);
            catalog.Characters = new[] { ranger, survivor };
            catalog.Perks = new[] { medic, scavenger, grenadier, sprinter, sharpshooter };
            catalog.Loadouts = new[] { assault, breacher, suppressor, marksman, heavy };
            catalog.Weapons = new[] { smg, rifle, shotgun, sniper, machineGun };
            catalog.Emotes = new[] { wave, salute, cheer };
            catalog.Titles = new[]
            {
                Title("first_extraction", ProfileStat.Extractions, 1),
                Title("boss_slayer", ProfileStat.BossKills, 1),
                Title("threat_five", ProfileStat.MaxExtractThreat, 5),
                Title("lifesaver", ProfileStat.Revives, 100),
                Title("reaper", ProfileStat.Kills, 5000),
                Title("twenty_minutes", ProfileStat.BestSurvivalSeconds, 1200),
                Title("veteran", ProfileStat.Runs, 25),
            };
            catalog.PerkCaps = new[]
            {
                Cap(StatId.MaxHealth, 10f), Cap(StatId.ReviveSpeedPct, 30f), Cap(StatId.PickupRadiusPct, 40f),
                Cap(StatId.ReloadSpeedPct, 10f), Cap(StatId.BonusGrenades, 1f), Cap(StatId.ReserveAmmoPct, 20f),
                Cap(StatId.MoveSpeedPct, 8f), Cap(StatId.DamageReductionPct, 8f), Cap(StatId.CritChance, 5f), Cap(StatId.FireRatePct, 6f),
            };
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        static T Item<T>(string prefix, string id, int price, bool owned, System.Action<T> init) where T : MetaItem =>
            WeaponContentBuilder.Ensure<T>(Folder + "/" + prefix + "_" + id + ".asset", item =>
            {
                item.Id = id;
                item.NameKey = "meta." + id;
                item.DescriptionKey = "meta." + id + ".desc";
                item.Price = price;
                item.OwnedByDefault = owned;
                init?.Invoke(item);
            });

        static PerkDefinition Perk(string id, int price, bool owned, StatId plus, float plusValue, StatId minus, float minusValue) =>
            Item<PerkDefinition>("PERK", id, price, owned, p =>
            {
                p.Plus = new StatModifier { Stat = plus, Value = plusValue };
                p.Minus = new StatModifier { Stat = minus, Value = minusValue };
            });

        static OutfitDefinition Outfit(string id, int price, bool owned, Color tint) =>
            Item<OutfitDefinition>("OUTFIT", id, price, owned, o => o.Tint = tint);

        static CharacterDefinition Character(string id, int price, bool owned, string body, PerkDefinition perk, OutfitDefinition[] outfits) =>
            Item<CharacterDefinition>("CHAR", id, price, owned, c =>
            {
                c.BodyId = body;
                c.DefaultPerk = perk;
                c.Outfits = outfits;
            });

        static WeaponUnlock WeaponItem(string id, string asset, int price, bool owned) =>
            Item<WeaponUnlock>("ARSENAL", id, price, owned, w => w.Weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(Weapons + asset + ".asset"));

        static LoadoutDefinition Loadout(string id, WeaponDefinition primary, int grenades) =>
            Item<LoadoutDefinition>("LOADOUT", id, 0, true, l =>
            {
                l.Primary = primary;
                l.Grenades = grenades;
            });

        static EmoteDefinition Emote(string id, int price, bool owned, Data.Crowd.CrowdClipId clip) =>
            Item<EmoteDefinition>("EMOTE", id, price, owned, e =>
            {
                e.Clip = (byte)clip;
                e.BubbleKey = "meta." + id + ".bubble";
            });

        static TitleDefinition Title(string id, ProfileStat stat, int threshold) =>
            Item<TitleDefinition>("TITLE", id, 0, false, t =>
            {
                t.Stat = stat;
                t.Threshold = threshold;
            });

        static MetaCatalog.PerkCap Cap(StatId stat, float max) => new MetaCatalog.PerkCap { Stat = stat, Max = max };
    }
}
