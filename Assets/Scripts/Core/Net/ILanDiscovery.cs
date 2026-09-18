using System;

namespace LastGround.Core.Net
{
    /// <summary>
    /// LAN discovery (TDD_02 §18). Implementations: UDP broadcast + Android MulticastLock; Bonjour on iOS later.
    /// Runs only while lobby/browse screens are open; <see cref="Poll"/> is called every frame.
    /// </summary>
    public interface ILanDiscovery : IDisposable
    {
        bool IsAdvertising { get; }
        bool IsBrowsing { get; }

        void StartAdvertising(string sessionName, ushort gamePort, byte players, byte maxPlayers);
        void UpdateAdvertisement(byte players, byte maxPlayers, bool inRun);
        void StopAdvertising();

        void StartBrowsing();
        void StopBrowsing();

        void Poll(double now);

        event Action<DiscoveredHost> HostFound;
        event Action<string, ushort> HostLost;
    }
}
