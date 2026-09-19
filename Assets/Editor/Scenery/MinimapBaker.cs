using System.IO;
using LastGround.Data.Map;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace LastGround.EditorTools.Scenery
{
    /// <summary>
    /// Mini-map prerender (TDD_01 §11.5): builds the map in a temporary scene, renders it orthographically from above
    /// into a 1024² texture (Assets/Art/Maps) and assigns it to the map definition. Optionally writes review shots of
    /// each region from the game camera angle. Needs a graphics device (batch mode without -nographics).
    /// CLI: -executeMethod LastGround.EditorTools.Scenery.MinimapBaker.BakeBatch [-lgReview &lt;folder&gt;]
    /// </summary>
    public static class MinimapBaker
    {
        const int Resolution = 1024;
        public const string MinimapPath = "Assets/Art/Maps/Industrial_Minimap.png";

        public static void BakeBatch()
        {
            string review = null;
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-lgReview") review = args[i + 1];
            try
            {
                Bake(review);
                EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        [MenuItem("LastGround/Map/Bake Industrial Map + Minimap")]
        public static void BakeMenu() => Bake(null);

        public static void Bake(string reviewFolder)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Lighting();
            IndustrialMapBuilder.Build();
            var map = AssetDatabase.LoadAssetAtPath<MapDefinition>(IndustrialMapBuilder.MapPath);

            var cameraGo = new GameObject("Bake Camera");
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.orthographic = true;
            camera.orthographicSize = map.Size * 0.5f;
            camera.farClipPlane = 300f;
            camera.nearClipPlane = 1f;
            camera.transform.SetPositionAndRotation(new Vector3(0f, 150f, 0f), Quaternion.Euler(90f, 0f, 0f));
            // The first render of a fresh batch session only warms the pipeline up.
            Object.DestroyImmediate(Render(camera, 64, 64));
            Save(Render(camera, Resolution, Resolution), MinimapPath);
            ConfigureMinimap();
            map.Minimap = AssetDatabase.LoadAssetAtPath<Texture2D>(MinimapPath);
            EditorUtility.SetDirty(map);

            if (!string.IsNullOrEmpty(reviewFolder)) Review(camera, map, reviewFolder);
            Object.DestroyImmediate(cameraGo);
            AssetDatabase.SaveAssets();
            Debug.Log("[MinimapBaker] " + MinimapPath);
        }

        /// <summary>Region shots from the game camera (pitch 55°, FOV 35°), a little further out than in play.</summary>
        static void Review(Camera camera, MapDefinition map, string folder)
        {
            Directory.CreateDirectory(folder);
            camera.orthographic = false;
            camera.fieldOfView = 35f;
            camera.farClipPlane = 150f;
            camera.backgroundColor = new Color(0.04f, 0.05f, 0.06f);
            var rotation = Quaternion.Euler(55f, 0f, 0f);
            foreach (MapZoneSet.Zone zone in map.Regions.Zones)
            {
                var focus = new Vector3(zone.Center.x, 0f, zone.Center.y);
                camera.transform.SetPositionAndRotation(focus - rotation * Vector3.forward * 60f, rotation);
                File.WriteAllBytes(Path.Combine(folder, zone.Id + ".png"), Render(camera, 1600, 900).EncodeToPNG());
            }
            var street = new Vector3(0f, 0f, 20f);
            camera.transform.SetPositionAndRotation(street - rotation * Vector3.forward * 30f, rotation);
            File.WriteAllBytes(Path.Combine(folder, "street_gameplay.png"), Render(camera, 1600, 900).EncodeToPNG());
        }

        static Texture2D Render(Camera camera, int width, int height)
        {
            var target = new RenderTexture(width, height, 24);
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            RenderTexture.active = null;
            camera.targetTexture = null;
            Object.DestroyImmediate(target);
            return texture;
        }

        static void Save(Texture2D texture, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
        }

        static void ConfigureMinimap()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(MinimapPath);
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = Resolution;
            importer.SaveAndReimport();
        }

        /// <summary>Same moonlight as the run scene (RunSceneBuilder).</summary>
        static void Lighting()
        {
            var light = new GameObject("Moon", typeof(Light));
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var l = light.GetComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = 0.9f;
            l.color = new Color(0.8f, 0.85f, 1f);
            l.shadows = LightShadows.None;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.25f, 0.25f, 0.3f);
        }
    }
}
