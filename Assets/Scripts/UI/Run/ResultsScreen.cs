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
    /// End-of-run panel: title (fallen / extracted), survival time, highest threat, kills, revives, run coins, the
    /// extraction bonus and what this player banks (M8 reward conversion), and a button back to the main menu.
    /// Appears when the run outcome ends, on every device.
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
        int _localPlayer;

        /// <param name="localPlayer">Index of this device's player (its share of the reward conversion).</param>
        public void Bind(RunOutcome outcome, ISessionService service, int localPlayer = 0)
        {
            _localPlayer = localPlayer;
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
                .Append(_localization.Get("results.coins")).Append("   ").Append(r.Coins).Append('\n');
            // Reward conversion (TDD_01 §2.2): extraction bonus, then what this player banks.
            if (r.Extracted && (r.StandingMask & (1 << _localPlayer)) != 0)
                _text.Append(_localization.Get("results.extraction_bonus")).Append("   +").Append(r.ExtractionBonus).Append('\n');
            _text.Append("<b>").Append(_localization.Get("results.banked")).Append("   ").Append(r.BankedFor(_localPlayer)).Append("</b>");
            _stats.SetCharArray(_text.Buffer, 0, _text.Length);
            _panel.SetActive(true);
        }
    }
}
