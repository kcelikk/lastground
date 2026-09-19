using LastGround.Core.Events;
using LastGround.Core.Net;
using LastGround.Core.Tick;
using LastGround.Data.Boss;
using LastGround.Gameplay.Crowd;
using LastGround.Gameplay.Director;

namespace LastGround.Gameplay.Run
{
    /// <summary>
    /// Client (M10, TDD_02 §19.5): keeps what this device knows about the run — replicated zombie deaths, the
    /// highest threat seen — so that losing the host mid-run still ends in a results screen with a partial reward
    /// (the team-wipe conversion of the team's run coin).
    /// </summary>
    public sealed class ConnectionLossTracker : ITickable
    {
        readonly IGameEventStream<CrowdDeath> _deaths;
        readonly RunStatus _status;
        EventReader<CrowdDeath> _reader;
        int _kills;
        int _maxThreat = 1;

        public ConnectionLossTracker(IGameEventStream<CrowdDeath> deaths, RunStatus status)
        {
            _deaths = deaths;
            _status = status;
            _reader = deaths.CreateReader();
        }

        public void Tick(float dt, uint tick)
        {
            while (_deaths.TryRead(ref _reader, out _)) _kills++;
            if (_status.Threat > _maxThreat) _maxThreat = _status.Threat;
        }

        /// <summary>The partial result: last known run time, threat, team kills and coins; banked like a wipe.</summary>
        public RunResult Result(int coins, ExtractionRulesDefinition rules)
        {
            if (_status.Threat > _maxThreat) _maxThreat = _status.Threat;
            int banked = rules != null ? rules.Convert(coins, _maxThreat, false, false) : coins;
            return new RunResult
            {
                SurvivalSeconds = _status.RunSeconds,
                MaxThreat = _maxThreat,
                Kills = _kills,
                Coins = coins,
                Banked = banked,
                BankedLeftBehind = banked,
                ConnectionLost = true,
            };
        }
    }
}
