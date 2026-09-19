using LastGround.App.Dev;
using LastGround.Core.Tick;
using LastGround.Data.Boss;
using LastGround.Gameplay.Boss;
using LastGround.Gameplay.Director;
using LastGround.Gameplay.Extraction;
using LastGround.Gameplay.Objectives;
using LastGround.Gameplay.Players;
using LastGround.Gameplay.Zombies;
using LastGround.Networking.Replication;
using UnityEngine;

namespace LastGround.App
{
    /// <summary>
    /// M8 wiring: the boss, extraction windows, virtual horde summary and their replication (TDD_01 §2.2, §10;
    /// TDD_02 §17.7). Host builds the controllers; every device gets the shared states and syncs.
    /// </summary>
    public sealed partial class RunInstaller
    {
        [SerializeField] BossDefinition _boss;
        [SerializeField] ExtractionRulesDefinition _extraction;
        [SerializeField] UI.Run.BossHealthBar _bossBar;
        [SerializeField] UI.Run.ExtractionHud _extractionHud;
        /// <summary>Boss Prop Throw debris (concrete).</summary>
        [SerializeField] Material _debrisMaterial;

        /// <summary>Host: boss controller, extraction controller, director summary, boss priority in replication.</summary>
        void BuildBossHost(ref RunParts parts, TickLoop loop, ZombieWorld world, ObjectiveSystem objectives, CrowdReplicationSender sender,
            uint seed)
        {
            parts.Director.Summary = parts.Horde;
            parts.Referee.Rules = _extraction;
            Gameplay.Boss.BossState bossState = parts.Boss;
            parts.Referee.BossKills = () => bossState.Defeats;
            if (_boss != null && _boss.Zombie != null)
            {
                var boss = new BossController(world, parts.Players, _boss, parts.Status, parts.Boss, seed)
                {
                    Director = parts.Director, Damage = parts.Health, Loot = parts.Registry, Barrels = parts.InteractableHost,
                };
                world.DamageModifier = boss;
                sender.PriorityType = _boss.Zombie.TypeIndex;
                loop.Register(TickPhase.Combat, Gate(boss));
                parts.BossHost = boss;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (DevAutomation.BossAt >= 0f)
                {
                    float at = DevAutomation.BossAt;
                    RunStatus status = parts.Status;
                    bool fired = false;
                    loop.Register(TickPhase.Combat, new TickAction(_ =>
                    {
                        if (fired || status.RunSeconds - DevAutomation.RunTimeSkip < at) return;
                        fired = true;
                        boss.ForceAppear();
                    }));
                }
#endif
            }
            if (_extraction != null)
            {
                var extraction = new ExtractionController(parts.Players, _extraction, _map, parts.Boss, parts.Extraction, parts.Referee, seed)
                {
                    Director = parts.Director, Objectives = objectives,
                };
                loop.Register(TickPhase.Combat, Gate(extraction));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (DevAutomation.ExtractAt >= 0f) extraction.OpenSoon(DevAutomation.ExtractAt);
#endif
            }
        }

        /// <summary>Every device: boss, extraction and horde summary replication.</summary>
        void BuildBossSync(ref RunParts parts, TickLoop loop)
        {
            var bossSync = new BossSync(parts.Session, parts.Boss);
            _disposables.Add(bossSync);
            loop.Register(TickPhase.NetSend, bossSync);
            var extractionSync = new ExtractionSync(parts.Session, parts.Extraction);
            _disposables.Add(extractionSync);
            loop.Register(TickPhase.NetSend, extractionSync);
            var hordeSync = new HordeSummarySync(parts.Session, parts.Horde);
            _disposables.Add(hordeSync);
            loop.Register(TickPhase.NetSend, hordeSync);
        }

        /// <summary>Every device: boss telegraphs and weak point, landing zone, boss bar, extraction line, horde ring.</summary>
        void BuildBossPresentation(in RunParts parts, TickLoop loop, Rendering.TopDownCameraRig cameraRig)
        {
            var bossView = new Rendering.Boss.BossView(parts.Boss, _boss, parts.Crowd, cameraRig, _tracerMaterial, _debrisMaterial);
            _disposables.Add(bossView);
            loop.Register(TickPhase.Presentation, bossView);
            var zoneView = new Rendering.Boss.ExtractionZoneView(parts.Extraction, _tracerMaterial, _bloodParticleMaterial);
            _disposables.Add(zoneView);
            loop.Register(TickPhase.Presentation, zoneView);
            if (_bossBar != null) _bossBar.Bind(parts.Boss, _boss);
            if (_extractionHud != null)
                _extractionHud.Bind(parts.Extraction, Zones, _camera, (RectTransform)_extractionHud.GetComponentInParent<Canvas>().transform);
            if (_minimap != null) _minimap.BindHorde(parts.Horde);
        }

        /// <summary>Dev bot (-lg-extract): walk to an open landing zone and stay in it.</summary>
        static Vector2 DevExtractionDirection(PlayerStateTable players, ExtractionState extraction, DevPathSeeker seeker)
        {
            int me = players.Local.IsValid ? players.Local.Value : -1;
            if (me < 0 || DevAutomation.ExtractAt < 0f || !extraction.IsOpen || !players.CanAct(me)) return Vector2.zero;
            var from = new Unity.Mathematics.float2(players.X[me], players.Z[me]);
            var goal = new Unity.Mathematics.float2(extraction.X, extraction.Z);
            if (Unity.Mathematics.math.distancesq(from, goal) <= extraction.Radius * extraction.Radius * 0.25f) return Vector2.zero;
            return seeker.Direction(from, goal);
        }
    }
}
