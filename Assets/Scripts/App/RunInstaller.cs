using System.Collections.Generic;
using LastGround.App.Dev;
using LastGround.Core.Events;
using LastGround.Core.Ids;
using LastGround.Core.Net;
using LastGround.Core.Net.Session;
using LastGround.Core.Services;
using LastGround.Core.Tick;
using LastGround.Data.Crowd;
using LastGround.Data.Director;
using LastGround.Data.Map;
using LastGround.Data.Players;
using LastGround.Data.Presentation;
using LastGround.Data.Quality;
using LastGround.Data.Weapons;
using LastGround.Data.Zombies;
using LastGround.Gameplay.Combat;
using LastGround.Gameplay.Crowd;
using LastGround.Gameplay.Director;
using LastGround.Gameplay.Navigation;
using LastGround.Gameplay.Players;
using LastGround.Gameplay.Run;
using LastGround.Gameplay.Zombies;
using LastGround.Input;
using LastGround.Networking.Replication;
using LastGround.Rendering;
using LastGround.Rendering.Quality;
using LastGround.Save;
using LastGround.UI.Run;
using UnityEngine;

namespace LastGround.App
{
    /// <summary>
    /// Run scene composition root (TDD_02 §15.3): builds the system set for the session role and registers it on
    /// the tick scheduler. Role checks live here, not in gameplay code.
    /// Host/offline: horde simulation (ZombieWorld + test spawner, or the benchmark driver), combat authority, player
    /// health, replication senders. Client: replica + receivers, hit claims to the host.
    /// Everyone: twin-stick input, aim resolver, motor, weapon, camera, crowd/combat presentation, HUD.
    /// Presentation wiring lives in <c>RunInstaller.Presentation.cs</c>.
    /// </summary>
    public sealed partial class RunInstaller : MonoBehaviour
    {
        const int CrowdCapacity = 512;
        const float BenchmarkWorldSize = 140f;

        [SerializeField] Camera _camera;
        [SerializeField] Transform _worldRoot;
        [SerializeField] CrowdVisualCatalog _crowdCatalog;
        [SerializeField] NavGridAsset _navGrid;
        [SerializeField] WeaponDefinition _weapon;
        [SerializeField] ZombieDefinition _walker;
        [SerializeField] PlayerDefinition _playerDefinition;
        [SerializeField] CameraProfile _cameraProfile;
        [SerializeField] DirectorProfile _directorProfile;
        [SerializeField] ThreatCurveDefinition _threatCurve;
        [SerializeField] PlayerCountScalingProfile _playerScaling;
        [SerializeField] Material _bloodParticleMaterial;
        [SerializeField] Material _bloodSplatMaterial;
        [SerializeField] Material _tracerMaterial;
        [SerializeField] Mesh _playerMesh;
        [SerializeField] Material _playerMaterial;
        [SerializeField] TouchTwinStickInput _input;
        [SerializeField] RunHud _hud;
        [SerializeField] CombatHud _combatHud;
        [SerializeField] RunStatusHud _statusHud;
        [SerializeField] TeamPanel _teamPanel;
        [SerializeField] TeammateIndicators _teammateIndicators;
        [SerializeField] ResultsScreen _results;

        readonly List<System.IDisposable> _disposables = new List<System.IDisposable>();
        SessionService _service;
        TickScheduler _scheduler;
        bool _started;
        readonly RunOutcome _outcome = new RunOutcome();

        /// <summary>Everything the presentation half needs, collected while the simulation half is built.</summary>
        struct RunParts
        {
            public ISession Session;
            public QualityPresetDefinition Preset;
            public NavGrid Nav;
            public PlayerStateTable Players;
            public ICrowdRenderSource Crowd;
            public IGameEventStream<CrowdDeath> Deaths;
            public IGameEventStream<CrowdHit> Hits;
            public EventChannel<ShotFired> Shots;
            public AimResolver Aim;
            public WeaponController Weapon;
            public ZombieWorld World;
            public CombatAuthority Authority;
            public PlayerHealthSystem Health;
            public BenchmarkCrowdDriver Benchmark;
            public RunStatus Status;
            public HordeDirector Director;
            public RunReferee Referee;
        }

