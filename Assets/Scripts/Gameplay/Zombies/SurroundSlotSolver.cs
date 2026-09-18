using System;
using Unity.Collections;
using Unity.Mathematics;

namespace LastGround.Gameplay.Zombies
{
    /// <summary>
    /// Layered encirclement (TDD_01 §8.4). Zombies near a player get a slot = (angle, ring radius). Rings are filled
    /// from the inside out, nearest zombies first; a ring's sector holds only as many zombies as fit on its arc,
    /// so the rest queue in outer rings instead of piling onto the player. Preferred sector = approach direction;
    /// a full sector overflows to its neighbours before moving outwards. Allocation-free.
    /// </summary>
    public sealed class SurroundSlotSolver
    {
        const int MaxLayers = 24;
        const int NeighbourReach = 2;

        readonly int _sectors;
        readonly float _sectorSize;
        readonly float _innerRadius;
        readonly float _spacing;
        readonly int[] _occupancy;
        readonly int[] _capacity;
        readonly int[] _members;
        readonly float[] _memberDistance;

        public SurroundSlotSolver(int sectors, int capacity, float innerRadius = 1.15f, float spacing = 0.85f)
        {
            _sectors = sectors;
            _sectorSize = 2f * math.PI / sectors;
            _innerRadius = innerRadius;
            _spacing = spacing;
            _occupancy = new int[sectors * MaxLayers];
            _capacity = new int[MaxLayers];
            for (int layer = 0; layer < MaxLayers; layer++)
            {
                float radius = RingRadius(layer);
                _capacity[layer] = math.max(1, (int)math.floor(radius * _sectorSize / spacing));
            }
            _members = new int[capacity];
            _memberDistance = new float[capacity];
        }

        public float RingRadius(int layer) => _innerRadius + layer * _spacing;

        /// <summary>
        /// Assigns SlotAngle (radians, world XZ) and SlotRing (metres) to every zombie targeting this player within
        /// <paramref name="range"/>. Zombies further away keep their approach angle and the outermost ring.
        /// </summary>
        public void Solve(int player, float2 playerPosition, float range, NativeArray<float2> positions, NativeArray<byte> alive,
            NativeArray<byte> targets, NativeArray<float> slotAngle, NativeArray<float> slotRing)
        {
            Array.Clear(_occupancy, 0, _occupancy.Length);
            int n = 0;
            float range2 = range * range;
            for (int i = 0; i < positions.Length; i++)
            {
                if (alive[i] == 0 || targets[i] != player) continue;
                float2 offset = positions[i] - playerPosition;
                float d2 = math.lengthsq(offset);
                if (d2 > range2)
                {
                    slotAngle[i] = math.atan2(offset.y, offset.x);
                    slotRing[i] = RingRadius(MaxLayers - 1);
                    continue;
                }
                _members[n] = i;
                _memberDistance[n] = d2;
                n++;
            }
            if (n == 0) return;

            // Nearest first: they take the inner rings.
            Array.Sort(_memberDistance, _members, 0, n);
            for (int m = 0; m < n; m++)
            {
                int zombie = _members[m];
                float2 offset = positions[zombie] - playerPosition;
                int preferred = SectorOf(math.atan2(offset.y, offset.x));
                Assign(zombie, preferred, slotAngle, slotRing);
            }
        }

        void Assign(int zombie, int preferred, NativeArray<float> slotAngle, NativeArray<float> slotRing)
        {
            for (int layer = 0; layer < MaxLayers; layer++)
            {
                for (int k = 0; k <= NeighbourReach * 2; k++)
                {
                    int offset = (k + 1) / 2 * (k % 2 == 1 ? 1 : -1);
                    int sector = (preferred + offset + _sectors) % _sectors;
                    int cell = sector * MaxLayers + layer;
                    int used = _occupancy[cell];
                    if (used >= _capacity[layer]) continue;
                    _occupancy[cell] = used + 1;
                    slotAngle[zombie] = (sector + (used + 0.5f) / _capacity[layer]) * _sectorSize - math.PI;
                    slotRing[zombie] = RingRadius(layer);
                    return;
                }
            }
            slotRing[zombie] = RingRadius(MaxLayers - 1);
        }

        int SectorOf(float angle)
        {
            int s = (int)math.floor((angle + math.PI) / _sectorSize);
            return math.clamp(s, 0, _sectors - 1);
        }
    }
}
