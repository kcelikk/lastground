using Unity.Mathematics;

namespace LastGround.Gameplay.Zombies
{
    /// <summary>Scales damage to driven bodies (boss weak point, intro invulnerability).</summary>
    public interface IZombieDamageModifier
    {
        /// <param name="direction">Unit direction the damage travelled (shot direction).</param>
        /// <param name="amount">Damage before scaling.</param>
        /// <param name="sourcePlayer">Player who dealt it, -1 for none.</param>
        float Scale(int slot, float2 direction, float amount, int sourcePlayer);
    }
}
