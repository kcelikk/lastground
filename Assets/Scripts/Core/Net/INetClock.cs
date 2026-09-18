namespace LastGround.Core.Net
{
    /// <summary>Host time estimate on this device (TDD_02 §15.4).</summary>
    public interface INetClock
    {
        /// <summary>Estimated current host time in seconds.</summary>
        double HostTime { get; }

        /// <summary>Time at which remote snapshots are rendered (HostTime minus interpolation delay).</summary>
        double RenderTime { get; }

        /// <summary>Smoothed round-trip time in seconds.</summary>
        double Rtt { get; }
    }
}
