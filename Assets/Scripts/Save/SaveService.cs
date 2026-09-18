using System;
using System.Globalization;
using LastGround.Core.Logging;
using Newtonsoft.Json;

namespace LastGround.Save
{
    /// <summary>
    /// Owns settings (and later the meta profile). Run state is never written here (D-005).
    /// </summary>
    public sealed class SaveService : ISaveService
    {
        public const string SettingsKey = "settings";
        const float DebounceSeconds = 1f;

        static readonly JsonSerializerSettings Json = new JsonSerializerSettings
        {
            Culture = CultureInfo.InvariantCulture,
            Formatting = Formatting.Indented,
        };

        readonly ISaveStore _store;
        float _now;
        float _saveAt = -1f;

        public SettingsData Settings { get; }

        public SaveService(ISaveStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            Settings = LoadSettings();
        }

        public void RequestSave()
        {
            _saveAt = _now + DebounceSeconds;
        }

        public void SaveNow()
        {
            _saveAt = -1f;
            _store.Write(SettingsKey, JsonConvert.SerializeObject(Settings, Json));
        }

        public void Tick(float unscaledTime)
        {
            _now = unscaledTime;
            if (_saveAt >= 0f && _now >= _saveAt)
                SaveNow();
        }

        SettingsData LoadSettings()
        {
            if (_store.TryRead(SettingsKey, out string json))
            {
                try
                {
                    var data = JsonConvert.DeserializeObject<SettingsData>(json, Json);
                    if (data != null)
                        return Migrate(data);
                }
                catch (JsonException e)
                {
                    Log.Warning(LogCategory.Save, "Settings unreadable, using defaults: " + e.Message);
                }
            }
            return new SettingsData();
        }

        static SettingsData Migrate(SettingsData data)
        {
            // v1 is the first format; future steps go here (v1 → v2 → …).
            data.Version = SettingsData.CurrentVersion;
            return data;
        }
    }
}
