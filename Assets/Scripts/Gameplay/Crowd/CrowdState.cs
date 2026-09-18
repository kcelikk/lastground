using LastGround.Core.Events;

namespace LastGround.Gameplay.Crowd
{
    /// <summary>
    /// Authoritative crowd data on the host as structure-of-arrays (TDD_01 §8.1). A slot's generation increases
    /// every time it is reused, so stale handles and replicas can be detected (TDD_02 §17.1).
    /// </summary>
    public sealed class CrowdState : ICrowdRenderSource
    {
        public readonly bool[] AliveSlots;
        public readonly float[] PosX;
        public readonly float[] PosZ;
        public readonly float[] Heading;
        public readonly byte[] Generation;
        public readonly byte[] Type;
        public readonly byte[] Anim;
        public readonly byte[] Flags;

        /// <summary>Deaths in order; presentation reads them with its own cursor.</summary>
        public readonly EventChannel<CrowdDeath> Deaths = new EventChannel<CrowdDeath>(256);

        public CrowdState(int capacity)
        {
            Capacity = capacity;
            AliveSlots = new bool[capacity];
            PosX = new float[capacity];
            PosZ = new float[capacity];
            Heading = new float[capacity];
            Generation = new byte[capacity];
            Type = new byte[capacity];
            Anim = new byte[capacity];
            Flags = new byte[capacity];
        }

        public int Capacity { get; }
        public int ActiveCount { get; private set; }
        public bool[] Alive => AliveSlots;
        public float[] X => PosX;
        public float[] Z => PosZ;
        public float[] Yaw => Heading;
        public byte[] AnimState => Anim;

        /// <summary>Activates a free slot; returns -1 when full.</summary>
        public int Spawn(byte type, float x, float z, float heading)
        {
            for (int i = 0; i < Capacity; i++)
            {
                if (AliveSlots[i]) continue;
                AliveSlots[i] = true;
                Generation[i]++;
                Type[i] = type;
                PosX[i] = x;
                PosZ[i] = z;
                Heading[i] = heading;
                Anim[i] = 0;
                Flags[i] = 0;
                ActiveCount++;
                return i;
            }
            return -1;
        }

        /// <summary>Removes a slot. <paramref name="died"/> publishes a death for corpses and blood.</summary>
        public void Despawn(int slot, bool died = false)
        {
            if (!AliveSlots[slot]) return;
            AliveSlots[slot] = false;
            ActiveCount--;
            if (died) Deaths.Publish(new CrowdDeath { Slot = slot, X = PosX[slot], Z = PosZ[slot], Yaw = Heading[slot] });
        }
    }
}
