using LastGround.Gameplay.Crowd;
using LastGround.Gameplay.Navigation;
using Unity.Mathematics;

namespace LastGround.Gameplay.Combat
{
    /// <summary>
    /// Bullet vs crowd (TDD_01 §5.1): ray against zombie circles, no physics colliders. Walls (NavGrid) end the ray.
    /// Brute force over the crowd's slots: at 300–512 slots and ≤ 40 shots/s this is a few µs, so the spatial grid
    /// is not needed yet. Allocation-free; results go into caller-owned arrays, nearest first. Slots flagged in
    /// <c>ignore</c> are transparent (zombies the shooter already expects to be dead).
    /// </summary>
    public static class HitQuery
    {
        /// <summary>
        /// Casts from <paramref name="origin"/> along the unit vector <paramref name="dir"/>. Returns the number of
        /// zombies hit (≤ maxHits); <paramref name="endDistance"/> is where the tracer stops (wall, range, or the
        /// last zombie the bullet could not pass).
        /// </summary>
        /// <param name="typeExtraRadius">Extra body radius per zombie type (Tank, boss); null = same for all.</param>
        public static int Cast(ICrowdRenderSource crowd, NavGrid nav, float2 origin, float2 dir, float range, float radius,
            int maxHits, int[] slots, float[] distances, out float endDistance, bool[] ignore = null, float[] typeExtraRadius = null)
        {
            float wall = nav != null ? nav.Raycast(origin, dir, range) : range;
            int count = 0;
            bool[] alive = crowd.Alive;
            float[] xs = crowd.X;
            float[] zs = crowd.Z;
            byte[] types = crowd.Types;
            for (int i = 0; i < crowd.Capacity; i++)
            {
                if (!alive[i] || (ignore != null && ignore[i])) continue;
                float r = typeExtraRadius != null && types[i] < typeExtraRadius.Length ? radius + typeExtraRadius[types[i]] : radius;
                float r2 = r * r;
                float2 to = new float2(xs[i], zs[i]) - origin;
                float along = math.dot(to, dir);
                if (along < 0f || along > wall + r) continue;
                float side2 = math.lengthsq(to) - along * along;
                if (side2 > r2) continue;
                float entry = along - math.sqrt(r2 - side2);
                if (entry > wall) continue;
                Insert(i, math.max(0f, entry), maxHits, slots, distances, ref count);
            }
            endDistance = count == maxHits ? distances[count - 1] : wall;
            return count;
        }

        /// <summary>Extra hit radius per type: body radius beyond the standard zombie's (<paramref name="baseRadius"/>).</summary>
        public static float[] ExtraRadii(Data.Zombies.ZombieDefinition[] types, float baseRadius)
        {
            var extra = new float[types != null ? types.Length : 0];
            for (int t = 0; t < extra.Length; t++) extra[t] = types[t] != null ? math.max(0f, types[t].Radius - baseRadius) : 0f;
            return extra;
        }

        /// <summary>Keeps the nearest <paramref name="maxHits"/> hits sorted by distance.</summary>
        static void Insert(int slot, float distance, int maxHits, int[] slots, float[] distances, ref int count)
        {
            int at = count;
            while (at > 0 && distances[at - 1] > distance) at--;
            if (at >= maxHits) return;
            int last = count < maxHits ? count : maxHits - 1;
            for (int k = last; k > at; k--)
            {
                slots[k] = slots[k - 1];
                distances[k] = distances[k - 1];
            }
            slots[at] = slot;
            distances[at] = distance;
            if (count < maxHits) count++;
        }
    }
}
