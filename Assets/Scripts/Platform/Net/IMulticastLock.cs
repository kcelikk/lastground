namespace LastGround.Platform.Net
{
    /// <summary>
    /// Keeps the Wi-Fi driver delivering broadcast packets while discovery runs (Android WifiManager.MulticastLock).
    /// Acquire only on discovery screens; release afterwards to save battery (TDD_02 §18.2).
    /// </summary>
    public interface IMulticastLock
    {
        bool IsHeld { get; }
        void Acquire();
        void Release();
    }
}
