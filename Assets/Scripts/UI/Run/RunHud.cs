using LastGround.Core.Net;
using LastGround.Core.Net.Session;
using LastGround.Core.Run;
using LastGround.Core.Services;
using LastGround.Gameplay.Crowd;
using LastGround.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastGround.UI.Run
{
    /// <summary>
    /// M1 run overlay: role and player count, waiting state, leave button, and (development builds) live network
    /// stats: RTT, payload KB/s in/out, crowd counts. Stats refresh at 2 Hz with allocation-free SetText.
    /// </summary>
    public sealed class RunHud : MonoBehaviour
    {
        const float RefreshInterval = 0.5f;

        [SerializeField] TMP_Text _statusLabel;
        [SerializeField] TMP_Text _netLabel;
        [SerializeField] TMP_Text _crowdLabel;
        [SerializeField] Button _leaveButton;

        ISessionService _service;
        ILocalizationService _localization;
        ICrowdRenderSource _crowd;
        float _timer;
        int _shownPlayers = -1;
        bool _shownStarted;

        public void Bind(ISessionService service, ICrowdRenderSource crowd)
        {
            _service = service;
            _crowd = crowd;
            _localization = AppServices.Get<ILocalizationService>();
            _leaveButton.onClick.AddListener(_service.Leave);
            _localization.LanguageChanged += OnLanguageChanged;
#if !(UNITY_EDITOR || DEVELOPMENT_BUILD)
            _netLabel.gameObject.SetActive(false);
            _crowdLabel.gameObject.SetActive(false);
#endif
            RefreshStatus();
        }

        void OnDestroy()
        {
            if (_localization != null) _localization.LanguageChanged -= OnLanguageChanged;
        }

        void Update()
        {
            if (_service == null) return;
            ISession session = _service.Session;
            if (session.Players.Count != _shownPlayers || _service.RunStarted != _shownStarted) RefreshStatus();

            _timer += Time.unscaledDeltaTime;
            if (_timer < RefreshInterval) return;
            _timer = 0f;
            INetStats stats = session.Stats;
            _netLabel.SetText("RTT {0:0} ms  IN {1:1} KB/s  OUT {2:1} KB/s",
                (float)(session.Clock.Rtt * 1000.0), stats.InBytesPerSecond / 1024f, stats.OutBytesPerSecond / 1024f);
            _crowdLabel.SetText("CROWD {0}  MSG IN {1}/s  OUT {2}/s", _crowd.ActiveCount, stats.InMessagesPerSecond, stats.OutMessagesPerSecond);
        }

        void OnLanguageChanged(string language) => RefreshStatus();

        void RefreshStatus()
        {
            ISession session = _service.Session;
            _shownPlayers = session.Players.Count;
            _shownStarted = _service.RunStarted;
            if (!_shownStarted)
            {
                _statusLabel.text = _localization.Get("run.waiting");
                return;
            }
            string role = session.Role == RunRole.Offline ? "run.role.offline" : session.IsAuthority ? "run.role.host" : "run.role.client";
            _statusLabel.text = _localization.Get(role) + " · " + _localization.Format("run.players", _shownPlayers);
        }
    }
}
