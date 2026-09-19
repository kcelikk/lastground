using System.Collections.Generic;
using LastGround.Core.Ids;
using LastGround.Data.Boss;
using LastGround.Data.Combat;
using LastGround.Gameplay.Boss;
using LastGround.Gameplay.Crowd;
using LastGround.Gameplay.Director;
using LastGround.Gameplay.Navigation;
using LastGround.Gameplay.Players;
using LastGround.Gameplay.Zombies;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace LastGround.Tests
{
    /// <summary>M8 boss (TDD_01 §10, D-021): appearance, scaling, intro, weak point, phases, telegraphs, defeat.</summary>
    public class BossTests
    {
        const float Dt = 1f / 30f;

        sealed class Recorder : IPlayerDamageSink
        {
            public readonly List<(float time, int player, float amount)> Hits = new List<(float, int, float)>(4096);
            public float Now;

            public void Damage(int player, float amount) => Hits.Add((Now, player, amount));
            public void Slow(int player, float multiplier, float seconds) { }
        }

        sealed class Rig : System.IDisposable
        {
            public readonly CrowdState Crowd = new CrowdState(128);
            public readonly PlayerStateTable Players = new PlayerStateTable { Local = new PlayerId(0) };
            public readonly NavGrid Nav = NavGrid.Open(120);
            public readonly RunStatus Status = new RunStatus();
            public readonly BossState State = new BossState();
            public readonly Recorder Damage = new Recorder();
            public readonly ZombieWorld World;
            public readonly BossController Boss;
            public readonly BossDefinition Definition;
            public float Time;
            uint _tick;

            public Rig()
            {
                var catalog = AssetDatabase.LoadAssetAtPath<CombatCatalog>("Assets/ScriptableObjects/Combat/CMB_Catalog.asset");
                Definition = AssetDatabase.LoadAssetAtPath<BossDefinition>("Assets/ScriptableObjects/Boss/BOSS_MutantBrute.asset");
                Players.SetLocal(0f, 0f, 0f, 0f, 0f);
                World = new ZombieWorld(Crowd, Players, Nav, new ZombieTuning(), catalog.Zombies, 3u);
                Boss = new BossController(World, Players, Definition, Status, State, 3u) { Damage = Damage };
                World.DamageModifier = Boss;
            }

            public void Run(float seconds)
            {
                for (int t = 0; t < seconds * 30f; t++)
                {
                    Time += Dt;
                    Damage.Now = Time;
                    World.Tick(Dt, _tick);
                    Boss.Tick(Dt, _tick);
                    _tick++;
                }
            }

            public void Dispose()
            {
                World.Dispose();
                Nav.Dispose();
            }
        }

        [Test]
        public void Boss_AppearsAtThreatIV_OffScreen_WithScaledHealth_AndAnnounces()
        {
            using (var rig = new Rig())
            {
                var announcements = rig.Status.Announcements.CreateReader();
                rig.Status.Threat = rig.Definition.FirstThreat - 1;
                rig.Run(1f);
                Assert.IsFalse(rig.State.Active, "not before its threat level");

                rig.Status.Threat = rig.Definition.FirstThreat;
                rig.Run(Dt);
                Assert.AreEqual(BossPhase.Intro, rig.State.Phase);
                Assert.AreEqual(rig.Definition.HealthFor(1, 0), rig.State.MaxHealth, 1e-3f);
                Assert.AreEqual(rig.Definition.BaseHealth, rig.State.MaxHealth, 1e-3f, "solo: base health");
                float2 at = rig.World.PositionOf(rig.State.Slot);
                Assert.GreaterOrEqual(math.length(at), rig.Definition.SpawnDistance * 0.5f - 0.1f, "appears away from the player");
                Assert.IsTrue(rig.World.IsPinned(rig.State.Slot));
                Assert.IsTrue(rig.Status.Announcements.TryRead(ref announcements, out DirectorAnnouncement a));
                Assert.AreEqual(AnnouncementKind.BossArrived, a.Kind);
            }
        }

        [Test]
        public void Intro_IsInvulnerable_ThenTheBackTakesDoubleDamage()
        {
            using (var rig = new Rig())
            {
                rig.Boss.ForceAppear();
                rig.Run(Dt);
                int slot = rig.State.Slot;
                float full = rig.World.HealthOf(slot);
                rig.World.ApplyDamage(slot, 100f, new float2(0f, 1f), 0f, false, false, 0);
                Assert.AreEqual(full, rig.World.HealthOf(slot), 1e-3f, "roaring intro: no damage");

                rig.Run(rig.Definition.IntroSeconds + 0.1f);
                Assert.AreNotEqual(BossPhase.Intro, rig.State.Phase);
                float heading = rig.Crowd.Heading[slot];
                math.sincos(math.radians(heading), out float s, out float c);
                var facing = new float2(s, c);

                float before = rig.World.HealthOf(slot);
                rig.World.ApplyDamage(slot, 100f, -facing, 0f, false, false, 0);
                float front = before - rig.World.HealthOf(slot);
                before = rig.World.HealthOf(slot);
                rig.World.ApplyDamage(slot, 100f, facing, 0f, false, false, 0);
                float back = before - rig.World.HealthOf(slot);
                Assert.AreEqual(100f, front, 1e-3f, "shot in the face");
                Assert.AreEqual(200f, back, 1e-3f, "shot in the tumour on its back");
                Assert.AreEqual(1, rig.Boss.WeakPointHits);
            }
        }

        [Test]
        public void Phases_FollowHealth_AndNeverGoBack()
        {
            using (var rig = new Rig())
            {
                rig.Boss.ForceAppear();
                rig.Run(rig.Definition.IntroSeconds + 0.2f);
                int slot = rig.State.Slot;
                Assert.AreEqual(BossPhase.Phase1, rig.State.Phase);
                float max = rig.State.MaxHealth;
                rig.World.SetHealth(slot, max * (rig.Definition.Phase2At - 0.05f));
                rig.Run(Dt);
                Assert.AreEqual(BossPhase.Phase2, rig.State.Phase);
                rig.World.SetHealth(slot, max * (rig.Definition.EnragedAt - 0.05f));
                rig.Run(Dt);
                Assert.AreEqual(BossPhase.Enraged, rig.State.Phase);
                rig.World.SetHealth(slot, max);
                rig.Run(Dt);
                Assert.AreEqual(BossPhase.Enraged, rig.State.Phase, "healing does not calm it down");
            }
        }

        [Test]
        public void Attacks_TelegraphAtLeast08s_AndOnlyHitAfterTheTelegraph()
        {
            using (var rig = new Rig())
            {
                var attacks = rig.State.Attacks.CreateReader();
                rig.Boss.ForceAppear();
                rig.Run(rig.Definition.IntroSeconds + 0.2f);
                var started = new List<(float time, BossAttackStarted attack)>();
                for (int s = 0; s < 60 * 30 && started.Count < 4; s++)
                {
                    rig.Run(Dt);
                    while (rig.State.Attacks.TryRead(ref attacks, out BossAttackStarted a)) started.Add((rig.Time, a));
                }
                Assert.GreaterOrEqual(started.Count, 2, "the boss attacks within a minute");
                rig.Run(3f);
                foreach ((float time, BossAttackStarted attack) in started)
                {
                    BossAttackDefinition definition = rig.Definition.Attacks[attack.Attack];
                    Assert.GreaterOrEqual(definition.TelegraphSeconds, BossAttackDefinition.MinTelegraph, definition.Id);
                    foreach ((float hitTime, int _, float _) in rig.Damage.Hits)
                    {
                        if (hitTime < time || hitTime > time + definition.TelegraphSeconds) continue;
                        Assert.GreaterOrEqual(hitTime, time + definition.TelegraphSeconds - 2f * Dt,
                            definition.Id + ": no damage before the telegraph ends");
                    }
                }
                Assert.Greater(rig.Damage.Hits.Count, 0, "a player standing still gets hit");
            }
        }

        [Test]
        public void Summon_BringsRunners_AndDefeatDropsTheBossForAReturnLater()
        {
            using (var rig = new Rig())
            {
                rig.Boss.ForceAppear();
                rig.Run(rig.Definition.IntroSeconds + 0.2f);
                rig.Run(40f);
                Assert.Greater(rig.Boss.AttacksStarted, 2);
                Assert.Greater(rig.Boss.Summoned, 0, "summon scream within 40 s");

                int slot = rig.State.Slot;
                rig.World.SetHealth(slot, 1f);
                rig.World.ApplyDamage(slot, 50f, float2.zero, 0f, false, false, 0);
                rig.Run(Dt);
                Assert.AreEqual(BossPhase.Dead, rig.State.Phase);
                Assert.AreEqual(1, rig.State.Defeats);
                Assert.IsFalse(rig.State.Active);

                rig.Status.Threat = rig.Definition.ReturnFromThreat - 1;
                rig.Run(1f);
                Assert.IsFalse(rig.State.Active, "no return before its threat level");
                rig.Status.Threat = rig.Definition.ReturnFromThreat;
                rig.Run(Dt);
                Assert.IsTrue(rig.State.Active, "comes back later");
                Assert.AreEqual(1, rig.State.Appearance);
                Assert.Greater(rig.State.MaxHealth, rig.Definition.BaseHealth, "tougher on return");
            }
        }

        [Test]
        public void BossFight_TicksWithoutAllocating()
        {
            using (var rig = new Rig())
            {
                rig.Boss.ForceAppear();
                rig.Run(rig.Definition.IntroSeconds + 5f);
                long before = System.GC.GetAllocatedBytesForCurrentThread();
                rig.Run(3f);
                long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.IsTrue(rig.State.Active);
                Assert.AreEqual(0, allocated, "boss + world tick: 0 B");
            }
        }
    }
}
