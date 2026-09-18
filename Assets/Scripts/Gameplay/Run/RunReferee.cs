using LastGround.Core.Tick;
using LastGround.Gameplay.Director;
using LastGround.Gameplay.Players;

namespace LastGround.Gameplay.Run
{
    /// <summary>
    /// Host: decides when the run is over (M5: the whole team is down or dead; extraction arrives in M8) and fills
    /// in the result. Kill and coin counts come from whoever tracks them (combat authority, wallet).
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

        public void Tick(float dt, uint tick)
        {
            if (_status.Threat > _maxThreat) _maxThreat = _status.Threat;
            if (_outcome.Ended || !_health.TeamWiped) return;
            _outcome.End(new RunResult
            {
                SurvivalSeconds = _status.RunSeconds,
                MaxThreat = _maxThreat,
                Kills = Kills != null ? Kills() : 0,
                Revives = _health.Revives,
                Coins = Coins != null ? Coins() : 0,
                Extracted = false,
            });
        }
    }
}
