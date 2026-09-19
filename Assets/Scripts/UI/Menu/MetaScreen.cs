using System.Collections.Generic;
using System.Globalization;
using LastGround.Core.Services;
using LastGround.Data.Meta;
using LastGround.Data.Upgrades;
using LastGround.Localization;
using LastGround.Meta;
using LastGround.Save;
using LastGround.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastGround.UI.Menu
{
    /// <summary>
    /// "Preparation" screen (M9, TDD_01 §14.7): tabs for characters, outfits, perks, loadouts, the Arsenal (weapons),
    /// emotes and titles. Each row shows the item, what it does (perks: the advantage and the drawback) and one action:
    /// equip, equipped, unlock for Scrap, or locked. The right column shows Scrap and lifetime statistics.
    /// Menu-time only: rows are instantiated from a template (no gameplay hot path here).
    /// </summary>
    public sealed class MetaScreen : MonoBehaviour
    {
        enum Tab
        {
            Characters,
            Outfits,
            Perks,
            Loadouts,
            Weapons,
            Emotes,
            Titles,
        }

        static readonly string[] TabKeys =
        {
            "meta.tab.characters", "meta.tab.outfits", "meta.tab.perks", "meta.tab.loadouts", "meta.tab.weapons", "meta.tab.emotes",
            "meta.tab.titles",
        };

        static readonly Color Equipped = new Color(0.25f, 0.55f, 0.3f, 0.95f);
        static readonly Color Buyable = new Color(0.78f, 0.12f, 0.1f, 0.95f);
        static readonly Color Locked = new Color(0.2f, 0.2f, 0.22f, 0.95f);
        static readonly Color Equip = new Color(0.16f, 0.18f, 0.22f, 0.95f);

        [SerializeField] Button[] _tabButtons;
        [SerializeField] RectTransform _list;
        [SerializeField] GameObject _rowTemplate;
        [SerializeField] TMP_Text _scrapLabel;
        [SerializeField] TMP_Text _statsLabel;
        [SerializeField] TMP_Text _statusLabel;
        [SerializeField] Button _backButton;
        [SerializeField] ScreenRouter _router;
        [SerializeField] GameObject _mainScreen;

        readonly List<GameObject> _rows = new List<GameObject>();
        IMetaStore _meta;
        ILocalizationService _localization;
        Tab _tab;

        void Awake()
        {
            _meta = AppServices.Get<IMetaStore>();
            _localization = AppServices.Get<ILocalizationService>();
            _rowTemplate.SetActive(false);
            for (int i = 0; i < _tabButtons.Length; i++)
            {
                var tab = (Tab)i;
                _tabButtons[i].onClick.AddListener(() => Select(tab));
            }
            _backButton.onClick.AddListener(() => _router.Show(_mainScreen));
        }

        void OnEnable()
        {
            _meta.Changed += Rebuild;
            _localization.LanguageChanged += OnLanguage;
            _statusLabel.text = string.Empty;
            Rebuild();
        }

        void OnDisable()
        {
            _meta.Changed -= Rebuild;
            _localization.LanguageChanged -= OnLanguage;
        }

        void OnLanguage(string language) => Rebuild();

        void Select(Tab tab)
        {
            _tab = tab;
            _statusLabel.text = string.Empty;
            Rebuild();
        }

        void Rebuild()
        {
            for (int i = 0; i < _tabButtons.Length && i < TabKeys.Length; i++)
            {
                _tabButtons[i].GetComponentInChildren<TMP_Text>().text = _localization.Get(TabKeys[i]);
                _tabButtons[i].GetComponent<Image>().color = i == (int)_tab ? Buyable : Equip;
            }
            foreach (GameObject row in _rows) Destroy(row);
            _rows.Clear();

            MetaProfile profile = _meta.Profile;
            MetaCatalog catalog = _meta.Catalog;
            switch (_tab)
            {
                case Tab.Characters:
                    for (int i = 0; i < catalog.Characters.Length; i++) AddRow(catalog.Characters[i], i == profile.CharacterIndex, null);
                    break;
                case Tab.Outfits:
                    CharacterDefinition character = profile.Character;
                    int outfit = profile.OutfitIndex(character);
                    for (int i = 0; i < character.Outfits.Length; i++) AddRow(character.Outfits[i], i == outfit, character);
                    break;
                case Tab.Perks:
                    for (int i = 0; i < catalog.Perks.Length; i++) AddRow(catalog.Perks[i], i == profile.PerkIndex, null);
                    break;
                case Tab.Loadouts:
                    for (int i = 0; i < catalog.Loadouts.Length; i++) AddRow(catalog.Loadouts[i], i == profile.LoadoutIndex, null);
                    break;
                case Tab.Weapons:
                    foreach (WeaponUnlock weapon in catalog.Weapons) AddRow(weapon, false, null);
                    break;
                case Tab.Emotes:
                    var equipped = new List<EmoteDefinition>();
                    profile.EquippedEmotes(equipped);
                    foreach (EmoteDefinition emote in catalog.Emotes) AddRow(emote, equipped.Contains(emote), null);
                    break;
                default:
                    for (int i = 0; i < catalog.Titles.Length; i++) AddRow(catalog.Titles[i], i == profile.TitleIndex, null);
                    break;
            }
            RefreshSide();
        }

        void AddRow(MetaItem item, bool equipped, CharacterDefinition outfitOwner)
        {
            GameObject row = Instantiate(_rowTemplate, _list);
            row.SetActive(true);
            _rows.Add(row);
            TMP_Text[] texts = row.GetComponentsInChildren<TMP_Text>(true);
            texts[0].text = _localization.Get(item.NameKey);
            texts[1].text = Describe(item);
            Button action = row.GetComponentInChildren<Button>(true);
            TMP_Text actionLabel = action.GetComponentInChildren<TMP_Text>(true);
            Image actionImage = action.GetComponent<Image>();

            bool owned = _meta.Profile.Owns(item);
            bool equippable = !(item is WeaponUnlock) && owned;
            if (item is LoadoutDefinition loadout) equippable = _meta.Profile.CanUse(loadout);
            if (equipped)
            {
                actionLabel.text = _localization.Get("meta.equipped");
                actionImage.color = Equipped;
                action.interactable = false;
            }
            else if (equippable)
            {
                actionLabel.text = _localization.Get("meta.equip");
                actionImage.color = Equip;
                action.onClick.AddListener(() =>
                {
                    if (outfitOwner != null) _meta.EquipOutfit(outfitOwner, (OutfitDefinition)item);
                    else _meta.Equip(item);
                });
            }
            else if (owned)
            {
                actionLabel.text = _localization.Get(item is WeaponUnlock ? "meta.owned" : "meta.needs_weapon");
                actionImage.color = Locked;
                action.interactable = false;
            }
            else if (item is TitleDefinition title)
            {
                actionLabel.text = string.Format(CultureInfo.InvariantCulture, "{0}/{1}",
                    MetaProgressionService.Value(_meta.Profile.Data.Stats, title.Stat), title.Threshold);
                actionImage.color = Locked;
                action.interactable = false;
            }
            else
            {
                actionLabel.text = string.Format(CultureInfo.InvariantCulture, _localization.Get("meta.unlock"), item.Price);
                actionImage.color = _meta.Profile.Scrap >= item.Price ? Buyable : Locked;
                action.onClick.AddListener(() => Buy(item));
            }
        }

        void Buy(MetaItem item)
        {
            UnlockResult result = _meta.Unlock(item);
            _statusLabel.text = result == UnlockResult.Unlocked ? string.Empty : _localization.Get("meta.result." + ResultKey(result));
        }

        static string ResultKey(UnlockResult result)
        {
            switch (result)
            {
                case UnlockResult.NotEnoughScrap: return "not_enough";
                case UnlockResult.MissingRequirement: return "requires";
                default: return "unavailable";
            }
        }

        string Describe(MetaItem item)
        {
            switch (item)
            {
                case PerkDefinition perk:
                    return Modifier(perk.Plus) + "   " + Modifier(perk.Minus);
                case LoadoutDefinition loadout:
                    return string.Format(CultureInfo.InvariantCulture, _localization.Get("meta.loadout_line"),
                        loadout.Primary != null ? _localization.Get(loadout.Primary.DisplayNameKey) : "-", loadout.Grenades);
                default:
                    return string.IsNullOrEmpty(item.DescriptionKey) ? string.Empty : _localization.Get(item.DescriptionKey);
            }
        }

        string Modifier(StatModifier modifier)
        {
            string sign = modifier.Value > 0f ? "+" : "−";
            float value = Mathf.Abs(modifier.Value);
            string unit = modifier.Stat == StatId.MaxHealth || modifier.Stat == StatId.BonusGrenades ? string.Empty : "%";
            return sign + value.ToString("0.#", CultureInfo.InvariantCulture) + unit + " " + _localization.Get("stat." + StatKey(modifier.Stat));
        }

        static string StatKey(StatId stat)
        {
            switch (stat)
            {
                case StatId.MaxHealth: return "max_health";
                case StatId.MoveSpeedPct: return "move_speed";
                case StatId.FireRatePct: return "fire_rate";
                case StatId.ReloadSpeedPct: return "reload_speed";
                case StatId.CritChance: return "crit_chance";
                case StatId.DamageReductionPct: return "damage_reduction";
                case StatId.PickupRadiusPct: return "pickup_radius";
                case StatId.ReviveSpeedPct: return "revive_speed";
                case StatId.BonusGrenades: return "bonus_grenades";
                case StatId.ReserveAmmoPct: return "reserve_ammo";
                default: return "other";
            }
        }

        void RefreshSide()
        {
            _scrapLabel.text = string.Format(CultureInfo.InvariantCulture, _localization.Get("meta.scrap"), _meta.Profile.Scrap);
            ProfileStats s = _meta.Profile.Data.Stats;
            _statsLabel.text = string.Format(CultureInfo.InvariantCulture, _localization.Get("meta.stats"), s.Runs, s.Extractions,
                s.BossKills, s.Kills, s.Revives, s.BestSurvivalSeconds / 60, s.BestSurvivalSeconds % 60, s.MaxThreat);
        }
    }
}
