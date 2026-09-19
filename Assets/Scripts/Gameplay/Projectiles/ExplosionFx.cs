using LastGround.Data.Combat;

namespace LastGround.Gameplay.Projectiles
{
    /// <summary>A blast went off (presentation: flash, debris, sound, shake). Host publishes; clients get it replicated.</summary>
    public struct ExplosionFx
    {
        public float X;
        public float Z;
        public float Radius;
        public ExplosionKind Kind;
    }
}
