using System.Collections.Generic;
using LastGround.App.Dev;
using LastGround.Core.Ids;
using LastGround.Core.Net.Session;
using LastGround.Core.Services;
using LastGround.Core.Tick;
using LastGround.Gameplay.Crowd;
using LastGround.Gameplay.Players;
using LastGround.Input;
using LastGround.Networking.Replication;
using LastGround.Rendering;
using LastGround.UI.Run;
using UnityEngine;

namespace LastGround.App
{
    /// <summary>
    /// Run scene composition root (TDD_02 §15.3): builds the system set for the session role and registers it on
    /// the tick scheduler. Role checks live here, not in gameplay code.
    /// Host/offline: dummy crowd simulation + replication sender. Client: crowd replica + receiver.
    /// Everyone: local motor, player sync, rendering, camera, HUD.
    /// </summary>
    public sealed class RunInstaller : MonoBehaviour
    {
        const int CrowdCapacity = 512;
        const int DummyCount = 300;
        const float WorldSize = 140f;
        const float CrowdHeight = 1.8f;

        [SerializeField] Camera _camera;
        [SerializeField] Transform _worldRoot;
        [SerializeField] Mesh _crowdMesh;
        [SerializeField] Material _crowdMaterial;
        [SerializeField] Mesh _playerMesh;
        [SerializeField] Material _playerMaterial;
        [SerializeField] TouchMoveInput _input;
        [SerializeField] RunHud _hud;

        readonly List<System.IDisposable> _disposables = new List<System.IDisposable>();
        SessionService _service;
        TickScheduler _scheduler;
        bool _started;

        void Start()
        {
            _service = AppServices.Get<SessionService>();
            _service.EnsureRunForDirectPlay();
            ISession session = _service.Session;

            _scheduler = gameObject.AddComponent<TickScheduler>();
            TickLoop loop = _scheduler.Loop;

            var players = new PlayerStateTable { Local = session.LocalPlayer };
            PlayerId me = session.LocalPlayer;
            var motor = new PlayerMotor(players, _input, WorldSize, me.Value * 3f - 4.5f, -3f);
            var sync = new PlayerSync(session, players, PlayerMotor.MoveSpeed);
            _disposables.Add(sync);

            ICrowdRenderSource crowdSource;
            if (session.IsAuthority)
            {
                var crowd = new CrowdState(CrowdCapacity);
                var sim = new DummyCrowdSim(crowd, DummyCount, WorldSize, _service.CurrentRun.Seed);
                var sender = new CrowdReplicationSender(session, crowd, players, new ReplicationTuning());
                _disposables.Add(sender);
                loop.Register(TickPhase.ZombieSim, Gate(sim));
                loop.Register(TickPhase.NetSend, sender);
                crowdSource = crowd;
            }
            else
            {
                var replica = new CrowdReplica(CrowdCapacity);
                var receiver = new CrowdReplicationReceiver(session, replica);
                _disposables.Add(receiver);
                loop.Register(TickPhase.Presentation, receiver);
                crowdSource = replica;
            }

            loop.Register(TickPhase.Input, _input);
            loop.Register(TickPhase.LocalPlayer, Gate(motor));
            loop.Register(TickPhase.NetSend, sync);
            loop.Register(TickPhase.Presentation, new TickAction(_ => sync.Interpolate()));
            loop.Register(TickPhase.Presentation, new CrowdRenderer(crowdSource, _crowdMesh, _crowdMaterial, CrowdHeight));
            loop.Register(TickPhase.Presentation, new PlayerViews(players, _worldRoot, _playerMesh, _playerMaterial));
            loop.Register(TickPhase.Presentation, new FollowCamera(_camera, players));

            _hud.Bind(_service, crowdSource);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            gameObject.AddComponent<RunTelemetry>().Bind(session, crowdSource);
#endif
            _service.RunStartedEvent += OnRunStarted;
            _started = _service.RunStarted;
            session.AcceptingPlayers = false;
            _service.NotifyRunSceneReady();
        }

        void OnDestroy()
        {
            if (_service != null) _service.RunStartedEvent -= OnRunStarted;
            for (int i = 0; i < _disposables.Count; i++) _disposables[i].Dispose();
            _disposables.Clear();
        }

        void OnRunStarted() => _started = true;

        /// <summary>Holds a system until every device has loaded the run (RunStart).</summary>
        ITickable Gate(ITickable inner) => new TickAction(dt =>
        {
            if (_started) inner.Tick(dt, _scheduler.Loop.SimTick);
        });
    }
}
