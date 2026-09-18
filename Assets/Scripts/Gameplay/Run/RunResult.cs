namespace LastGround.Gameplay.Run
{
    /// <summary>End-of-run summary (TDD_01 §2.2 score) shown on every device's results screen.</summary>
    public struct RunResult
    {
        public float SurvivalSeconds;
        public int MaxThreat;
        public int Kills;
        public int Revives;
        public int Coins;
        public bool Extracted;
    }
}
