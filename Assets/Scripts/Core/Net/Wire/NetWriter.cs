using System;
using System.Text;

namespace LastGround.Core.Net.Wire
{
    /// <summary>
    /// Growable little-endian byte writer with a bit-packing mode for snapshots (TDD_02 §15.8, §17.2).
    /// Reused per session; <see cref="Reset"/> keeps the buffer so steady-state writes do not allocate.
    /// </summary>
    public sealed class NetWriter
    {
        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        byte[] _buffer;
        int _position;
        ulong _bitScratch;
        int _bitCount;

        public NetWriter(int capacity = 1200)
        {
            _buffer = new byte[capacity];
        }

        public int Length => _position;
        public byte[] Buffer => _buffer;
        public ArraySegment<byte> Segment => new ArraySegment<byte>(_buffer, 0, _position);

        public void Reset()
        {
            _position = 0;
            _bitScratch = 0;
            _bitCount = 0;
        }

        public void WriteByte(byte value)
        {
            Ensure(1);
            _buffer[_position++] = value;
        }

        public void WriteBool(bool value) => WriteByte(value ? (byte)1 : (byte)0);

        public void WriteUShort(ushort value)
        {
            Ensure(2);
            _buffer[_position++] = (byte)value;
            _buffer[_position++] = (byte)(value >> 8);
        }

        public void WriteUInt(uint value)
        {
            Ensure(4);
            _buffer[_position++] = (byte)value;
            _buffer[_position++] = (byte)(value >> 8);
            _buffer[_position++] = (byte)(value >> 16);
            _buffer[_position++] = (byte)(value >> 24);
        }

        public void WriteShort(short value) => WriteUShort((ushort)value);

        public unsafe void WriteFloat(float value) => WriteUInt(*(uint*)&value);

        public void WriteDouble(double value)
        {
            ulong bits = (ulong)BitConverter.DoubleToInt64Bits(value);
            WriteUInt((uint)bits);
            WriteUInt((uint)(bits >> 32));
        }

        /// <summary>LEB128 variable-length unsigned integer (1 byte below 128).</summary>
        public void WriteVarUInt(uint value)
        {
            while (value >= 0x80)
            {
                WriteByte((byte)(value | 0x80));
                value >>= 7;
            }
            WriteByte((byte)value);
        }

        /// <summary>UTF-8 string with a varint byte-length prefix. Null is written as empty.</summary>
        public void WriteString(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                WriteVarUInt(0);
                return;
            }
            int max = Utf8.GetMaxByteCount(value.Length);
            Ensure(max + 5);
            int count = Utf8.GetBytes(value, 0, value.Length, _buffer, _position + 5);
            // Write the prefix, then move the bytes right behind it.
            int start = _position + 5;
            WriteVarUInt((uint)count);
            System.Buffer.BlockCopy(_buffer, start, _buffer, _position, count);
            _position += count;
        }

        public void WriteBytes(byte[] source, int offset, int count)
        {
            Ensure(count);
            System.Buffer.BlockCopy(source, offset, _buffer, _position, count);
            _position += count;
        }

        /// <summary>Appends the lowest <paramref name="bits"/> bits of value (LSB first). Call <see cref="FlushBits"/> after the last one.</summary>
        public void WriteBits(uint value, int bits)
        {
            _bitScratch |= ((ulong)value & ((1UL << bits) - 1UL)) << _bitCount;
            _bitCount += bits;
            while (_bitCount >= 8)
            {
                WriteByte((byte)_bitScratch);
                _bitScratch >>= 8;
                _bitCount -= 8;
            }
        }

        /// <summary>Writes any pending partial byte.</summary>
        public void FlushBits()
        {
            if (_bitCount > 0)
            {
                WriteByte((byte)_bitScratch);
                _bitScratch = 0;
                _bitCount = 0;
            }
        }

        void Ensure(int extra)
        {
            int needed = _position + extra;
            if (needed <= _buffer.Length) return;
            int size = _buffer.Length * 2;
            while (size < needed) size *= 2;
            Array.Resize(ref _buffer, size);
        }
    }
}
