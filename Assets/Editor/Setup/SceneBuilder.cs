using System.IO;
using LastGround.App;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace LastGround.EditorTools.Setup
{
    /// <summary>
    /// Generates Boot, Menu and Run scenes (TDD_02 §28) and registers them in Build Settings. Existing scenes are
    /// left untouched so manual edits survive; use LastGround/Setup/Rebuild Scenes to regenerate on purpose.
    /// </summary>
    static class SceneBuilder
    {
        public const string BootPath = "Assets/Scenes/Boot/Boot.unity";
        public const string MenuPath = "Assets/Scenes/Menu/Menu.unity";
        public const string RunPath = "Assets/Scenes/Run/Run.unity";

        public static void BuildAll(bool rebuild = false)
        {
            if (rebuild)
            {
                AssetDatabase.DeleteAsset(MenuPath);
                AssetDatabase.DeleteAsset(RunPath);
            }
            if (!File.Exists(BootPath)) BuildBoot();
            if (!File.Exists(MenuPath)) MenuSceneBuilder.Build(MenuPath);
            if (!File.Exists(RunPath)) RunSceneBuilder.Build(RunPath);

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(BootPath, true),
                new EditorBuildSettingsScene(MenuPath, true),
                new EditorBuildSettingsScene(RunPath, true),
            };
        }

        static void BuildBoot()
        {
            Scene scene = NewScene("Assets/Scenes/Boot");
            CreateCamera(70f);
            new GameObject("Boot", typeof(BootLoader));
            EditorSceneManager.SaveScene(scene, BootPath);
        }

        public static Scene NewScene(string folder)
        {
            ProjectSetup.EnsureFolder(folder);
            return EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        public static Camera CreateCamera(float farClip)
        {
            var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            go.tag = "MainCamera";
            var camera = go.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = UiFactory.Background;
            camera.farClipPlane = farClip;
            return camera;
        }

        public static void CreateEventSystem()
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }
    }
}
