using System.Collections.Generic;
using LastGround.App.Dev;
using LastGround.Core.Events;
using LastGround.Core.Ids;
using LastGround.Core.Net;
using LastGround.Core.Net.Session;
using LastGround.Core.Services;
using LastGround.Core.Tick;
using LastGround.Data.Combat;
using LastGround.Data.Crowd;
using LastGround.Data.Director;
using LastGround.Data.Loot;
using LastGround.Data.Map;
using LastGround.Data.Objectives;
using LastGround.Data.Players;
using LastGround.Data.Presentation;
using LastGround.Data.Quality;
using LastGround.Data.Upgrades;
using LastGround.Data.Weapons;
using LastGround.Data.Zombies;
using LastGround.Gameplay.Combat;
using LastGround.Gameplay.Crowd;
using LastGround.Gameplay.Loot;
using LastGround.Gameplay.Director;
using LastGround.Gameplay.Interactables;
using LastGround.Gameplay.Navigation;
using LastGround.Gameplay.Objectives;
using LastGround.Gameplay.Players;
using LastGround.Gameplay.Projectiles;
using LastGround.Gameplay.Run;
using LastGround.Gameplay.Upgrades;
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
        [SerializeField] MapDefinition _map;
        [SerializeField] NavGridAsset _navGrid;
        [SerializeField] CombatCatalog _combat;
        [SerializeField] SpawnDeckDefinition _spawnDeck;
        [SerializeField] PlayerDefinition _playerDefinition;
        [SerializeField] CameraProfile _cameraProfile;
        [SerializeField] DirectorProfile _directorProfile;
        [SerializeField] ThreatCurveDefinition _threatCurve;
        [SerializeField] PlayerCountScalingProfile _playerScaling;
        [SerializeField] UpgradeCatalog _upgrades;
        [SerializeField] LevelCurveDefinition _levelCurve;
        [SerializeField] LootDefinition _loot;
        [SerializeField] MapZoneSet _zones;
        [SerializeField] ObjectiveDefinition _clearArea;
        /// <summary>Every map event (M7); empty = Clear Area only.</summary>
        [SerializeField] ObjectiveDefinition[] _objectives;
        [SerializeField] InteractableProfile _interactables;

        ObjectiveDefinition[] Events => _objectives != null && _objectives.Length > 0 ? _objectives : new[] { _clearArea };
        [SerializeField] Material _coinMaterial;
        [SerializeField] Material _medkitMaterial;
        [SerializeField] Material _ammoMaterial;
        [SerializeField] Material _grenadeMaterial;
        [SerializeField] Material _weaponPickupMaterial;
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
        [SerializeField] LevelUpPanel _levelUp;
        [SerializeField] XpBar _xpBar;
        [SerializeField] ObjectivePanel _objectivePanel;
        [SerializeField] ObjectiveIndicator _objectiveIndicator;
        [SerializeField] WeaponHud _weaponHud;
        [SerializeField] MinimapHud _minimap;
        [SerializeField] RegionCard _regionCard;

        /// <summary>The map's regions, or the M5 greybox zones when no map is assigned.</summary>
        MapZoneSet Zones => _map != null && _map.Regions != null ? _map.Regions : _zones;

        NavGridAsset NavAsset => _map != null && _map.NavGrid != null ? _map.NavGrid : _navGrid;

        /// <summary>Player start: the map's spawn points by player index, else the M4 line.</summary>
        Vector2 SpawnPoint(int player)
        {
            if (_map != null && _map.PlayerSpawns != null && _map.PlayerSpawns.Length > 0)
                return _map.PlayerSpawns[player % _map.PlayerSpawns.Length];
            return new Vector2(player * 3f - 4.5f, -3f);
        }

        readonly List<System.IDisposable> _disposables = new List<System.IDisposable>();
        SessionService _service;
        TickScheduler _scheduler;
        bool _started;
        readonly RunOutcome _outcome = new RunOutcome();
        LevelUpPanel _pause;

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
            public LoadoutTable Loadouts;
            public LoadoutAuthority LoadoutAuthority;
            public ProjectileTable Projectiles;
            public EventChannel<ExplosionFx> Blasts;
            public PickupCollector Collector;
            public ExplosionSystem Explosions;
            public ProjectileSystem ProjectileSim;
            public InteractableTable Interactables;
            public InteractableSystem InteractableHost;
            public ZombieWorld World;
            public CombatAuthority Authority;
            public PlayerHealthSystem Health;
            public BenchmarkCrowdDriver Benchmark;
            public RunStatus Status;
            public HordeDirector Director;
            public RunReferee Referee;
            public TeamBuilds Builds;
            public TeamXp Xp;
            public LocalOffers Offers;
            public TeamProgress Progress;
            public PickupTable Pickups;
            public TeamWallet Wallet;
            public PickupRegistry Registry;
            public ObjectiveState Objective;
            public Gameplay.Boss.BossState Boss;
            public Gameplay.Boss.BossController BossHost;
            public Gameplay.Extraction.ExtractionState Extraction;
            public HordeSummary Horde;
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
                Nav = benchmark || NavAsset == null ? NavGrid.Open((int)BenchmarkWorldSize) : NavGrid.FromAsset(NavAsset),
                Players = new PlayerStateTable { Local = session.LocalPlayer },
                Shots = new EventChannel<ShotFired>(128),
                Status = new RunStatus(),
                Builds = new TeamBuilds(_upgrades),
                Xp = new TeamXp(),
                Offers = new LocalOffers(session.LocalPlayer.Value),
                Pickups = new PickupTable(256),
                Wallet = new TeamWallet(),
                Objective = new ObjectiveState(),
                Loadouts = new LoadoutTable(_combat.Weapons),
                Projectiles = new ProjectileTable(_combat.Projectiles),
                Blasts = new EventChannel<ExplosionFx>(32),
                Interactables = new InteractableTable(_map),
                Boss = new Gameplay.Boss.BossState(),
                Extraction = new Gameplay.Extraction.ExtractionState(),
                Horde = new HordeSummary(),
            };
            _disposables.Add(parts.Nav);
            float worldSize = parts.Nav.Width * parts.Nav.CellSize;
            PlayerStateTable players = parts.Players;

            var sync = new PlayerSync(session, players, _playerDefinition.MoveSpeed) { Builds = parts.Builds };
            _disposables.Add(sync);
            var vitals = new PlayerVitalsSync(session, players);
            _disposables.Add(vitals);
            var directorInfo = new DirectorInfoSync(session, parts.Status);
            _disposables.Add(directorInfo);
            var runEnd = new RunEndSync(session, _outcome);
            _disposables.Add(runEnd);

            IHitClaimSink claims = session.IsAuthority ? BuildHost(ref parts, loop, benchmark, seed) : BuildClient(ref parts, loop);
            if (!benchmark)
            {
                ApplyMeta(ref parts);
                BuildEmotes(parts, loop);
                BankWhenEnded(loop, session.LocalPlayer.IsValid ? session.LocalPlayer.Value : 0);
            }
            BuildBossSync(ref parts, loop);
            var objectiveSync = new ObjectiveSync(session, parts.Objective);
            _disposables.Add(objectiveSync);
            loop.Register(TickPhase.NetSend, objectiveSync);
            var lootSync = new LootSync(session, parts.Pickups, parts.Wallet, parts.Registry);
            _disposables.Add(lootSync);
            loop.Register(TickPhase.NetSend, lootSync);
            IPickupClaimSink pickupClaims = parts.Registry != null ? parts.Registry : (IPickupClaimSink)lootSync;
            var loadoutSync = new LoadoutSync(session, parts.Loadouts, parts.LoadoutAuthority);
            _disposables.Add(loadoutSync);
            loop.Register(TickPhase.NetSend, loadoutSync);
            var interactableSync = new InteractableSync(session, parts.Interactables, parts.InteractableHost);
            _disposables.Add(interactableSync);
            loop.Register(TickPhase.NetSend, interactableSync);
            var fxSync = new CombatFxSync(session, parts.Projectiles, parts.Blasts);
            _disposables.Add(fxSync);
            loop.Register(session.IsAuthority ? TickPhase.NetSend : TickPhase.Presentation, fxSync);
            var progression = new ProgressionSync(session, parts.Xp, parts.Builds, parts.Offers, parts.Progress);
            _disposables.Add(progression);
            loop.Register(TickPhase.NetSend, progression);
            if (parts.Progress != null)
            {
                parts.Progress.Offers = progression;
                parts.Offers.Choices = parts.Progress;
            }
            else
            {
                parts.Offers.Choices = progression;
            }

            // Local player: raw sticks → aim resolver → motor + weapon (TDD_01 §3.1).
            ISaveService save = AppServices.Get<ISaveService>();
            parts.Aim = new AimResolver(_input, players, parts.Crowd, parts.Nav, _combat.StartPrimary)
            {
                Mode = DevAutomation.ForceAutoFire ? Core.Input.ControlMode.AutoAimAutoFire : (Core.Input.ControlMode)save.Settings.ControlMode,
                AssistLevel = save.Settings.AimAssist * 0.5f,
            };
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Soak-test bot: walk to a downed teammate so revives happen without hands on the phones.
            Gameplay.Extraction.ExtractionState extractionState = parts.Extraction;
            var seeker = new DevPathSeeker(parts.Nav);
            loop.Register(TickPhase.Input, new TickAction(_ =>
            {
                Vector2 seek = DevReviveDirection(players);
                TouchTwinStickInput.DevSeek = seek != Vector2.zero ? seek : DevExtractionDirection(players, extractionState, seeker);
            }));
            if (DevAutomation.AutoGrenades) loop.Register(TickPhase.Input, new DevGrenadier(players, parts.Loadouts, parts.Crowd));
