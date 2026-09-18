using LastGround.Core.Net.Wire;
using LastGround.Networking.Discovery;
using NUnit.Framework;

namespace LastGround.Tests
{
    public class DiscoveryPacketTests
    {
        [Test]
        public void Response_RoundTrips()
        {
            var w = new NetWriter();
            DiscoveryPacket.Write(w, new DiscoveryPacket.Response
            {
                ProtocolVersion = 1, ContentHash = 99, SessionName = "KADİR'S GAME", Players = 2, MaxPlayers = 4,
                GamePort = 7777, InRun = true, Nonce = 5, EchoSentAt = 12.5,
            });
            Assert.AreEqual(DiscoveryPacket.KindResponse, DiscoveryPacket.ReadKind(w.Segment, out NetReader r));
            Assert.IsTrue(DiscoveryPacket.TryRead(ref r, out DiscoveryPacket.Response resp));
            Assert.AreEqual("KADİR'S GAME", resp.SessionName);
            Assert.AreEqual(7777, resp.GamePort);
            Assert.IsTrue(resp.InRun);
            Assert.AreEqual(12.5, resp.EchoSentAt);
        }

        [Test]
        public void ForeignTraffic_IsIgnored()
        {
            var bytes = new System.ArraySegment<byte>(new byte[] { 1, 2, 3, 4, 5, 6 });
            Assert.AreEqual(0, DiscoveryPacket.ReadKind(bytes, out _));
        }
    }
}
