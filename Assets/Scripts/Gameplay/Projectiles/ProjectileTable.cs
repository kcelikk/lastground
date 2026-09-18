using LastGround.Core.Events;
using LastGround.Data.Combat;
using Unity.Mathematics;

namespace LastGround.Gameplay.Projectiles
{
    /// <summary>A projectile appeared (host: ProjectileSystem; clients: ProjectileSpawn).</summary>
    public struct ProjectileLaunched
    {
        public int Id;
        public byte Definition;
        /// <summary>Thrower, -1 for zombies.</summary>
        public int Owner;
        public float OriginX, OriginZ, EndX, EndZ;
        /// <summary>Seconds from origin to end.</summary>
        public float Duration;
    }

    /// <summary>A projectile is gone: landed and blew up, hit a player, hit a wall or ran out of range.</summary>
    public struct ProjectileEnded
    {
        public int Id;
        public float X, Z;
        /// <summary>Player hit by a straight projectile, -1 otherwise.</summary>
        public int HitPlayer;
    }

    /// <summary>
    /// Every device's view of the flying projectiles (TDD_01 §6.3): fixed slots; each flight is closed-form (origin →
    /// end over a duration, arcs add a parabola height), so clients replay it from one spawn message and the age they
    /// advance locally. The host simulates hits in <see cref="ProjectileSystem"/>. No GameObject per projectile.
    /// </summary>
    public sealed class ProjectileTable
    {
        public const int DefaultCapacity = 64;
        /// <summary>Clients drop a projectile this long after its flight in case the end message is late or lost.</summary>
        const float ClientLinger = 1.5f;

        public readonly bool[] Active;
        public readonly byte[] Definition;
        public readonly int[] Owner;
        public readonly float2[] Origin;
        public readonly float2[] End;
        public readonly float[] Duration;
        public readonly float[] Age;

        public readonly EventChannel<ProjectileLaunched> Launched = new EventChannel<ProjectileLaunched>(64);
        public readonly EventChannel<ProjectileEnded> Ended = new EventChannel<ProjectileEnded>(64);

        readonly ProjectileDefinition[] _definitions;

        public ProjectileTable(ProjectileDefinition[] definitions, int capacity = DefaultCapacity)
        {
            _definitions = definitions;
            Capacity = capacity;
            Active = new bool[capacity];
            Definition = new byte[capacity];
            Owner = new int[capacity];
            Origin = new float2[capacity];
            End = new float2[capacity];
            Duration = new float[capacity];
            Age = new float[capacity];
        }

        public int Capacity { get; }
        public int ActiveCount { get; private set; }

        public ProjectileDefinition DefinitionOf(int id) =>
            Definition[id] < _definitions.Length ? _definitions[Definition[id]] : null;

        public int FreeSlot()
        {
            for (int id = 0; id < Capacity; id++) if (!Active[id]) return id;
            return -1;
        }

        public void Put(in ProjectileLaunched launch, float age)
        {
            int id = launch.Id;
            if ((uint)id >= (uint)Capacity) return;
            if (!Active[id]) ActiveCount++;
            Active[id] = true;
            Definition[id] = launch.Definition;
            Owner[id] = launch.Owner;
            Origin[id] = new float2(launch.OriginX, launch.OriginZ);
            End[id] = new float2(launch.EndX, launch.EndZ);
            Duration[id] = math.max(1e-3f, launch.Duration);
            Age[id] = math.max(0f, age);
            Launched.Publish(launch);
        }

        public void Remove(int id, float2 at, int hitPlayer)
        {
            if ((uint)id >= (uint)Capacity || !Active[id]) return;
            Active[id] = false;
            ActiveCount--;
            Ended.Publish(new ProjectileEnded { Id = id, X = at.x, Z = at.y, HitPlayer = hitPlayer });
        }

        /// <summary>Ground position along the flight (clamped at the end).</summary>
        public float2 PositionOf(int id) => math.lerp(Origin[id], End[id], math.saturate(Age[id] / Duration[id]));

        /// <summary>Height above ground: a parabola for arcs, a constant for straight shots.</summary>
        public float HeightOf(int id)
        {
            ProjectileDefinition definition = DefinitionOf(id);
            if (definition == null) return 0f;
            if (definition.Motion != ProjectileMotion.Arc) return 1.2f;
            float k = math.saturate(Age[id] / Duration[id]);
            float scale = Duration[id] / math.max(1e-3f, definition.FlightTime.y);
            return 0.3f + 4f * definition.ArcHeight * scale * k * (1f - k);
        }

        /// <summary>Clients: advance the replayed flights and drop stale ones.</summary>
        public void AdvanceReplay(float dt)
        {
            for (int id = 0; id < Capacity; id++)
            {
                if (!Active[id]) continue;
                Age[id] += dt;
                ProjectileDefinition definition = DefinitionOf(id);
                float fuse = definition != null && definition.Motion == ProjectileMotion.Arc ? definition.FuseAfterLanding : 0f;
                if (Age[id] > Duration[id] + fuse + ClientLinger) Remove(id, PositionOf(id), -1);
            }
        }
    }
}
