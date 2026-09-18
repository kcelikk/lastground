using System;
using LastGround.Core.Net;

namespace LastGround.Core.Events
{
    /// <summary>
    /// Allocation-free, single-writer, multi-reader ring buffer of struct events.
    /// Readers keep their own cursor; a slow reader loses the oldest events instead of blocking the writer
    /// and can see how many it missed via <see cref="EventReader{T}.Dropped"/>.
    /// </summary>
    public sealed class EventChannel<T> : IGameEventStream<T> where T : struct
    {
        readonly T[] _buffer;
        readonly int _mask;
        ulong _written;

        /// <param name="capacity">Rounded up to a power of two.</param>
        public EventChannel(int capacity = 256)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            int size = 1;
            while (size < capacity) size <<= 1;
            _buffer = new T[size];
            _mask = size - 1;
        }

        public int Capacity => _buffer.Length;

        /// <summary>Total events ever published.</summary>
        public ulong Written => _written;

        public void Publish(in T value)
        {
            _buffer[(int)(_written & (ulong)_mask)] = value;
            _written++;
        }

        public EventReader<T> CreateReader()
        {
            return new EventReader<T>(_written);
        }

        public bool TryRead(ref EventReader<T> reader, out T value)
        {
            if (reader.Cursor >= _written)
            {
                value = default;
                return false;
            }

            ulong oldest = _written > (ulong)_buffer.Length ? _written - (ulong)_buffer.Length : 0UL;
            if (reader.Cursor < oldest)
            {
                reader.Dropped += oldest - reader.Cursor;
                reader.Cursor = oldest;
            }

            value = _buffer[(int)(reader.Cursor & (ulong)_mask)];
            reader.Cursor++;
            return true;
        }
    }
}
