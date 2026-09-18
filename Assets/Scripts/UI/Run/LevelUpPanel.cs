using LastGround.Core.Services;
using LastGround.Data.Upgrades;
using LastGround.Gameplay.Upgrades;
using LastGround.Localization;
using LastGround.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastGround.UI.Run
{
    /// <summary>
    /// Compact level-up panel (TDD_01 §7.5): three upgrade cards at the bottom centre while the game keeps running.
    /// Can be minimised to a "LEVEL UP +N" badge and reopened. In a solo run it asks the game to pause while open
    /// (setting); in co-op it never pauses. Development builds can auto-pick for soak tests.
    /// </summary>
    public sealed class LevelUpPanel : MonoBehaviour
    {
        [SerializeField] GameObject _panel;
        [SerializeField] RectTransform _panelRect;
        [SerializeField] TMP_Text _title;
        [SerializeField] Button[] _cards;
        [SerializeField] Image[] _cardBands;
        [SerializeField] TMP_Text[] _cardNames;
        [SerializeField] TMP_Text[] _cardValues;
        [SerializeField] TMP_Text[] _cardRarities;
        [SerializeField] Button _minimize;
        [SerializeField] Button _badge;
        [SerializeField] TMP_Text _badgeLabel;

        readonly CharLine _line = new CharLine(96);
        LocalOffers _offers;
        UpgradeCatalog _catalog;
        ILocalizationService _localization;
        System.Func<bool> _isSolo;
        bool _minimized;
        ushort _shownOffer;
        int _shownCount = -1;
        float _autoPickTimer;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Soak tests: pick the rarest card automatically after a second.</summary>
        public static bool DevAutoPick;
#endif

        /// <summary>Picks made by tapping a card vs by the dev auto-pick (telemetry).</summary>
        public static int TappedPicks;
        public static int AutoPicks;

        /// <summary>True while the panel is open in a solo run (the installer pauses the simulation).</summary>
        public bool WantsPause => _isSolo != null && LevelUpPause.ShouldPause(_isSolo() ? 1 : 2, _panel.activeSelf, PauseInSolo);

        /// <summary>Settings: pause a solo run while choosing (TDD_01 §7.5, default on).</summary>
        public bool PauseInSolo { get; set; } = true;

        /// <summary>The panel's screen rectangle, so touches on it do not also move the sticks.</summary>
        public RectTransform BlockingRect => _panel.activeSelf ? _panelRect : null;

        public void Bind(LocalOffers offers, UpgradeCatalog catalog, System.Func<bool> isSolo)
        {
            _offers = offers;
            _catalog = catalog;
            _isSolo = isSolo;
            _localization = AppServices.Get<ILocalizationService>();
            for (int i = 0; i < _cards.Length; i++)
            {
                int choice = i;
                _cards[i].onClick.AddListener(() =>
                {
                    TappedPicks++;
                    Pick(choice);
                });
            }
            _minimize.onClick.AddListener(() => _minimized = true);
            _badge.onClick.AddListener(() => _minimized = false);
            _title.text = _localization.Get("levelup.title");
            _panel.SetActive(false);
            _badge.gameObject.SetActive(false);
        }

        void Update()
        {
            if (_offers == null) return;
            bool has = _offers.HasOffer;
            bool open = has && !_minimized;
            if (_panel.activeSelf != open) _panel.SetActive(open);
            bool badge = has && _minimized;
            if (_badge.gameObject.activeSelf != badge) _badge.gameObject.SetActive(badge);
            if (!has)
            {
                _minimized = false;
                return;
            }
            if (badge && _offers.Count != _shownCount)
            {
                _shownCount = _offers.Count;
                _badgeLabel.SetText(_localization.Get("levelup.badge"), _offers.Count);
            }
            if (open && _offers.Current.Id != _shownOffer) Show(_offers.Current);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (DevAutoPick)
            {
                _autoPickTimer += Time.unscaledDeltaTime;
                if (_autoPickTimer > 1f)
                {
                    AutoPicks++;
                    Pick(RarestChoice(_offers.Current));
                }
            }
#endif
        }

        void Show(in UpgradeOffer offer)
        {
            _shownOffer = offer.Id;
            _autoPickTimer = 0f;
            for (int i = 0; i < _cards.Length; i++)
            {
                bool used = i < offer.Count;
                _cards[i].gameObject.SetActive(used);
                if (!used) continue;
                UpgradeDefinition upgrade = _catalog.Upgrades[offer.UpgradeAt(i)];
                UpgradeRarity rarity = offer.RarityAt(i);
                Color color = _catalog.RarityColors[(int)rarity];
                _cardBands[i].color = color;
                _cardNames[i].text = _localization.Get(upgrade.NameKey);
                _cardValues[i].SetText(_localization.Get(upgrade.DescriptionKey), upgrade.ValueFor(rarity));
                _cardRarities[i].text = _localization.Get(_catalog.RarityKeys[(int)rarity]);
                _cardRarities[i].color = color;
            }
        }

        void Pick(int choice)
        {
            if (!_offers.HasOffer) return;
            _offers.Choose(choice);
            _shownOffer = 0;
            _shownCount = -1;
        }

        static int RarestChoice(in UpgradeOffer offer)
        {
            int best = 0;
            for (int i = 1; i < offer.Count; i++) if (offer.RarityAt(i) > offer.RarityAt(best)) best = i;
            return best;
        }
    }
}
