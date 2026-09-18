using LastGround.Core.Events;
using NUnit.Framework;

namespace LastGround.Tests
{
    public class EventChannelTests
    {
        struct Ping
        {
            public int Value;
        }

        [Test]
        public void Reader_SeesOnlyEventsAfterCreation()
        {
            var channel = new EventChannel<Ping>(8);
            channel.Publish(new Ping { Value = 1 });
            var reader = channel.CreateReader();
            channel.Publish(new Ping { Value = 2 });

            Assert.IsTrue(channel.TryRead(ref reader, out Ping p));
            Assert.AreEqual(2, p.Value);
            Assert.IsFalse(channel.TryRead(ref reader, out _));
        }

        [Test]
        public void MultipleReaders_AreIndependent()
        {
            var channel = new EventChannel<Ping>(8);
            var a = channel.CreateReader();
            var b = channel.CreateReader();
            channel.Publish(new Ping { Value = 5 });

            Assert.IsTrue(channel.TryRead(ref a, out Ping pa));
            Assert.IsTrue(channel.TryRead(ref b, out Ping pb));
            Assert.AreEqual(pa.Value, pb.Value);
        }

        [Test]
        public void SlowReader_SkipsOverwrittenEvents_AndCountsDrops()
        {
            var channel = new EventChannel<Ping>(4);
            var reader = channel.CreateReader();
            for (int i = 0; i < 10; i++)
                channel.Publish(new Ping { Value = i });

            Assert.IsTrue(channel.TryRead(ref reader, out Ping p));
            Assert.AreEqual(6, p.Value);
            Assert.AreEqual(6UL, reader.Dropped);
        }

        [Test]
        public void Capacity_RoundsUpToPowerOfTwo()
        {
            Assert.AreEqual(8, new EventChannel<Ping>(5).Capacity);
        }
    }
}
