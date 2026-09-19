using LastGround.Data.Meta;
using LastGround.Save;

namespace LastGround.Meta
{
    /// <summary>
    /// Banks a finished run into the profile (TDD_01 §2.2, §14.7): Scrap from the banked coins, lifetime statistics
    /// (totals and maxima) and any badges whose threshold is now reached. Called once per run on every device for its
    /// own player; the caller saves afterwards.
    /// </summary>
    public sealed class MetaProgressionService
    {
        readonly MetaProfile _profile;

        public MetaProgressionService(MetaProfile profile)
        {
            _profile = profile;
        }

        public BankReport Bank(in RunSummary run)
        {
            var report = new BankReport();
            ProfileData data = _profile.Data;
            ProfileStats stats = data.Stats;
            int scrap = (int)System.Math.Floor(System.Math.Max(0, run.BankedCoins) * _profile.Catalog.ScrapPerCoin);
            data.Scrap += scrap;
            report.ScrapGained = scrap;

            stats.Runs++;
            if (run.Extracted)
            {
                stats.Extractions++;
                if (run.MaxThreat > stats.MaxExtractThreat) stats.MaxExtractThreat = run.MaxThreat;
            }
            else
            {
                stats.Wipes++;
            }
            stats.Kills += run.Kills;
            stats.Revives += run.Revives;
            stats.BossKills += run.BossKills;
            stats.ScrapEarned += scrap;
            if ((int)run.SurvivalSeconds > stats.BestSurvivalSeconds) stats.BestSurvivalSeconds = (int)run.SurvivalSeconds;
            if (run.MaxThreat > stats.MaxThreat) stats.MaxThreat = run.MaxThreat;

            foreach (TitleDefinition title in _profile.Catalog.Titles)
            {
                if (title == null || data.Badges.Contains(title.Id) || Value(stats, title.Stat) < title.Threshold) continue;
                data.Badges.Add(title.Id);
                report.NewBadges.Add(title);
            }
            return report;
        }

        public static int Value(ProfileStats stats, ProfileStat stat)
        {
            switch (stat)
            {
                case ProfileStat.Runs: return stats.Runs;
                case ProfileStat.Extractions: return stats.Extractions;
                case ProfileStat.BossKills: return stats.BossKills;
                case ProfileStat.Kills: return stats.Kills;
                case ProfileStat.Revives: return stats.Revives;
                case ProfileStat.BestSurvivalSeconds: return stats.BestSurvivalSeconds;
                case ProfileStat.MaxThreat: return stats.MaxThreat;
                default: return stats.MaxExtractThreat;
            }
        }
    }
}
