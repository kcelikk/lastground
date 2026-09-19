using Unity.Mathematics;

namespace LastGround.Gameplay.Projectiles
{
    /// <summary>Things other than zombies and players that blasts affect (barrels, fuel tanks: chain reactions).</summary>
    public interface IBlastListener
    {
        void OnBlast(float2 center, float radius, float damage);
    }
}
