namespace LastGround.Core.Net.Session
{
    /// <summary>
    /// Host: HostTime = local session time. Client: offset estimated from ping/pong,
    /// smoothed so snapshots do not jitter, snapped when the error is large.
    /// </summary>
    public sealed class NetClock : INetClock
    {
        public const double InterpolationDelay = 0.1;
        const double Smoothing = 0.1;
        const double SnapThreshold = 0.5;

        double _localNow;
        double _offset;

        public double HostTime => _localNow + _offset;
        public double RenderTime => HostTime - InterpolationDelay;
        public double Rtt { get; private set; }
        public bool IsSynchronized { get; private set; }

        public void SetLocalTime(double localNow)
        {
            _localNow = localNow;
        }

        /// <summary>Host: time 0 is "now".</summary>
        public void StartAsHost(double localNow)
        {
            _localNow = localNow;
            _offset = -localNow;
            Rtt = 0;
            IsSynchronized = true;
        }

        public void Reset()
        {
            _offset = 0;
            Rtt = 0;
            IsSynchronized = false;
        }

        /// <summary>Client: apply a pong. <paramref name="sentAt"/> and <paramref name="receivedAt"/> are local times.</summary>
        public void AddSample(double sentAt, double receivedAt, double hostTime)
        {
            double rtt = receivedAt - sentAt;
            if (rtt < 0) return;
            double offset = hostTime + rtt * 0.5 - receivedAt;

            if (!IsSynchronized || System.Math.Abs(offset - _offset) > SnapThreshold)
            {
                _offset = offset;
                Rtt = rtt;
                IsSynchronized = true;
                return;
            }
            _offset += (offset - _offset) * Smoothing;
            Rtt += (rtt - Rtt) * Smoothing;
        }
    }
}
