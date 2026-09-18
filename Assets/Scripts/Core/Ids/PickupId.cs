using System;

namespace LastGround.Core.Ids
{
    /// <summary>Host-assigned pickup id (TDD_01 §13.2).</summary>
    public readonly struct PickupId : IEquatable<PickupId>
    {
        public readonly ushort Value;

        public PickupId(ushort value)
        {
            Value = value;
        }

        public bool Equals(PickupId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is PickupId other && Equals(other);
        public override int GetHashCode() => Value;
        public static bool operator ==(PickupId a, PickupId b) => a.Value == b.Value;
        public static bool operator !=(PickupId a, PickupId b) => a.Value != b.Value;
        public override string ToString() => "K" + Value;
    }
}
