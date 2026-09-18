using LastGround.Core.Services;
using TMPro;
using UnityEngine;

namespace LastGround.Localization
{
    /// <summary>Binds a TMP text to a localization key and refreshes it when the language changes.</summary>
    [RequireComponent(typeof(TMP_Text))]
    [DisallowMultipleComponent]
    public sealed class LocalizedText : MonoBehaviour
    {
        [SerializeField] string _key;

        TMP_Text _text;
        ILocalizationService _localization;

        public string Key => _key;

        public void SetKey(string key)
        {
            _key = key;
            Refresh();
        }

        void Awake()
        {
            _text = GetComponent<TMP_Text>();
        }

        void OnEnable()
        {
            if (_localization == null && !AppServices.TryGet(out _localization))
                return;
            _localization.LanguageChanged += OnLanguageChanged;
            Refresh();
        }

        void OnDisable()
        {
            if (_localization != null)
                _localization.LanguageChanged -= OnLanguageChanged;
        }

        void OnLanguageChanged(string language)
        {
            Refresh();
        }

        void Refresh()
        {
            if (_text == null || _localization == null || string.IsNullOrEmpty(_key)) return;
            _text.text = _localization.Get(_key);
        }
    }
}
