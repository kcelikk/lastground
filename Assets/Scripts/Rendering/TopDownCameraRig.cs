using LastGround.Core.Events;
using LastGround.Core.Input;
using LastGround.Core.Tick;
using LastGround.Data.Presentation;
using LastGround.Gameplay.Players;
using UnityEngine;

namespace LastGround.Rendering
{
    /// <summary>
    /// Top-down camera (TDD_01 §4): fixed pitch/yaw, critically damped follow of the local player, look-ahead in the
    /// aim direction (or movement when not aiming), and trauma-based shake when the local player is hurt.
    /// Presentation phase; no Update of its own. Shake can be scaled down in settings.
    /// </summary>
    public sealed class TopDownCameraRig : ITickable
    {
        readonly Camera _camera;
        readonly PlayerStateTable _table;
        readonly IPlayerInputSource _input;
        readonly CameraProfile _profile;
        readonly Quaternion _rotation;
        EventReader<PlayerHurt> _hurtReader;
        Vector3 _focus;
        Vector3 _velocity;
        Vector3 _lookAhead;
        Vector3 _lookAheadVelocity;
        float _trauma;
        float _time;
        bool _snapped;

        public TopDownCameraRig(Camera camera, PlayerStateTable table, IPlayerInputSource input, CameraProfile profile)
        {
            _camera = camera;
            _table = table;
            _input = input;
            _profile = profile;
            _rotation = Quaternion.Euler(profile.Pitch, 0f, 0f);
            _camera.fieldOfView = profile.FieldOfView;
            _camera.transform.rotation = _rotation;
            _hurtReader = table.Hurt.CreateReader();
        }

        /// <summary>0 = no shake, 1 = full (Settings).</summary>
        public float ShakeScale { get; set; } = 1f;

        public void Tick(float dt, uint tick)
        {
            _time += dt;
            int me = _table.Local.IsValid ? _table.Local.Value : -1;
            while (_table.Hurt.TryRead(ref _hurtReader, out PlayerHurt hurt))
                if (hurt.Player == me) _trauma = Mathf.Min(1f, _trauma + _profile.HurtTrauma * (hurt.Died ? 2f : 1f));
            if (me < 0 || !_table.Active[me]) return;

            var player = new Vector3(_table.X[me], 0f, _table.Z[me]);
            PlayerInputFrame frame = _input.Current;
            Vector3 ahead = Vector3.zero;
            if (_table.Life[me] != PlayerLife.Alive) ahead = Vector3.zero;
            else if (frame.AimActive) ahead = new Vector3(frame.AimX, 0f, frame.AimY) * _profile.AimLookAhead;
            else ahead = Vector3.ClampMagnitude(new Vector3(frame.MoveX, 0f, frame.MoveY), 1f) * _profile.MoveLookAhead;
            _lookAhead = Vector3.SmoothDamp(_lookAhead, ahead, ref _lookAheadVelocity, _profile.LookAheadSmoothTime, Mathf.Infinity, dt);

            Vector3 target = player + _lookAhead;
            if (!_snapped)
            {
                _focus = target;
                _snapped = true;
            }
            else
            {
                _focus = Vector3.SmoothDamp(_focus, target, ref _velocity, _profile.FollowSmoothTime, Mathf.Infinity, dt);
            }

            Vector3 position = _focus - _rotation * Vector3.forward * _profile.Distance;
            Quaternion rotation = _rotation;
            if (_trauma > 0f)
            {
                float shake = _trauma * _trauma * ShakeScale;
                float t = _time * _profile.ShakeFrequency;
                position += new Vector3(Noise(t, 0f), 0f, Noise(t, 7.3f)) * (_profile.MaxShakeOffset * shake);
                rotation *= Quaternion.Euler(Noise(t, 13.1f) * _profile.MaxShakeAngle * shake, 0f, Noise(t, 21.7f) * _profile.MaxShakeAngle * shake);
                _trauma = Mathf.Max(0f, _trauma - _profile.TraumaDecay * dt);
            }
            _camera.transform.SetPositionAndRotation(position, rotation);
        }

        static float Noise(float t, float seed) => Mathf.PerlinNoise(t, seed) * 2f - 1f;
    }
}
