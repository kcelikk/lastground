using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using LastGround.Localization;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace LastGround.EditorTools.Localization
{
    /// <summary>
    /// Checks the JSON string tables against the default language (TDD_02 §24A.5): missing/extra keys,
    /// empty values, and mismatched {n} placeholders.
    /// </summary>
    public static class LocalizationValidator
    {
        public const string Root = "Assets/Resources/Localization";
        static readonly Regex Placeholder = new Regex(@"\{(\d+)(?::[^}]*)?\}", RegexOptions.CultureInvariant);

        [MenuItem("LastGround/Localization/Validate")]
        public static void ValidateMenu()
        {
            List<string> issues = Validate(Root);
            if (issues.Count == 0)
                Debug.Log("[Localization] OK — no issues.");
            else
                Debug.LogWarning("[Localization] " + issues.Count + " issue(s):\n" + string.Join("\n", issues));
        }

        public static List<string> Validate(string root)
        {
            var issues = new List<string>();
            var catalog = JsonConvert.DeserializeObject<LanguageCatalog>(File.ReadAllText(Path.Combine(root, "languages.json")));
            string reference = catalog.DefaultLanguage;

            foreach (string table in catalog.Tables)
            {
                Dictionary<string, string> baseline = Load(root, reference, table, issues);
                if (baseline == null) continue;

                foreach (LanguageInfo language in catalog.Languages)
                {
                    if (language.Code == reference)
                    {
                        CheckEmpty(language.Code, table, baseline, issues);
                        continue;
                    }

                    Dictionary<string, string> other = Load(root, language.Code, table, issues);
                    if (other == null) continue;
                    CheckEmpty(language.Code, table, other, issues);

                    foreach (var pair in baseline)
                    {
                        if (!other.TryGetValue(pair.Key, out string translated))
                        {
                            issues.Add($"{language.Code}/{table}: missing key '{pair.Key}'");
                            continue;
                        }
                        if (PlaceholderCount(pair.Value) != PlaceholderCount(translated))
                            issues.Add($"{language.Code}/{table}: placeholder mismatch in '{pair.Key}'");
                    }

                    foreach (string key in other.Keys)
                    {
                        if (!baseline.ContainsKey(key))
                            issues.Add($"{language.Code}/{table}: extra key '{key}' not in {reference}");
                    }
                }
            }
            return issues;
        }

        static Dictionary<string, string> Load(string root, string code, string table, List<string> issues)
        {
            string path = Path.Combine(root, code, table + ".json");
            if (!File.Exists(path))
            {
                issues.Add($"{code}/{table}: file missing");
                return null;
            }
            try
            {
                return JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(path));
            }
            catch (JsonException e)
            {
                issues.Add($"{code}/{table}: invalid JSON ({e.Message})");
                return null;
            }
        }

        static void CheckEmpty(string code, string table, Dictionary<string, string> strings, List<string> issues)
        {
            foreach (var pair in strings)
            {
                if (string.IsNullOrWhiteSpace(pair.Value))
                    issues.Add($"{code}/{table}: empty value for '{pair.Key}'");
            }
        }

        static int PlaceholderCount(string text)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match match in Placeholder.Matches(text ?? string.Empty))
                seen.Add(match.Groups[1].Value);
            return seen.Count;
        }
    }
}
