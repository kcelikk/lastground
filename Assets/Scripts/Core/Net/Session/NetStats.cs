using LastGround.Core.Net.Protocol;

namespace LastGround.Core.Net.Session
{
    /// <summary>Allocation-free counters; rolled into "last second" values once per second.</summary>
    public sealed class NetStats : INetStats
    {
        readonly int[] _inBytes = new int[NetMsgId.Count];
        readonly int[] _outBytes = new int[NetMsgId.Count];
        readonly int[] _inLast = new int[NetMsgId.Count];
        readonly int[] _outLast = new int[NetMsgId.Count];
        int _inMessages;
        int _outMessages;
        double _windowStart = -1;

        public float InBytesPerSecond { get; private set; }
        public float OutBytesPerSecond { get; private set; }
        public int InMessagesPerSecond { get; private set; }
        public int OutMessagesPerSecond { get; private set; }

        public int InBytesFor(byte messageId) => _inLast[messageId];
        public int OutBytesFor(byte messageId) => _outLast[messageId];

        public void CountIn(byte messageId, int bytes)
        {
            _inBytes[messageId] += bytes;
            _inMessages++;
        }

        public void CountOut(byte messageId, int bytes)
        {
            _outBytes[messageId] += bytes;
            _outMessages++;
        }

        public void Tick(double now)
        {
            if (_windowStart < 0)
            {
                _windowStart = now;
                return;
            }
            double elapsed = now - _windowStart;
            if (elapsed < 1.0) return;

            long totalIn = 0, totalOut = 0;
            for (int i = 0; i < _inBytes.Length; i++)
            {
                _inLast[i] = _inBytes[i];
                _outLast[i] = _outBytes[i];
                totalIn += _inBytes[i];
                totalOut += _outBytes[i];
                _inBytes[i] = 0;
                _outBytes[i] = 0;
            }
            InBytesPerSecond = (float)(totalIn / elapsed);
            OutBytesPerSecond = (float)(totalOut / elapsed);
            InMessagesPerSecond = (int)(_inMessages / elapsed);
            OutMessagesPerSecond = (int)(_outMessages / elapsed);
            _inMessages = 0;
            _outMessages = 0;
            _windowStart = now;
        }
    }
}
