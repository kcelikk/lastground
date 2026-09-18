using LastGround.Core.Random;
using LastGround.Core.Tick;
using UnityEngine;

namespace LastGround.Gameplay.Crowd
{
    /// <summary>
    /// M1 stand-in for the horde: N entities wandering in a square so that some are near, some mid and some far
    /// from players, with periodic respawns to exercise enter/exit and slot generations. Replaced by ZombieWorld in M3.
    /// </summary>
    public sealed class DummyCrowdSim : ITickable
    {
        public const byte AnimIdle = 0;
        public const byte AnimWalk = 1;

        readonly CrowdState _state;
        readonly float _halfSize;
        readonly float[] _speed;
        readonly float[] _turnTimer;
        DeterministicRandom _rng;
        float _respawnTimer;

        public DummyCrowdSim(CrowdState state, int count, float areaSize, uint seed)
        {
            _state = state;
            _halfSize = areaSize * 0.5f;
            _speed = new float[state.Capacity];
            _turnTimer = new float[state.Capacity];
            _rng = DeterministicRandom.ForStream(seed, "dummy-crowd");
            for (int i = 0; i < count; i++) SpawnRandom();
        }

        public CrowdState State => _state;

        public void Tick(float dt, uint tick)
        {
            for (int i = 0; i < _state.Capacity; i++)
            {
                if (!_state.AliveSlots[i]) continue;

                _turnTimer[i] -= dt;
                if (_turnTimer[i] <= 0f)
                {
                    _state.Heading[i] = _rng.Range(0f, 360f);
                    _turnTimer[i] = _rng.Range(2f, 5f);
                }

                float radians = _state.Heading[i] * Mathf.Deg2Rad;
                float x = _state.PosX[i] + Mathf.Sin(radians) * _speed[i] * dt;
                float z = _state.PosZ[i] + Mathf.Cos(radians) * _speed[i] * dt;
                if (x < -_halfSize || x > _halfSize || z < -_halfSize || z > _halfSize)
                {
                    // Turn back towards the centre instead of leaving the area.
                    _state.Heading[i] = Mathf.Atan2(-_state.PosX[i], -_state.PosZ[i]) * Mathf.Rad2Deg;
                    continue;
                }
                _state.PosX[i] = x;
                _state.PosZ[i] = z;
            }

            _respawnTimer -= dt;
            if (_respawnTimer <= 0f)
            {
                _respawnTimer = 0.5f;
                int slot = _rng.Range(0, _state.Capacity);
                if (_state.AliveSlots[slot])
                {
                    _state.Despawn(slot);
                    SpawnRandom();
                }
            }
        }

        void SpawnRandom()
        {
            int slot = _state.Spawn(0, _rng.Range(-_halfSize, _halfSize), _rng.Range(-_halfSize, _halfSize), _rng.Range(0f, 360f));
            if (slot < 0) return;
            _state.Anim[slot] = AnimWalk;
            _speed[slot] = _rng.Range(1f, 2.2f);
            _turnTimer[slot] = _rng.Range(0f, 4f);
        }
    }
}
