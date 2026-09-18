using LastGround.Core.Pooling;

namespace LastGround.Rendering.Crowd
{
    /// <summary>
    /// Client-local corpses (TDD_02 §21.6): a capped ring buffer; the oldest corpse is recycled when full.
    /// A corpse plays the death clip, holds the last frame, then sinks 0.5 m and disappears.
    /// </summary>
    public sealed class CorpseBuffer
    {
        public struct Corpse
        {
            public int Slot;
            public float X;
            public float Z;
            public float Yaw;
            public float DiedAt;
        }

        readonly RingBuffer<Corpse> _items;

        public CorpseBuffer(int capacity)
        {
            _items = new RingBuffer<Corpse>(capacity < 1 ? 1 : capacity);
            Enabled = capacity > 0;
        }

        public bool Enabled { get; }
        public int Count => _items.Count;

        public void Add(int slot, float x, float z, float yaw, float time)
        {
            if (Enabled) _items.PushOverwrite(new Corpse { Slot = slot, X = x, Z = z, Yaw = yaw, DiedAt = time });
        }

        public ref Corpse this[int index] => ref _items[index];

        /// <summary>Drops corpses older than <paramref name="maxAge"/> (they are ordered oldest first).</summary>
        public void Expire(float now, float maxAge)
        {
            while (_items.Count > 0 && now - _items[0].DiedAt > maxAge)
                _items.TryPopOldest(out _);
        }
    }
}
