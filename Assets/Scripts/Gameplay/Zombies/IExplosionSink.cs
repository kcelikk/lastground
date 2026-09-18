using LastGround.Data.Combat;
using Unity.Mathematics;

namespace LastGround.Gameplay.Zombies
{
    /// <summary>Host blasts requested by the horde (Exploders, Volatile elites); implemented by the ExplosionSystem.</summary>
    public interface IExplosionSink
    {
        /// <param name="sourcePlayer">Player credited with kills (grenades), -1 for zombie blasts.</param>
        void Explode(float2 center, in ExplosionSpec spec, ExplosionKind kind, int sourcePlayer);
    }
}
