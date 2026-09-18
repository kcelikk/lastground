using System;

namespace LastGround.Core.Pooling
{
    /// <summary>
    /// Fixed-capacity FIFO of structs. When full, <see cref="PushOverwrite"/> recycles the oldest entry —
    /// the policy for decorative data (corpses, decals, damage numbers).
    /// </summary>
    public sealed class RingBuffer<T> where T : struct
    {
        readonly T[] _items;
        int _head;
        int _count;

        public RingBuffer(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _items = new T[capacity];
        }

        public int Count => _count;
        public int Capacity => _items.Length;

        /// <summary>Oldest-first indexing.</summary>
        public ref T this[int index]
        {
            get
            {
                if ((uint)index >= (uint)_count) throw new ArgumentOutOfRangeException(nameof(index));
                return ref _items[(_head + index) % _items.Length];
            }
        }

        /// <summary>Adds an item; overwrites the oldest when full. Returns true if an item was overwritten.</summary>
        public bool PushOverwrite(in T item)
        {
            if (_count < _items.Length)
            {
                _items[(_head + _count) % _items.Length] = item;
                _count++;
                return false;
            }

            _items[_head] = item;
            _head = (_head + 1) % _items.Length;
            return true;
        }

        public bool TryPopOldest(out T item)
        {
            if (_count == 0)
            {
                item = default;
                return false;
            }

            item = _items[_head];
            _items[_head] = default;
            _head = (_head + 1) % _items.Length;
            _count--;
            return true;
        }

        public void Clear()
        {
            Array.Clear(_items, 0, _items.Length);
            _head = 0;
            _count = 0;
        }
    }
}
