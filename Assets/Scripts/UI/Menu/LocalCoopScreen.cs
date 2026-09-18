using System;
using System.Collections.Generic;
using System.Globalization;
using LastGround.Core.Net;
using LastGround.Core.Net.Protocol;
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
    /// Find games on this Wi-Fi, join by IP, or create a game (TDD_02 §19.1, reference panel 3).
    /// Discovery runs only while this screen is open.
    /// </summary>
    public sealed class LocalCoopScreen : MonoBehaviour
    {
        [SerializeField] ScreenRouter _router;
        [SerializeField] GameObject _mainScreen;
        [SerializeField] GameObject _lobbyScreen;
        [SerializeField] Button _createButton;
        [SerializeField] Button _joinIpButton;
        [SerializeField] Button _backButton;
        [SerializeField] TMP_InputField _ipInput;
        [SerializeField] TMP_Text _statusLabel;
        [SerializeField] Button[] _hostRows;
        [SerializeField] TMP_Text[] _hostLabels;

        readonly Dictionary<string, DiscoveredHost> _hosts = new Dictionary<string, DiscoveredHost>(StringComparer.Ordinal);
        readonly List<DiscoveredHost> _ordered = new List<DiscoveredHost>();
        ISessionService _service;
        ILocalizationService _localization;

        void Awake()
        {
            _service = AppServices.Get<ISessionService>();
            _localization = AppServices.Get<ILocalizationService>();
            _createButton.onClick.AddListener(CreateGame);
            _joinIpButton.onClick.AddListener(JoinByIp);
            _backButton.onClick.AddListener(() => _router.Show(_mainScreen));
            for (int i = 0; i < _hostRows.Length; i++)
            {
                int index = i;
                _hostRows[i].onClick.AddListener(() => JoinRow(index));
            }
        }

        void OnEnable()
        {
            _hosts.Clear();
            RefreshRows();
            if (string.IsNullOrEmpty(_ipInput.text)) _ipInput.text = _service.LastJoinAddress ?? string.Empty;

            string reason = NetMessageKeys.For(_service.LastDisconnect, _service.LastReject);
            SetStatus(reason ?? "coop.searching");
            _service.ClearLastDisconnect();

            _service.Discovery.HostFound += OnHostFound;
            _service.Discovery.HostLost += OnHostLost;
            _service.Session.StateChanged += OnStateChanged;
            _service.Session.Disconnected += OnDisconnected;
            _service.Discovery.StartBrowsing();
        }

        void OnDisable()
        {
            _service.Discovery.HostFound -= OnHostFound;
            _service.Discovery.HostLost -= OnHostLost;
            _service.Session.StateChanged -= OnStateChanged;
            _service.Session.Disconnected -= OnDisconnected;
            _service.Discovery.StopBrowsing();
        }

        void CreateGame()
        {
            if (_service.HostGame()) _router.Show(_lobbyScreen);
            else SetStatus("coop.host_failed");
        }

        void JoinByIp()
        {
            string text = (_ipInput.text ?? string.Empty).Trim();
            ushort port = NetProtocol.DefaultGamePort;
            int colon = text.LastIndexOf(':');
            if (colon > 0 && ushort.TryParse(text.Substring(colon + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out ushort parsed))
            {
                port = parsed;
                text = text.Substring(0, colon);
            }
            if (text.Length == 0) return;
            SetStatus("coop.connecting");
            _service.Join(text, port);
        }

        void JoinRow(int index)
        {
            if (index >= _ordered.Count || !_ordered[index].CanJoin) return;
            SetStatus("coop.connecting");
            _service.Join(_ordered[index].Address, _ordered[index].Port);
        }

        void OnHostFound(DiscoveredHost host)
        {
            _hosts[host.Address + ":" + host.Port] = host;
            RefreshRows();
        }

        void OnHostLost(string address, ushort port)
        {
            if (_hosts.Remove(address + ":" + port)) RefreshRows();
        }

        void OnStateChanged(SessionState state)
        {
            if (state == SessionState.Connected) _router.Show(_lobbyScreen);
        }

        void OnDisconnected(DisconnectReason reason)
        {
            string key = NetMessageKeys.For(reason, _service.Session.LastRejectReason);
            if (key != null) SetStatus(key);
            _service.ClearLastDisconnect();
            _service.Discovery.StartBrowsing();
        }

        void RefreshRows()
        {
            _ordered.Clear();
            _ordered.AddRange(_hosts.Values);
            _ordered.Sort((a, b) => a.PingMs.CompareTo(b.PingMs));

            for (int i = 0; i < _hostRows.Length; i++)
            {
                bool visible = i < _ordered.Count;
                _hostRows[i].gameObject.SetActive(visible);
                if (!visible) continue;
                DiscoveredHost host = _ordered[i];
                _hostRows[i].interactable = host.CanJoin;
                string format = !host.VersionMatches ? "coop.row_version" : host.InRun ? "coop.row_in_run" : "coop.row";
                _hostLabels[i].text = string.Format(CultureInfo.InvariantCulture, _localization.Get(format),
                    host.SessionName, host.Players, host.MaxPlayers, host.PingMs);
            }

            // Hide the "searching" hint once something is listed; show it again when the list empties.
            string searching = _localization.Get("coop.searching");
            if (_ordered.Count > 0 && _statusLabel.text == searching) _statusLabel.text = string.Empty;
            else if (_ordered.Count == 0 && string.IsNullOrEmpty(_statusLabel.text)) _statusLabel.text = searching;
        }

        void SetStatus(string key)
        {
            _statusLabel.text = _localization.Get(key);
        }
    }
}
