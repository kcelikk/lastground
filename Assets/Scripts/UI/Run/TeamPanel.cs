using LastGround.Core.Net.Session;
using LastGround.Core.Services;
using LastGround.Data.Presentation;
using LastGround.Gameplay.Players;
using LastGround.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastGround.UI.Run
{
    /// <summary>
    /// Teammate list under the local health bar (TDD_01 §14.3, board-2): colour swatch, name, health bar and state
    /// (down + bleedout, reviving %, dead + return timer). Up to three rows; updated with allocation-free SetText.
    /// </summary>
    public sealed class TeamPanel : MonoBehaviour
    {
        [SerializeField] GameObject[] _rows;
        [SerializeField] Image[] _swatches;
        [SerializeField] TMP_Text[] _names;
        [SerializeField] Image[] _healthFills;
        [SerializeField] TMP_Text[] _states;

        readonly int[] _rowPlayer = new int[3];
        readonly int[] _shownKey = new int[3];
        PlayerStateTable _players;
        ISession _session;
        ILocalizationService _localization;
        float _maxHealth;
        string _down, _dead, _deadWaiting, _reviving;
        int _shownRoster = -1;

        public void Bind(PlayerStateTable players, ISession session, float maxHealth)
        {
            _players = players;
            _session = session;
            _maxHealth = Mathf.Max(1f, maxHealth);
            _localization = AppServices.Get<ILocalizationService>();
            _localization.LanguageChanged += OnLanguageChanged;
            LoadTexts();
            for (int i = 0; i < _rows.Length; i++) _rows[i].SetActive(false);
        }

        void OnDestroy()
        {
            if (_localization != null) _localization.LanguageChanged -= OnLanguageChanged;
        }

        void Update()
        {
            if (_players == null) return;
            int roster = RosterKey();
            if (roster != _shownRoster) AssignRows(roster);

            for (int r = 0; r < _rows.Length; r++)
            {
                if (!_rows[r].activeSelf) continue;
                int p = _rowPlayer[r];
                _healthFills[r].fillAmount = _players.Health[p] / _maxHealth;
                PlayerLife life = _players.Life[p];
                int countdown = Mathf.CeilToInt(_players.Countdown[p]);
                int revive = Mathf.RoundToInt(_players.ReviveProgress[p] * 100f);
                int key = life == PlayerLife.Alive ? -1 : (int)life * 100000 + (revive > 0 ? 50000 + revive : countdown);
                if (key == _shownKey[r]) continue;
                _shownKey[r] = key;
                if (life == PlayerLife.Alive) _states[r].SetText(string.Empty);
                else if (life == PlayerLife.Downed && revive > 0) _states[r].SetText(_reviving, revive);
                else if (life == PlayerLife.Downed) _states[r].SetText(_down, countdown);
                else if (countdown > 0) _states[r].SetText(_dead, countdown);
                else _states[r].SetText(_deadWaiting);
            }
        }

        /// <summary>Bit mask of active remote players; rows are rebuilt only when it changes.</summary>
        int RosterKey()
        {
            int key = 0;
            for (int p = 0; p < PlayerStateTable.Max; p++)
                if (_players.Active[p] && p != _players.Local.Value) key |= 1 << p;
            return key;
        }

        void AssignRows(int roster)
        {
            _shownRoster = roster;
            int row = 0;
            for (int p = 0; p < PlayerStateTable.Max && row < _rows.Length; p++)
            {
                if ((roster & (1 << p)) == 0) continue;
                _rowPlayer[row] = p;
                _shownKey[row] = int.MinValue;
                _swatches[row].color = PlayerSlotColors.Of(p);
                _names[row].text = NameOf(p);
                _rows[row].SetActive(true);
                row++;
            }
            for (; row < _rows.Length; row++) _rows[row].SetActive(false);
        }

        string NameOf(int player)
        {
            var roster = _session.Players;
            for (int i = 0; i < roster.Count; i++)
                if (roster[i].Id.Value == player) return roster[i].Name;
            return "P" + (player + 1);
        }

        void OnLanguageChanged(string language)
        {
            LoadTexts();
            for (int i = 0; i < _shownKey.Length; i++) _shownKey[i] = int.MinValue;
        }

        void LoadTexts()
        {
            _down = _localization.Get("hud.team.down");
            _dead = _localization.Get("hud.team.dead");
            _deadWaiting = _localization.Get("hud.team.dead_waiting");
            _reviving = _localization.Get("hud.team.reviving");
        }
    }
}
