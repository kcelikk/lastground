using LastGround.Core.Random;
using LastGround.Data.Director;
using LastGround.Gameplay.Navigation;
using LastGround.Gameplay.Players;
using Unity.Mathematics;

namespace LastGround.Gameplay.Director
{
    /// <summary>
    /// Where a zombie may appear (TDD_01 §9.7): 22–45 m from the chosen player in the requested sector, walkable,
    /// reachable on that player's flow field, not closer than the minimum distance to any player, and outside
    /// every player's camera footprint — nobody sees a zombie pop in.
    /// </summary>
    public sealed class SpawnLocator
    {
        const int SectorAttempts = 8;
        const int AnyAttempts = 6;

        readonly NavGrid _nav;
        readonly FlowFieldSet _flow;
        readonly PlayerStateTable _players;
        readonly DirectorProfile _profile;
        readonly CameraFootprint _footprint;

        public SpawnLocator(NavGrid nav, FlowFieldSet flow, PlayerStateTable players, DirectorProfile profile, CameraFootprint footprint)
        {
            _nav = nav;
            _flow = flow;
            _players = players;
            _profile = profile;
            _footprint = footprint;
        }

        public int Rejected { get; private set; }

        /// <summary>Finds a spawn point around <paramref name="player"/> near <paramref name="angle"/> (radians, ±spread).</summary>
        public bool TryFind(int player, float angle, float spread, ref DeterministicRandom rng, out float2 position)
        {
            var centre = new float2(_players.X[player], _players.Z[player]);
            for (int attempt = 0; attempt < SectorAttempts + AnyAttempts; attempt++)
            {
                float a = attempt < SectorAttempts ? angle + rng.Range(-spread, spread) : rng.Range(-math.PI, math.PI);
                float distance = rng.Range(_profile.MinSpawnDistance, _profile.MaxSpawnDistance);
                position = centre + new float2(math.cos(a), math.sin(a)) * distance;
                if (IsValid(player, position)) return true;
                Rejected++;
            }
            position = float2.zero;
            return false;
        }

        public bool IsValid(int player, float2 position)
        {
            if (!_nav.IsWalkable(position)) return false;
            if (_flow.IsValid(player) && _flow.DistanceAt(player, position) == ushort.MaxValue) return false;
            float min2 = _profile.MinSpawnDistance * _profile.MinSpawnDistance;
            for (int p = 0; p < PlayerStateTable.Max; p++)
            {
                if (!_players.Active[p]) continue;
                var other = new float2(_players.X[p], _players.Z[p]);
                if (math.distancesq(other, position) < min2) return false;
                if (_footprint.Contains(other, position)) return false;
            }
            return true;
        }
    }
}
