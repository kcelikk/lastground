using LastGround.Data.Zombies;

namespace LastGround.Gameplay.Zombies
{
    /// <summary>
    /// Per-type movement numbers for the Burst steering job (TDD_01 §8.5), copied once from
    /// <see cref="ZombieDefinition"/> into a native table indexed by type.
    /// </summary>
    public struct ZombieTypeParams
    {
        public byte Behaviour;
        public float Mass;
        public float Radius;
        /// <summary>Centre distance at which a melee attack starts (bigger bodies reach further).</summary>
        public float AttackRange;
        /// <summary>Hard minimum centre distance to any player.</summary>
        public float KeepOut;
        public float PreferredMin;
        public float PreferredMax;
        public float StrafeSpeed;
        public float SprintRange;
        public float SprintMultiplier;

        public static ZombieTypeParams From(ZombieDefinition definition, ZombieTuning tuning)
        {
            float extra = definition.Radius - tuning.Radius;
            return new ZombieTypeParams
            {
                Behaviour = (byte)definition.Behaviour,
                Mass = definition.Mass > 0f ? definition.Mass : 1f,
                Radius = definition.Radius,
                AttackRange = tuning.AttackRange + extra,
                KeepOut = ZombieSteeringJob.MinPlayerDistance + extra,
                PreferredMin = definition.PreferredRange.x,
                PreferredMax = definition.PreferredRange.y,
                StrafeSpeed = definition.StrafeSpeed,
                SprintRange = definition.SprintRange,
                SprintMultiplier = definition.SprintMultiplier,
            };
        }
    }
}
