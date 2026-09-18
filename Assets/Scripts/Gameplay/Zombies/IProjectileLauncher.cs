using LastGround.Data.Combat;
using Unity.Mathematics;

namespace LastGround.Gameplay.Zombies
{
    /// <summary>Host projectiles fired by the horde (Spitter spit); implemented by the ProjectileSystem.</summary>
    public interface IProjectileLauncher
    {
        void Launch(ProjectileDefinition projectile, float2 origin, float2 target, int ownerPlayer);
    }
}
