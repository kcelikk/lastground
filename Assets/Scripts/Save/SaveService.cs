using System;
using System.Globalization;
using LastGround.Core.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LastGround.Save
{
    /// <summary>
    /// Owns settings and the meta profile (M9). Run state is never written here (D-005). A profile written by a newer
    /// game version is read but never overwritten.
    /// </summary>
    public sealed class SaveService : ISaveService
    {
        public const string SettingsKey = "settings";
        public const string ProfileKey = "profile";
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
        public ProfileData Profile { get; }

        /// <summary>The stored profile is from a newer version: it is kept read-only.</summary>
        public bool ProfileReadOnly { get; private set; }

        public SaveService(ISaveStore store, ProfileMigrator migrator = null)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            Settings = LoadSettings();
            Profile = LoadProfile(migrator ?? ProfileMigrator.Default);
        }

        public void RequestSave()
        {
            _saveAt = _now + DebounceSeconds;
        }

        public void SaveNow()
        {
            _saveAt = -1f;
            _store.Write(SettingsKey, JsonConvert.SerializeObject(Settings, Json));
            if (!ProfileReadOnly) _store.Write(ProfileKey, JsonConvert.SerializeObject(Profile, Json));
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

        ProfileData LoadProfile(ProfileMigrator migrator)
        {
            if (!_store.TryRead(ProfileKey, out string json)) return new ProfileData();
            try
            {
                JObject tree = JObject.Parse(json);
                switch (migrator.Migrate(tree, ProfileData.CurrentVersion))
                {
                    case ProfileMigrator.Outcome.TooNew:
                        Log.Warning(LogCategory.Save, "Profile is from a newer version; it will not be overwritten.");
                        ProfileReadOnly = true;
                        break;
                    case ProfileMigrator.Outcome.Unsupported:
                        Log.Warning(LogCategory.Save, "Profile version cannot be upgraded; kept read-only.");
                        ProfileReadOnly = true;
                        break;
                }
                ProfileData data = tree.ToObject<ProfileData>(JsonSerializer.Create(Json));
                return Normalize(data ?? new ProfileData());
            }
            catch (JsonException e)
            {
                Log.Warning(LogCategory.Save, "Profile unreadable, starting a new one: " + e.Message);
                return new ProfileData();
            }
        }

        /// <summary>Missing lists (hand-edited or older files) become empty ones.</summary>
        static ProfileData Normalize(ProfileData data)
        {
            if (data.Owned == null) data.Owned = new System.Collections.Generic.List<string>();
            if (data.Badges == null) data.Badges = new System.Collections.Generic.List<string>();
            if (data.Outfits == null) data.Outfits = new System.Collections.Generic.List<OutfitChoice>();
            if (data.Emotes == null) data.Emotes = new System.Collections.Generic.List<string>();
            if (data.Stats == null) data.Stats = new ProfileStats();
            return data;
        }

        static SettingsData Migrate(SettingsData data)
        {
            // v1 is the first format; future steps go here (v1 → v2 → …).
            data.Version = SettingsData.CurrentVersion;
            return data;
        }
    }
}
