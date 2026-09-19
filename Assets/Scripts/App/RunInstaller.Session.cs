using LastGround.Core.Net.Session;
using LastGround.Core.Tick;
using LastGround.Gameplay.Players;
using LastGround.Gameplay.Run;
using UnityEngine;

namespace LastGround.App
{
    /// <summary>
    /// M10 session flows (TDD_02 §19.5): the screen stays awake during the run; a client that loses the host mid-run
    /// ends with a partial result (banked like a wipe) instead of dropping to the menu; a dead player spectates
    /// teammates.
    /// </summary>
    public sealed partial class RunInstaller
    {
        [SerializeField] UI.Run.SpectatorBar _spectatorBar;

        SpectatorTarget _spectator;
        ConnectionLossTracker _lossTracker;
        Gameplay.Loot.TeamWallet _wallet;

        void BuildSessionFlow(ref RunParts parts, TickLoop loop)
        {
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            _spectator = new SpectatorTarget(parts.Players);
            if (_spectatorBar != null) _spectatorBar.Bind(_spectator, parts.Session);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (parts.Health != null && Dev.DevAutomation.DamageScale >= 0f) parts.Health.DamageScale = Dev.DevAutomation.DamageScale;
#endif
            if (parts.Session.IsAuthority) return;
            _wallet = parts.Wallet;
            _lossTracker = new ConnectionLossTracker(parts.Deaths, parts.Status);
            loop.Register(TickPhase.Presentation, _lossTracker);
            _service.RunConnectionLost += OnConnectionLost;
        }

        void EndSessionFlow()
        {
            Screen.sleepTimeout = SleepTimeout.SystemSetting;
            if (_service != null) _service.RunConnectionLost -= OnConnectionLost;
        }

        void OnConnectionLost(DisconnectReason reason)
        {
            if (_outcome.Ended || _lossTracker == null) return;
            _outcome.End(_lossTracker.Result(_wallet != null ? _wallet.Coins : 0, _extraction));
        }
    }
}
