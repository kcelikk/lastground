namespace LastGround.Gameplay.Boss
{
    /// <summary>Boss state machine (TDD_01 §10): Intro → Phase1 → Phase2 (≤ 60 %) → Enraged (≤ 25 %) → Dead.</summary>
    public enum BossPhase : byte
    {
        None = 0,
        Intro = 1,
        Phase1 = 2,
        Phase2 = 3,
        Enraged = 4,
        Dead = 5,
    }
}
