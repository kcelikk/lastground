using System;

namespace LastGround.Core.Ids
{
    /// <summary>Stable id of a scene-placed interactable, baked in the editor (TDD_01 §12.3).</summary>
    public readonly struct PropId : IEquatable<PropId>
    {
        public readonly ushort Value;

        public PropId(ushort value)
        {
            Value = value;
        }

        public bool Equals(PropId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is PropId other && Equals(other);
        public override int GetHashCode() => Value;
        public static bool operator ==(PropId a, PropId b) => a.Value == b.Value;
        public static bool operator !=(PropId a, PropId b) => a.Value != b.Value;
        public override string ToString() => "R" + Value;
    }
}
