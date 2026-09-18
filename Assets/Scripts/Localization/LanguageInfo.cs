namespace LastGround.Localization
{
    /// <summary>One entry of languages.json.</summary>
    public sealed class LanguageInfo
    {
        /// <summary>Lower-case code, also the folder name (e.g. "en", "tr").</summary>
        public string Code;

        /// <summary>Native display name ("English", "Türkçe").</summary>
        public string Name;

        /// <summary>Language used for missing keys; null for the reference language.</summary>
        public string Fallback;
    }
}
