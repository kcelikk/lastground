using LastGround.Core.Events;
using LastGround.Data.Map;
using Unity.Mathematics;

namespace LastGround.Gameplay.Interactables
{
    /// <summary>A barrel or fuel tank blew up (presentation hides it; host already resolved the blast).</summary>
    public struct InteractableChanged
    {
        public int Id;
        public bool Intact;
    }

    /// <summary>
    /// Every device's view of the map interactables (TDD_01 §12.3): kind and position from the map (index = network id,
    /// no NetworkObject), whether each barrel/tank is intact, and each medical station's charge. The host changes it
    /// through <see cref="InteractableSystem"/>; clients through replication.
    /// </summary>
    public sealed class InteractableTable
    {
        public readonly InteractableKind[] Kind;
        public readonly float2[] Position;
        public readonly bool[] Intact;
        /// <summary>Medical station charge 0..1 (others: unused).</summary>
        public readonly float[] Charge;
        public readonly EventChannel<InteractableChanged> Changed = new EventChannel<InteractableChanged>(32);
        public int Version;

        public InteractableTable(MapDefinition map)
        {
            int count = map != null && map.Interactables != null ? map.Interactables.Length : 0;
            Kind = new InteractableKind[count];
            Position = new float2[count];
            Intact = new bool[count];
            Charge = new float[count];
            for (int i = 0; i < count; i++)
            {
                Kind[i] = map.Interactables[i].Kind;
                Position[i] = new float2(map.Interactables[i].Position.x, map.Interactables[i].Position.y);
                Intact[i] = true;
                Charge[i] = 1f;
            }
        }

        public int Count => Kind.Length;

        public bool IsExplosive(int id) => Kind[id] == InteractableKind.ExplosiveBarrel || Kind[id] == InteractableKind.FuelTank;

        public void SetIntact(int id, bool intact)
        {
            if ((uint)id >= (uint)Count || Intact[id] == intact) return;
            Intact[id] = intact;
            Version++;
            Changed.Publish(new InteractableChanged { Id = id, Intact = intact });
        }

        public void SetCharge(int id, float charge)
        {
            if ((uint)id >= (uint)Count) return;
            // Only visible steps count as changes (replication stays quiet while the charge creeps).
            if (math.abs(Charge[id] - charge) < 0.02f && charge > 0f && charge < 1f) return;
            Charge[id] = charge;
            Version++;
        }
    }
}
