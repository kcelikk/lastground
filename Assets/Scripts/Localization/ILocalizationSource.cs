namespace LastGround.Localization
{
    /// <summary>Provides raw JSON text for localization files. Paths are relative, e.g. "languages" or "tr/ui".</summary>
    public interface ILocalizationSource
    {
        /// <summary>Returns the file content, or null when the file does not exist.</summary>
        string Load(string relativePath);
    }
}
