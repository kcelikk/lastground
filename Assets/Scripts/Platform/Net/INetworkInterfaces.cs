using System.Collections.Generic;

namespace LastGround.Platform.Net
{
    /// <summary>Enumerates active IPv4 interfaces (Wi-Fi, hotspot, Ethernet), excluding loopback.</summary>
    public interface INetworkInterfaces
    {
        IReadOnlyList<LocalInterface> GetIPv4();
    }
}
