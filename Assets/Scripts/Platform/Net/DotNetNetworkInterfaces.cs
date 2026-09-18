using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace LastGround.Platform.Net
{
    /// <summary>Editor / desktop implementation via System.Net. Unreliable on Android 11+ (TDD_02 §18.2).</summary>
    public sealed class DotNetNetworkInterfaces : INetworkInterfaces
    {
        public IReadOnlyList<LocalInterface> GetIPv4()
        {
            var result = new List<LocalInterface>();
            NetworkInterface[] interfaces;
            try
            {
                interfaces = NetworkInterface.GetAllNetworkInterfaces();
            }
            catch (Exception)
            {
                return result;
            }

            foreach (NetworkInterface nic in interfaces)
            {
                if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    continue;
                foreach (UnicastIPAddressInformation unicast in nic.GetIPProperties().UnicastAddresses)
                {
                    if (unicast.Address.AddressFamily != AddressFamily.InterNetwork || unicast.IPv4Mask == null) continue;
                    byte[] ip = unicast.Address.GetAddressBytes();
                    byte[] mask = unicast.IPv4Mask.GetAddressBytes();
                    var broadcast = new byte[4];
                    for (int i = 0; i < 4; i++) broadcast[i] = (byte)(ip[i] | ~mask[i]);
                    result.Add(new LocalInterface(nic.Name, unicast.Address.ToString(), new IPAddress(broadcast).ToString()));
                }
            }
            return result;
        }
    }
}
