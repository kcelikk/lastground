using System.Collections.Generic;
using LastGround.Core.Ids;
using LastGround.Data.Players;
using LastGround.Data.Zombies;
using LastGround.Gameplay.Crowd;
using LastGround.Gameplay.Director;
using LastGround.Gameplay.Navigation;
using LastGround.Gameplay.Players;
using LastGround.Gameplay.Run;
using LastGround.Gameplay.Zombies;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace LastGround.Tests
{
    /// <summary>M5 downed / revive / dead / team wipe (TDD_01 §14.2).</summary>
    public class LifeTests
    {
        const float Dt = 1f / 30f;
        readonly List<Object> _assets = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object a in _assets) Object.DestroyImmediate(a);
            _assets.Clear();
        }

        PlayerDefinition Definition()
        {
            var d = ScriptableObject.CreateInstance<PlayerDefinition>();
            _assets.Add(d);
            return d;
        }

        static PlayerStateTable Players(int count)
        {
            var players = new PlayerStateTable { Local = new PlayerId(0) };
            players.SetLocal(0f, 0f, 0f, 0f, 0f);
            for (int p = 1; p < count; p++) players.PushRemote(new PlayerId((byte)p), 30f, 0f, 0f, 0f, 0f, 0.0, 0.0);
            return players;
        }

        static void Run(PlayerHealthSystem health, float seconds)
        {
            for (int t = 0; t < seconds * 30f; t++) health.Tick(Dt, (uint)t);
        }

        static void Place(PlayerStateTable players, int p, float x, float z)
        {
            players.X[p] = x;
            players.Z[p] = z;
        }

        [Test]
        public void Solo_AdrenalineGetsYouUpOnce_ThenGoingDownEndsTheRun()
        {
            PlayerDefinition d = Definition();
            PlayerStateTable players = Players(1);
            var health = new PlayerHealthSystem(players, d);
            Run(health, Dt);

            health.Damage(0, 150f);
            Assert.AreEqual(PlayerLife.Downed, players.Life[0]);
            Assert.IsFalse(players.CanAct(0), "downed players cannot shoot");
            Assert.IsTrue(players.IsTargetable(0), "but zombies still come for them");
            Run(health, d.ReviveTime + 0.2f);
            Assert.AreEqual(PlayerLife.Alive, players.Life[0], "adrenaline self-revive");
            Assert.AreEqual(d.MaxHealth * d.ReviveHealthFraction, players.Health[0], 0.01f);
            Assert.IsTrue(players.Invulnerable[0]);
            Assert.AreEqual(0, health.AdrenalineLeft);
            Assert.IsFalse(health.TeamWiped);

            Run(health, d.ReviveInvulnerability + 0.1f);
            health.Damage(0, 150f);
            Run(health, 0.1f);
            Assert.AreEqual(PlayerLife.Dead, players.Life[0], "no adrenaline left: down means out");
            Assert.IsTrue(health.TeamWiped);
        }

        [Test]
        public void Teammate_StandingNextToYou_Revives_InFourSeconds()
        {
            PlayerDefinition d = Definition();
            PlayerStateTable players = Players(2);
            var health = new PlayerHealthSystem(players, d);
            Run(health, Dt);

            health.Damage(1, 200f);
            Assert.AreEqual(PlayerLife.Downed, players.Life[1]);
            Place(players, 0, 30f, 1.5f); // within 2 m
            Run(health, d.ReviveTime * 0.5f);
            Assert.AreEqual(0.5f, players.ReviveProgress[1], 0.05f);
            Place(players, 0, 0f, 0f); // walks away: progress fades, not reset
            Run(health, 1f);
            Assert.AreEqual(0.5f - d.ReviveDecayPerSecond, players.ReviveProgress[1], 0.05f);
            Place(players, 0, 30f, 1.5f);
            Run(health, d.ReviveTime * 0.8f);
            Assert.AreEqual(PlayerLife.Alive, players.Life[1]);
            Assert.AreEqual(1, health.Revives);
            Assert.AreEqual(d.MaxHealth * d.ReviveHealthFraction, players.Health[1], 0.01f);
        }

        [Test]
        public void Reviving_UnderFire_IsSlower()
        {
            PlayerDefinition d = Definition();
            PlayerStateTable players = Players(2);
            var health = new PlayerHealthSystem(players, d);
            Run(health, Dt);
            health.Damage(1, 200f);
            Place(players, 0, 30f, 1f);
            for (int t = 0; t < 30; t++)
            {
                health.Damage(0, 0.01f); // being clawed while reviving
                health.Tick(Dt, (uint)t);
            }
            Assert.AreEqual(1f / d.ReviveTime * d.HurtReviveFactor, players.ReviveProgress[1], 0.03f);
        }

        [Test]
        public void Bleedout_KillsTheDowned_AndTheyReturnWhileTheTeamStands()
        {
            PlayerDefinition d = Definition();
            PlayerStateTable players = Players(2);
            var health = new PlayerHealthSystem(players, d);
            var respawns = health.Respawns.CreateReader();
            Run(health, Dt);
            health.Damage(1, 200f);
            Run(health, d.BleedoutTime + 0.2f);
            Assert.AreEqual(PlayerLife.Dead, players.Life[1]);
            Assert.AreEqual(1, health.Deaths);
            Assert.IsFalse(health.TeamWiped, "one player still standing");
            Assert.IsFalse(players.IsTargetable(1));

            Run(health, d.RespawnDelay + 0.2f);
            Assert.AreEqual(PlayerLife.Alive, players.Life[1]);
            Assert.AreEqual(d.MaxHealth * d.RespawnHealthFraction, players.Health[1], 0.01f);
            Assert.IsTrue(health.Respawns.TryRead(ref respawns, out PlayerRespawn back));
            Assert.AreEqual(1, back.Player);
        }

        [Test]
        public void ThirdDownInOneLife_BleedsOutTwiceAsFast()
        {
            PlayerDefinition d = Definition();
            PlayerStateTable players = Players(2);
            var health = new PlayerHealthSystem(players, d);
            Run(health, Dt);
            for (int down = 1; down <= 3; down++)
            {
                Place(players, 0, 0f, 0f);
                health.Damage(1, 500f);
                float expected = down >= d.FastBleedoutFromDown ? d.BleedoutTime * d.FastBleedoutScale : d.BleedoutTime;
                Assert.AreEqual(expected, players.Countdown[1], 0.01f, "down " + down);
                Place(players, 0, 30f, 1f);
                Run(health, d.ReviveTime + d.ReviveInvulnerability + 0.3f);
                Assert.AreEqual(PlayerLife.Alive, players.Life[1]);
            }
        }

        [Test]
        public void TeamWipe_EndsTheRun_WithTheResult()
        {
            PlayerDefinition d = Definition();
            PlayerStateTable players = Players(2);
            var health = new PlayerHealthSystem(players, d);
            var status = new RunStatus { RunSeconds = 312f, Threat = 3 };
            var outcome = new RunOutcome();
            var referee = new RunReferee(health, status, outcome) { Kills = () => 480 };
            Run(health, Dt);
            referee.Tick(Dt, 0);
            health.Damage(0, 500f);
            health.Damage(1, 500f);
            health.Tick(Dt, 1);
            referee.Tick(Dt, 1);
            Assert.IsTrue(outcome.Ended);
            Assert.AreEqual(312f, outcome.Result.SurvivalSeconds, 0.01f);
            Assert.AreEqual(3, outcome.Result.MaxThreat);
            Assert.AreEqual(480, outcome.Result.Kills);
            Assert.IsFalse(outcome.Result.Extracted);
        }

        [Test]
        public void Zombies_PreferStandingPlayers_OverDownedOnes()
        {
            PlayerDefinition d = Definition();
            var walker = ScriptableObject.CreateInstance<ZombieDefinition>();
            _assets.Add(walker);
            PlayerStateTable players = Players(2);
            Place(players, 0, -10f, 0f);
            Place(players, 1, 10f, 0f);
            var health = new PlayerHealthSystem(players, d);
            Run(health, Dt);
            health.Damage(1, 500f); // player 1 is down
            var crowd = new CrowdState(8);
            using (NavGrid nav = NavGrid.Open(80))
            using (var world = new ZombieWorld(crowd, players, nav, new ZombieTuning(), walker, 1u))
            {
                int zombie = world.Spawn(new float2(1f, 0f), 0f); // 9 m from the downed player, 11 m from the other
                for (int t = 0; t < 45; t++) world.Tick(Dt, (uint)t);
                Assert.Less(world.PositionOf(zombie).x, 0.5f, "it heads for the standing player");
            }
        }
    }
}
