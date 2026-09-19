using LastGround.Data.Crowd;
using UnityEditor;
using UnityEngine;

namespace LastGround.EditorTools.Crowd
{
    /// <summary>Creates or updates the crowd catalog and its shared material after baking.</summary>
    static class CrowdCatalogBuilder
    {
        public const string CatalogPath = "Assets/Art/Crowd/CrowdCatalog.asset";
        const string MaterialPath = "Assets/Art/Crowd/M_Crowd.mat";
        const string AtlasPath = "Assets/ThirdParty/Quaternius/ZombieKit/Zombie_Atlas.png";

        public static void Build(CrowdAnimationSet[] bodies)
        {
            ConfigureAtlas();
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("LG/CrowdInstanced"));
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath));
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);

            var catalog = AssetDatabase.LoadAssetAtPath<CrowdVisualCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<CrowdVisualCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            catalog.Bodies = bodies;
            catalog.Material = material;
            catalog.TypeLooks = TypeLooks(bodies);
            EditorUtility.SetDirty(catalog);
        }

        /// <summary>
        /// Zombie type → bodies by body id (walker_*, runner, tank, spitter, exploder; TypeIndex order), with size and a
        /// faint constant glow for the Spitter (bile) and Exploder (pustules). Elite and status glows come on top at runtime.
        /// </summary>
        static CrowdTypeLook[] TypeLooks(CrowdAnimationSet[] bodies)
        {
            return new[]
            {
                Look("walker", bodies, "walker_", 1f, Color.black, 0f),
                Look("runner", bodies, "runner", 1f, Color.black, 0f),
                Look("tank", bodies, "tank", 1.25f, Color.black, 0f),
                Look("spitter", bodies, "spitter", 1f, new Color(0.45f, 1f, 0.2f), 0.08f),
                Look("exploder", bodies, "exploder", 1.1f, new Color(1f, 0.45f, 0.1f), 0.12f),
                BossLook(bodies),
            };
        }

        /// <summary>Mutant Brute (D-021): ~3.5× a player, clips start with each attack so telegraphs and swings line up.</summary>
        static CrowdTypeLook BossLook(CrowdAnimationSet[] bodies)
        {
            CrowdTypeLook look = Look("brute", bodies, "boss_", 3.5f, Color.black, 0f);
            look.ClipsFromStateStart = true;
            return look;
        }

        static CrowdTypeLook Look(string zombie, CrowdAnimationSet[] bodies, string prefix, float scale, Color glow, float strength)
        {
            var indices = new System.Collections.Generic.List<int>();
            for (int i = 0; i < bodies.Length; i++)
                if (bodies[i].Id.StartsWith(prefix, System.StringComparison.Ordinal)) indices.Add(i);
            return new CrowdTypeLook { ZombieId = zombie, Bodies = indices.ToArray(), Scale = scale, Glow = glow, GlowStrength = strength };
        }

        /// <summary>Palette atlas: no mipmaps or compression artefacts between colour cells.</summary>
        static void ConfigureAtlas()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(AtlasPath);
            if (importer == null || (!importer.mipmapEnabled && importer.textureCompression == TextureImporterCompression.Uncompressed)) return;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
    }
}
