using LastGround.Core.Tick;
using LastGround.Gameplay.Players;
using UnityEngine;

namespace LastGround.Rendering
{
    /// <summary>
    /// M1 top-down follow: fixed pitch and yaw, damped follow of the local player (TDD_01 §4 defaults:
    /// pitch 55°, yaw 0°, vertical FOV 35°, 22 m). Replaced by TopDownCameraRig with look-ahead and shake in M4.
    /// </summary>
    public sealed class FollowCamera : ITickable
    {
        const float Pitch = 55f;
        const float Distance = 22f;
        const float FieldOfView = 35f;
        const float SmoothTime = 0.12f;

        readonly Camera _camera;
        readonly PlayerStateTable _table;
        readonly Quaternion _rotation = Quaternion.Euler(Pitch, 0f, 0f);
        Vector3 _focus;
        Vector3 _velocity;
        bool _snapped;

        public FollowCamera(Camera camera, PlayerStateTable table)
        {
            _camera = camera;
            _table = table;
            _camera.fieldOfView = FieldOfView;
            _camera.transform.rotation = _rotation;
        }

        public void Tick(float dt, uint tick)
        {
            if (!_table.Local.IsValid || !_table.Active[_table.Local.Value]) return;
            int i = _table.Local.Value;
            var target = new Vector3(_table.X[i], 0f, _table.Z[i]);
            if (!_snapped)
            {
                _focus = target;
                _snapped = true;
            }
            else
            {
                _focus = Vector3.SmoothDamp(_focus, target, ref _velocity, SmoothTime, Mathf.Infinity, dt);
            }
            _camera.transform.SetPositionAndRotation(_focus - _rotation * Vector3.forward * Distance, _rotation);
        }
    }
}
