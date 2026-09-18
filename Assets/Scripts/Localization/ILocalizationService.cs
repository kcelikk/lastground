using System;
using System.Collections.Generic;

namespace LastGround.Localization
{
    /// <summary>Key-based string lookup (D-010). Code never contains user-facing text.</summary>
    public interface ILocalizationService
    {
        string CurrentLanguage { get; }
        IReadOnlyList<LanguageInfo> Languages { get; }

        /// <summary>Raised after the active language changed and all tables are loaded.</summary>
        event Action<string> LanguageChanged;

        /// <summary>Switches language. Unknown codes fall back to the default language. Returns the applied code.</summary>
        string SetLanguage(string code);

        /// <summary>Returns the localized string. Missing keys return "#key#" in development builds.</summary>
        string Get(string key);

        bool HasKey(string key);

        /// <summary>Formats a key with one argument using invariant culture (for menus; HUD uses TMP SetText).</summary>
        string Format(string key, object arg0);
    }
}
