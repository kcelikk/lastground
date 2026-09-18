using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace LastGround.Gameplay.Zombies
{
    /// <summary>Counting-sort spatial hash of zombie positions (2 m cells), TDD_01 §8.2 step 2.</summary>
    [BurstCompile]
    public struct SpatialGridBuildJob : IJob
    {
        [ReadOnly] public NativeArray<float2> Position;
        [ReadOnly] public NativeArray<byte> Alive;
        public NativeArray<int> CellStart;
        public NativeArray<int> CellCount;
        public NativeArray<int> Sorted;
        public NativeArray<int> CellOf;
        public float2 Origin;
        public float InvCellSize;
        public int GridWidth;
        public int GridHeight;

        public void Execute()
        {
            for (int c = 0; c < CellCount.Length; c++) CellCount[c] = 0;
            for (int i = 0; i < Position.Length; i++)
            {
                if (Alive[i] == 0)
                {
                    CellOf[i] = -1;
                    continue;
                }
                int2 cell = math.clamp((int2)math.floor((Position[i] - Origin) * InvCellSize), 0, new int2(GridWidth - 1, GridHeight - 1));
                int index = cell.y * GridWidth + cell.x;
                CellOf[i] = index;
                CellCount[index]++;
            }
            int running = 0;
            for (int c = 0; c < CellCount.Length; c++)
            {
                CellStart[c] = running;
                running += CellCount[c];
                CellCount[c] = 0;
            }
            for (int i = 0; i < Position.Length; i++)
            {
                int index = CellOf[i];
                if (index < 0) continue;
                Sorted[CellStart[index] + CellCount[index]] = i;
                CellCount[index]++;
            }
        }
    }

    /// <summary>
    /// Per-zombie steering (TDD_01 §8.2 steps 4–6): flow field far away, surround slot near the target, separation
    /// from up to 8 neighbours, keep-out around players, wall sliding on the walkable grid, AI LOD step skipping.
    /// Staggered zombies (knockback, respawn push) do not steer; their velocity decays by friction instead.
    /// Zombies without a target idle but still separate and slide. Writes into separate output arrays so
    /// parallel iterations never race.
    /// </summary>
    [BurstCompile]
    public struct ZombieSteeringJob : IJobParallelFor
    {
        public const byte StateIdle = 0;
        public const byte StateWalk = 1;
        public const byte StateAttack = 3;
        public const byte NoTarget = 255;
        const int MaxNeighbours = 8;
        const float MinPlayerDistance = 0.7f;
        const float StaggerFriction = 2f;

        [ReadOnly] public NativeArray<float2> Position;
        [ReadOnly] public NativeArray<float2> Velocity;
        [ReadOnly] public NativeArray<float> Heading;
        [ReadOnly] public NativeArray<float> Speed;
        [ReadOnly] public NativeArray<float> SlotAngle;
        [ReadOnly] public NativeArray<float> SlotRing;
        [ReadOnly] public NativeArray<byte> Target;
        [ReadOnly] public NativeArray<byte> Alive;
        [ReadOnly] public NativeArray<float> Stagger;

        [ReadOnly] public NativeArray<float2> PlayerPosition;
        [ReadOnly] public NativeArray<byte> PlayerActive;

        [ReadOnly] public NativeArray<int> CellStart;
        [ReadOnly] public NativeArray<int> CellCount;
        [ReadOnly] public NativeArray<int> Sorted;
        [ReadOnly] public NativeArray<int> CellOf;
        public int GridWidth;
        public int GridHeight;

        [ReadOnly] public NativeArray<byte> Walkable;
        [ReadOnly] public NativeArray<float2> FlowDirections;
        public int NavWidth;
        public int NavHeight;
        public float2 NavOrigin;
        public float NavCellSize;

        public float Dt;
        public uint Tick;
        public float Acceleration;
        public float Radius;
        public float SeparationStrength;
        public float AttackRange;
        public float SurroundRange;
        public float TierA;
        public float TierB;

        [WriteOnly] public NativeArray<float2> OutPosition;
        [WriteOnly] public NativeArray<float2> OutVelocity;
        [WriteOnly] public NativeArray<float> OutHeading;
        [WriteOnly] public NativeArray<byte> OutState;

        public void Execute(int i)
        {
            float2 p = Position[i];
            float2 v = Velocity[i];
            OutPosition[i] = p;
            OutVelocity[i] = v;
            OutHeading[i] = Heading[i];
            OutState[i] = StateIdle;
            if (Alive[i] == 0) return;

            byte t = Target[i];
            bool hasTarget = t != NoTarget && PlayerActive[t] != 0;
            bool staggered = Stagger[i] > 0f;
            if (!hasTarget && !staggered && math.lengthsq(v) < 1e-4f) return;

            float2 tp = hasTarget ? PlayerPosition[t] : p;
            float2 toTarget = tp - p;
            float distance = math.length(toTarget);

            // AI LOD (TDD_01 §8.2): near every tick, mid every 2nd, far every 6th with a larger step.
            int step = staggered ? 1 : !hasTarget ? 6 : distance < TierA ? 1 : distance < TierB ? 2 : 6;
            if (((uint)i + Tick) % (uint)step != 0)
            {
                OutState[i] = hasTarget ? StateWalk : StateIdle;
                return;
            }
            float dt = Dt * step;

            float2 desired;
            byte state = StateWalk;
            if (staggered)
            {
                desired = float2.zero;
            }
            else if (!hasTarget)
            {
                desired = float2.zero;
                state = StateIdle;
            }
            else if (distance < AttackRange)
            {
                desired = float2.zero;
                state = StateAttack;
            }
            else if (distance < SurroundRange)
            {
                // Layered surround slot from SurroundSlotSolver: inner ring attacks, outer rings queue (TDD_01 §8.4).
                float ring = SlotRing[i];
                float angle = SlotAngle[i];
                float2 slot = tp + new float2(math.cos(angle), math.sin(angle)) * ring;
                float2 toSlot = slot - p;
                float slotDistance = math.length(toSlot);
                // Arrive: slow down near the slot and hold it; never keep pushing towards the player.
                desired = slotDistance > 0.15f ? toSlot / slotDistance * Speed[i] * math.min(1f, slotDistance / 0.6f) : float2.zero;
            }
            else
            {
                float2 flow = SampleFlow(t, p);
                desired = (math.lengthsq(flow) > 0f ? flow : math.normalizesafe(toTarget)) * Speed[i];
            }

            desired += Separation(i, p) * SeparationStrength;

            // Never stand inside a player; players are not blocked by zombies (TDD_01 §8.4).
            for (int k = 0; k < PlayerPosition.Length; k++)
            {
                if (PlayerActive[k] == 0) continue;
                float2 away = p - PlayerPosition[k];
                float d = math.length(away);
                if (d < 0.9f && d > 1e-4f) desired += away / d * (0.9f - d) * 8f;
            }

            v = math.lerp(v, desired, math.saturate((staggered ? StaggerFriction : Acceleration) * dt));
            float2 next = p + v * dt;

            // Hard constraint: crowd pressure must never push a zombie into a player.
            for (int k = 0; k < PlayerPosition.Length; k++)
            {
                if (PlayerActive[k] == 0) continue;
                float2 away = next - PlayerPosition[k];
                float d = math.length(away);
                if (d < MinPlayerDistance)
                    next = PlayerPosition[k] + (d > 1e-4f ? away / d : new float2(1f, 0f)) * MinPlayerDistance;
            }
            if (!IsWalkable(next))
            {
                float2 slideX = new float2(next.x, p.y);
                float2 slideZ = new float2(p.x, next.y);
                if (IsWalkable(slideX)) { next = slideX; v.y = 0f; }
                else if (IsWalkable(slideZ)) { next = slideZ; v.x = 0f; }
                else { next = p; v = float2.zero; }
            }

            float heading = Heading[i];
            float2 face = math.lengthsq(v) > 0.04f ? v : toTarget;
            if (math.lengthsq(face) > 1e-4f)
            {
                float targetHeading = math.degrees(math.atan2(face.x, face.y));
                float delta = ((targetHeading - heading + 540f) % 360f) - 180f;
                heading += delta * math.saturate(10f * dt);
            }

            OutPosition[i] = next;
            OutVelocity[i] = v;
            OutHeading[i] = heading;
            OutState[i] = state;
        }

        float2 Separation(int self, float2 p)
        {
            int cell = CellOf[self];
            if (cell < 0) return float2.zero;
            int cx = cell % GridWidth, cy = cell / GridWidth;
            float2 push = float2.zero;
            int found = 0;
            float range = Radius * 2f;
            for (int dy = -1; dy <= 1 && found < MaxNeighbours; dy++)
            {
                int y = cy + dy;
                if (y < 0 || y >= GridHeight) continue;
                for (int dx = -1; dx <= 1 && found < MaxNeighbours; dx++)
                {
                    int x = cx + dx;
                    if (x < 0 || x >= GridWidth) continue;
                    int c = y * GridWidth + x;
                    int start = CellStart[c], end = start + CellCount[c];
                    for (int s = start; s < end && found < MaxNeighbours; s++)
                    {
                        int other = Sorted[s];
                        if (other == self) continue;
                        float2 away = p - Position[other];
                        float d = math.length(away);
                        if (d >= range) continue;
                        found++;
                        push += d > 1e-4f ? away / d * (range - d) / range : new float2(((self * 7919) & 1) * 2 - 1, 0.5f);
                    }
                }
            }
            return push;
        }

        float2 SampleFlow(int target, float2 p)
        {
            int2 c = (int2)math.floor((p - NavOrigin) / NavCellSize);
            if (c.x < 0 || c.y < 0 || c.x >= NavWidth || c.y >= NavHeight) return float2.zero;
            return FlowDirections[target * NavWidth * NavHeight + c.y * NavWidth + c.x];
        }

        bool IsWalkable(float2 p)
        {
            int2 c = (int2)math.floor((p - NavOrigin) / NavCellSize);
            return c.x >= 0 && c.y >= 0 && c.x < NavWidth && c.y < NavHeight && Walkable[c.y * NavWidth + c.x] != 0;
        }
    }
}
