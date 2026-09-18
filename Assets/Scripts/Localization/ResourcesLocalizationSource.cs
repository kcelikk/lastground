using UnityEngine;

namespace LastGround.Localization
{
    /// <summary>Loads JSON tables from Assets/Resources/Localization (no Addressables in MVP, TDD_03 §39).</summary>
    public sealed class ResourcesLocalizationSource : ILocalizationSource
    {
        public const string Root = "Localization/";

        public string Load(string relativePath)
        {
            var asset = Resources.Load<TextAsset>(Root + relativePath);
            if (asset == null) return null;
            string text = asset.text;
            Resources.UnloadAsset(asset);
            return text;
        }
    }
}
