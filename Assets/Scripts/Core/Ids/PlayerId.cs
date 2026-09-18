using System;

namespace LastGround.Core.Ids
{
    /// <summary>Session-local player slot (0..3). Stable for the whole run; never a GameObject reference.</summary>
    public readonly struct PlayerId : IEquatable<PlayerId>
    {
        public const byte InvalidValue = byte.MaxValue;
        public static readonly PlayerId Invalid = new PlayerId(InvalidValue);

        public readonly byte Value;

        public PlayerId(byte value)
        {
            Value = value;
        }

        public bool IsValid => Value != InvalidValue;

        /// <summary>Bit for this player in an ownerMask (instanced loot, TDD_01 §13.1).</summary>
        public byte MaskBit => IsValid ? (byte)(1 << Value) : (byte)0;

        public bool Equals(PlayerId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is PlayerId other && Equals(other);
        public override int GetHashCode() => Value;
        public static bool operator ==(PlayerId a, PlayerId b) => a.Value == b.Value;
        public static bool operator !=(PlayerId a, PlayerId b) => a.Value != b.Value;
        public override string ToString() => IsValid ? "P" + Value : "P-";
    }
}
