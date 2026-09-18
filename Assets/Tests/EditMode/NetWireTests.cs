using System;
using LastGround.Core.Net.Wire;
using NUnit.Framework;

namespace LastGround.Tests
{
    public class NetWireTests
    {
        [Test]
        public void Primitives_RoundTrip()
        {
            var w = new NetWriter(4);
            w.WriteByte(250);
            w.WriteBool(true);
            w.WriteUShort(65000);
            w.WriteShort(-1234);
            w.WriteUInt(4000000000u);
            w.WriteFloat(-3.25f);
            w.WriteDouble(12345.678901);
            w.WriteVarUInt(0);
            w.WriteVarUInt(127);
            w.WriteVarUInt(128);
            w.WriteVarUInt(uint.MaxValue);
            w.WriteString("Çağrı İşık ğüşıöç");
            w.WriteString(null);

            var r = new NetReader(w.Segment);
            Assert.AreEqual(250, r.ReadByte());
            Assert.IsTrue(r.ReadBool());
            Assert.AreEqual(65000, r.ReadUShort());
            Assert.AreEqual(-1234, r.ReadShort());
            Assert.AreEqual(4000000000u, r.ReadUInt());
            Assert.AreEqual(-3.25f, r.ReadFloat());
            Assert.AreEqual(12345.678901, r.ReadDouble());
            Assert.AreEqual(0u, r.ReadVarUInt());
            Assert.AreEqual(127u, r.ReadVarUInt());
            Assert.AreEqual(128u, r.ReadVarUInt());
            Assert.AreEqual(uint.MaxValue, r.ReadVarUInt());
            Assert.AreEqual("Çağrı İşık ğüşıöç", r.ReadString());
            Assert.AreEqual(string.Empty, r.ReadString());
            Assert.IsFalse(r.Failed);
            Assert.AreEqual(0, r.Remaining);
        }

        [Test]
        public void VarUInt_UsesOneByteBelow128()
        {
            var w = new NetWriter();
            w.WriteVarUInt(100);
            Assert.AreEqual(1, w.Length);
        }

        [Test]
        public void Bits_PackSnapshotEntryInto7Bytes()
        {
            var w = new NetWriter();
            for (int i = 0; i < 10; i++)
            {
                w.WriteBits((uint)(1000 - i), 10);
                w.WriteBits(65535u - (uint)i, 16);
                w.WriteBits((uint)i * 7, 16);
                w.WriteBits(63, 6);
                w.WriteBits(5, 3);
                w.WriteBits(0x15, 5);
            }
            w.FlushBits();
            Assert.AreEqual(70, w.Length);

            var r = new NetReader(w.Segment);
            for (int i = 0; i < 10; i++)
            {
                Assert.AreEqual((uint)(1000 - i), r.ReadBits(10));
                Assert.AreEqual(65535u - (uint)i, r.ReadBits(16));
                Assert.AreEqual((uint)i * 7, r.ReadBits(16));
                Assert.AreEqual(63u, r.ReadBits(6));
                Assert.AreEqual(5u, r.ReadBits(3));
                Assert.AreEqual(0x15u, r.ReadBits(5));
            }
        }

        [Test]
        public void Bits_Write32BitValues()
        {
            var w = new NetWriter();
            w.WriteBits(0xDEADBEEF, 32);
            w.WriteBits(1, 1);
            w.FlushBits();
            var r = new NetReader(w.Segment);
            Assert.AreEqual(0xDEADBEEF, r.ReadBits(32));
            Assert.AreEqual(1u, r.ReadBits(1));
        }

        [Test]
        public void Reader_PastEnd_FailsWithoutThrowing()
        {
            var r = new NetReader(new ArraySegment<byte>(new byte[] { 1, 2 }));
            Assert.AreEqual(0u, r.ReadUInt());
            Assert.IsTrue(r.Failed);
            Assert.AreEqual(0, r.ReadByte());
        }

        [Test]
        public void Reader_TruncatedString_Fails()
        {
            var w = new NetWriter();
            w.WriteString("hello world");
            var r = new NetReader(new ArraySegment<byte>(w.Buffer, 0, 4));
            Assert.AreEqual(string.Empty, r.ReadString());
            Assert.IsTrue(r.Failed);
        }

        [Test]
        public void PositionQuantization_ErrorBelow8mm()
        {
            for (float x = -250f; x < 250f; x += 3.137f)
                Assert.AreEqual(x, Quantize.Position(Quantize.Position(x)), 0.004f);
            Assert.AreEqual(0, Quantize.Position(-1000f));
            Assert.AreEqual(65535, Quantize.Position(1000f));
        }

        [Test]
        public void YawQuantization_WrapsAndRoundTrips()
        {
            Assert.AreEqual(0u, Quantize.Yaw(360f, 6));
            Assert.AreEqual(0u, Quantize.Yaw(-0.1f, 6));
            Assert.AreEqual(270f, Quantize.Yaw(Quantize.Yaw(-90f, 8), 8), 1.5f);
            Assert.AreEqual(123f, Quantize.Yaw(Quantize.Yaw(123f, 6), 6), 360f / 64f / 2f + 0.01f);
        }

        [Test]
        public void VelocityQuantization_CentimetrePrecision()
        {
            Assert.AreEqual(4.99f, Quantize.Velocity(Quantize.Velocity(4.994f)), 1e-4f);
            Assert.AreEqual(-2.5f, Quantize.Velocity(Quantize.Velocity(-2.5f)), 1e-4f);
        }
    }
}
