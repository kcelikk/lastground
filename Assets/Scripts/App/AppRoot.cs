using System;
using System.Globalization;
using System.IO;
using LastGround.App.Dev;
using LastGround.Core.Logging;
using LastGround.Core.Net.Protocol;
using LastGround.Core.Net.Session;
using LastGround.Core.Random;
using LastGround.Core.Services;
using LastGround.Localization;
using LastGround.Networking.Discovery;
using LastGround.Networking.MirrorLink;
using LastGround.Data.Quality;
using LastGround.Platform;
using LastGround.Platform.Net;
using LastGround.Rendering.Quality;
using LastGround.Save;
using LastGround.UI.Diagnostics;
using UnityEngine;

namespace LastGround.App
{
    /// <summary>
    /// App-lifetime composition root. Created before the first scene loads, so any scene can be played
    /// directly in the editor. Owns save, localization, quality, and the network session + discovery.
    /// </summary>
    public sealed class AppRoot : MonoBehaviour
    {
        ISaveService _save;
        SessionService _sessions;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void CreateOnStartup()
        {
            var go = new GameObject("[App]");
            DontDestroyOnLoad(go);
            go.AddComponent<AppRoot>();
        }

        void Awake()
        {
            AppServices.Clear();

            string saveDir = Path.Combine(Application.persistentDataPath, "save");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            string slot = DevAutomation.SaveSlot();
            if (!string.IsNullOrEmpty(slot)) saveDir = Path.Combine(saveDir, slot);
#endif
            var store = new JsonFileSaveStore(saveDir);
            _save = new SaveService(store);
            AppServices.Register(_save);

            var localization = new JsonLocalizationService(new ResourcesLocalizationSource());
            SettingsData settings = _save.Settings;
            if (string.IsNullOrEmpty(settings.Language))
            {
                settings.Language = JsonLocalizationService.SuggestLanguage(
                    Application.systemLanguage == SystemLanguage.Turkish, "en");
                _save.RequestSave();
            }
            settings.Language = localization.SetLanguage(settings.Language);
            localization.LanguageChanged += OnLanguageChanged;
            AppServices.Register<ILocalizationService>(localization);

            ApplyQuality(settings);
            EnsureIdentity(settings);
            var meta = new MetaService(_save, Resources.Load<Data.Meta.MetaCatalog>("Meta/META_Catalog"));
            AppServices.Register(meta);
            AppServices.Register<Meta.IMetaStore>(meta);
            CreateNetworking(settings);
            meta.Attach(AppServices.Get<ISession>());
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!string.IsNullOrEmpty(DevAutomation.Character)) meta.DevEquipCharacter(DevAutomation.Character);
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            PerfHud.Create();
#endif
            Log.Info(LogCategory.App, "App ready. Quality " + QualitySettings.GetQualityLevel()
                + ", " + Application.targetFrameRate + " FPS, language " + settings.Language);
        }

        void Update()
        {
            _save.Tick(Time.unscaledTime);
            _sessions.Tick(Time.realtimeSinceStartupAsDouble);
        }

        void OnDestroy()
        {
            _sessions?.Dispose();
        }

        /// <summary>Protocol version + app version: builds that differ refuse each other (TDD_02 §15.9).</summary>
        public static uint ContentHash => Hash32.Combine(NetProtocol.Version, Hash32.Of(Application.version));

        void CreateNetworking(SettingsData settings)
        {
            var config = new SessionConfig
            {
                ContentHash = ContentHash,
                PlayerName = settings.PlayerName,
                PlayerGuid = settings.PlayerGuid,
                SessionName = settings.PlayerName,
                TimeSource = () => Time.realtimeSinceStartupAsDouble,
            };
            var session = new NetSession(new MirrorLinkFactory(gameObject), config);
            INetworkInterfaces interfaces = PlatformNet.CreateInterfaces();
            var discovery = new UdpLanDiscovery(NetProtocol.Version, ContentHash, NetProtocol.DiscoveryPort,
                interfaces, PlatformNet.CreateMulticastLock());
            _sessions = new SessionService(session, discovery, interfaces, _save);

            AppServices.Register<ISession>(session);
            AppServices.Register<ISessionService>(_sessions);
            AppServices.Register(_sessions);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DevAutomation.Install(gameObject, _sessions);
#endif
        }

        void EnsureIdentity(SettingsData settings)
        {
            if (!string.IsNullOrEmpty(settings.PlayerName) && !string.IsNullOrEmpty(settings.PlayerGuid)) return;
            if (string.IsNullOrEmpty(settings.PlayerGuid)) settings.PlayerGuid = Guid.NewGuid().ToString("N");
            if (string.IsNullOrEmpty(settings.PlayerName))
            {
                var rng = new DeterministicRandom((uint)Environment.TickCount);
                settings.PlayerName = "Player-" + rng.Range(1000, 10000).ToString(CultureInfo.InvariantCulture);
            }
            _save.RequestSave();
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) _save.SaveNow();
        }

        void OnApplicationQuit()
        {
            _save.SaveNow();
        }

        void OnLanguageChanged(string language)
        {
            _save.Settings.Language = language;
            _save.RequestSave();
        }

        void ApplyQuality(SettingsData settings)
        {
            QualityPresetDefinition[] presets = Resources.LoadAll<QualityPresetDefinition>("Quality");
            System.Array.Sort(presets, (a, b) => a.QualityLevel.CompareTo(b.QualityLevel));
            var quality = new QualityService(presets);
            AppServices.Register(quality);

            if (settings.QualityLevel < 0 || settings.QualityLevel >= quality.LevelCount)
            {
                settings.QualityLevel = DeviceTierDetector.Detect();
                Log.Info(LogCategory.App, "Detected tier " + settings.QualityLevel + " (" + SystemInfo.graphicsDeviceName +
                    ", " + SystemInfo.systemMemorySize + " MB)");
                _save.RequestSave();
            }
            quality.Apply(settings.QualityLevel, settings.TargetFps);
        }
    }
}
