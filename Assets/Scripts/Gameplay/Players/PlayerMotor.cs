using LastGround.Core.Input;
using LastGround.Core.Tick;
using LastGround.Data.Players;
using LastGround.Gameplay.Crowd;
using LastGround.Gameplay.Navigation;
using Unity.Mathematics;
using UnityEngine;

namespace LastGround.Gameplay.Players
{
    /// <summary>
    /// Kinematic movement of the local player (client-authoritative, validated by the host — TDD_02 §16).
    /// Slides along walls, is slowed by touching zombies (−7 % each, at most −35 %, TDD_01 §8.4) but never blocked,
    /// faces the aim direction while aiming, crawls while downed and stands still while dead.
    /// </summary>
    public sealed class PlayerMotor : ITickable
    {
        readonly PlayerStateTable _table;
        readonly IPlayerInputSource _input;
        readonly PlayerDefinition _definition;
        readonly ICrowdRenderSource _crowd;
        readonly float _halfBounds;
        readonly NavGrid _nav;
        float _x;
        float _z;
        float _yaw;

        public PlayerMotor(PlayerStateTable table, IPlayerInputSource input, PlayerDefinition definition, float boundsSize,
            float startX, float startZ, NavGrid nav = null, ICrowdRenderSource crowd = null)
        {
            _table = table;
            _input = input;
            _definition = definition;
            _nav = nav;
            _crowd = crowd;
            _halfBounds = boundsSize * 0.5f;
            _x = startX;
            _z = startZ;
            _table.SetLocal(_x, _z, _yaw, 0f, 0f);
        }

        /// <summary>Speed multiplier from touching zombies last frame (1 = free).</summary>
        public float ContactFactor { get; private set; } = 1f;

        public void Tick(float dt, uint tick)
        {
            int me = _table.Local.IsValid ? _table.Local.Value : -1;
            if (me >= 0 && _table.Life[me] == PlayerLife.Dead)
            {
                _table.SetLocal(_x, _z, _yaw, 0f, 0f);
                return;
            }
            bool downed = me >= 0 && _table.Life[me] == PlayerLife.Downed;

            PlayerInputFrame frame = _input.Current;
            float mx = frame.MoveX;
            float mz = frame.MoveY;
            float magnitude = Mathf.Sqrt(mx * mx + mz * mz);
            if (magnitude > 1f)
            {
                mx /= magnitude;
                mz /= magnitude;
            }

            ContactFactor = 1f - Mathf.Min(_definition.MaxContactSlow, CountContacts() * _definition.SlowPerContact);
            float speed = _definition.MoveSpeed * ContactFactor * (downed ? _definition.DownedSpeedFactor : 1f);
            float vx = mx * speed;
            float vz = mz * speed;
            float nx = Mathf.Clamp(_x + vx * dt, -_halfBounds, _halfBounds);
            float nz = Mathf.Clamp(_z + vz * dt, -_halfBounds, _halfBounds);
            if (_nav != null && !_nav.IsWalkable(new float2(nx, nz)))
            {
                // Slide along walls like the zombies do.
                if (_nav.IsWalkable(new float2(nx, _z))) nz = _z;
                else if (_nav.IsWalkable(new float2(_x, nz))) nx = _x;
                else { nx = _x; nz = _z; }
            }
            _x = nx;
            _z = nz;
            if (!downed && frame.AimActive && (frame.AimX != 0f || frame.AimY != 0f))
                _yaw = Mathf.Atan2(frame.AimX, frame.AimY) * Mathf.Rad2Deg;
            else if (magnitude > 0.1f)
                _yaw = Mathf.Atan2(mx, mz) * Mathf.Rad2Deg;

            _table.SetLocal(_x, _z, _yaw, vx, vz);
        }

        int CountContacts()
        {
            if (_crowd == null) return 0;
            float r2 = _definition.ContactRadius * _definition.ContactRadius;
            bool[] alive = _crowd.Alive;
            float[] xs = _crowd.X;
            float[] zs = _crowd.Z;
            int count = 0;
            for (int i = 0; i < _crowd.Capacity; i++)
            {
                if (!alive[i]) continue;
                float dx = xs[i] - _x;
                float dz = zs[i] - _z;
                if (dx * dx + dz * dz < r2) count++;
            }
            return count;
        }
    }
}
