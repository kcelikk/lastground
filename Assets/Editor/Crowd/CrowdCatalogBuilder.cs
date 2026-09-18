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
            EditorUtility.SetDirty(catalog);
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
