using LastGround.Core.Net.Session;
using LastGround.Core.Services;
using LastGround.Gameplay.Players;
using LastGround.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastGround.UI.Run
{
    /// <summary>
    /// Spectating while dead (M10): "Watching: name" and a button to switch to the next teammate. Hidden while the
    /// local player is in the game. The text changes only when the watched player does.
    /// </summary>
    public sealed class SpectatorBar : MonoBehaviour
    {
        [SerializeField] GameObject _panel;
        [SerializeField] TMP_Text _label;
        [SerializeField] Button _next;

        SpectatorTarget _spectator;
        ISession _session;
        ILocalizationService _localization;
        int _shown = int.MinValue;

        public void Bind(SpectatorTarget spectator, ISession session)
        {
            _spectator = spectator;
            _session = session;
            _localization = AppServices.Get<ILocalizationService>();
            _localization.LanguageChanged += OnLanguageChanged;
            _next.onClick.AddListener(spectator.Next);
            _panel.SetActive(false);
        }

        void OnDestroy()
        {
            if (_localization != null) _localization.LanguageChanged -= OnLanguageChanged;
        }

        void OnLanguageChanged(string language) => _shown = int.MinValue;

        void Update()
        {
            if (_spectator == null) return;
            int target = _spectator.Target;
            if (target == _shown) return;
            _shown = target;
            _panel.SetActive(target >= 0);
            if (target >= 0) _label.text = _localization.Format("hud.spectate.watching", NameOf(target));
        }

        string NameOf(int player)
        {
            var roster = _session.Players;
            for (int i = 0; i < roster.Count; i++)
                if (roster[i].Id.Value == player) return roster[i].Name;
            return "P" + (player + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
