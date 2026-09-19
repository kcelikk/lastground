using System.IO;
using UnityEditor;
using UnityEngine;

namespace LastGround.EditorTools.Crowd
{
    /// <summary>
    /// One albedo texture per crowd body (the renderer binds it per body batch): the body's diffuse textures (UDIM tiles
    /// side by side) resampled on the CPU to <see cref="TileSize"/>, optionally with sci-fi cyan muted, saved as a PNG
    /// next to the baked body (committed; the Mixamo sources are not). Works in -nographics batch mode.
    /// </summary>
    static class BodyTextureBaker
    {
        const int TileSize = 1024;

        public static Texture2D Bake(CrowdBodySource source, string outputDir)
        {
            int tiles = source.DiffuseTiles.Length;
            var pixels = new Color32[TileSize * tiles * TileSize];
            for (int t = 0; t < tiles; t++)
            {
                Texture2D input = Load(source.DiffuseTiles[t]);
                Color32[] src = input.GetPixels32();
                for (int y = 0; y < TileSize; y++)
                {
                    int sy = Mathf.Min(input.height - 1, (int)((y + 0.5f) * input.height / TileSize));
                    for (int x = 0; x < TileSize; x++)
                    {
                        // Box-average a 2 × 2 source neighbourhood when shrinking (4K/2K → 1K).
                        int sx = Mathf.Min(input.width - 1, (int)((x + 0.5f) * input.width / TileSize));
                        Color32 c = Average(src, input.width, input.height, sx, sy);
                        if (source.MuteCyan) c = Mute(c);
                        c.a = 255;
                        pixels[y * TileSize * tiles + t * TileSize + x] = c;
                    }
                }
            }

            var texture = new Texture2D(TileSize * tiles, TileSize, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply(false);
            string path = outputDir + "/" + source.Id + "_Albedo.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.sRGBTexture = true;
            importer.mipmapEnabled = true;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.maxTextureSize = TileSize * tiles;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "Android", overridden = true, maxTextureSize = TileSize * tiles, format = TextureImporterFormat.ASTC_6x6,
            });
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Texture2D Load(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new FileNotFoundException(path);
            if (!importer.isReadable || importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.isReadable = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 4096;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Color32 Average(Color32[] src, int width, int height, int x, int y)
        {
            int x1 = Mathf.Min(width - 1, x + 1), y1 = Mathf.Min(height - 1, y + 1);
            Color32 a = src[y * width + x], b = src[y * width + x1], c = src[y1 * width + x], d = src[y1 * width + x1];
            return new Color32((byte)((a.r + b.r + c.r + d.r) >> 2), (byte)((a.g + b.g + c.g + d.g) >> 2),
                (byte)((a.b + b.b + c.b + d.b) >> 2), 255);
        }

        /// <summary>Saturated cyan/teal → the grey-brown of the surrounding flesh.</summary>
        static Color32 Mute(Color32 c)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            if (h < 0.42f || h > 0.58f || s < 0.35f) return c;
            Color flesh = Color.HSVToRGB(0.05f, 0.25f, v * 0.55f);
            return flesh;
        }
    }
}
