namespace LastGround.Save
{
    public interface ISaveService
    {
        SettingsData Settings { get; }

        /// <summary>Permanent meta profile (M9).</summary>
        ProfileData Profile { get; }

        /// <summary>Schedules a debounced save (1 s). Safe to call on every settings change.</summary>
        void RequestSave();

        /// <summary>Writes pending changes immediately (menu actions, app pause).</summary>
        void SaveNow();

        /// <summary>Drives the debounce timer; call once per frame with unscaled time.</summary>
        void Tick(float unscaledTime);
    }
}
