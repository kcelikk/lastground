using LastGround.Core.Net.Session;
using LastGround.Core.Services;
using LastGround.Gameplay.Run;
using LastGround.Localization;
using LastGround.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastGround.UI.Run
{
    /// <summary>
    /// End-of-run panel (M5, simple): title, survival time, highest threat, kills, revives, coins, and a button back
    /// to the main menu (leaves the session). Appears when the run outcome ends, on every device.
    /// </summary>
    public sealed class ResultsScreen : MonoBehaviour
    {
        [SerializeField] GameObject _panel;
        [SerializeField] TMP_Text _title;
        [SerializeField] TMP_Text _stats;
        [SerializeField] Button _menuButton;

        readonly CharLine _text = new CharLine(400);
        RunOutcome _outcome;
        ISessionService _service;
        ILocalizationService _localization;
        bool _shown;

        public void Bind(RunOutcome outcome, ISessionService service)
        {
            _outcome = outcome;
            _service = service;
            _localization = AppServices.Get<ILocalizationService>();
            _menuButton.onClick.AddListener(_service.Leave);
            _panel.SetActive(false);
        }

        void Update()
        {
            if (_shown || _outcome == null || !_outcome.Ended) return;
            _shown = true;
            RunResult r = _outcome.Result;
            _title.text = _localization.Get(r.Extracted ? "results.title_extracted" : "results.title");
            _text.Clear()
                .Append(_localization.Get("results.survival")).Append("   ").AppendClock((int)r.SurvivalSeconds).Append('\n')
                .Append(_localization.Get("results.threat")).Append("   ").AppendRoman(r.MaxThreat).Append('\n')
                .Append(_localization.Get("results.kills")).Append("   ").Append(r.Kills).Append('\n')
                .Append(_localization.Get("results.revives")).Append("   ").Append(r.Revives).Append('\n')
                .Append(_localization.Get("results.coins")).Append("   ").Append(r.Coins);
            _stats.SetCharArray(_text.Buffer, 0, _text.Length);
            _panel.SetActive(true);
        }
    }
}
