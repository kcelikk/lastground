using UnityEngine;

namespace LastGround.Platform
{
    /// <summary>Device temperature for benchmark reports (TDD_02 §22.6). NaN when unavailable.</summary>
    public static class DeviceThermals
    {
        /// <summary>Battery temperature in °C (Android BATTERY_CHANGED sticky intent).</summary>
        public static float BatteryTemperatureC()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var filter = new AndroidJavaObject("android.content.IntentFilter", "android.intent.action.BATTERY_CHANGED"))
                using (AndroidJavaObject intent = activity.Call<AndroidJavaObject>("registerReceiver", null, filter))
                {
                    if (intent == null) return float.NaN;
                    int tenths = intent.Call<int>("getIntExtra", "temperature", -1000);
                    return tenths <= -1000 ? float.NaN : tenths / 10f;
                }
            }
            catch (AndroidJavaException)
            {
                return float.NaN;
            }
#else
            return float.NaN;
#endif
        }
    }
}
