namespace LastGround.Gameplay.Players
{
    /// <summary>Life state (TDD_01 §14.2). Downed players crawl and can be revived; dead players wait to return.</summary>
    public enum PlayerLife : byte
    {
        Alive = 0,
        Downed = 1,
        Dead = 2,
    }
}
