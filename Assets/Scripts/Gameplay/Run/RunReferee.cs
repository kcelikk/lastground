using LastGround.Core.Tick;
using LastGround.Data.Boss;
using LastGround.Gameplay.Director;
using LastGround.Gameplay.Players;

namespace LastGround.Gameplay.Run
{
    /// <summary>
    /// Host: decides when the run is over — the whole team is down or dead (M5) or it extracted (M8) — and fills in
    /// the result with the reward conversion (TDD_01 §2.2). Kill and coin counts come from whoever tracks them.
    /// </summary>
    public sealed class RunReferee : ITickable
    {
        readonly PlayerHealthSystem _health;
        readonly RunStatus _status;
        readonly RunOutcome _outcome;
        int _maxThreat = 1;

        public RunReferee(PlayerHealthSystem health, RunStatus status, RunOutcome outcome)
        {
            _health = health;
            _status = status;
            _outcome = outcome;
        }

        /// <summary>Team kills so far (set by the installer from the combat authority).</summary>
        public System.Func<int> Kills { get; set; }

        /// <summary>Team run coin (M5 wallet).</summary>
        public System.Func<int> Coins { get; set; }

        /// <summary>Reward conversion (null: everything is kept).</summary>
        public ExtractionRulesDefinition Rules { get; set; }

        /// <summary>The team held the landing zone: the run ends as an extraction.</summary>
        public void Extract(byte standingMask)
        {
            if (_outcome.Ended) return;
            RunResult result = Base(true);
            result.StandingMask = standingMask;
            result.ExtractionBonus = Rules != null ? Rules.Convert(result.Coins, _maxThreat, true, true) - result.Coins : 0;
            result.Banked = result.Coins + result.ExtractionBonus;
            result.BankedLeftBehind = Rules != null ? Rules.Convert(result.Coins, _maxThreat, true, false) : result.Coins;
            _outcome.End(result);
        }

        public void Tick(float dt, uint tick)
        {
            if (_status.Threat > _maxThreat) _maxThreat = _status.Threat;
            if (_outcome.Ended || !_health.TeamWiped) return;
            RunResult result = Base(false);
            result.Banked = Rules != null ? Rules.Convert(result.Coins, _maxThreat, false, false) : result.Coins;
            result.BankedLeftBehind = result.Banked;
            _outcome.End(result);
        }

        RunResult Base(bool extracted)
        {
            if (_status.Threat > _maxThreat) _maxThreat = _status.Threat;
            return new RunResult
            {
                SurvivalSeconds = _status.RunSeconds,
                MaxThreat = _maxThreat,
                Kills = Kills != null ? Kills() : 0,
                Revives = _health.Revives,
                Coins = Coins != null ? Coins() : 0,
                Extracted = extracted,
            };
        }
    }
}
