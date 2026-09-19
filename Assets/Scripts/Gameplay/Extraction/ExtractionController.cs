using LastGround.Core.Random;
using LastGround.Core.Tick;
using LastGround.Data.Boss;
using LastGround.Data.Map;
using LastGround.Gameplay.Boss;
using LastGround.Gameplay.Director;
using LastGround.Gameplay.Objectives;
using LastGround.Gameplay.Players;
using LastGround.Gameplay.Run;
using Unity.Mathematics;

namespace LastGround.Gameplay.Extraction
{
    /// <summary>
    /// Host extraction windows (TDD_01 §2.2, D-004). A landing zone opens a few seconds after every boss kill and then
    /// every <see cref="ExtractionRulesDefinition.IntervalSeconds"/> after the previous window closed, at an extraction
    /// anchor away from the team's region. The window counts down while nobody is in the zone; while at least one
    /// standing player is inside, the hold progresses (and draws the horde). A full hold ends the run as an extraction;
    /// a missed window lets the run go on, harder. Map events pause while a boss lives or a zone is open.
    /// </summary>
    public sealed class ExtractionController : ITickable
    {
        const float MissedShowSeconds = 4f;

        readonly PlayerStateTable _players;
        readonly ExtractionRulesDefinition _rules;
        readonly MapDefinition _map;
        readonly BossState _boss;
        readonly ExtractionState _state;
        readonly RunReferee _referee;
        DeterministicRandom _rng;
        ushort _instance;
        int _seenDefeats;
        float _pendingOpen = -1f;
        float _periodic = -1f;
        float _window;
        float _hold;
        float _missedTimer;
        float2 _anchor;
        int _region = -1;

        public ExtractionController(PlayerStateTable players, ExtractionRulesDefinition rules, MapDefinition map, BossState boss,
            ExtractionState state, RunReferee referee, uint seed)
        {
            _players = players;
            _rules = rules;
            _map = map;
            _boss = boss;
            _state = state;
            _referee = referee;
            _rng = DeterministicRandom.ForStream(seed, "extraction");
        }

        /// <summary>Holding the zone draws the horde.</summary>
        public HordeDirector Director { get; set; }

        /// <summary>Paused while a boss lives or a zone is open.</summary>
        public ObjectiveSystem Objectives { get; set; }

        public int Opened { get; private set; }
        public int Missed { get; private set; }

        /// <summary>Dev (-lg-extract): open a window in a few seconds.</summary>
        public void OpenSoon(float seconds) => _pendingOpen = seconds;

        public void Tick(float dt, uint tick)
        {
            if (Objectives != null) Objectives.Paused = _boss.Active || _state.IsOpen;
            if (_boss.Defeats > _seenDefeats)
            {
                _seenDefeats = _boss.Defeats;
                if (!_state.IsOpen) _pendingOpen = _rules.AfterBossDelay;
            }

            switch (_state.Phase)
            {
                case ExtractionPhase.Open:
                    TickOpen(dt);
                    return;
                case ExtractionPhase.Extracted:
                    return;
                case ExtractionPhase.Missed:
                    _missedTimer -= dt;
                    if (_missedTimer <= 0f) Publish(ExtractionPhase.None, false);
                    break;
            }

            if (_pendingOpen >= 0f)
            {
                _pendingOpen -= dt;
                if (_pendingOpen <= 0f) Open();
            }
            else if (_periodic >= 0f && !_boss.Active)
            {
                _periodic -= dt;
                if (_periodic <= 0f) Open();
            }
        }

        void Open()
        {
            if (!PickAnchor(out _anchor, out _region))
            {
                _pendingOpen = -1f;
                return;
            }
            _pendingOpen = -1f;
            _periodic = -1f;
            _instance++;
            _window = _rules.WindowSeconds;
            _hold = 0f;
            Opened++;
            Publish(ExtractionPhase.Open, false);
        }

        void TickOpen(float dt)
        {
            bool inside = false;
            byte standing = 0;
            float r2 = _rules.Radius * _rules.Radius;
            for (int p = 0; p < PlayerStateTable.Max; p++)
            {
                if (!_players.CanAct(p)) continue;
                if (math.distancesq(new float2(_players.X[p], _players.Z[p]), _anchor) > r2) continue;
                inside = true;
                standing |= (byte)(1 << p);
            }

            if (inside)
            {
                _hold += dt;
                if (_rules.PressureWhileHolding) Director?.HoldPressure();
                if (_hold >= _rules.HoldSeconds)
                {
                    Publish(ExtractionPhase.Extracted, true);
                    _referee?.Extract(standing);
                    return;
                }
            }
            else
            {
                _window -= dt;
                if (_window <= 0f)
                {
                    Missed++;
                    _missedTimer = MissedShowSeconds;
                    _periodic = _rules.IntervalSeconds;
                    Publish(ExtractionPhase.Missed, false);
                    return;
                }
            }
            Publish(ExtractionPhase.Open, inside);
        }

        void Publish(ExtractionPhase phase, bool holding)
        {
            _state.Set(phase, _instance, _anchor.x, _anchor.y, _rules.Radius, _region, (int)math.ceil(math.max(0f, _window)),
                (int)(_hold * 10f), (int)math.round(_rules.HoldSeconds * 10f), holding);
        }

        /// <summary>An extraction anchor outside the team's current region if possible (the team has to travel).</summary>
        bool PickAnchor(out float2 position, out int region)
        {
            position = float2.zero;
            region = -1;
            if (_map == null || _map.Anchors == null) return false;
            int teamRegion = TeamRegion();
            int count = 0, far = 0;
            foreach (MapDefinition.Anchor a in _map.Anchors)
            {
                if (a.Kind != MapAnchorKind.Extraction) continue;
                count++;
                if (a.Region != teamRegion) far++;
            }
            if (count == 0) return false;
            bool avoid = far > 0;
            int pick = _rng.Range(0, avoid ? far : count);
            foreach (MapDefinition.Anchor a in _map.Anchors)
            {
                if (a.Kind != MapAnchorKind.Extraction || (avoid && a.Region == teamRegion)) continue;
                if (pick-- > 0) continue;
                position = new float2(a.Position.x, a.Position.y);
                region = a.Region;
                return true;
            }
            return false;
        }

        int TeamRegion()
        {
            float2 sum = float2.zero;
            int n = 0;
            for (int p = 0; p < PlayerStateTable.Max; p++)
            {
                if (!_players.CanAct(p)) continue;
                sum += new float2(_players.X[p], _players.Z[p]);
                n++;
            }
            return n > 0 ? _map.RegionAt(sum.x / n, sum.y / n) : -1;
        }
    }
}
