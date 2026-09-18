using LastGround.Core.Input;
using LastGround.Core.Tick;
using LastGround.Gameplay.Navigation;
using Unity.Mathematics;
using UnityEngine;

namespace LastGround.Gameplay.Players
{
    /// <summary>
    /// Kinematic movement of the local player (client-authoritative, validated by the host — TDD_02 §16).
    /// M1: straight movement inside square bounds; collisions and zombie slow-down arrive in M3/M4.
    /// </summary>
    public sealed class PlayerMotor : ITickable
    {
        public const float MoveSpeed = 5f;

        readonly PlayerStateTable _table;
        readonly IPlayerInputSource _input;
        readonly float _halfBounds;
        readonly NavGrid _nav;
        float _x;
        float _z;
        float _yaw;

        public PlayerMotor(PlayerStateTable table, IPlayerInputSource input, float boundsSize, float startX, float startZ, NavGrid nav = null)
        {
            _table = table;
            _input = input;
            _nav = nav;
            _halfBounds = boundsSize * 0.5f;
            _x = startX;
            _z = startZ;
            _table.SetLocal(_x, _z, _yaw, 0f, 0f);
        }

        public void Tick(float dt, uint tick)
        {
            PlayerInputFrame frame = _input.Current;
            float mx = frame.MoveX;
            float mz = frame.MoveY;
            float magnitude = Mathf.Sqrt(mx * mx + mz * mz);
            if (magnitude > 1f)
            {
                mx /= magnitude;
                mz /= magnitude;
            }

            float vx = mx * MoveSpeed;
            float vz = mz * MoveSpeed;
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
            if (magnitude > 0.1f)
                _yaw = Mathf.Atan2(mx, mz) * Mathf.Rad2Deg;

            _table.SetLocal(_x, _z, _yaw, vx, vz);
        }
    }
}
