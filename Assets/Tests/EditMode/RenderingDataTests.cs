using LastGround.Gameplay.Crowd;
using LastGround.Platform;
using LastGround.Rendering.Crowd;
using NUnit.Framework;
using UnityEngine;

namespace LastGround.Tests
{
    public class RenderingDataTests
    {
        [TestCase(5848, "Adreno (TM) 540", DeviceTierDetector.Low)]      // OnePlus 5T
        [TestCase(7600, "Adreno (TM) 710", DeviceTierDetector.Medium)]   // Redmi Pad Pro (8 GB)
        [TestCase(11500, "Adreno (TM) 740", DeviceTierDetector.High)]
        [TestCase(11500, "Adreno (TM) 530", DeviceTierDetector.Low)]      // RAM alone must not promote an old GPU
        [TestCase(7000, "Mali-G57 MC2", DeviceTierDetector.Low)]
        [TestCase(12000, "Mali-G78", DeviceTierDetector.Medium)]
        [TestCase(4000, "Adreno (TM) 740", DeviceTierDetector.Low)]
        public void Tier_FromMemoryAndGpu(int ramMb, string gpu, int expected)
        {
            Assert.AreEqual(expected, DeviceTierDetector.Detect(ramMb, gpu));
        }

        [Test]
        public void CorpseBuffer_RecyclesOldestAndExpires()
        {
            var corpses = new CorpseBuffer(2);
            corpses.Add(1, 0f, 0f, 0f, 0f);
            corpses.Add(2, 0f, 0f, 0f, 1f);
            corpses.Add(3, 0f, 0f, 0f, 2f);
            Assert.AreEqual(2, corpses.Count);
            Assert.AreEqual(2, corpses[0].Slot);
            corpses.Expire(10f, 8.5f);
            Assert.AreEqual(1, corpses.Count);
            Assert.AreEqual(3, corpses[0].Slot);
        }

        [Test]
        public void CorpseBuffer_ZeroCapacityIsDisabled()
        {
            var corpses = new CorpseBuffer(0);
            corpses.Add(1, 0f, 0f, 0f, 0f);
            Assert.AreEqual(0, corpses.Count);
        }

        [Test]
        public void BenchmarkDriver_HoldsTargetAndProducesDeaths()
        {
            var state = new CrowdState(512);
            var driver = new BenchmarkCrowdDriver(state, 1u) { TargetCount = 150, DeathsPerSecond = 4f };
            var reader = state.Deaths.CreateReader();
            for (int i = 0; i < 90; i++) driver.Tick(1f / 30f, 0);
            Assert.AreEqual(150, state.ActiveCount);

            int deaths = 0;
            while (state.Deaths.TryRead(ref reader, out _)) deaths++;
            Assert.That(deaths, Is.InRange(10, 14));

            driver.TargetCount = 50;
            driver.Tick(1f / 30f, 0);
            Assert.AreEqual(50, state.ActiveCount);
        }

        [Test]
        public void Footprint_ContainsPointsUnderTopDownCamera()
        {
            var go = new GameObject("cam");
            try
            {
                var camera = go.AddComponent<Camera>();
                camera.fieldOfView = 35f;
                camera.aspect = 2f;
                var rotation = Quaternion.Euler(55f, 0f, 0f);
                go.transform.SetPositionAndRotation(-(rotation * Vector3.forward) * 22f, rotation);

                var footprint = new GroundFootprint();
                footprint.Update(camera, 0f);
                Assert.AreEqual(0f, footprint.Focus.x, 0.01f);
                Assert.AreEqual(0f, footprint.Focus.y, 0.01f);
                Assert.IsTrue(footprint.Contains(0f, 0f));
                Assert.IsTrue(footprint.Contains(8f, 3f));
                Assert.IsFalse(footprint.Contains(60f, 0f));
                Assert.IsFalse(footprint.Contains(0f, -40f));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Variety_IsStablePerSlotAndInRange()
        {
            uint h = CrowdVariety.Hash(42);
            Assert.AreEqual(h, CrowdVariety.Hash(42));
            Assert.That(CrowdVariety.Body(h, 4), Is.InRange(0, 3));
            Assert.That(CrowdVariety.Tint(h), Is.InRange(0, 7));
            Assert.That(CrowdVariety.Scale(h, 0.08f), Is.InRange(0.92f, 1.08f));
        }
    }
}
