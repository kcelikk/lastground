using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LastGround.EditorTools.Setup
{
    /// <summary>
    /// Rewrites ProjectSettings/QualitySettings.asset to exactly the given tiers (D-006). Unity has no public API
    /// to add or rename quality levels, so this edits the serialized asset.
    /// </summary>
    static class QualityLevels
    {
        public readonly struct Level
        {
            public readonly string Name;
            public readonly RenderPipelineAsset Pipeline;

            public Level(string name, RenderPipelineAsset pipeline)
            {
                Name = name;
                Pipeline = pipeline;
            }
        }

        public static void Apply(Level[] levels, int defaultIndex)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset");
            var settings = new SerializedObject(assets[0]);
            var list = settings.FindProperty("m_QualitySettings");

            while (list.arraySize < levels.Length)
                list.InsertArrayElementAtIndex(list.arraySize);
            while (list.arraySize > levels.Length)
                list.DeleteArrayElementAtIndex(list.arraySize - 1);

            for (int i = 0; i < levels.Length; i++)
            {
                var element = list.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("name").stringValue = levels[i].Name;
                element.FindPropertyRelative("customRenderPipeline").objectReferenceValue = levels[i].Pipeline;
                element.FindPropertyRelative("vSyncCount").intValue = 0;
                element.FindPropertyRelative("antiAliasing").intValue = 0; // MSAA lives in the URP asset
                element.FindPropertyRelative("pixelLightCount").intValue = 0;
                element.FindPropertyRelative("realtimeReflectionProbes").boolValue = false;
                element.FindPropertyRelative("softParticles").boolValue = false;
                element.FindPropertyRelative("lodBias").floatValue = 1f;
                element.FindPropertyRelative("globalTextureMipmapLimit").intValue = 0;
            }

            settings.FindProperty("m_CurrentQuality").intValue = defaultIndex;

            // Default tier per platform: every platform starts at MEDIUM; AppRoot re-picks from RAM on first launch.
            var perPlatform = settings.FindProperty("m_PerPlatformDefaultQuality");
            for (int i = 0; i < perPlatform.arraySize; i++)
                perPlatform.GetArrayElementAtIndex(i).FindPropertyRelative("second").intValue = defaultIndex;

            settings.ApplyModifiedPropertiesWithoutUndo();
            QualitySettings.SetQualityLevel(defaultIndex, true);
        }
    }
}
