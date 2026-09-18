using System.Collections.Generic;
using UnityEngine;

namespace LastGround.Platform.Net
{
    /// <summary>
    /// Android implementation through java.net.NetworkInterface. The .NET API can return nothing on Android 11+
    /// for apps targeting API 30+ (netlink restrictions), so the Java API is the reliable path (TDD_02 §18.2).
    /// </summary>
    public sealed class AndroidNetworkInterfaces : INetworkInterfaces
    {
        public IReadOnlyList<LocalInterface> GetIPv4()
        {
            var result = new List<LocalInterface>();
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var nicClass = new AndroidJavaClass("java.net.NetworkInterface"))
                using (AndroidJavaObject nics = nicClass.CallStatic<AndroidJavaObject>("getNetworkInterfaces"))
                {
                    while (nics != null && nics.Call<bool>("hasMoreElements"))
                    {
                        using (AndroidJavaObject nic = nics.Call<AndroidJavaObject>("nextElement"))
                        {
                            if (!nic.Call<bool>("isUp") || nic.Call<bool>("isLoopback")) continue;
                            string name = nic.Call<string>("getName");
                            using (AndroidJavaObject addresses = nic.Call<AndroidJavaObject>("getInterfaceAddresses"))
                            {
                                int count = addresses.Call<int>("size");
                                for (int i = 0; i < count; i++)
                                {
                                    using (AndroidJavaObject entry = addresses.Call<AndroidJavaObject>("get", i))
                                    using (AndroidJavaObject address = entry.Call<AndroidJavaObject>("getAddress"))
                                    using (AndroidJavaObject broadcast = entry.Call<AndroidJavaObject>("getBroadcast"))
                                    {
                                        string host = address?.Call<string>("getHostAddress");
                                        if (string.IsNullOrEmpty(host) || host.Contains(":") || broadcast == null) continue;
                                        result.Add(new LocalInterface(name, host, broadcast.Call<string>("getHostAddress")));
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (AndroidJavaException e)
            {
                Debug.LogWarning("[Net] Interface enumeration failed: " + e.Message);
            }
#endif
            return result;
        }
    }
}
