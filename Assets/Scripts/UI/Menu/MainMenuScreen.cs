using LastGround.Core.Services;
using LastGround.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastGround.UI.Menu
{
    /// <summary>
    /// M0 main menu: language switch and quit work; Solo / Local Co-op / Settings arrive in later milestones.
    /// </summary>
    public sealed class MainMenuScreen : MonoBehaviour
    {
        const float StatusSeconds = 2.5f;

        [SerializeField] Button _soloButton;
        [SerializeField] Button _coopButton;
        [SerializeField] Button _settingsButton;
        [SerializeField] Button _languageButton;
        [SerializeField] Button _quitButton;
        [SerializeField] TMP_Text _languageLabel;
        [SerializeField] TMP_Text _statusLabel;
        [SerializeField] TMP_Text _versionLabel;

        ILocalizationService _localization;
        float _statusHideAt;

        void Awake()
        {
            _localization = AppServices.Get<ILocalizationService>();

            _soloButton.onClick.AddListener(ShowNotAvailable);
            _coopButton.onClick.AddListener(ShowNotAvailable);
            _settingsButton.onClick.AddListener(ShowNotAvailable);
            _languageButton.onClick.AddListener(CycleLanguage);
            _quitButton.onClick.AddListener(Application.Quit);
            _statusLabel.gameObject.SetActive(false);
        }

        void OnEnable()
        {
            _localization.LanguageChanged += OnLanguageChanged;
            RefreshTexts();
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

        void ShowNotAvailable()
        {
            _statusLabel.text = _localization.Get("menu.not_available");
            _statusLabel.gameObject.SetActive(true);
            _statusHideAt = Time.unscaledTime + StatusSeconds;
        }

        void OnLanguageChanged(string language)
        {
            RefreshTexts();
            if (_statusLabel.gameObject.activeSelf)
                _statusLabel.text = _localization.Get("menu.not_available");
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
