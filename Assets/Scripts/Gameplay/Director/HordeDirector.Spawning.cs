using LastGround.Gameplay.Players;
using Unity.Mathematics;

namespace LastGround.Gameplay.Director
{
    /// <summary>Pattern selection, spawn queue and far-zombie cleanup.</summary>
    public sealed partial class HordeDirector
    {
        void PlanPattern(int cap)
        {
            int room = cap - _world.Crowd.ActiveCount;
            if (room <= 0) return;
            // Choose once, then save up for it: rerolling every tick would starve the big patterns.
            if (_pendingSize <= 0 || _pendingState != _status.State)
            {
                _pendingPattern = PickPattern();
                _pendingSize = PatternSize(_pendingPattern);
                _pendingState = _status.State;
            }
            HordePattern pattern = _pendingPattern;
            int size = math.min(_pendingSize, room);
            if (size <= 0 || _budget < size) return;
            _pendingSize = 0;

            int player = RandomTargetablePlayer();
            if (player < 0) return;
            float angle = _rng.Range(-math.PI, math.PI);
            float spread = math.radians(_profile.PackSpreadDeg);
            int planned = 0;
            switch (pattern)
            {
                case HordePattern.Trickle:
                    for (int i = 0; i < size; i++) planned += Plan(player, _rng.Range(-math.PI, math.PI), math.PI / 6f);
                    break;
                case HordePattern.Pack:
                    for (int i = 0; i < size; i++) planned += Plan(player, angle, spread);
                    break;
                case HordePattern.Pincer:
                    for (int i = 0; i < size; i++) planned += Plan(player, angle + (i % 2) * math.PI, spread);
                    break;
                default:
                    int sectors = _rng.Range(_profile.SurroundSectors.x, _profile.SurroundSectors.y + 1);
                    for (int i = 0; i < size; i++) planned += Plan(player, angle + (i % sectors) * (2f * math.PI / sectors), spread);
                    break;
            }
            _budget -= planned;
            LastPattern = pattern;
            Patterns++;
        }

        HordePattern _pendingPattern;
        int _pendingSize;
        DirectorState _pendingState;

        public HordePattern LastPattern { get; private set; }
        public int Patterns { get; private set; }

        HordePattern PickPattern()
        {
            // The run's pattern taste shifts rolls towards packs or towards the wider shapes.
            float roll = math.saturate(_rng.NextFloat() + _packBias);
            switch (_status.State)
            {
                case DirectorState.BuildUp:
                    return roll < 0.5f ? HordePattern.Trickle : roll < 0.9f ? HordePattern.Pack : HordePattern.Pincer;
                case DirectorState.Peak:
                    return roll < 0.45f ? HordePattern.Surround : roll < 0.75f ? HordePattern.Pincer : HordePattern.Pack;
                case DirectorState.PeakHold:
                    return roll < 0.2f ? HordePattern.Trickle : roll < 0.7f ? HordePattern.Pack : HordePattern.Pincer;
                default:
                    return HordePattern.Trickle;
            }
        }

        int PatternSize(HordePattern pattern)
        {
            switch (pattern)
            {
                case HordePattern.Trickle: return _rng.Range(_profile.TrickleSize.x, _profile.TrickleSize.y + 1);
                case HordePattern.Pack: return _rng.Range(_profile.PackSize.x, _profile.PackSize.y + 1);
                case HordePattern.Pincer: return 2 * _rng.Range(_profile.PincerSideSize.x, _profile.PincerSideSize.y + 1);
                default:
                    int sectors = _rng.Range(_profile.SurroundSectors.x, _profile.SurroundSectors.y + 1);
                    return sectors * _rng.Range(_profile.SurroundSectorSize.x, _profile.SurroundSectorSize.y + 1);
            }
        }

        int Plan(int player, float angle, float spread)
        {
            if (_queueCount >= _queue.Length) return 0;
            if (!_locator.TryFind(player, angle, spread, ref _rng, out float2 position)) return 0;
            _queue[(_queueHead + _queueCount) % _queue.Length] = position;
            _queueCount++;
            return 1;
        }

        void SpawnQueued(int cap)
        {
            for (int n = 0; n < _profile.SpawnsPerTick && _queueCount > 0; n++)
            {
                if (_world.Crowd.ActiveCount >= cap)
                {
                    // Over the cap (governor dropped it): give the points back and forget the rest.
                    _budget += _queueCount;
                    _queueCount = 0;
                    return;
                }
                float2 position = _queue[_queueHead];
                _queueHead = (_queueHead + 1) % _queue.Length;
                _queueCount--;
                if (_world.Spawn(position, _rng.Range(0f, 360f)) >= 0) Spawned++;
            }
        }

        void DespawnFar()
        {
            float d2 = _profile.DespawnDistance * _profile.DespawnDistance;
            for (int i = 0; i < _world.Crowd.Capacity; i++)
            {
                if (!_world.IsAlive(i)) continue;
                float2 p = _world.PositionOf(i);
                bool near = false;
                for (int k = 0; k < PlayerStateTable.Max && !near; k++)
                    near = _players.Active[k] && math.distancesq(p, new float2(_players.X[k], _players.Z[k])) < d2;
                if (near) continue;
                _world.Remove(i, false);
                Despawned++;
            }
        }

        int RandomTargetablePlayer()
        {
            int count = 0;
            for (int p = 0; p < PlayerStateTable.Max; p++) if (_players.IsTargetable(p)) count++;
            if (count == 0) return -1;
            int pick = _rng.Range(0, count);
            for (int p = 0; p < PlayerStateTable.Max; p++)
            {
                if (!_players.IsTargetable(p)) continue;
                if (pick-- == 0) return p;
            }
            return -1;
        }
    }
}