#endif
            loop.Register(TickPhase.Input, _input);
            loop.Register(TickPhase.Input, parts.Aim);
            parts.Weapon = new WeaponController(players, parts.Aim, parts.Loadouts, parts.Crowd, parts.Nav, seed, claims, parts.Shots,
                session.IsAuthority ? null : parts.Crowd as CrowdReplica, _combat)
            {
                Builds = parts.Builds, Grenades = loadoutSync, Aim = parts.Aim, Pickups = parts.Pickups,
                Interactables = parts.Interactables, InteractableHits = interactableSync,
            };
            parts.Aim.Ignore = parts.Weapon.PresumedDeadMask;
            _input.GrenadeTapDistance = _combat.TapThrowDistance;
            if (_combat.Grenade != null) _input.GrenadeMaxDistance = _combat.Grenade.MaxRange;
            if (benchmark)
            {
                // The benchmark player stands still at the centre of the horde.
                players.SetLocal(0f, 0f, 180f, 0f, 0f);
                parts.Loadouts.Set(session.LocalPlayer.Value, _combat.StartPrimary.NetIndex, _combat.StartSidearm.NetIndex, 0);
            }
            else
            {
                PlayerId me = session.LocalPlayer;
                Vector2 spawn = SpawnPoint(me.Value);
                loop.Register(TickPhase.LocalPlayer, Gate(new PlayerMotor(players, parts.Aim, _playerDefinition, worldSize,
                    spawn.x, spawn.y, parts.Nav, parts.Crowd) { Builds = parts.Builds, Weapon = parts.Weapon }));
                loop.Register(TickPhase.LocalPlayer, Gate(parts.Weapon));
                WeaponController weapon = parts.Weapon;
                parts.Collector = new PickupCollector(parts.Pickups, players, _loot, pickupClaims)
                {
                    Builds = parts.Builds, NeedsAmmo = () => weapon.NeedsAmmo, Loadouts = parts.Loadouts, MaxGrenades = _combat.MaxGrenades,
                };
                loop.Register(TickPhase.LocalPlayer, Gate(parts.Collector));
                if (_interactables != null)
                    loop.Register(TickPhase.LocalPlayer, Gate(new LocalStations(parts.Interactables, _interactables, players, parts.Weapon)));
            }
            loop.Register(TickPhase.NetSend, sync);
            loop.Register(TickPhase.NetSend, vitals);
            loop.Register(session.IsAuthority ? TickPhase.NetSend : TickPhase.Presentation, Gate(directorInfo));
            loop.Register(TickPhase.NetSend, runEnd);
            loop.Register(TickPhase.Presentation, new TickAction(_ => sync.Interpolate()));

            BuildPresentation(parts, loop);
            _statusHud.Bind(parts.Status, _combat);
            _weaponHud.Bind(parts.Weapon, parts.Collector, parts.Pickups, parts.Loadouts);
            _input.Blockers.Add(_weaponHud.TakeButtonRect);
            _xpBar.Bind(parts.Xp);
            _objectivePanel.Bind(parts.Objective, Zones, Events);
            _objectiveIndicator.Bind(parts.Objective, Zones, _camera);
            if (_minimap != null && _map != null) _minimap.Bind(_map, players, parts.Crowd);
            if (_regionCard != null) _regionCard.Bind(Zones, players);
            _levelUp.Bind(parts.Offers, _upgrades, () => CountActive(players) <= 1);
            _pause = _levelUp;
            _input.Blocker = () => _levelUp.BlockingRect;
            _teamPanel.Bind(players, session, _playerDefinition.MaxHealth);
            _teammateIndicators.Bind(players, _camera);
            _results.Bind(_outcome, _service, session.LocalPlayer.IsValid ? session.LocalPlayer.Value : 0);
            _combatHud.SetOutcome(_outcome);
            _combatHud.SetWallet(parts.Wallet);
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
            telemetry.BindProgress(parts.Xp, parts.Builds);
            telemetry.BindBoss(parts.Boss, parts.BossHost, parts.Extraction);
            telemetry.BindLoot(parts.Wallet, parts.Registry);
            telemetry.BindContent(parts.LoadoutAuthority, parts.Explosions, parts.ProjectileSim);
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
            parts.Health = new PlayerHealthSystem(parts.Players, _playerDefinition) { Builds = parts.Builds };
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

            var world = parts.World = new ZombieWorld(crowd, parts.Players, parts.Nav, new ZombieTuning(), _combat.Zombies, seed)
            {
                Elites = _combat.Elites, MinSlowMultiplier = _combat.MinSlowMultiplier,
            };
            _disposables.Add(world);
            world.DamageSink = parts.Health;
            world.Respawns = parts.Health.Respawns;
            var explosions = parts.Explosions = new ExplosionSystem(world, parts.Players, parts.Health, parts.Blasts);
            var projectiles = parts.ProjectileSim = new ProjectileSystem(parts.Projectiles, _combat.Projectiles, parts.Players, parts.Nav,
                parts.Health, explosions);
            world.ExplosionSink = explosions;
            world.ProjectileLauncher = projectiles;
            parts.LoadoutAuthority = new LoadoutAuthority(parts.Loadouts, parts.Players, _combat, projectiles);
            var footprint = new CameraFootprint(_cameraProfile, 3f);
            parts.Director = new HordeDirector(world, parts.Players, _directorProfile, _threatCurve, _playerScaling, footprint,
                _playerDefinition.MaxHealth, parts.Status, seed);
            parts.Director.Governor.TargetFrameSeconds = 1f / Mathf.Max(30, parts.Preset.TargetFps);
            parts.Director.Deck = _spawnDeck;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (DevAutomation.RunTimeSkip > 0f) parts.Director.SkipTo(DevAutomation.RunTimeSkip);
