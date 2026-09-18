using System;
using System.Text;

namespace LastGround.Core.Net.Wire
{
    /// <summary>
    /// Reader over a received segment. Out-of-range reads set <see cref="Failed"/> and return zeros instead of
    /// throwing, so a malformed packet is dropped by the caller rather than crashing the session.
    /// </summary>
    public struct NetReader
    {
        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, false);

        readonly byte[] _buffer;
        readonly int _end;
        int _position;
        ulong _bitScratch;
        int _bitCount;

        public NetReader(ArraySegment<byte> segment)
        {
            _buffer = segment.Array;
            _position = segment.Offset;
            _end = segment.Offset + segment.Count;
            _bitScratch = 0;
            _bitCount = 0;
            Failed = false;
        }

        public bool Failed { get; private set; }
        public int Remaining => _end - _position;

        public byte ReadByte()
        {
            if (_position + 1 > _end) return Fail();
            return _buffer[_position++];
        }

        public bool ReadBool() => ReadByte() != 0;

        public ushort ReadUShort()
        {
            if (_position + 2 > _end) return Fail();
            ushort value = (ushort)(_buffer[_position] | (_buffer[_position + 1] << 8));
            _position += 2;
            return value;
        }

        public short ReadShort() => (short)ReadUShort();

        public uint ReadUInt()
        {
            if (_position + 4 > _end) return Fail();
            uint value = (uint)(_buffer[_position] | (_buffer[_position + 1] << 8) |
                                (_buffer[_position + 2] << 16) | (_buffer[_position + 3] << 24));
            _position += 4;
            return value;
        }

        public unsafe float ReadFloat()
        {
            uint bits = ReadUInt();
            return *(float*)&bits;
        }

        public double ReadDouble()
        {
            ulong low = ReadUInt();
            ulong high = ReadUInt();
            return BitConverter.Int64BitsToDouble((long)(low | (high << 32)));
        }

        public uint ReadVarUInt()
        {
            uint result = 0;
            for (int shift = 0; shift < 35; shift += 7)
            {
                byte b = ReadByte();
                if (Failed) return 0;
                result |= (uint)(b & 0x7F) << shift;
                if ((b & 0x80) == 0) return result;
            }
            Fail();
            return 0;
        }

        public string ReadString()
        {
            uint count = ReadVarUInt();
            if (count == 0 || Failed) return string.Empty;
            if (_position + count > _end)
            {
                Fail();
                return string.Empty;
            }
            string value = Utf8.GetString(_buffer, _position, (int)count);
            _position += (int)count;
            return value;
        }

        public uint ReadBits(int bits)
        {
            while (_bitCount < bits)
            {
                byte b = ReadByte();
                if (Failed) return 0;
                _bitScratch |= (ulong)b << _bitCount;
                _bitCount += 8;
            }
            uint value = (uint)(_bitScratch & ((1UL << bits) - 1UL));
            _bitScratch >>= bits;
            _bitCount -= bits;
            return value;
        }

        /// <summary>Discards leftover bits of the current byte (pairs with NetWriter.FlushBits).</summary>
        public void AlignBits()
        {
            _bitScratch = 0;
            _bitCount = 0;
        }

        byte Fail()
        {
            Failed = true;
            _position = _end;
            return 0;
        }
    }
}
