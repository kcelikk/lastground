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

        /// <summary>Host: boss controller, extraction controller, director summary, boss priority in replication.</summary>
        void BuildBossHost(ref RunParts parts, TickLoop loop, ZombieWorld world, ObjectiveSystem objectives, CrowdReplicationSender sender,
            uint seed)
        {
            parts.Director.Summary = parts.Horde;
            parts.Referee.Rules = _extraction;
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

        /// <summary>Dev bot (-lg-extract): walk to an open landing zone and stay in it.</summary>
        static Vector2 DevExtractionDirection(PlayerStateTable players, ExtractionState extraction)
        {
            int me = players.Local.IsValid ? players.Local.Value : -1;
            if (me < 0 || DevAutomation.ExtractAt < 0f || !extraction.IsOpen || !players.CanAct(me)) return Vector2.zero;
            var to = new Vector2(extraction.X - players.X[me], extraction.Z - players.Z[me]);
            return to.sqrMagnitude > extraction.Radius * extraction.Radius * 0.25f ? to.normalized : Vector2.zero;
        }
    }
}
