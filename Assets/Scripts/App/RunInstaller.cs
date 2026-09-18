using System.Collections.Generic;
using LastGround.App.Dev;
using LastGround.Core.Ids;
using LastGround.Core.Net;
using LastGround.Core.Net.Session;
using LastGround.Core.Services;
using LastGround.Core.Tick;
using LastGround.Data.Crowd;
using LastGround.Data.Quality;
using LastGround.Gameplay.Crowd;
using LastGround.Gameplay.Players;
using LastGround.Input;
using LastGround.Networking.Replication;
using LastGround.Rendering;
using LastGround.Rendering.Carnage;
using LastGround.Rendering.Crowd;
using LastGround.Rendering.Quality;
using LastGround.UI.Run;
using UnityEngine;

namespace LastGround.App
{
    /// <summary>
    /// Run scene composition root (TDD_02 §15.3): builds the system set for the session role and registers it on
    /// the tick scheduler. Role checks live here, not in gameplay code.
    /// Host/offline: crowd simulation (dummy or benchmark driver) + replication sender. Client: replica + receiver.
    /// Everyone: local motor, player sync, crowd/corpse/blood rendering, camera, HUD.
    /// </summary>
    public sealed class RunInstaller : MonoBehaviour
    {
        const int CrowdCapacity = 512;
        const int DummyCount = 300;
        const float WorldSize = 140f;

        [SerializeField] Camera _camera;
        [SerializeField] Transform _worldRoot;
        [SerializeField] CrowdVisualCatalog _crowdCatalog;
        [SerializeField] Material _bloodParticleMaterial;
        [SerializeField] Material _bloodSplatMaterial;
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
            QualityPresetDefinition preset = AppServices.Get<QualityService>().Current;
            bool benchmark = _service.CurrentRun.Benchmark;

            _scheduler = gameObject.AddComponent<TickScheduler>();
            TickLoop loop = _scheduler.Loop;

            var players = new PlayerStateTable { Local = session.LocalPlayer };
            PlayerId me = session.LocalPlayer;
            var sync = new PlayerSync(session, players, PlayerMotor.MoveSpeed);
            _disposables.Add(sync);

            ICrowdRenderSource crowdSource;
            IGameEventStream<CrowdDeath> deaths = null;
            BenchmarkCrowdDriver benchmarkDriver = null;
            if (session.IsAuthority)
            {
                var crowd = new CrowdState(CrowdCapacity);
                deaths = crowd.Deaths;
                if (benchmark)
                {
                    benchmarkDriver = new BenchmarkCrowdDriver(crowd, _service.CurrentRun.Seed);
                    loop.Register(TickPhase.ZombieSim, benchmarkDriver);
                }
                else
                {
                    loop.Register(TickPhase.ZombieSim, Gate(new DummyCrowdSim(crowd, DummyCount, WorldSize, _service.CurrentRun.Seed)));
                    var sender = new CrowdReplicationSender(session, crowd, players, new ReplicationTuning());
                    _disposables.Add(sender);
                    loop.Register(TickPhase.NetSend, sender);
                }
                crowdSource = crowd;
            }
            else
            {
                var replica = new CrowdReplica(CrowdCapacity);
                var receiver = new CrowdReplicationReceiver(session, replica);
                _disposables.Add(receiver);
                loop.Register(TickPhase.Presentation, receiver);
                crowdSource = replica;
                // Client corpses/blood arrive with death replication in M3.
            }

            loop.Register(TickPhase.Input, _input);
            if (benchmark)
            {
                // The benchmark player stands still at the centre of the horde.
                players.SetLocal(0f, 0f, 180f, 0f, 0f);
            }
            else
            {
                loop.Register(TickPhase.LocalPlayer, Gate(new PlayerMotor(players, _input, WorldSize, me.Value * 3f - 4.5f, -3f)));
            }
            loop.Register(TickPhase.NetSend, sync);
            loop.Register(TickPhase.Presentation, new TickAction(_ => sync.Interpolate()));
            loop.Register(TickPhase.Presentation, new FollowCamera(_camera, players));

            var crowdRenderer = new ZombieRenderSystem(crowdSource, _crowdCatalog, _camera, preset, deaths);
            _disposables.Add(crowdRenderer);
            loop.Register(TickPhase.Presentation, crowdRenderer);
            if (deaths != null)
            {
                var blood = new BloodSystem(deaths, preset, _worldRoot, _bloodParticleMaterial, _bloodSplatMaterial);
                _disposables.Add(blood);
                loop.Register(TickPhase.Presentation, blood);
            }
            loop.Register(TickPhase.Presentation, new PlayerViews(players, _worldRoot, _playerMesh, _playerMaterial));

            _hud.Bind(_service, crowdSource);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            gameObject.AddComponent<RunTelemetry>().Bind(session, crowdSource);
            if (benchmarkDriver != null)
            {
                gameObject.AddComponent<PerfBenchmarkRunner>().Bind(benchmarkDriver, crowdRenderer, AppServices.Get<QualityService>(),
                    _service.CurrentRun.BenchmarkStepSeconds, _service.CurrentRun.QuitAfterBenchmark);
            }
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
