using LastGround.Core.Random;
using NUnit.Framework;

namespace LastGround.Tests
{
    public class DeterministicRandomTests
    {
        [Test]
        public void SameSeed_ProducesSameSequence()
        {
            var a = new DeterministicRandom(12345u, 7u);
            var b = new DeterministicRandom(12345u, 7u);
            for (int i = 0; i < 1000; i++)
                Assert.AreEqual(a.NextUInt(), b.NextUInt());
        }

        [Test]
        public void KnownSeed_ProducesPinnedValues()
        {
            // Reference PCG32 output (pcg32-demo, seed 42 / stream 54). Pinned so an accidental algorithm
            // change — which would desync host and clients — fails loudly.
            var rng = new DeterministicRandom(42u, 54u);
            Assert.AreEqual(0xa15c02b7u, rng.NextUInt());
            Assert.AreEqual(2068313097u, rng.NextUInt());
            Assert.AreEqual(3122475824u, rng.NextUInt());
        }

        [Test]
        public void Streams_AreIndependent()
        {
            var shots = DeterministicRandom.ForStream(99u, "shot", 1);
            var upgrades = DeterministicRandom.ForStream(99u, "upgrade", 1);
            int equal = 0;
            for (int i = 0; i < 100; i++)
                if (shots.NextUInt() == upgrades.NextUInt()) equal++;
            Assert.Less(equal, 3);
        }

        [Test]
        public void Ranges_StayInBounds()
        {
            var rng = new DeterministicRandom(1u);
            for (int i = 0; i < 10000; i++)
            {
                float f = rng.NextFloat();
                Assert.GreaterOrEqual(f, 0f);
                Assert.Less(f, 1f);

                int n = rng.Range(-3, 5);
                Assert.GreaterOrEqual(n, -3);
                Assert.Less(n, 5);
            }
            Assert.AreEqual(4, rng.Range(4, 4));
        }

        [Test]
        public void Hash_IsStableAndOrderSensitive()
        {
            Assert.AreEqual(Hash32.Of("upgrade"), Hash32.Of("upgrade"));
            Assert.AreNotEqual(Hash32.Combine(1u, 2u), Hash32.Combine(2u, 1u));
        }
    }
}
