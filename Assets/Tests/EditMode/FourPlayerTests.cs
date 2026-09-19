using System.Collections.Generic;
using LastGround.Core.Ids;
using LastGround.Core.Net.Protocol;
using LastGround.Core.Net.Session;
using LastGround.Data.Boss;
using LastGround.Gameplay.Crowd;
using LastGround.Gameplay.Director;
using LastGround.Gameplay.Players;
using LastGround.Gameplay.Run;
using LastGround.Networking.Replication;
using NUnit.Framework;
using UnityEditor;

namespace LastGround.Tests
{
    /// <summary>M10: four players on one host — upload budget, disconnect grace, spectating, partial results.</summary>
    public class FourPlayerTests
    {
        sealed class Peer
        {
            public NetSession Session;
            public PlayerStateTable Players;
            public PlayerSync Sync;
        }

        static List<Peer> Team(TestNet net, int clients)
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var peers = new List<Peer>();
            NetSession host = net.CreateSession("Host");
            Assert.IsTrue(host.StartHost());
            peers.Add(new Peer { Session = host });
            for (int i = 0; i < clients; i++)
            {
                NetSession client = net.CreateSession("Client" + i);
                client.Join("loopback", NetProtocol.DefaultGamePort);
                peers.Add(new Peer { Session = client });
            }
            net.Step(0.5);
            for (int i = 0; i < peers.Count; i++)
            {
                Peer peer = peers[i];
                Assert.AreEqual(i, peer.Session.LocalPlayer.Value, "joined in order");
                peer.Players = new PlayerStateTable { Local = peer.Session.LocalPlayer };
                // Spread out: each player sees a different part of the horde.
                peer.Players.SetLocal(-45f + i * 30f, 0f, 0f, 0f, 0f);
                peer.Sync = new PlayerSync(peer.Session, peer.Players, 5f);
            }
            return peers;
        }

        static void TickAll(List<Peer> peers, float dt)
        {
            foreach (Peer peer in peers) peer.Sync.Tick(dt, 0);
            foreach (Peer peer in peers) peer.Sync.Interpolate();
        }

        [Test]
        public void HostUpload_WithThreeClientsAndThreeHundredZombies_StaysInBudget()
        {
            var net = new TestNet();
            List<Peer> peers = Team(net, 3);
            var crowd = new CrowdState(512);
            var sim = new DummyCrowdSim(crowd, 300, 140f, 7u);
            var sender = new CrowdReplicationSender(peers[0].Session, crowd, peers[0].Players, new ReplicationTuning());
            var receivers = new List<CrowdReplicationReceiver>();
            for (int i = 1; i < peers.Count; i++) receivers.Add(new CrowdReplicationReceiver(peers[i].Session, new CrowdReplica(512)));

            net.Step(12.0, perTick: dt =>
            {
                sim.Tick(dt, 0);
                TickAll(peers, dt);
                sender.Tick(dt, 0);
                foreach (CrowdReplicationReceiver r in receivers) r.Tick(dt, 0);
            });

            float upload = peers[0].Session.Stats.OutBytesPerSecond;
            UnityEngine.Debug.Log($"[Test] host upload with 3 clients: {upload / 1024f:0.00} KB/s");
            Assert.LessOrEqual(upload, 60f * 1024f, "TDD_02 §32: host upstream ≤ 60 KB/s with three clients");
            for (int i = 1; i < peers.Count; i++)
                Assert.LessOrEqual(peers[i].Session.Stats.InBytesPerSecond, 20f * 1024f, "each client ≤ 20 KB/s downstream");
            Assert.IsTrue(peers[1].Players.Active[3], "every player is visible to every other");
            sender.Dispose();
        }

        [Test]
        public void LeavingPlayer_StaysFrozenForTheGracePeriod_ThenIsRemovedEverywhere()
        {
            var net = new TestNet();
            List<Peer> peers = Team(net, 3);
            net.Step(1.0, perTick: dt => TickAll(peers, dt));
            Assert.IsTrue(peers[0].Players.Active[2] && peers[1].Players.Active[2]);

            peers[2].Session.Leave();
            net.Step(1.0, perTick: dt => TickAll(peers, dt));
            foreach (int p in new[] { 0, 1, 3 })
            {
                Assert.IsTrue(peers[p].Players.Active[2], "still in the run on device " + p);
                Assert.IsTrue(peers[p].Players.Disconnected[2], "marked disconnected on device " + p);
            }

            net.Step(PlayerSync.DisconnectGraceSeconds + 1.0, perTick: dt => TickAll(peers, dt));
            foreach (int p in new[] { 0, 1, 3 })
            {
                Assert.IsFalse(peers[p].Players.Active[2], "removed on device " + p);
                Assert.IsFalse(peers[p].Players.Disconnected[2]);
                Assert.IsTrue(peers[p].Players.Active[1], "the others stay");
            }
        }

        [Test]
        public void DeadPlayer_SpectatesTeammates_AndReturnsToOwnBody()
        {
            var players = new PlayerStateTable { Local = new PlayerId(1) };
            for (int p = 0; p < 4; p++) players.PushRemote(new PlayerId((byte)p), p, 0f, 0f, 0f, 0f, 0.0, 0.0);
            players.SetLocal(1f, 0f, 0f, 0f, 0f);
            var spectator = new SpectatorTarget(players);

            spectator.Update();
            Assert.AreEqual(-1, spectator.Target, "alive: own body");

            players.Life[1] = PlayerLife.Dead;
            spectator.Update();
            Assert.AreEqual(2, spectator.Target, "the next teammate after me");
            spectator.Next();
            Assert.AreEqual(3, spectator.Target);
            players.Life[0] = PlayerLife.Dead;
            spectator.Next();
            Assert.AreEqual(2, spectator.Target, "dead teammates are skipped");
            players.MarkDisconnected(new PlayerId(2));
            players.Remove(new PlayerId(2));
            spectator.Update();
            Assert.AreEqual(3, spectator.Target, "a teammate who left is replaced");

            players.Life[1] = PlayerLife.Alive;
            spectator.Update();
            Assert.AreEqual(-1, spectator.Target, "back in the game: own body again");
        }

        [Test]
        public void LostHost_GivesAPartialResult_BankedLikeAWipe()
        {
            var deaths = new Core.Events.EventChannel<CrowdDeath>(16);
            var status = new RunStatus { RunSeconds = 312f, Threat = 3 };
            var tracker = new ConnectionLossTracker(deaths, status);
            for (int i = 0; i < 7; i++) deaths.Publish(new CrowdDeath());
            tracker.Tick(0.1f, 0);
            status.Threat = 2;

            var rules = AssetDatabase.LoadAssetAtPath<ExtractionRulesDefinition>("Assets/ScriptableObjects/Boss/EXT_Rules.asset");
            RunResult result = tracker.Result(200, rules);
            Assert.IsTrue(result.ConnectionLost);
            Assert.IsFalse(result.Extracted);
            Assert.AreEqual(7, result.Kills);
            Assert.AreEqual(3, result.MaxThreat, "the highest threat seen");
            Assert.AreEqual(312f, result.SurvivalSeconds, 1e-3f);
            Assert.IsNotNull(rules);
            int expected = rules.Convert(200, 3, false, false);
            Assert.AreEqual(expected, result.BankedFor(0));
            Assert.Less(result.BankedFor(0), 200, "a partial reward, not the full run coin");
        }
    }
}
