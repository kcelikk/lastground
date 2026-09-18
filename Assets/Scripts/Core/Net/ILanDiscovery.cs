using System;

namespace LastGround.Core.Net
{
    /// <summary>
    /// LAN discovery (TDD_02 §18). Platform implementations: UDP broadcast + Android MulticastLock; Bonjour on iOS later.
    /// </summary>
    public interface ILanDiscovery
    {
        bool IsAdvertising { get; }
        bool IsBrowsing { get; }

        void StartAdvertising(string sessionName, ushort gamePort, byte players, byte maxPlayers);
        void UpdateAdvertisement(byte players, byte maxPlayers);
        void StopAdvertising();

        void StartBrowsing();
        void StopBrowsing();

        event Action<DiscoveredHost> HostFound;
        event Action<string, ushort> HostLost;
    }
}
