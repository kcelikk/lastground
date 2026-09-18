using LastGround.App;
using LastGround.Data.Crowd;
using LastGround.EditorTools.Map;
using LastGround.Input;
using LastGround.UI.Common;
using LastGround.UI.Run;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LastGround.EditorTools.Setup
{
    /// <summary>
    /// M1 run scene: greybox ground, light, camera, HUD with floating joystick, and the RunInstaller wired to
    /// placeholder meshes/materials (TDD_02 §28). Map content moves to additive map scenes in M3/M7.
    /// </summary>
    static class RunSceneBuilder
    {
        const string MaterialDir = "Assets/Art/Materials";
        const float GroundSize = 140f;

        public static void Build(string path)
        {
            Scene scene = SceneBuilder.NewScene("Assets/Scenes/Run");
            Camera camera = SceneBuilder.CreateCamera(120f);
            SceneBuilder.CreateEventSystem();

            var light = new GameObject("Directional Light (moon)", typeof(Light));
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var l = light.GetComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = 0.9f;
            l.color = new Color(0.8f, 0.85f, 1f);
            l.shadows = LightShadows.None;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.25f, 0.25f, 0.3f);

            Material player = CreateMaterial("M1_Player", Color.white);
            Material bloodParticle = CreateFxMaterial("M_BloodParticle", "LG/FX_AlphaBlend");
            Material bloodSplat = CreateFxMaterial("M_BloodSplat", "LG/GroundDecal");
            var catalog = AssetDatabase.LoadAssetAtPath<CrowdVisualCatalog>("Assets/Art/Crowd/CrowdCatalog.asset");
            if (catalog == null) Debug.LogWarning("[Setup] Crowd catalog missing; run LastGround/Crowd/Bake Bodies first.");
            Material ground = CreateMaterial("M1_Ground", new Color(0.16f, 0.17f, 0.18f));

            GameObject map = GreyboxMapBuilder.Build(CreateMaterial("M1_Wall", new Color(0.24f, 0.25f, 0.27f)),
                CreateMaterial("M1_Prop", new Color(0.3f, 0.27f, 0.22f)));
            var navGrid = NavGridBaker.Bake(map, GreyboxMapBuilder.Size, "Assets/Art/Maps/Greybox_NavGrid.asset");

            var groundGo = GameObject.CreatePrimitive(PrimitiveType.Plane);
            groundGo.name = "Ground (greybox)";
            groundGo.transform.localScale = new Vector3(GroundSize / 10f, 1f, GroundSize / 10f);
            groundGo.GetComponent<MeshRenderer>().sharedMaterial = ground;
            Object.DestroyImmediate(groundGo.GetComponent<Collider>());

            // Light pools: one quad over the ground drawing the lighting grid additively.
            var pools = GameObject.CreatePrimitive(PrimitiveType.Quad);
            pools.name = "LightPools (lighting grid)";
            pools.transform.SetPositionAndRotation(new Vector3(0f, 0.02f, 0f), Quaternion.Euler(90f, 0f, 0f));
            pools.transform.localScale = new Vector3(GroundSize, GroundSize, 1f);
            pools.GetComponent<MeshRenderer>().sharedMaterial = CreateFxMaterial("M_LightPools", "LG/LightPoolGround");
            Object.DestroyImmediate(pools.GetComponent<Collider>());

            var world = new GameObject("World");
            Mesh capsule = Resources.GetBuiltinResource<Mesh>("Capsule.fbx");

            (TouchMoveInput input, RunHud hud) = BuildHud();

            var systems = new GameObject("[Systems]");
            var installer = systems.AddComponent<RunInstaller>();
            UiFactory.Assign(installer, "_camera", camera);
            UiFactory.Assign(installer, "_worldRoot", world.transform);
            UiFactory.Assign(installer, "_crowdCatalog", catalog);
            UiFactory.Assign(installer, "_navGrid", navGrid);
            UiFactory.Assign(installer, "_bloodParticleMaterial", bloodParticle);
            UiFactory.Assign(installer, "_bloodSplatMaterial", bloodSplat);
            UiFactory.Assign(installer, "_playerMesh", capsule);
            UiFactory.Assign(installer, "_playerMaterial", player);
            UiFactory.Assign(installer, "_input", input);
            UiFactory.Assign(installer, "_hud", hud);

            EditorSceneManager.SaveScene(scene, path);
        }

        static (TouchMoveInput, RunHud) BuildHud()
        {
            var canvasGo = new GameObject("Canvas_HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;

            // Joystick lives directly under the canvas: its position is set from screen coordinates.
            Sprite knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            RectTransform stickBase = UiFactory.Rect("StickBase", canvasGo.transform);
            stickBase.anchorMin = stickBase.anchorMax = new Vector2(0.5f, 0.5f);
            stickBase.sizeDelta = new Vector2(260f, 260f);
            var baseImage = stickBase.gameObject.AddComponent<Image>();
            baseImage.sprite = knob;
            baseImage.color = new Color(1f, 1f, 1f, 0.15f);
            baseImage.raycastTarget = false;
            RectTransform stickKnob = UiFactory.Rect("Knob", stickBase);
            stickKnob.sizeDelta = new Vector2(110f, 110f);
            var knobImage = stickKnob.gameObject.AddComponent<Image>();
            knobImage.sprite = knob;
            knobImage.color = new Color(1f, 1f, 1f, 0.5f);
            knobImage.raycastTarget = false;

            RectTransform safe = UiFactory.Panel("SafeArea", canvasGo.transform);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            TMP_Text status = UiFactory.Label("Status", safe, null, 34, FontStyles.Bold, Color.white);
            UiFactory.Place(status.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(1000f, 50f));
            status.alignment = TextAlignmentOptions.Top;
            TMP_Text net = UiFactory.Label("NetStats", safe, null, 22, FontStyles.Normal, new Color(0.55f, 1f, 0.55f, 0.9f));
            UiFactory.Place(net.rectTransform, new Vector2(0f, 1f), new Vector2(12f, -62f), new Vector2(900f, 30f));
            TMP_Text crowd = UiFactory.Label("CrowdStats", safe, null, 22, FontStyles.Normal, new Color(0.55f, 1f, 0.55f, 0.9f));
            UiFactory.Place(crowd.rectTransform, new Vector2(0f, 1f), new Vector2(12f, -88f), new Vector2(900f, 30f));

            Button leave = UiFactory.Button("Leave", safe, "run.leave", new Vector2(240f, 84f), out _);
            UiFactory.Place((RectTransform)leave.transform, new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(240f, 84f));

            var input = canvasGo.AddComponent<TouchMoveInput>();
            UiFactory.Assign(input, "_stickBase", stickBase);
            UiFactory.Assign(input, "_stickKnob", stickKnob);

            var hud = canvasGo.AddComponent<RunHud>();
            UiFactory.Assign(hud, "_statusLabel", status);
            UiFactory.Assign(hud, "_netLabel", net);
            UiFactory.Assign(hud, "_crowdLabel", crowd);
            UiFactory.Assign(hud, "_leaveButton", leave);
            return (input, hud);
        }

        static Material CreateFxMaterial(string name, string shader)
        {
            ProjectSetup.EnsureFolder(MaterialDir);
            string path = MaterialDir + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find(shader));
                AssetDatabase.CreateAsset(material, path);
            }
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material CreateMaterial(string name, Color color)
        {
            ProjectSetup.EnsureFolder(MaterialDir);
            string path = MaterialDir + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.1f);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
