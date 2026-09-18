using LastGround.Core.Pooling;
using NUnit.Framework;

namespace LastGround.Tests
{
    public class RingBufferTests
    {
        [Test]
        public void PushOverwrite_RecyclesOldest()
        {
            var ring = new RingBuffer<int>(3);
            Assert.IsFalse(ring.PushOverwrite(1));
            ring.PushOverwrite(2);
            ring.PushOverwrite(3);
            Assert.IsTrue(ring.PushOverwrite(4));

            Assert.AreEqual(3, ring.Count);
            Assert.AreEqual(2, ring[0]);
            Assert.AreEqual(4, ring[2]);
        }

        [Test]
        public void TryPopOldest_IsFifo()
        {
            var ring = new RingBuffer<int>(2);
            ring.PushOverwrite(10);
            ring.PushOverwrite(20);
            Assert.IsTrue(ring.TryPopOldest(out int a));
            Assert.AreEqual(10, a);
            Assert.IsTrue(ring.TryPopOldest(out int b));
            Assert.AreEqual(20, b);
            Assert.IsFalse(ring.TryPopOldest(out _));
        }
    }
}
