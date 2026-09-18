using UnityEngine;

namespace LastGround.Platform.Net
{
    /// <summary>Android MulticastLock via JNI; a no-op on other platforms.</summary>
    public sealed class AndroidMulticastLock : IMulticastLock
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidJavaObject _lock;
#endif

        public bool IsHeld { get; private set; }

        public void Acquire()
        {
            if (IsHeld) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                if (_lock == null)
                {
                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    using (AndroidJavaObject context = activity.Call<AndroidJavaObject>("getApplicationContext"))
                    using (AndroidJavaObject wifi = context.Call<AndroidJavaObject>("getSystemService", "wifi"))
                    {
                        _lock = wifi.Call<AndroidJavaObject>("createMulticastLock", "lastground-discovery");
                        _lock.Call("setReferenceCounted", false);
                    }
                }
                _lock.Call("acquire");
            }
            catch (AndroidJavaException e)
            {
                Debug.LogWarning("[Net] MulticastLock failed: " + e.Message);
                return;
            }
#endif
            IsHeld = true;
        }

        public void Release()
        {
            if (!IsHeld) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                _lock?.Call("release");
            }
            catch (AndroidJavaException e)
            {
                Debug.LogWarning("[Net] MulticastLock release failed: " + e.Message);
            }
#endif
            IsHeld = false;
        }
    }
}
