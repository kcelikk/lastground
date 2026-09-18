using System;
using System.Collections.Generic;
using LastGround.Core.Ids;
using LastGround.Core.Logging;
using LastGround.Core.Net;
using LastGround.Core.Net.Protocol;
using LastGround.Core.Net.Session;
using LastGround.Core.Run;
using LastGround.Platform.Net;
using LastGround.Save;
using UnityEngine.SceneManagement;

namespace LastGround.App
{
    /// <summary>
    /// Owns the session and discovery for the app lifetime and runs the menu → lobby → run flow
    /// (TDD_02 §19). Scene changes happen only here.
    /// </summary>
    public sealed class SessionService : ISessionService, IDisposable
    {
        const double ReadyTimeout = 10.0;
        const string DefaultMap = "greybox";

        readonly NetSession _session;
        readonly ILanDiscovery _discovery;
        readonly INetworkInterfaces _interfaces;
        readonly ISaveService _save;
        readonly HashSet<PlayerId> _ready = new HashSet<PlayerId>();
        double _now;
        double _readyDeadline = -1;

        public SessionService(NetSession session, ILanDiscovery discovery, INetworkInterfaces interfaces, ISaveService save)
        {
            _session = session;
            _discovery = discovery;
            _interfaces = interfaces;
            _save = save;
            _session.Disconnected += OnDisconnected;
            _session.RosterChanged += OnRosterChanged;
            _session.PlayerLeft += OnPlayerLeft;
            _session.Subscribe<LoadRun>(OnLoadRun);
            _session.Subscribe<RunReady>(OnRunReady);
            _session.Subscribe<RunStart>(OnRunStart);
        }

        public ISession Session => _session;
        public ILanDiscovery Discovery => _discovery;
        public string LocalAddress => PlatformNet.PreferredAddress(_interfaces.GetIPv4());
        public string LastJoinAddress => _save.Settings.LastJoinAddress;
        public RunLaunch CurrentRun { get; private set; }
        public bool RunStarted { get; private set; }
        public DisconnectReason LastDisconnect { get; private set; }
        public JoinRejectReason LastReject { get; private set; }
        public event Action RunStartedEvent;

        public void Tick(double now)
        {
            _now = now;
            _session.Tick(now);
            _discovery.Poll(now);
            if (_readyDeadline > 0 && now >= _readyDeadline)
            {
                Log.Warning(LogCategory.Net, "Not all players reported ready; starting anyway.");
                BeginRun();
            }
        }

        public bool HostGame()
        {
            ClearLastDisconnect();
            if (!_session.StartHost())
            {
                Log.Warning(LogCategory.Net, "HostGame: could not start host");
                return false;
            }
            Log.Info(LogCategory.Net, "HostGame: listening on " + _session.Config.Port + ", address " + LocalAddress);
            _discovery.StartAdvertising(_session.SessionName, _session.Config.Port, 1, (byte)_session.Config.MaxPlayers);
            return true;
        }

        public void Join(string address, ushort port)
        {
            ClearLastDisconnect();
            if (address != _save.Settings.LastJoinAddress)
            {
                _save.Settings.LastJoinAddress = address;
                _save.RequestSave();
            }
            _discovery.StopBrowsing();
            _session.Join(address, port);
        }

        public void StartSolo()
        {
            ClearLastDisconnect();
            _session.StartOffline();
            StartRun();
        }

        public void StartRun()
        {
            Log.Info(LogCategory.App, "StartRun: role " + _session.Role + ", state " + _session.State + ", players " + _session.Players.Count);
            if (!_session.IsAuthority || _session.State == SessionState.Idle) return;
            _session.AcceptingPlayers = false;
            _discovery.UpdateAdvertisement((byte)_session.Players.Count, (byte)_session.Config.MaxPlayers, true);

            var launch = new LoadRun { RunSeed = (uint)Environment.TickCount, MapId = DefaultMap };
            _session.SendToClients(launch);
            LoadRunScene(launch);
        }

        /// <summary>Editor convenience: playing the Run scene directly starts an offline session in place.</summary>
        public void EnsureRunForDirectPlay()
        {
            if (_session.State == SessionState.Idle)
                Log.Warning(LogCategory.App, "Run scene started without a session; starting offline");
            if (_session.State == SessionState.Idle) _session.StartOffline();
            if (CurrentRun == null) CurrentRun = new RunLaunch { Seed = 1, MapId = DefaultMap };
        }

        public void NotifyRunSceneReady()
        {
            if (_session.IsAuthority)
            {
                _ready.Add(_session.LocalPlayer);
                _readyDeadline = _now + ReadyTimeout;
                TryBeginRun();
            }
            else
            {
                _session.SendToHost(new RunReady());
            }
        }

        public void Leave()
        {
            Log.Info(LogCategory.App, "Leave requested");
            _discovery.StopAdvertising();
            _discovery.StopBrowsing();
            _session.Leave();
            ResetRun();
            LoadMenu();
        }

        public void ClearLastDisconnect()
        {
            LastDisconnect = DisconnectReason.None;
            LastReject = JoinRejectReason.None;
        }

        public void Dispose()
        {
            _discovery.Dispose();
            _session.Dispose();
        }

        void OnLoadRun(PlayerId sender, in LoadRun message)
        {
            LoadRunScene(message);
        }

        void LoadRunScene(in LoadRun message)
        {
            ResetRun();
            CurrentRun = new RunLaunch { Seed = message.RunSeed, MapId = message.MapId };
            Log.Info(LogCategory.App, "Loading run, seed " + message.RunSeed);
            SceneManager.LoadScene(SceneNames.Run);
        }

        void OnRunReady(PlayerId sender, in RunReady message)
        {
            if (!_session.IsAuthority || RunStarted) return;
            _ready.Add(sender);
            TryBeginRun();
        }

        void TryBeginRun()
        {
            if (RunStarted || !_ready.Contains(_session.LocalPlayer)) return;
            var players = _session.Players;
            for (int i = 0; i < players.Count; i++)
            {
                if (!_ready.Contains(players[i].Id)) return;
            }
            BeginRun();
        }

        void BeginRun()
        {
            _readyDeadline = -1;
            _session.SendToClients(new RunStart { StartTick = 0 });
            MarkStarted();
        }

        void OnRunStart(PlayerId sender, in RunStart message)
        {
            if (!_session.IsAuthority) MarkStarted();
        }

        void MarkStarted()
        {
            if (RunStarted) return;
            RunStarted = true;
            Log.Info(LogCategory.App, "Run started with " + _session.Players.Count + " player(s)");
            RunStartedEvent?.Invoke();
        }

        void OnPlayerLeft(PlayerId player)
        {
            _ready.Remove(player);
            TryBeginRun();
        }

        void OnRosterChanged()
        {
            if (_session.Role == RunRole.Host)
                _discovery.UpdateAdvertisement((byte)_session.Players.Count, (byte)_session.Config.MaxPlayers, !_session.AcceptingPlayers);
        }

        void OnDisconnected(DisconnectReason reason)
        {
            if (reason == DisconnectReason.Left) return;
            LastDisconnect = reason;
            LastReject = _session.LastRejectReason;
            _discovery.StopAdvertising();
            ResetRun();
            if (SceneManager.GetActiveScene().name != SceneNames.Menu) LoadMenu();
        }

        void ResetRun()
        {
            CurrentRun = null;
            RunStarted = false;
            _ready.Clear();
            _readyDeadline = -1;
        }

        static void LoadMenu()
        {
            if (SceneManager.GetActiveScene().name != SceneNames.Menu)
                SceneManager.LoadScene(SceneNames.Menu);
        }
    }
}
