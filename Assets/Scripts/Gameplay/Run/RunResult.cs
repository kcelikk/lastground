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
        /// <summary>Extraction bonus per player (threat-based, TDD_01 §2.2).</summary>
        public int ExtractionBonus;
        /// <summary>Players standing in the landing zone at extraction (bit per player).</summary>
        public byte StandingMask;
        /// <summary>Coins banked by a standing extractor (coins + bonus), or by everyone after a wipe (60 %).</summary>
        public int Banked;
        /// <summary>Coins banked by players who were down or dead when the others extracted.</summary>
        public int BankedLeftBehind;

        /// <summary>What this player keeps (M8 reward conversion; M9 turns it into Scrap).</summary>
        public int BankedFor(int player) => Extracted && (StandingMask & (1 << player)) == 0 ? BankedLeftBehind : Banked;
    }
}
