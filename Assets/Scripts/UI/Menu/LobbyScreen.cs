using System.Globalization;
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
    /// Lobby (TDD_02 §19.3): players with ping, host address for Join by IP, START for the host.
    /// M1: no ready toggles or loadouts; the host starts whenever it wants.
    /// </summary>
    public sealed class LobbyScreen : MonoBehaviour
    {
        [SerializeField] ScreenRouter _router;
        [SerializeField] GameObject _coopScreen;
        [SerializeField] TMP_Text _titleLabel;
        [SerializeField] TMP_Text _addressLabel;
        [SerializeField] TMP_Text _waitingLabel;
        [SerializeField] TMP_Text[] _playerLabels;
        [SerializeField] Button _startButton;
        [SerializeField] Button _leaveButton;

        ISessionService _service;
        ILocalizationService _localization;

        void Awake()
        {
            _service = AppServices.Get<ISessionService>();
            _localization = AppServices.Get<ILocalizationService>();
            _startButton.onClick.AddListener(() => _service.StartRun());
            _leaveButton.onClick.AddListener(Leave);
        }

        void OnEnable()
        {
            _service.Session.RosterChanged += Refresh;
            _service.Session.Disconnected += OnDisconnected;
            _localization.LanguageChanged += OnLanguageChanged;
            Refresh();
        }

        void OnDisable()
        {
            _service.Session.RosterChanged -= Refresh;
            _service.Session.Disconnected -= OnDisconnected;
            _localization.LanguageChanged -= OnLanguageChanged;
        }

        void Leave()
        {
            _service.Leave();
            _router.Show(_coopScreen);
        }

        void OnDisconnected(DisconnectReason reason)
        {
            if (reason != DisconnectReason.Left) _router.Show(_coopScreen);
        }

        void OnLanguageChanged(string language) => Refresh();

        void Refresh()
        {
            ISession session = _service.Session;
            bool isHost = session.IsAuthority;
            _titleLabel.text = _localization.Format("lobby.session_name", session.SessionName);
            _startButton.gameObject.SetActive(isHost);
            _waitingLabel.gameObject.SetActive(!isHost);

            string address = isHost ? _service.LocalAddress : null;
            _addressLabel.gameObject.SetActive(address != null);
            if (address != null) _addressLabel.text = _localization.Format("lobby.address", address);

            var players = session.Players;
            for (int i = 0; i < _playerLabels.Length; i++)
            {
                bool visible = i < players.Count;
                _playerLabels[i].gameObject.SetActive(visible);
                if (!visible) continue;
                LobbyPlayer p = players[i];
                string key = p.IsHost ? "lobby.row_host" : "lobby.row";
                _playerLabels[i].text = string.Format(CultureInfo.InvariantCulture, _localization.Get(key), p.Name, p.RttMs) + MetaLine(p.Meta);
            }
        }

        /// <summary>Character and title under the name (M9 meta selection from the roster).</summary>
        string MetaLine(ulong packed)
        {
            if (packed == 0UL || !AppServices.TryGet(out Meta.IMetaStore meta)) return string.Empty;
            Gameplay.Meta.PlayerMeta selection = Gameplay.Meta.PlayerMeta.Unpack(packed);
            Data.Meta.CharacterDefinition character = Data.Meta.MetaCatalog.At(meta.Catalog.Characters, selection.Character);
            Data.Meta.TitleDefinition title = Data.Meta.MetaCatalog.At(meta.Catalog.Titles, selection.Title);
            string line = character != null ? _localization.Get(character.NameKey) : string.Empty;
            if (title != null) line += "  ·  " + _localization.Get(title.NameKey);
            return line.Length > 0 ? "\n<size=70%><color=#B0B0B0>" + line + "</color>" : string.Empty;
        }
    }
}
