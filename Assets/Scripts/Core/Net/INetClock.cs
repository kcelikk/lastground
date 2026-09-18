namespace LastGround.Core.Net
{
    /// <summary>Host time estimate on this device (TDD_02 §15.4).</summary>
    public interface INetClock
    {
        /// <summary>Estimated current host time in seconds since the session started on the host.</summary>
        double HostTime { get; }

        /// <summary>Time at which remote snapshots are rendered (HostTime minus interpolation delay).</summary>
        double RenderTime { get; }

        /// <summary>Smoothed round-trip time in seconds (0 on the host).</summary>
        double Rtt { get; }

        /// <summary>True once the first clock sample arrived (always true on the host).</summary>
        bool IsSynchronized { get; }
    }
}
