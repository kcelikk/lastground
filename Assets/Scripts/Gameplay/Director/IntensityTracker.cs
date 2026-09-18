using LastGround.Core.Events;
using LastGround.Data.Director;
using LastGround.Gameplay.Crowd;
using LastGround.Gameplay.Players;
using Unity.Mathematics;

namespace LastGround.Gameplay.Director
{
    /// <summary>
    /// Player stress (TDD_01 §9.3): damage taken (as a fraction of max health) adds stress that decays over time;
    /// zombies within reach and low health add instantaneous terms. Team intensity weights the most stressed player
    /// extra. Sampled by the director at 2 Hz; allocation-free.
    /// </summary>
    public sealed class IntensityTracker
    {
        readonly PlayerStateTable _players;
        readonly CrowdState _crowd;
        readonly DirectorProfile _profile;
        readonly float _maxHealth;
        readonly float[] _stress = new float[PlayerStateTable.Max];
        readonly float[] _intensity = new float[PlayerStateTable.Max];
        readonly int[] _nearby = new int[PlayerStateTable.Max];
        EventReader<PlayerHurt> _hurt;

        public IntensityTracker(PlayerStateTable players, CrowdState crowd, DirectorProfile profile, float maxHealth)
        {
            _players = players;
            _crowd = crowd;
            _profile = profile;
            _maxHealth = math.max(1f, maxHealth);
            _hurt = players.Hurt.CreateReader();
        }

        public float Team { get; private set; }
        public float Of(int player) => _intensity[player];

        public void Sample(float dt)
        {
            while (_players.Hurt.TryRead(ref _hurt, out PlayerHurt hurt))
                if ((uint)hurt.Player < PlayerStateTable.Max) _stress[hurt.Player] += hurt.Amount / _maxHealth * _profile.DamageStress;

            CountNearby();
            float max = 0f, sum = 0f;
            int count = 0;
            for (int p = 0; p < PlayerStateTable.Max; p++)
            {
                _stress[p] = math.max(0f, _stress[p] - _profile.StressDecay * dt);
                if (!_players.Active[p])
                {
                    _stress[p] = 0f;
                    _intensity[p] = 0f;
                    continue;
                }
                float crowd = math.min(1f, _nearby[p] / (float)math.max(1, _profile.NearbySaturation)) * _profile.NearbyStress;
                float lowHealth = (1f - math.saturate(_players.Health[p] / _maxHealth)) * _profile.LowHealthStress;
                float value = _players.Dead[p] ? 1f : math.saturate(_stress[p] + crowd + lowHealth);
                _intensity[p] = value;
                max = math.max(max, value);
                sum += value;
                count++;
            }
            Team = count == 0 ? 0f : 0.6f * max + 0.4f * (sum / count);
        }

        void CountNearby()
        {
            float r2 = _profile.NearbyRadius * _profile.NearbyRadius;
            for (int p = 0; p < PlayerStateTable.Max; p++) _nearby[p] = 0;
            for (int i = 0; i < _crowd.Capacity; i++)
            {
                if (!_crowd.AliveSlots[i]) continue;
                float x = _crowd.PosX[i], z = _crowd.PosZ[i];
                for (int p = 0; p < PlayerStateTable.Max; p++)
                {
                    if (!_players.Active[p]) continue;
                    float dx = x - _players.X[p], dz = z - _players.Z[p];
                    if (dx * dx + dz * dz < r2) _nearby[p]++;
                }
            }
        }
    }
}
