namespace LastGround.Gameplay.Players
{
    /// <summary>Host-side entry point for harm to players: zombie attacks, spit, explosions.</summary>
    public interface IPlayerDamageSink
    {
        void Damage(int player, float amount);

        /// <summary>Move speed × <paramref name="multiplier"/> for <paramref name="seconds"/> (Spitter spit, Toxic elites).</summary>
        void Slow(int player, float multiplier, float seconds);
    }
}
