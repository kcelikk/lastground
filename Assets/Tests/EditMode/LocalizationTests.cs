using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using LastGround.EditorTools.Localization;
using LastGround.Localization;
using NUnit.Framework;

namespace LastGround.Tests
{
    public class LocalizationTests
    {
        sealed class MemorySource : ILocalizationSource
        {
            readonly Dictionary<string, string> _files;
            public MemorySource(Dictionary<string, string> files) => _files = files;
            public string Load(string relativePath) => _files.TryGetValue(relativePath, out string s) ? s : null;
        }

        static JsonLocalizationService CreateService()
        {
            return new JsonLocalizationService(new MemorySource(new Dictionary<string, string>
            {
                ["languages"] = "{\"defaultLanguage\":\"en\",\"tables\":[\"ui\"],\"languages\":[" +
                                "{\"code\":\"en\",\"name\":\"English\",\"fallback\":null}," +
                                "{\"code\":\"tr\",\"name\":\"Türkçe\",\"fallback\":\"en\"}]}",
                ["en/ui"] = "{\"a.title\":\"TITLE\",\"a.only_en\":\"ONLY EN\",\"a.count\":\"COUNT {0}\"}",
                ["tr/ui"] = "{\"a.title\":\"BAŞLIK\",\"a.count\":\"SAYI {0}\"}",
            }));
        }

        [Test]
        public void Turkish_OverridesEnglish_AndFallsBackForMissingKeys()
        {
            var service = CreateService();
            service.SetLanguage("tr");
            Assert.AreEqual("BAŞLIK", service.Get("a.title"));
            Assert.AreEqual("ONLY EN", service.Get("a.only_en"));
        }

        [Test]
        public void UnknownLanguage_FallsBackToDefault()
        {
            var service = CreateService();
            Assert.AreEqual("en", service.SetLanguage("de"));
        }

        [Test]
        public void MissingKey_IsMarked()
        {
            var service = CreateService();
            service.SetLanguage("en");
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            Assert.AreEqual("#nope#", service.Get("nope"));
        }

        [Test]
        public void LanguageChanged_IsRaised()
        {
            var service = CreateService();
            string changed = null;
            service.LanguageChanged += code => changed = code;
            service.SetLanguage("tr");
            Assert.AreEqual("tr", changed);
        }

        [Test]
        public void LookupAndFormat_WorkUnderTurkishCulture()
        {
            CultureInfo previous = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("tr-TR");
                var service = CreateService();
                Assert.AreEqual("en", service.SetLanguage("EN"));
                Assert.AreEqual("COUNT 1.5", service.Format("a.count", 1.5f));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = previous;
            }
        }

        [Test]
        public void SuggestLanguage_PrefersTurkishOnTurkishDevices()
        {
            Assert.AreEqual("tr", JsonLocalizationService.SuggestLanguage(true, "en"));
            Assert.AreEqual("en", JsonLocalizationService.SuggestLanguage(false, "en"));
        }

        [Test]
        public void ProjectTables_PassValidation()
        {
            List<string> issues = LocalizationValidator.Validate(LocalizationValidator.Root);
            Assert.IsEmpty(issues, string.Join("\n", issues));
        }
    }
}
