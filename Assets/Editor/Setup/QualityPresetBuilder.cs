using LastGround.Data.Quality;
using UnityEditor;

namespace LastGround.EditorTools.Setup
{
    /// <summary>
    /// Creates the three presentation presets from TDD_02 §21.9 under Resources/Quality (loaded by AppRoot).
    /// Values are starting hypotheses; M2 benchmarks adjust them. Existing assets are updated in place.
    /// </summary>
    static class QualityPresetBuilder
    {
        const string Folder = "Assets/Resources/Quality";

        public static void Build()
        {
            ProjectSetup.EnsureFolder(Folder);
            Write("LOW", 0, 30, lod0: 6f, lod1: 14f, maxVisible: 250, rim: false, corpses: 20, corpseLife: 8f, splats: 0, particles: 0.5f);
            Write("MEDIUM", 1, 60, lod0: 12f, lod1: 24f, maxVisible: 250, rim: true, corpses: 50, corpseLife: 12f, splats: 96, particles: 0.8f);
            Write("HIGH", 2, 60, lod0: 18f, lod1: 35f, maxVisible: 300, rim: true, corpses: 100, corpseLife: 20f, splats: 192, particles: 1f);
        }

        static void Write(string id, int level, int fps, float lod0, float lod1, int maxVisible, bool rim,
            int corpses, float corpseLife, int splats, float particles)
        {
            string path = Folder + "/" + id + ".asset";
            var preset = AssetDatabase.LoadAssetAtPath<QualityPresetDefinition>(path);
            if (preset == null)
            {
                preset = UnityEngine.ScriptableObject.CreateInstance<QualityPresetDefinition>();
                AssetDatabase.CreateAsset(preset, path);
            }
            preset.Id = id;
            preset.QualityLevel = level;
            preset.TargetFps = fps;
            preset.Lod0Distance = lod0;
            preset.Lod1Distance = lod1;
            preset.MaxVisibleCrowd = maxVisible;
            preset.RimLight = rim;
            preset.CorpseCap = corpses;
            preset.CorpseLifetime = corpseLife;
            preset.BloodSplatCap = splats;
            preset.ParticleMultiplier = particles;
            EditorUtility.SetDirty(preset);
        }
    }
}
