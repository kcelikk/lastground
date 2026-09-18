using System.Collections.Generic;

namespace LastGround.Localization
{
    /// <summary>Deserialized languages.json (TDD_02 §24A.2).</summary>
    public sealed class LanguageCatalog
    {
        /// <summary>Default and final fallback language.</summary>
        public string DefaultLanguage = "en";

        /// <summary>Table (file) names loaded for every language, e.g. "ui", "gameplay".</summary>
        public List<string> Tables = new List<string>();

        public List<LanguageInfo> Languages = new List<LanguageInfo>();
    }
}
