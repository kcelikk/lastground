namespace LastGround.Gameplay.Director
{
    /// <summary>Spawn shapes (TDD_01 §9.6). Flood and Ambush need map data and arrive in M7.</summary>
    public enum HordePattern : byte
    {
        Trickle = 0,
        Pack = 1,
        Pincer = 2,
        Surround = 3,
    }
}
