using System.Collections.Generic;
using LastGround.Core.Ids;
using LastGround.Data.Boss;
using LastGround.Data.Map;
using LastGround.Data.Players;
using LastGround.Gameplay.Boss;
using LastGround.Gameplay.Director;
using LastGround.Gameplay.Extraction;
using LastGround.Gameplay.Players;
using LastGround.Gameplay.Run;
using NUnit.Framework;
using UnityEngine;

namespace LastGround.Tests
{
    /// <summary>M8 extraction windows and reward conversion (TDD_01 §2.2, D-004).</summary>
    public class ExtractionTests
    {
        const float Dt = 1f / 30f;
        readonly List<Object> _assets = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object a in _assets) Object.DestroyImmediate(a);
            _assets.Clear();
        }

        T Asset<T>() where T : ScriptableObject
        {
            var a = ScriptableObject.CreateInstance<T>();
            _assets.Add(a);
            return a;
        }

        sealed class Rig
        {
            public readonly PlayerStateTable Players = new PlayerStateTable { Local = new PlayerId(0) };
            public readonly RunStatus Status = new RunStatus();
            public readonly BossState Boss = new BossState();
            public readonly ExtractionState State = new ExtractionState();
            public readonly RunOutcome Outcome = new RunOutcome();
            public ExtractionRulesDefinition Rules;
            public PlayerHealthSystem Health;
            public RunReferee Referee;
            public ExtractionController Controller;
            public int Coins = 100;

            public void Run(float seconds)
            {
                for (int t = 0; t < seconds * 30f; t++)
                {
                    Status.RunSeconds += Dt;
                    Controller.Tick(Dt, (uint)t);
                    Referee.Tick(Dt, (uint)t);
                }
            }

            public void MoveTo(float x, float z) => Players.SetLocal(x, z, 0f, 0f, 0f);
        }

        Rig Create()
        {
            var zones = Asset<MapZoneSet>();
            zones.Zones = new[]
            {
                new MapZoneSet.Zone { Id = "a", NameKey = "zone.a", Center = new Vector2(-30f, 0f), HalfSize = new Vector2(20f, 20f) },
                new MapZoneSet.Zone { Id = "b", NameKey = "zone.b", Center = new Vector2(30f, 0f), HalfSize = new Vector2(20f, 20f) },
            };
            var map = Asset<MapDefinition>();
            map.Regions = zones;
            map.Anchors = new[]
            {
                new MapDefinition.Anchor { Id = "a_lz", Kind = MapAnchorKind.Extraction, Region = 0, Position = new Vector2(-30f, 0f) },
                new MapDefinition.Anchor { Id = "b_lz", Kind = MapAnchorKind.Extraction, Region = 1, Position = new Vector2(30f, 0f) },
            };
            var rig = new Rig { Rules = Asset<ExtractionRulesDefinition>() };
            rig.MoveTo(-30f, 5f);
            var player = Asset<PlayerDefinition>();
            player.SoloAdrenaline = 0;
            rig.Health = new PlayerHealthSystem(rig.Players, player);
            rig.Status.Threat = 4;
            rig.Referee = new RunReferee(rig.Health, rig.Status, rig.Outcome) { Rules = rig.Rules };
            rig.Referee.Coins = () => rig.Coins;
            rig.Controller = new ExtractionController(rig.Players, rig.Rules, map, rig.Boss, rig.State, rig.Referee, 5u);
            return rig;
        }

        [Test]
        public void BossKill_OpensAZoneInAnotherRegion_WhichClosesUnheld()
        {
            Rig rig = Create();
            rig.Run(5f);
            Assert.AreEqual(ExtractionPhase.None, rig.State.Phase, "no window before a boss dies");

            rig.Boss.Set(BossPhase.Dead, -1, 0f, 1f, 0, false, 1);
            rig.Run(rig.Rules.AfterBossDelay + 0.2f);
            Assert.AreEqual(ExtractionPhase.Open, rig.State.Phase);
            Assert.AreEqual(1, rig.State.Region, "the team stands in region 0: the zone opens in region 1");
            Assert.AreEqual(30f, rig.State.X, 0.01f);
            Assert.AreEqual((int)rig.Rules.WindowSeconds, rig.State.SecondsLeft, 1);

            rig.Run(rig.Rules.WindowSeconds + 1f);
            Assert.AreEqual(ExtractionPhase.Missed, rig.State.Phase);
            Assert.IsFalse(rig.Outcome.Ended, "the run goes on");
            rig.Run(5f);
            Assert.AreEqual(ExtractionPhase.None, rig.State.Phase);
            rig.Run(rig.Rules.IntervalSeconds);
            Assert.AreEqual(ExtractionPhase.Open, rig.State.Phase, "the next window comes periodically");
        }

        [Test]
        public void HoldingTheZone_Extracts_WithCoinsPlusThreatBonus()
        {
            Rig rig = Create();
            rig.Controller.OpenSoon(0.5f);
            rig.Run(1f);
            Assert.AreEqual(ExtractionPhase.Open, rig.State.Phase);

            rig.MoveTo(rig.State.X + 1f, rig.State.Z);
            rig.Run(rig.Rules.HoldSeconds * 0.5f);
            Assert.IsTrue(rig.State.Holding);
            int half = rig.State.Hold;
            Assert.Greater(half, 0);
            int windowAtHalf = rig.State.SecondsLeft;

            rig.MoveTo(0f, 0f);
            rig.Run(2f);
            Assert.AreEqual(half, rig.State.Hold, 1, "progress is kept while away");
            Assert.Less(rig.State.SecondsLeft, windowAtHalf, "the window runs while nobody holds");

            rig.MoveTo(rig.State.X, rig.State.Z);
            rig.Run(rig.Rules.HoldSeconds * 0.5f + 0.5f);
            Assert.AreEqual(ExtractionPhase.Extracted, rig.State.Phase);
            Assert.IsTrue(rig.Outcome.Ended);
            RunResult result = rig.Outcome.Result;
            Assert.IsTrue(result.Extracted);
            Assert.AreEqual(1, result.StandingMask);
            Assert.AreEqual(rig.Rules.BonusPerThreat * 4, result.ExtractionBonus);
            Assert.AreEqual(100 + rig.Rules.BonusPerThreat * 4, result.BankedFor(0));
            Assert.AreEqual(100, result.BankedFor(1), "a teammate who was down keeps the base");
        }

        [Test]
        public void TeamWipe_KeepsSixtyPercent()
        {
            Rig rig = Create();
            rig.Health.Tick(Dt, 0);
            rig.Health.Damage(0, 10000f);
            for (int t = 0; t < 60 * 30 && !rig.Outcome.Ended; t++)
            {
                rig.Health.Tick(Dt, (uint)t);
                rig.Referee.Tick(Dt, (uint)t);
            }
            Assert.IsTrue(rig.Outcome.Ended);
            Assert.IsFalse(rig.Outcome.Result.Extracted);
            Assert.AreEqual(60, rig.Outcome.Result.BankedFor(0));
        }
    }
}
