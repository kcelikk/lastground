using System.IO;
using LastGround.Core.Logging;
using LastGround.Core.Services;
using LastGround.Localization;
using LastGround.Save;
using LastGround.UI.Diagnostics;
using UnityEngine;

namespace LastGround.App
{
    /// <summary>
    /// App-lifetime composition root. Created before the first scene loads, so any scene can be played
    /// directly in the editor. Owns save, localization and quality setup.
    /// </summary>
    public sealed class AppRoot : MonoBehaviour
    {
        ISaveService _save;

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

            var store = new JsonFileSaveStore(Path.Combine(Application.persistentDataPath, "save"));
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            PerfHud.Create();
#endif
            Log.Info(LogCategory.App, "App ready. Quality " + QualitySettings.GetQualityLevel()
                + ", " + Application.targetFrameRate + " FPS, language " + settings.Language);
        }

        void Update()
        {
            _save.Tick(Time.unscaledTime);
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
            if (settings.QualityLevel < 0 || settings.QualityLevel >= QualitySettings.names.Length)
            {
                settings.QualityLevel = QualityDefaults.FromSystemMemory(SystemInfo.systemMemorySize);
                _save.RequestSave();
            }

            QualitySettings.SetQualityLevel(settings.QualityLevel, true);
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = QualityDefaults.TargetFps(settings.QualityLevel, settings.TargetFps);
        }
    }
}
