using Unity.Mathematics;

namespace LastGround.Gameplay.Combat
{
    /// <summary>Where the local player's grenade throws go (host: LoadoutAuthority; client: network).</summary>
    public interface IGrenadeSink
    {
        void Throw(int player, float2 target);
    }
}
