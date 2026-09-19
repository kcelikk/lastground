using LastGround.Gameplay.Players;
using Unity.Mathematics;

namespace LastGround.Gameplay.Director
{
    /// <summary>
    /// Virtual hordes (TDD_02 §17.7, D-021): far groups live on the host as position + size per type and march on the
    /// team. Some Peak patterns start far away as a group (paid from the budget, a bit bigger, arriving later), and
    /// zombies left behind beyond the despawn distance join a group instead of vanishing. Once a group is within
    /// <c>MaterializeDistance</c> of a player it turns into ordinary zombies through the spawn queue (off screen, cap
    /// respected). A 1 Hz <see cref="HordeSummary"/> feeds the mini-map ring.
    /// </summary>
    public sealed partial class HordeDirector
    {
        const int MaxTypes = 16;
        const float SummaryInterval = 1f;

        struct VirtualGroup
        {
            public bool Active;
            public float2 Position;
            public int Count;
        }

        readonly VirtualGroup[] _groups = new VirtualGroup[HordeSummary.MaxGroups];
        readonly int[] _groupTypes = new int[HordeSummary.MaxGroups * MaxTypes];
        float _summaryTimer;

        /// <summary>Far groups for replication and the mini-map (null = no summary).</summary>
        public HordeSummary Summary { get; set; }

        public int VirtualLaunched { get; private set; }
        public int VirtualFolded { get; private set; }
        public int VirtualMaterialized { get; private set; }

        /// <summary>Zombies waiting in far groups.</summary>
        public int VirtualCount
        {
            get
            {
                int n = 0;
                for (int g = 0; g < _groups.Length; g++) if (_groups[g].Active) n += _groups[g].Count;
                return n;
            }
        }

        /// <summary>Peak packs and surrounds may start far away as a marching group (budget already counted by the caller).</summary>
        bool TryLaunchVirtual(HordePattern pattern, int size, int player)
        {
            if (_status.Threat < _profile.VirtualMinThreat || _rng.NextFloat() >= _profile.VirtualLaunchChance) return false;
            if (_status.State != DirectorState.Peak && _status.State != DirectorState.PeakHold) return false;
            if (pattern != HordePattern.Pack && pattern != HordePattern.Surround) return false;
            int g = FreeGroup();
            if (g < 0) return false;
            var centre = new float2(_players.X[player], _players.Z[player]);
            float start = _rng.Range(-math.PI, math.PI);
            for (int k = 0; k < 8; k++)
            {
                float angle = start + k * (math.PI / 4f);
                float distance = _rng.Range(_profile.VirtualLaunchDistance.x, _profile.VirtualLaunchDistance.y);
                float2 p = centre + new float2(math.cos(angle), math.sin(angle)) * distance;
                if (!_world.Nav.IsWalkable(p)) continue;
                int count = (int)math.round(size * _profile.VirtualSizeScale);
                _deck?.CountAlive(_world.Crowd);
                int players = math.max(1, CountTargetable());
                for (int i = 0; i < count; i++)
                {
                    byte type = 0;
                    if (_deck != null)
                    {
                        int card = _deck.Pick(_status.RunSeconds, count - i, players, ref _rng);
                        if (card >= 0) type = _deck.Card(card).Zombie.TypeIndex;
                    }
                    _groupTypes[g * MaxTypes + math.min((int)type, MaxTypes - 1)]++;
                }
                _groups[g] = new VirtualGroup { Active = true, Position = p, Count = count };
                VirtualLaunched++;
                return true;
            }
            return false;
        }

        /// <summary>A zombie beyond the despawn distance joins the nearest group (or starts one) instead of vanishing.</summary>
        bool Fold(float2 position, byte type)
        {
            int best = -1;
            float bestD2 = _profile.GroupMergeRadius * _profile.GroupMergeRadius;
            for (int g = 0; g < _groups.Length; g++)
            {
                if (!_groups[g].Active) continue;
                float d2 = math.distancesq(_groups[g].Position, position);
                if (d2 >= bestD2) continue;
                bestD2 = d2;
                best = g;
            }
            if (best < 0)
            {
                best = FreeGroup();
                if (best < 0) return false;
                _groups[best] = new VirtualGroup { Active = true, Position = position, Count = 0 };
            }
            _groups[best].Count++;
            _groupTypes[best * MaxTypes + math.min((int)type, MaxTypes - 1)]++;
            VirtualFolded++;
            return true;
        }

        void TickVirtual(float dt, int cap)
        {
            for (int g = 0; g < _groups.Length; g++)
            {
                if (!_groups[g].Active) continue;
                if (_groups[g].Count <= 0)
                {
                    _groups[g].Active = false;
                    continue;
                }
                int player = NearestTargetable(_groups[g].Position, out float distance);
                if (player < 0) continue;
                var goal = new float2(_players.X[player], _players.Z[player]);
                if (distance > _profile.MaterializeDistance)
                {
                    _groups[g].Position += (goal - _groups[g].Position) / math.max(distance, 1e-3f) * _profile.VirtualSpeed * dt;
                    continue;
                }
                Materialize(g, player, goal, cap);
            }

            _summaryTimer -= dt;
            if (Summary == null || _summaryTimer > 0f) return;
            _summaryTimer = SummaryInterval;
            Summary.Clear();
            for (int g = 0; g < _groups.Length; g++)
                if (_groups[g].Active && _groups[g].Count > 0) Summary.Add(_groups[g].Position.x, _groups[g].Position.y, _groups[g].Count);
            Summary.Commit();
        }

        /// <summary>Queues the group's zombies off screen on its side of the player, a few per tick, within the cap.</summary>
        void Materialize(int g, int player, float2 goal, int cap)
        {
            float2 from = _groups[g].Position - goal;
            float angle = math.atan2(from.y, from.x);
            float spread = math.radians(_profile.PackSpreadDeg);
            for (int n = 0; n < _profile.SpawnsPerTick && _groups[g].Count > 0; n++)
            {
                if (_world.Crowd.ActiveCount + _queueCount >= cap || _queueCount >= _queue.Length) return;
                byte type = TakeType(g);
                if (Plan(player, angle, spread, type, 0) == 0)
                {
                    _groupTypes[g * MaxTypes + type]++;
                    return;
                }
                _groups[g].Count--;
                VirtualMaterialized++;
            }
        }

        byte TakeType(int g)
        {
            for (int t = MaxTypes - 1; t >= 0; t--)
            {
                int index = g * MaxTypes + t;
                if (_groupTypes[index] <= 0) continue;
                _groupTypes[index]--;
                return (byte)t;
            }
            return 0;
        }

        int FreeGroup()
        {
            for (int g = 0; g < _groups.Length; g++)
            {
                if (_groups[g].Active) continue;
                for (int t = 0; t < MaxTypes; t++) _groupTypes[g * MaxTypes + t] = 0;
                return g;
            }
            return -1;
        }

        int NearestTargetable(float2 position, out float distance)
        {
            int best = -1;
            float bestD2 = float.MaxValue;
            for (int p = 0; p < PlayerStateTable.Max; p++)
            {
                if (!_players.IsTargetable(p)) continue;
                float d2 = math.distancesq(position, new float2(_players.X[p], _players.Z[p]));
                if (d2 >= bestD2) continue;
                bestD2 = d2;
                best = p;
            }
            distance = best >= 0 ? math.sqrt(bestD2) : 0f;
            return best;
        }
    }
}
