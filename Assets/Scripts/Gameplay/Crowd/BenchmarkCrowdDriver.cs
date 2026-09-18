using LastGround.Core.Random;
using LastGround.Core.Tick;
using UnityEngine;

namespace LastGround.Gameplay.Crowd
{
    /// <summary>
    /// Rendering benchmark load (TDD_02 §22.6): keeps <see cref="TargetCount"/> entities inside the camera's view
    /// around the player (peak-horde framing, TDD_01 §0.1), wandering, with those close to the player attacking,
    /// and a steady death rate so corpses and blood are part of the measured cost.
    /// </summary>
    public sealed class BenchmarkCrowdDriver : ITickable
    {
        const byte AnimWalk = 1;
        const byte AnimAttack = 3;
        const float AttackRange = 3.5f;
        const float PlayerClearance = 2.5f;

        readonly CrowdState _state;
        readonly float _halfWidth;
        readonly float _halfDepth;
        readonly float[] _speed;
        readonly float[] _turn;
        DeterministicRandom _rng;
        float _deathTimer;

        public BenchmarkCrowdDriver(CrowdState state, uint seed, float width = 30f, float depth = 16f)
        {
            _state = state;
            _halfWidth = width * 0.5f;
            _halfDepth = depth * 0.5f;
            _speed = new float[state.Capacity];
            _turn = new float[state.Capacity];
            _rng = DeterministicRandom.ForStream(seed, "benchmark-crowd");
        }

        public int TargetCount { get; set; }
        public float DeathsPerSecond { get; set; } = 2f;

        public void Tick(float dt, uint tick)
        {
            while (_state.ActiveCount < TargetCount && Spawn()) { }
            for (int i = _state.Capacity - 1; i >= 0 && _state.ActiveCount > TargetCount; i--)
                if (_state.AliveSlots[i]) _state.Despawn(i);

            for (int i = 0; i < _state.Capacity; i++)
            {
                if (!_state.AliveSlots[i]) continue;
                float x = _state.PosX[i], z = _state.PosZ[i];
                float distance = Mathf.Sqrt(x * x + z * z);
                if (distance < AttackRange)
                {
                    _state.Anim[i] = AnimAttack;
                    _state.Heading[i] = Mathf.Atan2(-x, -z) * Mathf.Rad2Deg;
                    continue;
                }

                _state.Anim[i] = AnimWalk;
                _turn[i] -= dt;
                if (_turn[i] <= 0f)
                {
                    // Drift towards the player like a horde, with some randomness.
                    float toPlayer = Mathf.Atan2(-x, -z) * Mathf.Rad2Deg;
                    _state.Heading[i] = toPlayer + _rng.Range(-70f, 70f);
                    _turn[i] = _rng.Range(1.5f, 4f);
                }
                float r = _state.Heading[i] * Mathf.Deg2Rad;
                float nx = x + Mathf.Sin(r) * _speed[i] * dt;
                float nz = z + Mathf.Cos(r) * _speed[i] * dt;
                if (Mathf.Abs(nx) > _halfWidth || Mathf.Abs(nz) > _halfDepth || nx * nx + nz * nz < PlayerClearance * PlayerClearance)
                {
                    _turn[i] = 0f;
                    continue;
                }
                _state.PosX[i] = nx;
                _state.PosZ[i] = nz;
            }

            if (_state.ActiveCount == 0) return;
            _deathTimer += dt * DeathsPerSecond;
            while (_deathTimer >= 1f)
            {
                _deathTimer -= 1f;
                int slot = _rng.Range(0, _state.Capacity);
                for (int probe = 0; probe < _state.Capacity && !_state.AliveSlots[slot]; probe++)
                    slot = (slot + 1) % _state.Capacity;
                if (_state.AliveSlots[slot])
                {
                    _state.Despawn(slot, died: true);
                    Spawn();
                }
            }
        }

        bool Spawn()
        {
            float x, z;
            do
            {
                x = _rng.Range(-_halfWidth, _halfWidth);
                z = _rng.Range(-_halfDepth, _halfDepth);
            } while (x * x + z * z < 16f);

            int slot = _state.Spawn(0, x, z, _rng.Range(0f, 360f));
            if (slot < 0) return false;
            _state.Anim[slot] = AnimWalk;
            _speed[slot] = _rng.Range(0.6f, 1.3f);
            _turn[slot] = _rng.Range(0f, 3f);
            return true;
        }
    }
}
