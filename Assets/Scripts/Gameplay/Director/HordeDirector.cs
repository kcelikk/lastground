using LastGround.Core.Random;
using LastGround.Core.Tick;
using LastGround.Data.Director;
using LastGround.Gameplay.Players;
using LastGround.Gameplay.Zombies;
using Unity.Mathematics;

namespace LastGround.Gameplay.Director
{
    /// <summary>
    /// Waveless horde director (TDD_01 §9, D-003). Spawn points accrue every tick from a time curve × Threat ×
    /// player count × tension state × host governor; when enough points are banked the director picks a pattern
    /// (trickle, pack, pincer, surround) for the current state and queues its spawns, a few per tick. The tension
    /// cycle (Calm → BuildUp → Peak → PeakHold → Relax) reacts to team intensity with randomized durations, so every
    /// run breathes differently. Host only; replaces the M3 TestHordeSpawner.
    /// </summary>
    public sealed partial class HordeDirector : ITickable
    {
        const float SampleInterval = 0.5f;
        const float PeakDuration = 6f;
        const float RelaxMaxDuration = 45f;
        const float DespawnInterval = 1f;
        const int QueueCapacity = 256;

        readonly ZombieWorld _world;
        readonly PlayerStateTable _players;
        readonly DirectorProfile _profile;
        readonly ThreatCurveDefinition _threat;
        readonly PlayerCountScalingProfile _scaling;
        readonly RunStatus _status;
        readonly IntensityTracker _intensity;
        readonly SpawnLocator _locator;
        readonly float2[] _queue = new float2[QueueCapacity];
        DeterministicRandom _rng;
        int _queueHead;
        int _queueCount;
        float _budget;
        float _sampleTimer;
        float _stateTimer;
        float _stateDuration;
        float _despawnTimer;
        float _labelCandidateTime;
        HordeLevel _labelCandidate;
        // Run personality: every run is a different mix of tempo, build-up length, peak trigger and pattern taste.
        readonly float _rateScale;
        readonly float _buildUpScale;
        readonly float _peakIntensity;
        readonly float _packBias;

        public HordeDirector(ZombieWorld world, PlayerStateTable players, DirectorProfile profile, ThreatCurveDefinition threat,
            PlayerCountScalingProfile scaling, CameraFootprint footprint, float playerMaxHealth, RunStatus status, uint seed)
        {
            _world = world;
            _players = players;
            _profile = profile;
            _threat = threat;
            _scaling = scaling;
            _status = status;
            _rng = DeterministicRandom.ForStream(seed, "director");
            _rateScale = 1f + _rng.Range(-profile.RateVariance, profile.RateVariance);
            _buildUpScale = 1f + _rng.Range(-profile.BuildUpVariance, profile.BuildUpVariance);
            _peakIntensity = profile.PeakIntensity + _rng.Range(-profile.PeakThresholdVariance, profile.PeakThresholdVariance);
            _packBias = _rng.Range(-0.15f, 0.15f);
            _intensity = new IntensityTracker(players, world.Crowd, profile, playerMaxHealth);
            _locator = new SpawnLocator(world.Nav, world.Flow, players, profile, footprint);
            Enter(DirectorState.Calm, profile.CalmAtStart);
        }

        public PerformanceGovernor Governor { get; } = new PerformanceGovernor(1f / 60f);
        public int Spawned { get; private set; }
        public int Despawned { get; private set; }
        public int SpawnFailures => _locator.Rejected;

        /// <summary>Run seconds at which each Peak began (diagnostics, tests).</summary>
        public readonly System.Collections.Generic.List<float> PeakTimes = new System.Collections.Generic.List<float>(64);

        public void Tick(float dt, uint tick)
        {
            _status.RunSeconds += dt;
            _status.Threat = _threat.LevelAt(_status.RunSeconds);

            _sampleTimer -= dt;
            if (_sampleTimer <= 0f)
            {
                _sampleTimer += SampleInterval;
                _intensity.Sample(SampleInterval);
                _status.Intensity = _intensity.Team;
                UpdateLabel(SampleInterval);
            }
            UpdateState(dt);

            int players = CountTargetable();
            float threatScale = 1f + (_status.Threat - 1) * _threat.BudgetPerLevel;
            float minutes = _status.RunSeconds / 60f;
            float governor = Governor.Multiplier;
            float rate = math.min(_profile.SpawnRateCap, _profile.SpawnRateStart + _profile.SpawnRatePerMinute * minutes)
                         * StateRate() * _scaling.SpawnRateFor(math.max(1, players)) * threatScale * governor * _rateScale;
            float maxAlive = (_profile.MaxAliveStart + _profile.MaxAlivePerMinute * minutes)
                             * _scaling.HordeCountFor(math.max(1, players)) * threatScale;
            int cap = (int)math.min(_profile.MaxAliveCap * governor, maxAlive);
            _status.SpawnRate = rate;
            _status.MaxAlive = cap;
            _status.Alive = _world.Crowd.ActiveCount;

            _budget = math.min(_profile.BudgetCarryCap, _budget + rate * dt);
            if (players > 0 && _queueCount == 0) PlanPattern(cap);
            SpawnQueued(cap);

            _despawnTimer -= dt;
            if (_despawnTimer <= 0f)
            {
                _despawnTimer = DespawnInterval;
                DespawnFar();
            }
        }