#endif
            HordeDirector director = parts.Director;
            loop.Register(TickPhase.Director, Gate(director));
            loop.Register(TickPhase.Presentation, new TickAction(_ => director.Governor.Report(Time.unscaledDeltaTime)));
            loop.Register(TickPhase.ZombieSim, Gate(world));

            parts.Authority = new CombatAuthority(world, parts.Players, parts.Nav, _combat.Weapons, seed)
            {
                Builds = parts.Builds, Loadouts = parts.Loadouts, Catalog = _combat,
            };
            CombatAuthority authority = parts.Authority;
            world.KillSink = parts.Health;
            parts.Referee.Kills = () => authority.Kills;
            parts.Progress = new TeamProgress(crowd, parts.Players, parts.Builds, _levelCurve, _combat.Zombies[0].Xp, seed)
            {
                ZombieTypes = _combat.Zombies,
            };
            loop.Register(TickPhase.Combat, Gate(parts.Progress));
            parts.Registry = new PickupRegistry(parts.Pickups, crowd, parts.Players, _loot, _combat.Zombies[0], parts.Wallet, seed)
            {
                Health = parts.Health, Builds = parts.Builds, ZombieTypes = _combat.Zombies, Loadouts = parts.LoadoutAuthority,
                Weapons = _combat.Weapons,
            };
            loop.Register(TickPhase.LootEvents, Gate(parts.Registry));
            TeamWallet wallet = parts.Wallet;
            parts.Referee.Coins = () => wallet.Coins;
            var objectives = new ObjectiveSystem(crowd, parts.Players, Zones, Events, parts.Status, parts.Objective, seed)
            {
                Director = parts.Director, Loot = parts.Registry, Map = _map, World = world, Progress = parts.Progress,
                Health = parts.Health, Turret = new SentryTurret(world, parts.Nav, parts.Shots),
            };
            if (_interactables != null)
            {
                parts.InteractableHost = new InteractableSystem(parts.Interactables, _interactables, parts.Players, parts.Nav, explosions);
                explosions.Listener = parts.InteractableHost;
                loop.Register(TickPhase.Combat, Gate(parts.InteractableHost));
            }
            loop.Register(TickPhase.LootEvents, Gate(objectives));
            loop.Register(TickPhase.Combat, Gate(parts.LoadoutAuthority));
            loop.Register(TickPhase.Combat, Gate(parts.Authority));
            loop.Register(TickPhase.Combat, Gate(projectiles));
            loop.Register(TickPhase.Combat, Gate(explosions));
            var claimSync = new HitClaimSync(parts.Session, parts.Authority);
            _disposables.Add(claimSync);

            var sender = new CrowdReplicationSender(parts.Session, crowd, parts.Players, new ReplicationTuning());
            _disposables.Add(sender);
            loop.Register(TickPhase.NetSend, sender);
            BuildBossHost(ref parts, loop, world, objectives, sender, seed);
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
            // Solo only: an open level-up panel pauses the run (co-op never pauses, TDD_01 §7.5).
            if (_started && !_outcome.Ended && (_pause == null || !_pause.WantsPause)) inner.Tick(dt, _scheduler.Loop.SimTick);
        });

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        static Vector2 DevReviveDirection(PlayerStateTable players)
        {
            int me = players.Local.IsValid ? players.Local.Value : -1;
            if (me < 0 || !players.CanAct(me)) return Vector2.zero;
            for (int p = 0; p < PlayerStateTable.Max; p++)
            {
                if (p == me || !players.IsDowned(p)) continue;
                players.GetDisplay(p, out float x, out float z, out _);
                var to = new Vector2(x - players.X[me], z - players.Z[me]);
                return to.sqrMagnitude > 1f ? to.normalized : Vector2.zero;
            }
            return Vector2.zero;
        }
#endif

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
