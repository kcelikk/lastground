using System.IO;
using LastGround.EditorTools.Setup;
using UnityEditor;
using UnityEngine;

namespace LastGround.EditorTools.Scenery
{
    /// <summary>
    /// Brings the concept images under docs/reference into the game as 2D art (D-020): the menu background and the
    /// region cards. Each image is scaled down to a UI size and saved as JPG under Assets/Art/UI/Concept (committed);
    /// when docs/reference is missing, the existing outputs are kept.
    /// </summary>
    static class ConceptArtImport
    {
        public const string Output = "Assets/Art/UI/Concept";
        public const string MenuBackground = Output + "/menu_background.jpg";

        static readonly (string source, string output, int width)[] Images =
        {
            ("docs/reference/maps/01-industrial-district.png", MenuBackground, 1260),
            ("docs/reference/maps/02-foundry-yard.png", Output + "/region_foundry_yard.jpg", 768),
            ("docs/reference/maps/03-gas-station.png", Output + "/region_gas_station.jpg", 768),
            ("docs/reference/maps/04-hospital-extraction.png", Output + "/region_hospital_yard.jpg", 768),
        };

        /// <summary>Region card image for a zone id (null when not imported).</summary>
        public static Texture2D RegionCard(string zoneId) =>
            AssetDatabase.LoadAssetAtPath<Texture2D>(Output + "/region_" + zoneId + ".jpg");

        public static void Import()
        {
            ProjectSetup.EnsureFolder(Output);
            foreach ((string source, string output, int width) in Images)
            {
                if (!File.Exists(source))
                {
                    if (!File.Exists(output)) Debug.LogWarning("[ConceptArt] missing " + source);
                    continue;
                }
                if (File.Exists(output) && File.GetLastWriteTimeUtc(output) >= File.GetLastWriteTimeUtc(source)) continue;
                var original = new Texture2D(2, 2);
                original.LoadImage(File.ReadAllBytes(source));
                int height = Mathf.RoundToInt(width * (float)original.height / original.width);
                Texture2D scaled = Downsample(original, width, height);
                File.WriteAllBytes(output, scaled.EncodeToJPG(85));
                Object.DestroyImmediate(original);
                Object.DestroyImmediate(scaled);
                AssetDatabase.ImportAsset(output);
                Configure(output, width);
            }
        }

        /// <summary>CPU resample (batch mode runs without a GPU): 2 × 2 bilinear taps per output pixel.</summary>
        static Texture2D Downsample(Texture2D source, int width, int height)
        {
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color sum = Color.clear;
                    for (int sy = 0; sy < 2; sy++)
                        for (int sx = 0; sx < 2; sx++)
                            sum += source.GetPixelBilinear((x + 0.25f + sx * 0.5f) / width, (y + 0.25f + sy * 0.5f) / height);
                    pixels[y * width + x] = sum * 0.25f;
                }
            }
            var result = new Texture2D(width, height, TextureFormat.RGB24, false);
            result.SetPixels32(pixels);
            result.Apply();
            return result;
        }

        static void Configure(string path, int width)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = width > 1024 ? 2048 : 1024;
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "Android", overridden = true, maxTextureSize = importer.maxTextureSize, format = TextureImporterFormat.ASTC_6x6,
            });
            importer.SaveAndReimport();
        }
    }
}
