using System.Collections.Generic;
using LastGround.Core.Ids;
using LastGround.Core.Net.Protocol;
using LastGround.Core.Net.Session;
using LastGround.Core.Net.Wire;
using LastGround.Core.Run;
using NUnit.Framework;

namespace LastGround.Tests
{
    public class NetSessionTests
    {
        [SetUp]
        public void SetUp()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
        }

        static (TestNet net, NetSession host) Host(int maxPlayers = NetProtocol.MaxPlayers)
        {
            var net = new TestNet();
            var host = net.CreateSession("Host", maxPlayers: maxPlayers);
            Assert.IsTrue(host.StartHost());
            return (net, host);
        }

        [Test]
        public void ClientsJoin_GetIdsAndSameRoster()
        {
            var (net, host) = Host();
            var a = net.CreateSession("Ayşe");
            var b = net.CreateSession("Bora");
            a.Join("loopback", NetProtocol.DefaultGamePort);
            b.Join("loopback", NetProtocol.DefaultGamePort);
            net.Step(0.5);

            Assert.AreEqual(SessionState.Hosting, host.State);
            Assert.AreEqual(SessionState.Connected, a.State);
            Assert.AreEqual(SessionState.Connected, b.State);
            Assert.AreEqual(new PlayerId(0), host.LocalPlayer);
            Assert.AreEqual(new PlayerId(1), a.LocalPlayer);
            Assert.AreEqual(new PlayerId(2), b.LocalPlayer);
            Assert.AreEqual(RunRole.Client, a.Role);
            Assert.AreEqual("Host's game", a.SessionName);

            foreach (var s in new[] { host, a, b })
            {
                Assert.AreEqual(3, s.Players.Count);
                Assert.AreEqual("Host", s.Players[0].Name);
                Assert.IsTrue(s.Players[0].IsHost);
                Assert.AreEqual("Ayşe", s.Players[1].Name);
            }
        }

        [Test]
        public void NegativeTransportIds_Work()
        {
            // Regression (found on device): kcp2k produced a negative connection id and the host
            // treated the player as "no connection", so JoinAccepted was never sent.
            var net = new TestNet();
            net.Network.NegativeConnectionIds = true;
            var host = net.CreateSession("Host");
            Assert.IsTrue(host.StartHost());
            var a = net.CreateSession("A");
            a.Join("loopback", NetProtocol.DefaultGamePort);
            net.Step(0.5);

            Assert.AreEqual(SessionState.Connected, a.State);
            Assert.AreEqual(2, a.Players.Count);
            uint seed = 0;
            a.Subscribe((PlayerId from, in LoadRun m) => seed = m.RunSeed);
            host.SendToClients(new LoadRun { RunSeed = 5, MapId = "x" });
            net.Step(0.1);
            Assert.AreEqual(5u, seed);
        }

        [Test]
        public void ContentMismatch_IsRejected()
        {
            var (net, host) = Host();
            var old = net.CreateSession("Old", contentHash: 7);
            DisconnectReason reason = DisconnectReason.None;
            old.Disconnected += r => reason = r;
            old.Join("loopback", NetProtocol.DefaultGamePort);
            net.Step(1.0);

            Assert.AreEqual(DisconnectReason.Rejected, reason);
            Assert.AreEqual(JoinRejectReason.VersionMismatch, old.LastRejectReason);
            Assert.AreEqual(SessionState.Idle, old.State);
            Assert.AreEqual(1, host.Players.Count);
        }

        [Test]
        public void FullSession_IsRejected()
        {
            var (net, host) = Host(maxPlayers: 2);
            var a = net.CreateSession("A");
            var b = net.CreateSession("B");
            a.Join("loopback", NetProtocol.DefaultGamePort);
            net.Step(0.3);
            b.Join("loopback", NetProtocol.DefaultGamePort);
            net.Step(2.0); // transport retries (M10) before giving up

            Assert.AreEqual(SessionState.Connected, a.State);
            Assert.AreEqual(SessionState.Idle, b.State);
            Assert.AreEqual(2, host.Players.Count);
        }

        [Test]
        public void JoinDuringRun_IsRejectedAsInProgress()
        {
            var (net, host) = Host();
            host.AcceptingPlayers = false;
            var late = net.CreateSession("Late");
            late.Join("loopback", NetProtocol.DefaultGamePort);
            net.Step(1.0);
            Assert.AreEqual(JoinRejectReason.InProgress, late.LastRejectReason);
        }

        [Test]
        public void NoHost_FailsToConnect()
        {
            var net = new TestNet();
            var c = net.CreateSession("C");
            DisconnectReason reason = DisconnectReason.None;
            c.Disconnected += r => reason = r;
            c.Join("loopback", NetProtocol.DefaultGamePort);
            net.Step(0.1);
            Assert.AreEqual(DisconnectReason.None, reason, "M10: the transport is tried again first");
            Assert.AreEqual(SessionState.Connecting, c.State);
            net.Step(2.0);
            Assert.AreEqual(DisconnectReason.ConnectFailed, reason, "gives up after the configured attempts");
        }

