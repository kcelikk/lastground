using System;

namespace LastGround.Core.Ids
{
    /// <summary>
    /// Zombie identity = SoA slot + generation (TDD_02 §17.1). A stale handle (slot reused) fails the generation check.
    /// </summary>
    public readonly struct ZombieHandle : IEquatable<ZombieHandle>
    {
        public readonly ushort Slot;
        public readonly byte Generation;

        public ZombieHandle(ushort slot, byte generation)
        {
            Slot = slot;
            Generation = generation;
        }

        public bool Equals(ZombieHandle other) => Slot == other.Slot && Generation == other.Generation;
        public override bool Equals(object obj) => obj is ZombieHandle other && Equals(other);
        public override int GetHashCode() => (Slot << 8) | Generation;
        public static bool operator ==(ZombieHandle a, ZombieHandle b) => a.Equals(b);
        public static bool operator !=(ZombieHandle a, ZombieHandle b) => !a.Equals(b);
        public override string ToString() => "Z" + Slot + ":" + Generation;
    }
}
