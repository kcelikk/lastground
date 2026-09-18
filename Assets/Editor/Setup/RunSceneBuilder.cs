using LastGround.App;
using LastGround.Data.Crowd;
using LastGround.Data.Players;
using LastGround.Data.Presentation;
using LastGround.Data.Weapons;
using LastGround.Data.Zombies;
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
    /// Run scene: greybox map + nav grid, light, camera, twin-stick HUD with combat panels, and the RunInstaller
    /// wired to placeholder meshes/materials and the combat data assets (TDD_02 §28). Map content moves to additive
    /// map scenes in M7.
    /// </summary>
    static partial class RunSceneBuilder
    {
        const string MaterialDir = "Assets/Art/Materials";
        const float GroundSize = 140f;
        static RunStatusHud _statusHud;
        static TeamPanel _teamPanel;
        static TeammateIndicators _indicators;
        static ResultsScreen _results;

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
            Material tracer = CreateFxMaterial("M_Tracer", "LG/FX_Additive");
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

            (TouchTwinStickInput input, RunHud hud, CombatHud combatHud) = BuildHud();

            var systems = new GameObject("[Systems]");
            var installer = systems.AddComponent<RunInstaller>();
            UiFactory.Assign(installer, "_camera", camera);
            UiFactory.Assign(installer, "_worldRoot", world.transform);
            UiFactory.Assign(installer, "_crowdCatalog", catalog);
            UiFactory.Assign(installer, "_navGrid", navGrid);
            UiFactory.Assign(installer, "_bloodParticleMaterial", bloodParticle);
            UiFactory.Assign(installer, "_bloodSplatMaterial", bloodSplat);
            UiFactory.Assign(installer, "_tracerMaterial", tracer);
            UiFactory.Assign(installer, "_weapon", AssetDatabase.LoadAssetAtPath<WeaponDefinition>(CombatContentBuilder.WeaponPath));
            UiFactory.Assign(installer, "_walker", AssetDatabase.LoadAssetAtPath<ZombieDefinition>(CombatContentBuilder.WalkerPath));
            UiFactory.Assign(installer, "_playerDefinition", AssetDatabase.LoadAssetAtPath<PlayerDefinition>(CombatContentBuilder.PlayerPath));
            UiFactory.Assign(installer, "_cameraProfile", AssetDatabase.LoadAssetAtPath<CameraProfile>(CombatContentBuilder.CameraPath));
            UiFactory.Assign(installer, "_playerMesh", capsule);
            UiFactory.Assign(installer, "_playerMaterial", player);
            UiFactory.Assign(installer, "_input", input);
            UiFactory.Assign(installer, "_hud", hud);
            UiFactory.Assign(installer, "_combatHud", combatHud);
            UiFactory.Assign(installer, "_statusHud", _statusHud);
            UiFactory.Assign(installer, "_teamPanel", _teamPanel);
            UiFactory.Assign(installer, "_teammateIndicators", _indicators);
            UiFactory.Assign(installer, "_results", _results);
            UiFactory.Assign(installer, "_directorProfile", AssetDatabase.LoadAssetAtPath<LastGround.Data.Director.DirectorProfile>(CombatContentBuilder.DirectorPath));
            UiFactory.Assign(installer, "_threatCurve", AssetDatabase.LoadAssetAtPath<LastGround.Data.Director.ThreatCurveDefinition>(CombatContentBuilder.ThreatPath));
            UiFactory.Assign(installer, "_playerScaling", AssetDatabase.LoadAssetAtPath<LastGround.Data.Director.PlayerCountScalingProfile>(CombatContentBuilder.ScalingPath));

            EditorSceneManager.SaveScene(scene, path);
        }

        static (TouchTwinStickInput, RunHud, CombatHud) BuildHud()
        {
            var canvasGo = new GameObject("Canvas_HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;

            // Hurt vignette first so everything else draws above it.
            Image vignette = BuildVignette(canvasGo.transform);

            // Sticks live directly under the canvas: their positions are set from screen coordinates.
            (RectTransform moveBase, RectTransform moveKnob, _) = BuildStick("MoveStick", canvasGo.transform);
            (RectTransform aimBase, RectTransform aimKnob, Image aimKnobImage) = BuildStick("AimStick", canvasGo.transform);

            RectTransform safe = UiFactory.Panel("SafeArea", canvasGo.transform);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            // SURVIVAL · HORDE · THREAT line on top, role/player count small below it.
            TMP_Text runLine = UiFactory.Label("RunStatus", safe, null, 34, FontStyles.Bold, Color.white);
            UiFactory.Place(runLine.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(1100f, 50f));
            runLine.alignment = TextAlignmentOptions.Top;
            runLine.richText = true;
            TMP_Text threatBanner = UiFactory.Label("ThreatBanner", safe, null, 64, FontStyles.Bold, new Color(1f, 0.35f, 0.25f));
            UiFactory.Place(threatBanner.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(1200f, 140f));
            threatBanner.alignment = TextAlignmentOptions.Top;
            threatBanner.richText = true;
            TMP_Text status = UiFactory.Label("Status", safe, null, 22, FontStyles.Normal, UiFactory.Muted);
            UiFactory.Place(status.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(1000f, 30f));
            status.alignment = TextAlignmentOptions.Top;

            (Image healthFill, TMP_Text healthLabel) = BuildHealthBar(safe);
            TMP_Text ammo = UiFactory.Label("Ammo", safe, null, 40, FontStyles.Bold, Color.white);
            UiFactory.Place(ammo.rectTransform, new Vector2(0f, 1f), new Vector2(74f, -72f), new Vector2(400f, 50f));
            Image reloadRing = BuildReloadRing(safe);

            // Dev stats sit at the bottom centre above the PerfHud rows (development builds only).
            TMP_Text net = UiFactory.Label("NetStats", safe, null, 22, FontStyles.Normal, new Color(0.55f, 1f, 0.55f, 0.9f));
            UiFactory.Place(net.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 86f), new Vector2(900f, 26f));
            net.alignment = TextAlignmentOptions.Bottom;
            TMP_Text crowd = UiFactory.Label("CrowdStats", safe, null, 22, FontStyles.Normal, new Color(0.55f, 1f, 0.55f, 0.9f));
            UiFactory.Place(crowd.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(900f, 26f));
            crowd.alignment = TextAlignmentOptions.Bottom;

            Button leave = UiFactory.Button("Leave", safe, "run.leave", new Vector2(240f, 84f), out _);
            UiFactory.Place((RectTransform)leave.transform, new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(240f, 84f));
            Button autoFire = UiFactory.Button("AutoFire", safe, null, new Vector2(300f, 70f), out TMP_Text autoFireLabel, 28f);
            UiFactory.Place((RectTransform)autoFire.transform, new Vector2(1f, 1f), new Vector2(-24f, -122f), new Vector2(300f, 70f));

            (GameObject deathOverlay, TMP_Text deathLabel, Image reviveRing) = BuildDeathOverlay(canvasGo.transform);
            _teamPanel = BuildTeamPanel(safe);
            _indicators = BuildIndicators(canvasGo.transform);
            _results = BuildResults(canvasGo.transform);

            var input = canvasGo.AddComponent<TouchTwinStickInput>();
            UiFactory.Assign(input, "_moveBase", moveBase);
            UiFactory.Assign(input, "_moveKnob", moveKnob);
            UiFactory.Assign(input, "_aimBase", aimBase);
            UiFactory.Assign(input, "_aimKnob", aimKnob);
            UiFactory.Assign(input, "_aimKnobImage", aimKnobImage);

            var hud = canvasGo.AddComponent<RunHud>();
            UiFactory.Assign(hud, "_statusLabel", status);
            UiFactory.Assign(hud, "_netLabel", net);
            UiFactory.Assign(hud, "_crowdLabel", crowd);
            UiFactory.Assign(hud, "_leaveButton", leave);

            var runStatus = canvasGo.AddComponent<RunStatusHud>();
            UiFactory.Assign(runStatus, "_line", runLine);
            UiFactory.Assign(runStatus, "_banner", threatBanner);

            var combat = canvasGo.AddComponent<CombatHud>();
            UiFactory.Assign(combat, "_healthFill", healthFill);
            UiFactory.Assign(combat, "_healthLabel", healthLabel);
            UiFactory.Assign(combat, "_ammoLabel", ammo);
            UiFactory.Assign(combat, "_reloadRing", reloadRing);
            UiFactory.Assign(combat, "_hurtVignette", vignette);
            UiFactory.Assign(combat, "_deathOverlay", deathOverlay);
            UiFactory.Assign(combat, "_deathLabel", deathLabel);
            UiFactory.Assign(combat, "_reviveRing", reviveRing);
            UiFactory.Assign(combat, "_autoFireButton", autoFire);
            UiFactory.Assign(combat, "_autoFireLabel", autoFireLabel);
            _statusHud = runStatus;
            return (input, hud, combat);
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
