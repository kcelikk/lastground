using System;
using System.Collections.Generic;
using System.Globalization;
using LastGround.Core.Logging;
using Newtonsoft.Json;

namespace LastGround.Localization
{
    /// <summary>
    /// Loads flat key/value JSON tables per language and merges the fallback chain (tr → en).
    /// Allocations happen only when the language changes; lookups are dictionary reads.
    /// </summary>
    public sealed class JsonLocalizationService : ILocalizationService
    {
        readonly ILocalizationSource _source;
        readonly LanguageCatalog _catalog;
        readonly Dictionary<string, string> _strings = new Dictionary<string, string>(1024, StringComparer.Ordinal);
        readonly HashSet<string> _reportedMissing = new HashSet<string>(StringComparer.Ordinal);

        public string CurrentLanguage { get; private set; }
        public IReadOnlyList<LanguageInfo> Languages => _catalog.Languages;
        public event Action<string> LanguageChanged;

        public JsonLocalizationService(ILocalizationSource source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            string json = _source.Load("languages");
            if (json == null) throw new InvalidOperationException("Localization catalog 'languages' not found.");
            _catalog = JsonConvert.DeserializeObject<LanguageCatalog>(json, JsonSettings.Invariant);
            if (_catalog == null || _catalog.Languages.Count == 0)
                throw new InvalidOperationException("Localization catalog has no languages.");
        }

        /// <summary>Picks "tr" for Turkish devices, otherwise the default language (TDD_02 §24A.3).</summary>
        public static string SuggestLanguage(bool deviceIsTurkish, string defaultLanguage)
        {
            return deviceIsTurkish ? "tr" : defaultLanguage;
        }

        public string SetLanguage(string code)
        {
            LanguageInfo language = Find(code) ?? Find(_catalog.DefaultLanguage) ?? _catalog.Languages[0];

            _strings.Clear();
            _reportedMissing.Clear();

            // Load the fallback chain from the root down so the requested language wins.
            var chain = new List<LanguageInfo>(4);
            for (LanguageInfo l = language; l != null && !chain.Contains(l); l = Find(l.Fallback))
                chain.Add(l);
            for (int i = chain.Count - 1; i >= 0; i--)
                LoadTables(chain[i].Code);

            CurrentLanguage = language.Code;
            Log.Info(LogCategory.Localization, "Language: " + language.Code + " (" + _strings.Count + " keys)");
            LanguageChanged?.Invoke(CurrentLanguage);
            return CurrentLanguage;
        }

        public string Get(string key)
        {
            if (key != null && _strings.TryGetValue(key, out string value))
                return value;
            return Missing(key);
        }

        public bool HasKey(string key)
        {
            return key != null && _strings.ContainsKey(key);
        }

        public string Format(string key, object arg0)
        {
            return string.Format(CultureInfo.InvariantCulture, Get(key), arg0);
        }

        LanguageInfo Find(string code)
        {
            if (string.IsNullOrEmpty(code)) return null;
            for (int i = 0; i < _catalog.Languages.Count; i++)
            {
                if (string.Equals(_catalog.Languages[i].Code, code, StringComparison.OrdinalIgnoreCase))
                    return _catalog.Languages[i];
            }
            return null;
        }

        void LoadTables(string code)
        {
            for (int t = 0; t < _catalog.Tables.Count; t++)
            {
                string path = code + "/" + _catalog.Tables[t];
                string json = _source.Load(path);
                if (json == null)
                {
                    Log.Warning(LogCategory.Localization, "Missing table: " + path);
                    continue;
                }

                var table = JsonConvert.DeserializeObject<Dictionary<string, string>>(json, JsonSettings.Invariant);
                if (table == null) continue;
                foreach (var pair in table)
                    _strings[pair.Key] = pair.Value;
            }
        }

        string Missing(string key)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (key != null && _reportedMissing.Add(key))
                Log.Warning(LogCategory.Localization, "Missing key '" + key + "' for " + CurrentLanguage);
            return "#" + key + "#";
#else
            return key ?? string.Empty;
#endif
        }
    }
}
