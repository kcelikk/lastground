using System.IO;
using LastGround.Save;
using NUnit.Framework;

namespace LastGround.Tests
{
    public class SaveTests
    {
        string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "lg_save_test_" + System.Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        [Test]
        public void WriteThenRead_RoundTrips()
        {
            var store = new JsonFileSaveStore(_dir);
            store.Write("k", "{\"a\":\"ğüşıöç İ\"}");
            Assert.IsTrue(store.TryRead("k", out string data));
            Assert.AreEqual("{\"a\":\"ğüşıöç İ\"}", data);
        }

        [Test]
        public void CorruptedFile_FallsBackToBackup()
        {
            var store = new JsonFileSaveStore(_dir);
            store.Write("k", "v1");
            store.Write("k", "v2");
            File.WriteAllText(Path.Combine(_dir, "k.json"), "LGSAVE1 00000000\ngarbage");

            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            Assert.IsTrue(store.TryRead("k", out string data));
            Assert.AreEqual("v1", data);
        }

        [Test]
        public void MissingKey_ReturnsFalse()
        {
            var store = new JsonFileSaveStore(_dir);
            Assert.IsFalse(store.TryRead("none", out _));
        }

        [Test]
        public void SaveService_PersistsSettings_AfterDebounce()
        {
            var store = new JsonFileSaveStore(_dir);
            var service = new SaveService(store);
            service.Settings.Language = "tr";
            service.Settings.QualityLevel = 2;
            service.Tick(10f);
            service.RequestSave();
            service.Tick(10.5f);
            Assert.IsFalse(store.TryRead(SaveService.SettingsKey, out _), "saved before debounce");
            service.Tick(11.1f);

            var reloaded = new SaveService(new JsonFileSaveStore(_dir));
            Assert.AreEqual("tr", reloaded.Settings.Language);
            Assert.AreEqual(2, reloaded.Settings.QualityLevel);
        }

        [Test]
        public void SaveService_UsesDefaults_WhenNoFile()
        {
            var service = new SaveService(new JsonFileSaveStore(_dir));
            Assert.IsNull(service.Settings.Language);
            Assert.AreEqual(-1, service.Settings.QualityLevel);
            Assert.AreEqual(SettingsData.CurrentVersion, service.Settings.Version);
        }
    }
}
