namespace LastGround.Gameplay.Director
{
    /// <summary>Director tension cycle (TDD_01 §9.4).</summary>
    public enum DirectorState : byte
    {
        Calm = 0,
        BuildUp = 1,
        Peak = 2,
        PeakHold = 3,
        Relax = 4,
    }
}
