namespace LastGround.Gameplay.Players
{
    /// <summary>A player's damage killed a zombie (bullets, burn, grenades): on-kill upgrades.</summary>
    public interface IKillCreditSink
    {
        void OnKill(int player);
    }
}
