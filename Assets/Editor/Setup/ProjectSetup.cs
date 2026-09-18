using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LastGround.EditorTools.Setup
{
    /// <summary>
    /// Idempotent project configuration (TDD_02 §21.1, §21.9, §30.2; TDD_03 §40.2).
    /// Re-run after Unity upgrades or when settings drift:
    ///   Menu: LastGround/Setup/Apply Project Settings
    ///   CLI:  Unity -batchmode -quit -projectPath . -executeMethod LastGround.EditorTools.Setup.ProjectSetup.ApplyAllBatch
    /// </summary>
    public static class ProjectSetup
    {
        public const string CompanyName = "AsgardGame";
        public const string ProductName = "Last Ground";
        // Placeholder until the Play Console listing is created (OPEN_QUESTIONS C4). Changeable until the first upload.
        public const string ApplicationId = "com.asgardgame.lastground";
        public const string Version = "0.1.0";

        const string RenderingDir = "Assets/Settings/Rendering";

        [MenuItem("LastGround/Setup/Apply Project Settings")]
        public static void ApplyAll()
        {
            ApplyEditorSettings();
            ApplyPlayerSettings();
            ApplyRendering();
            QualityPresetBuilder.Build();
            SceneBuilder.BuildAll();
            AssetDatabase.SaveAssets();
            Debug.Log("[Setup] Project settings applied.");
        }

        [MenuItem("LastGround/Setup/Rebuild Scenes (overwrites Menu and Run)")]
        public static void RebuildScenes()
        {
            SceneBuilder.BuildAll(rebuild: true);
            AssetDatabase.SaveAssets();
        }

        public static void RebuildScenesBatch()
        {
            RunBatch(() =>
            {
                ApplyAll();
                RebuildScenes();
            });
        }

        /// <summary>Batch entry point; exits with a non-zero code on failure.</summary>
        public static void ApplyAllBatch()
        {
            RunBatch(ApplyAll);
        }

        static void RunBatch(Action action)
        {
            try
            {
                action();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        static void ApplyEditorSettings()
        {
            EditorSettings.serializationMode = SerializationMode.ForceText;
            VersionControlSettings.mode = "Visible Meta Files";
            EditorSettings.enterPlayModeOptionsEnabled = false;
        }

        static void ApplyPlayerSettings()
        {
            var android = NamedBuildTarget.Android;

            PlayerSettings.companyName = CompanyName;
            PlayerSettings.productName = ProductName;
            PlayerSettings.bundleVersion = Version;
            PlayerSettings.SetApplicationIdentifier(android, ApplicationId);
            PlayerSettings.Android.bundleVersionCode = Math.Max(1, PlayerSettings.Android.bundleVersionCode);

            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.gcIncremental = true;
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(android, ManagedStrippingLevel.Medium);

            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.GameActivity;
            PlayerSettings.Android.forceInternetPermission = true;
            PlayerSettings.Android.optimizedFramePacing = true;
            PlayerSettings.Android.renderOutsideSafeArea = true;

            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,
                new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

            PlayerSettings.enableFrameTimingStats = true;

            // Desktop builds are LAN test peers only (TDD_02 §31.1): small window, keep running unfocused.
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;

            // Input System only (no legacy UnityEngine.Input). 0 = old, 1 = new, 2 = both.
            var playerSettings = new SerializedObject(Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings"));
            playerSettings.FindProperty("activeInputHandler").intValue = 1;
            playerSettings.ApplyModifiedPropertiesWithoutUndo();
        }

        static void ApplyRendering()
        {
            EnsureFolder(RenderingDir);

            var low = CreateOrLoadPipeline("URP_Low", p =>
            {
                p.renderScale = 0.7f;
                p.msaaSampleCount = 1;
                p.supportsHDR = false;
                SetMainLightShadows(p, false);
                p.shadowDistance = 0f;
                SetAdditionalLights(p, LightRenderingMode.Disabled);
                p.maxAdditionalLightsCount = 0;
            });
            var medium = CreateOrLoadPipeline("URP_Medium", p =>
            {
                p.renderScale = 0.85f;
                p.msaaSampleCount = 2;
                p.supportsHDR = true;
                SetMainLightShadows(p, true);
                p.mainLightShadowmapResolution = 512;
                p.shadowDistance = 25f;
                p.shadowCascadeCount = 1;
                SetAdditionalLights(p, LightRenderingMode.PerPixel);
                p.maxAdditionalLightsCount = 2;
            });
            var high = CreateOrLoadPipeline("URP_High", p =>
            {
                p.renderScale = 1f;
                p.msaaSampleCount = 4;
                p.supportsHDR = true;
                SetMainLightShadows(p, true);
                p.mainLightShadowmapResolution = 1024;
                p.shadowDistance = 25f;
                p.shadowCascadeCount = 1;
                SetAdditionalLights(p, LightRenderingMode.PerPixel);
                p.maxAdditionalLightsCount = 4;
            });

            GraphicsSettings.defaultRenderPipeline = medium;
            QualityLevels.Apply(new[]
            {
                new QualityLevels.Level("LOW", low),
                new QualityLevels.Level("MEDIUM", medium),
                new QualityLevels.Level("HIGH", high),
            }, defaultIndex: 1);
        }

        static UniversalRenderPipelineAsset CreateOrLoadPipeline(string name, Action<UniversalRenderPipelineAsset> configure)
        {
            string path = RenderingDir + "/" + name + ".asset";
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (asset == null)
            {
                var renderer = CreateRendererData(RenderingDir + "/" + name + "_Renderer.asset");
                asset = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(asset, path);
            }

            // Shared mobile rules (TDD_02 §21.1): no depth/opaque copies, SRP batcher, no dynamic batching.
            asset.supportsCameraDepthTexture = false;
            asset.supportsCameraOpaqueTexture = false;
            SetSerialized(asset, "m_SoftShadowsSupported", false);
            SetSerialized(asset, "m_AdditionalLightShadowsSupported", false);
            asset.useSRPBatcher = true;
            asset.supportsDynamicBatching = false;
            configure(asset);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        // Some URP properties only have internal setters; write their serialized fields instead.
        static void SetMainLightShadows(UniversalRenderPipelineAsset asset, bool enabled)
        {
            SetSerialized(asset, "m_MainLightShadowsSupported", enabled);
        }

        static void SetAdditionalLights(UniversalRenderPipelineAsset asset, LightRenderingMode mode)
        {
            var so = new SerializedObject(asset);
            so.FindProperty("m_AdditionalLightsRenderingMode").intValue = (int)mode;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetSerialized(UniversalRenderPipelineAsset asset, string field, bool value)
        {
            var so = new SerializedObject(asset);
            var property = so.FindProperty(field)
                           ?? throw new InvalidOperationException("URP field not found: " + field);
            property.boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static ScriptableRendererData CreateRendererData(string path)
        {
            // URP only exposes renderer creation through an internal helper that also assigns default resources.
            MethodInfo method = typeof(UniversalRenderPipelineAsset).GetMethod("CreateRendererAsset",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (method == null)
                throw new InvalidOperationException("URP CreateRendererAsset not found; check the URP version.");
            return (ScriptableRendererData)method.Invoke(null,
                new object[] { path, RendererType.UniversalRenderer, false, "Renderer" });
        }

        internal static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
