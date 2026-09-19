using System;
using System.Collections.Generic;
using LastGround.Core.Services;
using LastGround.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastGround.UI.Run
{
    /// <summary>
    /// Emote button in the run HUD (M9): tapping it opens the equipped emotes for a few seconds; tapping one plays it.
    /// </summary>
    public sealed class EmoteBar : MonoBehaviour
    {
        const float OpenSeconds = 4f;

        [SerializeField] Button _toggle;
        [SerializeField] GameObject _options;
        [SerializeField] Button[] _buttons;

        readonly List<byte> _emotes = new List<byte>(3);
        Action<byte> _play;
        float _closeAt;

        /// <param name="emotes">Catalog index and name key of each equipped emote.</param>
        public void Bind(IReadOnlyList<(byte index, string nameKey)> emotes, Action<byte> play)
        {
            _play = play;
            var localization = AppServices.Get<ILocalizationService>();
            _emotes.Clear();
            for (int i = 0; i < _buttons.Length; i++)
            {
                bool used = i < emotes.Count;
                _buttons[i].gameObject.SetActive(used);
                if (!used) continue;
                _emotes.Add(emotes[i].index);
                _buttons[i].GetComponentInChildren<TMP_Text>().text = localization.Get(emotes[i].nameKey);
                int slot = i;
                _buttons[i].onClick.AddListener(() => Play(slot));
            }
            _toggle.onClick.AddListener(Toggle);
            _options.SetActive(false);
            gameObject.SetActive(emotes.Count > 0);
        }

        void Toggle()
        {
            bool open = !_options.activeSelf;
            _options.SetActive(open);
            _closeAt = Time.unscaledTime + OpenSeconds;
        }

        void Play(int slot)
        {
            if (slot < _emotes.Count) _play?.Invoke(_emotes[slot]);
            _options.SetActive(false);
        }

        void Update()
        {
            if (_options.activeSelf && Time.unscaledTime >= _closeAt) _options.SetActive(false);
        }
    }
}
