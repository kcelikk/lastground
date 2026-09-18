using LastGround.App;
using NUnit.Framework;

namespace LastGround.Tests
{
    public class QualityDefaultsTests
    {
        [TestCase(4096, QualityDefaults.Low)]
        [TestCase(5800, QualityDefaults.Low)]
        [TestCase(7700, QualityDefaults.Medium)]
        [TestCase(12000, QualityDefaults.High)]
        public void Tier_FromMemory(int megabytes, int expected)
        {
            Assert.AreEqual(expected, QualityDefaults.FromSystemMemory(megabytes));
        }

        [Test]
        public void Low_IsLockedTo30()
        {
            Assert.AreEqual(30, QualityDefaults.TargetFps(QualityDefaults.Low, 60));
            Assert.AreEqual(60, QualityDefaults.TargetFps(QualityDefaults.Medium, 60));
        }
    }
}
