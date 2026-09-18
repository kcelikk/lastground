using LastGround.Core.Net.Protocol;
using LastGround.Core.Net.Session;
using LastGround.Gameplay.Crowd;
using LastGround.Gameplay.Players;
using LastGround.Networking.Replication;
using NUnit.Framework;

namespace LastGround.Tests
{
    /// <summary>
    /// M1 exit criteria in miniature: 300 simulated entities replicated host → client over loopback (TDD_03 §36 M1).
    /// </summary>
    public class ReplicationTests
    {
        sealed class Rig
        {
            public TestNet Net;
            public NetSession Host, Client;
            public PlayerStateTable HostPlayers, ClientPlayers;
            public CrowdState Crowd;
            public DummyCrowdSim Sim;
            public PlayerSync HostSync, ClientSync;
            public CrowdReplicationSender Sender;
            public CrowdReplica Replica;
            public CrowdReplicationReceiver Receiver;

            public void Tick(float dt)
            {
                Sim.Tick(dt, 0);
                ClientSync.Tick(dt, 0);
                HostSync.Tick(dt, 0);
                Sender.Tick(dt, 0);
                Receiver.Tick(dt, 0);
                ClientSync.Interpolate();
                HostSync.Interpolate();
            }
        }

        static Rig Create(double latency = 0, float loss = 0f, int entities = 300)
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var rig = new Rig { Net = new TestNet() };
            rig.Host = rig.Net.CreateSession("Host");
            Assert.IsTrue(rig.Host.StartHost());
            rig.Client = rig.Net.CreateSession("Client");
            rig.Client.Join("loopback", NetProtocol.DefaultGamePort);
            rig.Net.Step(0.5);
            Assert.AreEqual(SessionState.Connected, rig.Client.State);
            rig.Net.Network.Latency = latency;
            rig.Net.Network.UnreliableLoss = loss;

            rig.HostPlayers = new PlayerStateTable { Local = rig.Host.LocalPlayer };
            rig.ClientPlayers = new PlayerStateTable { Local = rig.Client.LocalPlayer };
            rig.HostPlayers.SetLocal(10f, 10f, 0f, 0f, 0f);
            rig.ClientPlayers.SetLocal(0f, 0f, 0f, 0f, 0f);

            rig.Crowd = new CrowdState(512);
            rig.Sim = new DummyCrowdSim(rig.Crowd, entities, 140f, 7u);
            rig.HostSync = new PlayerSync(rig.Host, rig.HostPlayers, PlayerMotor.MoveSpeed);
            rig.ClientSync = new PlayerSync(rig.Client, rig.ClientPlayers, PlayerMotor.MoveSpeed);
            rig.Sender = new CrowdReplicationSender(rig.Host, rig.Crowd, rig.HostPlayers, new ReplicationTuning());
            rig.Replica = new CrowdReplica(512);
            rig.Receiver = new CrowdReplicationReceiver(rig.Client, rig.Replica);
            return rig;
        }

        [Test]
        public void ThreeHundredEntities_StayWithinClientBandwidthBudget()
        {
            var rig = Create();
            rig.Net.Step(12.0, perTick: rig.Tick);

            float downstream = rig.Client.Stats.InBytesPerSecond;
            UnityEngine.Debug.Log($"[Test] client downstream {downstream / 1024f:0.00} KB/s, relevant {rig.Sender.RelevantCount(rig.Client.LocalPlayer)}");
            Assert.LessOrEqual(downstream, 20f * 1024f, "TDD_03 §36 M1: client downstream ≤ 20 KB/s with 300 entities");
            Assert.Greater(downstream, 2f * 1024f, "stream should actually be flowing");
        }

        [Test]
        public void Replica_MatchesRelevantSet()
        {
            var rig = Create();
            rig.Net.Step(8.0, perTick: rig.Tick);
            int relevant = rig.Sender.RelevantCount(rig.Client.LocalPlayer);
            Assert.Greater(relevant, 50);
            Assert.Less(relevant, 300, "far entities must be culled (> 60 m)");
            Assert.AreEqual(relevant, rig.Replica.ActiveCount);
        }

        [Test]
        public void NearEntities_TrackHostPositions()
        {
            var rig = Create();
            rig.Net.Step(6.0, perTick: rig.Tick);
            AssertNearEntitiesClose(rig, 0.75f);
        }

        [Test]
        public void NearEntities_TrackUnderLatencyAndLoss()
        {
            var rig = Create(latency: 0.05, loss: 0.05f);
            rig.Net.Step(8.0, perTick: rig.Tick);
            AssertNearEntitiesClose(rig, 1.2f);
            Assert.LessOrEqual(rig.Client.Stats.InBytesPerSecond, 20f * 1024f);
        }

        [Test]
        public void Players_SeeEachOther()
        {
            var rig = Create(entities: 0);
            rig.Net.Step(2.0, perTick: rig.Tick);

            int clientIndex = rig.Client.LocalPlayer.Value;
            Assert.IsTrue(rig.HostPlayers.Active[clientIndex]);
            Assert.AreEqual(0f, rig.HostPlayers.X[clientIndex], 0.05f);

            Assert.IsTrue(rig.ClientPlayers.Active[0]);
            rig.ClientPlayers.GetDisplay(0, out float hx, out float hz, out _);
            Assert.AreEqual(10f, hx, 0.05f);
            Assert.AreEqual(10f, hz, 0.05f);
        }

        [Test]
        public void Host_ClampsTeleports()
        {
            var rig = Create(entities: 0);
            rig.Net.Step(1.0, perTick: rig.Tick);
            rig.ClientPlayers.SetLocal(60f, 60f, 0f, 0f, 0f);
            rig.Net.Step(0.1, perTick: rig.Tick);

            int clientIndex = rig.Client.LocalPlayer.Value;
            Assert.Greater(rig.HostSync.Corrections, 0);
            Assert.Less(rig.HostPlayers.X[clientIndex], 5f);
        }

        [Test]
        public void ClientLeaving_ClearsItsPlayerOnHost()
        {
            var rig = Create(entities: 0);
            rig.Net.Step(1.0, perTick: rig.Tick);
            int clientIndex = rig.Client.LocalPlayer.Value;
            rig.Client.Leave();
            rig.Net.Step(0.3, perTick: rig.Tick);
            Assert.IsFalse(rig.HostPlayers.Active[clientIndex]);
        }

        static void AssertNearEntitiesClose(Rig rig, float tolerance)
        {
            int checkedCount = 0;
            for (int i = 0; i < rig.Crowd.Capacity; i++)
            {
                if (!rig.Crowd.AliveSlots[i] || !rig.Replica.Alive[i]) continue;
                float dx = rig.Crowd.PosX[i], dz = rig.Crowd.PosZ[i];
                if (dx * dx + dz * dz > 15f * 15f) continue;
                float ex = rig.Replica.X[i] - rig.Crowd.PosX[i];
                float ez = rig.Replica.Z[i] - rig.Crowd.PosZ[i];
                Assert.Less(ex * ex + ez * ez, tolerance * tolerance, "slot " + i);
                checkedCount++;
            }
            Assert.Greater(checkedCount, 3);
        }
    }
}
