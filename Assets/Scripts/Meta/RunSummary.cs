namespace LastGround.Meta
{
    /// <summary>What a finished run contributes to the profile (built by the app from the run result).</summary>
    public struct RunSummary
    {
        /// <summary>Coins this player banked (after the extraction bonus or the wipe conversion).</summary>
        public int BankedCoins;
        public bool Extracted;
        public int MaxThreat;
        public float SurvivalSeconds;
        public int Kills;
        public int Revives;
        public int BossKills;
    }
}
