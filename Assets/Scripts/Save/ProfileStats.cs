namespace LastGround.Save
{
    /// <summary>Lifetime statistics (badge conditions, profile screen). Totals and maxima only, so profiles merge later (cloud save).</summary>
    public sealed class ProfileStats
    {
        public int Runs;
        public int Extractions;
        public int Wipes;
        public int BossKills;
        public int Kills;
        public int Revives;
        public int BestSurvivalSeconds;
        public int MaxThreat;
        public int MaxExtractThreat;
        public int ScrapEarned;
    }
}
