using System;
using Unity.Collections;
using Unity.Mathematics;

namespace LastGround.Gameplay.Zombies
{
    /// <summary>
    /// Spreads zombies close to a player over angular sectors so they encircle instead of queueing behind each other
    /// (TDD_01 §8.4). Over-full sectors hand their extra members to the nearest empty sectors. Allocation-free.
    /// </summary>
    public sealed class SurroundSlotSolver
    {
        readonly int _sectors;
        readonly int[] _count;
        readonly int[] _members;
        readonly int[] _memberSector;
        readonly float _sectorSize;

        public SurroundSlotSolver(int sectors, int capacity)
        {
            _sectors = sectors;
            _sectorSize = 2f * math.PI / sectors;
            _count = new int[sectors];
            _members = new int[capacity];
            _memberSector = new int[capacity];
        }

        /// <summary>Assigns SlotAngle (radians, world XZ) to every zombie within range of the target player.</summary>
        public void Solve(int player, float2 playerPosition, float range, NativeArray<float2> positions, NativeArray<byte> alive,
            NativeArray<byte> targets, NativeArray<float> slotAngle)
        {
            Array.Clear(_count, 0, _sectors);
            int n = 0;
            float range2 = range * range;
            for (int i = 0; i < positions.Length; i++)
            {
                if (alive[i] == 0 || targets[i] != player) continue;
                float2 offset = positions[i] - playerPosition;
                if (math.lengthsq(offset) > range2)
                {
                    // Far away: keep the approach direction so the ring forms where they arrive.
                    slotAngle[i] = math.atan2(offset.y, offset.x);
                    continue;
                }
                int sector = SectorOf(math.atan2(offset.y, offset.x));
                _members[n] = i;
                _memberSector[n] = sector;
                _count[sector]++;
                n++;
            }
            if (n == 0) return;

            int limit = (n + _sectors - 1) / _sectors + 1;
            for (int m = 0; m < n; m++)
            {
                int sector = _memberSector[m];
                if (_count[sector] > limit)
                {
                    int target = NearestUnderfilled(sector, limit);
                    if (target != sector)
                    {
                        _count[sector]--;
                        _count[target]++;
                        _memberSector[m] = target;
                    }
                }
                int zombie = _members[m];
                // Small per-zombie offset inside the sector so members of one sector do not share a point.
                float jitter = ((zombie * 2654435761u) & 1023u) / 1023f - 0.5f;
                slotAngle[zombie] = (_memberSector[m] + 0.5f + jitter * 0.6f) * _sectorSize - math.PI;
            }
        }

        int SectorOf(float angle)
        {
            int s = (int)math.floor((angle + math.PI) / _sectorSize);
            return math.clamp(s, 0, _sectors - 1);
        }

        int NearestUnderfilled(int from, int limit)
        {
            for (int step = 1; step <= _sectors / 2; step++)
            {
                int a = (from + step) % _sectors;
                if (_count[a] < limit - 1) return a;
                int b = (from - step + _sectors) % _sectors;
                if (_count[b] < limit - 1) return b;
            }
            return from;
        }
    }
}
