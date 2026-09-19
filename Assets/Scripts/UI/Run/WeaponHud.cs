using LastGround.Core.Services;
using LastGround.Data.Weapons;
using LastGround.Gameplay.Combat;
using LastGround.Gameplay.Loot;
using LastGround.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastGround.UI.Run
{
    /// <summary>
    /// Weapon controls around the aim stick (TDD_01 §3.5, §0.9): the weapon in hand, the swap button showing the other
    /// slot, the grenade button with its count, and a "take" button when a weapon pickup is in reach. The swap and
    /// grenade buttons are hit-tested by the touch input; this only draws them. Text changes only when values change.
    /// </summary>
    public sealed class WeaponHud : MonoBehaviour
    {
        [SerializeField] TMP_Text _weaponName;
        [SerializeField] GameObject _swapButton;
        [SerializeField] TMP_Text _swapLabel;
        [SerializeField] GameObject _grenadeButton;
        [SerializeField] TMP_Text _grenadeCount;
        [SerializeField] Button _takeButton;
        [SerializeField] TMP_Text _takeLabel;

        IWeaponStatus _weapon;
        PickupCollector _collector;
        PickupTable _pickups;
        LoadoutTable _loadouts;
        ILocalizationService _localization;
        WeaponDefinition _shownActive, _shownOther;
        int _shownGrenades = -1;
        int _shownTake = -2;
        string _take;

        public RectTransform TakeButtonRect => (RectTransform)_takeButton.transform;

        public void Bind(IWeaponStatus weapon, PickupCollector collector, PickupTable pickups, LoadoutTable loadouts)
        {
            _weapon = weapon;
            _collector = collector;
            _pickups = pickups;
            _loadouts = loadouts;
            _localization = AppServices.Get<ILocalizationService>();
            _localization.LanguageChanged += OnLanguageChanged;
            _takeButton.onClick.AddListener(() => _collector?.TakeWeapon());
            LoadTexts();
        }

        void OnDestroy()
        {
            if (_localization != null) _localization.LanguageChanged -= OnLanguageChanged;
        }

        void Update()
        {
            if (_weapon == null) return;
            if (_weapon.Active != _shownActive)
            {
                _shownActive = _weapon.Active;
                _weaponName.text = _shownActive != null ? _localization.Get(_shownActive.DisplayNameKey) : string.Empty;
            }
            if (_weapon.Other != _shownOther)
            {
                _shownOther = _weapon.Other;
                _swapButton.SetActive(_shownOther != null);
                if (_shownOther != null) _swapLabel.text = _localization.Get(_shownOther.DisplayNameKey);
            }
            if (_weapon.Grenades != _shownGrenades)
            {
                _shownGrenades = _weapon.Grenades;
                _grenadeCount.SetText("{0}", _shownGrenades);
            }
            int take = _collector != null ? _collector.NearbyWeapon : -1;
            if (take == _shownTake) return;
            _shownTake = take;
            bool show = take >= 0;
            _takeButton.gameObject.SetActive(show);
            if (!show) return;
            WeaponDefinition offered = _loadouts.Weapon(_pickups.Value[take]);
            _takeLabel.text = offered != null ? _take + "  " + _localization.Get(offered.DisplayNameKey) : _take;
        }

        void OnLanguageChanged(string language)
        {
            LoadTexts();
            _shownActive = _shownOther = null;
            _shownTake = -2;
        }

        void LoadTexts() => _take = _localization.Get("hud.take_weapon");
    }
}