        [Test]
        public void FirstConnectFailure_IsRetried_AndJoinsWhenTheHostAnswers()
        {
            var net = new TestNet();
            var c = net.CreateSession("C");
            DisconnectReason reason = DisconnectReason.None;
            c.Disconnected += r => reason = r;
            c.Join("loopback", NetProtocol.DefaultGamePort);
            net.Step(0.1);
            var host = net.CreateSession("Host");
            Assert.IsTrue(host.StartHost());
            net.Step(1.0);
            Assert.AreEqual(DisconnectReason.None, reason);
            Assert.AreEqual(SessionState.Connected, c.State);
            Assert.AreEqual(2, host.Players.Count);
        }

        [Test]
        public void ClientLeaving_RemovesItFromHostAndOthers()
        {
            var (net, host) = Host();
            var a = net.CreateSession("A");
            var b = net.CreateSession("B");
            a.Join("loopback", NetProtocol.DefaultGamePort);
            b.Join("loopback", NetProtocol.DefaultGamePort);
            net.Step(0.5);

            var left = new List<PlayerId>();
            host.PlayerLeft += left.Add;
            var leftOnB = new List<PlayerId>();
            b.PlayerLeft += leftOnB.Add;
            a.Leave();
            net.Step(0.5);

            CollectionAssert.AreEqual(new[] { new PlayerId(1) }, left);
            CollectionAssert.AreEqual(new[] { new PlayerId(1) }, leftOnB);
            Assert.AreEqual(2, host.Players.Count);
            Assert.AreEqual(2, b.Players.Count);
        }

        [Test]
        public void HostLeaving_DisconnectsClientsWithHostLost()
        {
            var (net, host) = Host();
            var a = net.CreateSession("A");
            a.Join("loopback", NetProtocol.DefaultGamePort);
            net.Step(0.5);
            DisconnectReason reason = DisconnectReason.None;
            a.Disconnected += r => reason = r;

            host.Leave();
            net.Step(0.2);

            Assert.AreEqual(DisconnectReason.HostLost, reason);
            Assert.AreEqual(SessionState.Idle, a.State);
        }

        [Test]
        public void TypedMessages_FlowBothWays_AndHostLoopsBackLocally()
        {
            var (net, host) = Host();
            var a = net.CreateSession("A");
            a.Join("loopback", NetProtocol.DefaultGamePort);
            net.Step(0.5);

            var atHost = new List<PlayerId>();
            host.Subscribe((PlayerId from, in RunReady _) => atHost.Add(from));
            uint seedOnClient = 0;
            a.Subscribe((PlayerId from, in LoadRun m) => seedOnClient = m.RunSeed);

            a.SendToHost(new RunReady());
            host.SendToHost(new RunReady());   // host's own player: local dispatch, no transport
            host.SendToClients(new LoadRun { RunSeed = 99, MapId = "greybox" });
            net.Step(0.2);

            CollectionAssert.AreEquivalent(new[] { new PlayerId(1), new PlayerId(0) }, atHost);
            Assert.AreEqual(99u, seedOnClient);
        }

        [Test]
        public void RawMessages_ReachEverySubscriberWithFullPayload()
        {
            var (net, host) = Host();
            var a = net.CreateSession("A");
            a.Join("loopback", NetProtocol.DefaultGamePort);
            net.Step(0.5);

            int first = 0, second = 0;
            a.Subscribe(NetMsgId.PlayerStates, (PlayerId from, ref NetReader r) => first = r.ReadUShort());
            a.Subscribe(NetMsgId.PlayerStates, (PlayerId from, ref NetReader r) => second = r.ReadUShort());

            host.Begin(NetMsgId.PlayerStates).WriteUShort(4321);
            host.SendToClients(NetChannel.Unreliable);
            net.Step(0.1);

            Assert.AreEqual(4321, first);
            Assert.AreEqual(4321, second);
        }

        [Test]
        public void ClockSync_EstimatesHostTimeAndRtt()
        {
            var (net, host) = Host();
            net.Network.Latency = 0.05;
            var a = net.CreateSession("A");
            a.Join("loopback", NetProtocol.DefaultGamePort);
            net.Step(5.0, dt: 0.01);

            Assert.IsTrue(a.Clock.IsSynchronized);
            Assert.AreEqual(0.1, a.Clock.Rtt, 0.03);
            Assert.AreEqual(host.Clock.HostTime, a.Clock.HostTime, 0.03);
            Assert.Greater(host.Players[1].RttMs, 70);
        }

        [Test]
        public void Stats_CountTraffic()
        {
            var (net, host) = Host();
            var a = net.CreateSession("A");
            a.Join("loopback", NetProtocol.DefaultGamePort);
            net.Step(3.0);
            Assert.Greater(a.Stats.InBytesPerSecond, 0f);
            Assert.Greater(a.Stats.OutBytesFor(NetMsgId.Ping), 0);
            Assert.Greater(host.Stats.OutBytesFor(NetMsgId.Pong), 0);
        }

        [Test]
        public void Offline_ActsAsHostWithoutTransport()
        {
            var net = new TestNet();
            var solo = net.CreateSession("Solo");
            solo.StartOffline();
            Assert.AreEqual(RunRole.Offline, solo.Role);
            Assert.IsTrue(solo.IsAuthority);
            Assert.AreEqual(1, solo.Players.Count);
            Assert.IsFalse(solo.AcceptingPlayers);

            var other = net.CreateSession("Other");
            other.Join("loopback", NetProtocol.DefaultGamePort);
            net.Step(2.0); // transport retries (M10) before giving up
            Assert.AreEqual(SessionState.Idle, other.State);
        }
    }
}
