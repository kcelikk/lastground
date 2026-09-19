using LastGround.Core.Services;
using LastGround.Gameplay.Director;
using LastGround.Localization;
using LastGround.UI.Common;
using TMPro;
using UnityEngine;

namespace LastGround.UI.Run
{
    /// <summary>
    /// Top-centre run line (TDD_01 §9.11, D-003): <c>SURVIVAL 08:42 · HORDE: HIGH · THREAT III</c>, with the HORDE
    /// word coloured by level, and a short banner when THREAT rises, a new zombie type shows up ("Runners detected")
    /// or an elite spawns. No wave or remaining-zombie count by design. The run line is rebuilt only when a shown value
    /// changes, into a reused char buffer (no GC); announcement banners format once per event.
    /// </summary>
    public sealed class RunStatusHud : MonoBehaviour
    {
        const float BannerTime = 2.5f;
        static readonly string[] LevelKeys = { "hud.horde.calm", "hud.horde.low", "hud.horde.medium", "hud.horde.high", "hud.horde.extreme" };
        static readonly string[] LevelColors = { "<color=#9A9A9A>", "<color=#FFFFFF>", "<color=#FFD24A>", "<color=#FF9A2E>", "<color=#FF3B30>" };

        [SerializeField] TMP_Text _line;
        [SerializeField] TMP_Text _banner;

        readonly CharLine _text = new CharLine();
        readonly CharLine _bannerText = new CharLine();
        readonly string[] _levelWords = new string[LevelKeys.Length];
        RunStatus _status;
        ILocalizationService _localization;
        string _survival, _horde, _threat, _threatUp;
        int _shownSecond = -1;
        HordeLevel _shownLevel;
        int _shownThreat;
        float _bannerTimer;
        Core.Events.EventReader<DirectorAnnouncement> _announcements;
        Data.Combat.CombatCatalog _catalog;
        string _newZombie, _eliteSpawned;

        /// <param name="catalog">Zombie and elite names for announcements; null = no announcement banners.</param>
        public void Bind(RunStatus status, Data.Combat.CombatCatalog catalog = null)
        {
            _status = status;
            _catalog = catalog;
            _announcements = status.Announcements.CreateReader();
            _localization = AppServices.Get<ILocalizationService>();
            _localization.LanguageChanged += OnLanguageChanged;
            _banner.gameObject.SetActive(false);
            LoadTexts();
            _shownThreat = status.Threat;
        }

        void OnDestroy()
        {
            if (_localization != null) _localization.LanguageChanged -= OnLanguageChanged;
        }

        void Update()
        {
            if (_status == null) return;
            int second = (int)_status.RunSeconds;
            if (second != _shownSecond || _status.Horde != _shownLevel || _status.Threat != _shownThreat)
            {
                if (_status.Threat > _shownThreat && _shownSecond >= 0) ShowBanner(_status.Threat);
                _shownSecond = second;
                _shownLevel = _status.Horde;
                _shownThreat = _status.Threat;
                Rebuild();
            }

            while (_status.Announcements.TryRead(ref _announcements, out DirectorAnnouncement a)) ShowAnnouncement(a);
            if (_bannerTimer <= 0f) return;
            _bannerTimer -= Time.unscaledDeltaTime;
            Color c = _banner.color;
            c.a = Mathf.Clamp01(_bannerTimer / 0.5f);
            _banner.color = c;
            if (_bannerTimer <= 0f) _banner.gameObject.SetActive(false);
        }

        void Rebuild()
        {
            int level = Mathf.Clamp((int)_shownLevel, 0, LevelKeys.Length - 1);
            _text.Clear().Append(_survival).Append(' ').AppendClock(_shownSecond)
                .Append("   ·   ").Append(_horde).Append(": ").Append(LevelColors[level]).Append(_levelWords[level]).Append("</color>")
                .Append("   ·   ").Append(_threat).Append(' ').AppendRoman(_shownThreat);
            _line.SetCharArray(_text.Buffer, 0, _text.Length);
        }

        void ShowBanner(int threat)
        {
            _bannerText.Clear().Append(_threat).Append(' ').AppendRoman(threat).Append('\n').Append("<size=60%>").Append(_threatUp);
            _banner.SetCharArray(_bannerText.Buffer, 0, _bannerText.Length);
            _banner.gameObject.SetActive(true);
            _bannerTimer = BannerTime;
        }

        void ShowAnnouncement(in DirectorAnnouncement a)
        {
            var zombie = _catalog != null ? _catalog.Zombie(a.ZombieType) : null;
            if (zombie == null) return;
            string name = _localization.Get(zombie.DisplayNameKey);
            string text;
            if (a.Kind == AnnouncementKind.BossArrived)
            {
                // Boss banner: full-size name, the weak point hint under it.
                _banner.text = name + "\n<size=55%>" + _localization.Get("hud.boss_hint");
                _banner.gameObject.SetActive(true);
                _bannerTimer = BannerTime;
                return;
            }
            if (a.Kind == AnnouncementKind.EliteSpawned)
            {
                var elite = _catalog.Elite(a.Elite);
                string modifier = elite != null ? _localization.Get(elite.DisplayNameKey) : string.Empty;
                text = string.Format(System.Globalization.CultureInfo.InvariantCulture, _eliteSpawned, modifier, name);
            }
            else
            {
                text = string.Format(System.Globalization.CultureInfo.InvariantCulture, _newZombie, name);
            }
            _banner.text = "<size=70%>" + text;
            _banner.gameObject.SetActive(true);
            _bannerTimer = BannerTime;
        }

        void OnLanguageChanged(string language)
        {
            LoadTexts();
            if (_shownSecond >= 0) Rebuild();
        }

        void LoadTexts()
        {
            _survival = _localization.Get("hud.survival");
            _horde = _localization.Get("hud.horde");
            _threat = _localization.Get("hud.threat");
            _threatUp = _localization.Get("hud.threat_up");
            _newZombie = _localization.Get("hud.new_zombie");
            _eliteSpawned = _localization.Get("hud.elite_spawned");
            for (int i = 0; i < LevelKeys.Length; i++) _levelWords[i] = _localization.Get(LevelKeys[i]);
        }
    }
}
