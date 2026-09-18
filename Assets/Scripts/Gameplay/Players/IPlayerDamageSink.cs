namespace LastGround.Gameplay.Players
{
    /// <summary>Host-side entry point for damage to players (zombie attacks now; explosions and bosses later).</summary>
    public interface IPlayerDamageSink
    {
        void Damage(int player, float amount);
    }
}
