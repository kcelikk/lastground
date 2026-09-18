using LastGround.Core.Net.Session;
using LastGround.Core.Services;
using LastGround.Localization;
using LastGround.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastGround.UI.Menu
{
    /// <summary>
    /// Main menu: solo run, local co-op, language, quit. Settings arrives in a later milestone.
    /// </summary>
    public sealed class MainMenuScreen : MonoBehaviour
    {
        const float StatusSeconds = 2.5f;
        const float DisconnectStatusSeconds = 6f;

        [SerializeField] Button _soloButton;
        [SerializeField] Button _coopButton;
        [SerializeField] Button _settingsButton;
        [SerializeField] Button _languageButton;
        [SerializeField] Button _quitButton;
        [SerializeField] TMP_Text _languageLabel;
        [SerializeField] TMP_Text _statusLabel;
        [SerializeField] TMP_Text _versionLabel;
        [SerializeField] ScreenRouter _router;
        [SerializeField] GameObject _coopScreen;

        ILocalizationService _localization;
        ISessionService _sessions;
        float _statusHideAt;
        string _statusKey;

        void Awake()
        {
            _localization = AppServices.Get<ILocalizationService>();
            _sessions = AppServices.Get<ISessionService>();

            _soloButton.onClick.AddListener(_sessions.StartSolo);
            _coopButton.onClick.AddListener(() => _router.Show(_coopScreen));
            _settingsButton.onClick.AddListener(ShowNotAvailable);
            _languageButton.onClick.AddListener(CycleLanguage);
            _quitButton.onClick.AddListener(Application.Quit);
            _statusLabel.gameObject.SetActive(false);
        }

        void OnEnable()
        {
            _localization.LanguageChanged += OnLanguageChanged;
            RefreshTexts();

            // A run that ended by disconnect returns here; tell the player why.
            string reason = NetMessageKeys.For(_sessions.LastDisconnect, _sessions.LastReject);
            if (reason != null)
            {
                ShowStatus(reason, DisconnectStatusSeconds);
                _sessions.ClearLastDisconnect();
            }
        }

        void OnDisable()
        {
            _localization.LanguageChanged -= OnLanguageChanged;
        }

        void Update()
        {
            if (_statusLabel.gameObject.activeSelf && Time.unscaledTime >= _statusHideAt)
                _statusLabel.gameObject.SetActive(false);
        }

        void CycleLanguage()
        {
            var languages = _localization.Languages;
            int current = 0;
            for (int i = 0; i < languages.Count; i++)
            {
                if (languages[i].Code == _localization.CurrentLanguage)
                    current = i;
            }
            _localization.SetLanguage(languages[(current + 1) % languages.Count].Code);
        }

        void ShowNotAvailable() => ShowStatus("menu.not_available", StatusSeconds);

        void ShowStatus(string key, float seconds)
        {
            _statusKey = key;
            _statusLabel.text = _localization.Get(key);
            _statusLabel.gameObject.SetActive(true);
            _statusHideAt = Time.unscaledTime + seconds;
        }

        void OnLanguageChanged(string language)
        {
            RefreshTexts();
            if (_statusLabel.gameObject.activeSelf && _statusKey != null)
                _statusLabel.text = _localization.Get(_statusKey);
        }

        void RefreshTexts()
        {
            _languageLabel.text = _localization.Format("menu.language", CurrentLanguageName());
            _versionLabel.text = _localization.Format("menu.version", Application.version);
        }

        string CurrentLanguageName()
        {
            var languages = _localization.Languages;
            for (int i = 0; i < languages.Count; i++)
            {
                if (languages[i].Code == _localization.CurrentLanguage)
                    return languages[i].Name;
            }
            return _localization.CurrentLanguage;
        }
    }
}