        void Start()
        {
            _service = AppServices.Get<SessionService>();
            _service.EnsureRunForDirectPlay();
            ISession session = _service.Session;
            bool benchmark = _service.CurrentRun.Benchmark;
            uint seed = _service.CurrentRun.Seed;

            _scheduler = gameObject.AddComponent<TickScheduler>();
            TickLoop loop = _scheduler.Loop;

            var parts = new RunParts
            {
                Session = session,
                Preset = AppServices.Get<QualityService>().Current,
                Nav = benchmark || _navGrid == null ? NavGrid.Open((int)BenchmarkWorldSize) : NavGrid.FromAsset(_navGrid),
                Players = new PlayerStateTable { Local = session.LocalPlayer },
                Shots = new EventChannel<ShotFired>(128),
                Status = new RunStatus(),
            };
            _disposables.Add(parts.Nav);
            float worldSize = parts.Nav.Width * parts.Nav.CellSize;
            PlayerStateTable players = parts.Players;

            var sync = new PlayerSync(session, players, _playerDefinition.MoveSpeed);
            _disposables.Add(sync);
            var vitals = new PlayerVitalsSync(session, players);
            _disposables.Add(vitals);
            var directorInfo = new DirectorInfoSync(session, parts.Status);
            _disposables.Add(directorInfo);
            var runEnd = new RunEndSync(session, _outcome);
            _disposables.Add(runEnd);

            IHitClaimSink claims = session.IsAuthority ? BuildHost(ref parts, loop, benchmark, seed) : BuildClient(ref parts, loop);

            // Local player: raw sticks → aim resolver → motor + weapon (TDD_01 §3.1).
            ISaveService save = AppServices.Get<ISaveService>();
            parts.Aim = new AimResolver(_input, players, parts.Crowd, parts.Nav, _weapon)
            {
                Mode = DevAutomation.ForceAutoFire ? Core.Input.ControlMode.AutoAimAutoFire : (Core.Input.ControlMode)save.Settings.ControlMode,
                AssistLevel = save.Settings.AimAssist * 0.5f,
            };
            loop.Register(TickPhase.Input, _input);
            loop.Register(TickPhase.Input, parts.Aim);
            parts.Weapon = new WeaponController(players, parts.Aim, _weapon, parts.Crowd, parts.Nav, seed, claims, parts.Shots,
                session.IsAuthority ? null : parts.Crowd as CrowdReplica, _walker.MaxHealth);
            parts.Aim.Ignore = parts.Weapon.PresumedDeadMask;
            if (benchmark)
            {
                // The benchmark player stands still at the centre of the horde.
                players.SetLocal(0f, 0f, 180f, 0f, 0f);
            }
            else
            {
                PlayerId me = session.LocalPlayer;
                loop.Register(TickPhase.LocalPlayer, Gate(new PlayerMotor(players, parts.Aim, _playerDefinition, worldSize,
                    me.Value * 3f - 4.5f, -3f, parts.Nav, parts.Crowd)));
                loop.Register(TickPhase.LocalPlayer, Gate(parts.Weapon));
            }
            loop.Register(TickPhase.NetSend, sync);
            loop.Register(TickPhase.NetSend, vitals);
            loop.Register(session.IsAuthority ? TickPhase.NetSend : TickPhase.Presentation, Gate(directorInfo));
            loop.Register(TickPhase.NetSend, runEnd);
            loop.Register(TickPhase.Presentation, new TickAction(_ => sync.Interpolate()));

            BuildPresentation(parts, loop);
            _statusHud.Bind(parts.Status);
            _teamPanel.Bind(players, session, _playerDefinition.MaxHealth);
            _teammateIndicators.Bind(players, _camera);
            _results.Bind(_outcome, _service);
            _combatHud.Bind(players, parts.Weapon, parts.Aim, _playerDefinition.MaxHealth, () => CountActive(players) <= 1, mode =>
            {
                save.Settings.ControlMode = (int)mode;
                save.RequestSave();
            });
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var telemetry = gameObject.AddComponent<RunTelemetry>();
            telemetry.Bind(session, parts.Crowd, parts.World);
            telemetry.BindCombat(players, parts.Weapon, parts.Authority, parts.Health);
            telemetry.BindDirector(parts.Status, parts.Director);
            if (parts.Director != null) gameObject.AddComponent<DirectorLog>().Bind(parts.Status, parts.Director, _service.CurrentRun.Seed);
            if (parts.Benchmark != null)
            {
                gameObject.AddComponent<PerfBenchmarkRunner>().Bind(parts.Benchmark, _crowdRenderer, AppServices.Get<QualityService>(),
                    _service.CurrentRun.BenchmarkStepSeconds, _service.CurrentRun.QuitAfterBenchmark);
            }
#endif
            _service.RunStartedEvent += OnRunStarted;
            _started = _service.RunStarted;
            session.AcceptingPlayers = false;
            _service.NotifyRunSceneReady();
        }