        /// <summary>An objective was completed: the team gets a breather now (TDD_01 §12.4).</summary>
        public void ForceRelax()
        {
            Enter(DirectorState.Relax, Range(_profile.RelaxMinDuration));
            _pendingSize = 0;
        }

        void UpdateState(float dt)
        {
            _stateTimer += dt;
            float intensity = _status.Intensity;
            switch (_status.State)
            {
                case DirectorState.Calm:
                    if (_stateTimer >= _stateDuration) Enter(DirectorState.BuildUp, Range(_profile.BuildUpDuration) * _buildUpScale);
                    break;
                case DirectorState.BuildUp:
                    if (intensity >= _peakIntensity || _stateTimer >= _stateDuration) Enter(DirectorState.Peak, PeakDuration);
                    break;
                case DirectorState.Peak:
                    if (_stateTimer >= _stateDuration) Enter(DirectorState.PeakHold, Range(_profile.PeakHoldDuration));
                    break;
                case DirectorState.PeakHold:
                    if (_stateTimer >= _stateDuration) Enter(DirectorState.Relax, Range(_profile.RelaxMinDuration));
                    break;
                case DirectorState.Relax:
                    bool rested = _stateTimer >= _stateDuration && intensity < _profile.RelaxIntensity;
                    if (rested || _stateTimer >= RelaxMaxDuration) Enter(DirectorState.BuildUp, Range(_profile.BuildUpDuration) * _buildUpScale);
                    break;
            }
        }

        void Enter(DirectorState state, float duration)
        {
            if (state == DirectorState.Peak && PeakTimes.Count < PeakTimes.Capacity) PeakTimes.Add(_status.RunSeconds);
            _status.State = state;
            _stateTimer = 0f;
            _stateDuration = duration;
        }

        float StateRate()
        {
            switch (_status.State)
            {
                case DirectorState.Calm: return _profile.CalmRate;
                case DirectorState.BuildUp: return _profile.BuildUpRate;
                case DirectorState.Peak: return _profile.PeakRate;
                case DirectorState.PeakHold: return _profile.PeakHoldRate;
                default: return _profile.RelaxRate;
            }
        }

        /// <summary>HORDE label: intensity blended with state pressure, held 2 s before it changes (no flicker).</summary>
        void UpdateLabel(float dt)
        {
            float pressure;
            switch (_status.State)
            {
                case DirectorState.BuildUp: pressure = 0.45f; break;
                case DirectorState.Peak: pressure = 1f; break;
                case DirectorState.PeakHold: pressure = 0.8f; break;
                case DirectorState.Relax: pressure = 0.1f; break;
                default: pressure = 0f; break;
            }
            float value = 0.65f * _status.Intensity + 0.35f * pressure;
            HordeLevel level = value >= _profile.ExtremeThreshold ? HordeLevel.Extreme
                : value >= _profile.HighThreshold ? HordeLevel.High
                : value >= _profile.MediumThreshold ? HordeLevel.Medium
                : value >= _profile.LowThreshold ? HordeLevel.Low : HordeLevel.Calm;
            if (level == _status.Horde)
            {
                _labelCandidateTime = 0f;
                return;
            }
            if (level != _labelCandidate)
            {
                _labelCandidate = level;
                _labelCandidateTime = 0f;
            }
            _labelCandidateTime += dt;
            if (_labelCandidateTime >= _profile.LabelHysteresis) _status.Horde = level;
        }

        float Range(UnityEngine.Vector2 range) => _rng.Range(range.x, range.y);

        int CountTargetable()
        {
            int count = 0;
            for (int p = 0; p < PlayerStateTable.Max; p++) if (_players.IsTargetable(p)) count++;
            return count;
        }
    }
}
