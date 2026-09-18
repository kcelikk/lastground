using System.Collections.Generic;

namespace LastGround.Platform.Net
{
    /// <summary>Picks platform implementations and the address to show on the host screen.</summary>
    public static class PlatformNet
    {
        public static INetworkInterfaces CreateInterfaces()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return new AndroidNetworkInterfaces();
#else
            return new DotNetNetworkInterfaces();
#endif
        }

        public static IMulticastLock CreateMulticastLock() => new AndroidMulticastLock();

        /// <summary>Wi-Fi first, then hotspot interfaces (wlan0 / ap0 / swlan0), then anything else.</summary>
        public static string PreferredAddress(IReadOnlyList<LocalInterface> interfaces)
        {
            string fallback = null;
            for (int i = 0; i < interfaces.Count; i++)
            {
                string name = interfaces[i].Name ?? string.Empty;
                if (name.StartsWith("wlan", System.StringComparison.Ordinal)) return interfaces[i].Address;
                if (fallback == null || name.StartsWith("ap", System.StringComparison.Ordinal) ||
                    name.StartsWith("swlan", System.StringComparison.Ordinal))
                    fallback = interfaces[i].Address;
            }
            return fallback;
        }
    }
}