        /// <summary>Host/offline: authoritative simulation, combat and health.</summary>
        IHitClaimSink BuildHost(ref RunParts parts, TickLoop loop, bool benchmark, uint seed)
        {
            var crowd = new CrowdState(CrowdCapacity);
            parts.Crowd = crowd;
            parts.Deaths = crowd.Deaths;
            parts.Hits = crowd.Hits;
            parts.Health = new PlayerHealthSystem(parts.Players, _playerDefinition);
            loop.Register(TickPhase.Combat, Gate(parts.Health));
            var referee = new RunReferee(parts.Health, parts.Status, _outcome);
            loop.Register(TickPhase.Combat, Gate(referee));
            parts.Referee = referee;

            if (benchmark)
            {
                parts.Benchmark = new BenchmarkCrowdDriver(crowd, seed);
                loop.Register(TickPhase.ZombieSim, parts.Benchmark);
                return new DiscardClaims();
            }

            var world = parts.World = new ZombieWorld(crowd, parts.Players, parts.Nav, new ZombieTuning(), _walker, seed);
            _disposables.Add(world);
            world.DamageSink = parts.Health;
            world.Respawns = parts.Health.Respawns;
            var footprint = new CameraFootprint(_cameraProfile, 3f);
            parts.Director = new HordeDirector(world, parts.Players, _directorProfile, _threatCurve, _playerScaling, footprint,
                _playerDefinition.MaxHealth, parts.Status, seed);
            parts.Director.Governor.TargetFrameSeconds = 1f / Mathf.Max(30, parts.Preset.TargetFps);
            HordeDirector director = parts.Director;
            loop.Register(TickPhase.Director, Gate(director));
            loop.Register(TickPhase.Presentation, new TickAction(_ => director.Governor.Report(Time.unscaledDeltaTime)));
            loop.Register(TickPhase.ZombieSim, Gate(world));

            parts.Authority = new CombatAuthority(world, parts.Players, parts.Nav, WeaponTable(), seed);
            CombatAuthority authority = parts.Authority;
            parts.Referee.Kills = () => authority.Kills;
            loop.Register(TickPhase.Combat, Gate(parts.Authority));
            var claimSync = new HitClaimSync(parts.Session, parts.Authority);
            _disposables.Add(claimSync);

            var sender = new CrowdReplicationSender(parts.Session, crowd, parts.Players, new ReplicationTuning());
            _disposables.Add(sender);
            loop.Register(TickPhase.NetSend, sender);
            return parts.Authority;
        }

        /// <summary>Client: replica of the host's crowd; hit claims go to the host.</summary>
        IHitClaimSink BuildClient(ref RunParts parts, TickLoop loop)
        {
            var replica = new CrowdReplica(CrowdCapacity);
            var receiver = new CrowdReplicationReceiver(parts.Session, replica);
            _disposables.Add(receiver);
            loop.Register(TickPhase.Presentation, receiver);
            parts.Crowd = replica;
            parts.Deaths = replica.Deaths;
            parts.Hits = replica.Hits;

            var claimSync = new HitClaimSync(parts.Session, null);
            _disposables.Add(claimSync);
            loop.Register(TickPhase.NetSend, claimSync);
            return claimSync;
        }

        /// <summary>Weapons indexed by their wire id (M4: the assault rifle only).</summary>
        WeaponDefinition[] WeaponTable()
        {
            var table = new WeaponDefinition[_weapon.NetIndex + 1];
            table[_weapon.NetIndex] = _weapon;
            return table;
        }

        void OnDestroy()
        {
            if (_service != null) _service.RunStartedEvent -= OnRunStarted;
            for (int i = 0; i < _disposables.Count; i++) _disposables[i].Dispose();
            _disposables.Clear();
        }

        void OnRunStarted() => _started = true;

        /// <summary>Runs a system only between RunStart (every device loaded) and the end of the run.</summary>
        ITickable Gate(ITickable inner) => new TickAction(dt =>
        {
            if (_started && !_outcome.Ended) inner.Tick(dt, _scheduler.Loop.SimTick);
        });

        static int CountActive(PlayerStateTable players)
        {
            int count = 0;
            for (int p = 0; p < PlayerStateTable.Max; p++) if (players.Active[p]) count++;
            return count;
        }

        /// <summary>Benchmark runs have no combat authority; the idle weapon's claims go nowhere.</summary>
        sealed class DiscardClaims : IHitClaimSink
        {
            public void Submit(in HitClaim claim) { }
        }
    }
}
