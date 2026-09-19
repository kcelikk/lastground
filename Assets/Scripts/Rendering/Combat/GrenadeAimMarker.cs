using LastGround.Core.Tick;
using LastGround.Gameplay.Players;
using UnityEngine;
using UnityEngine.Rendering;

namespace LastGround.Rendering.Combat
{
    /// <summary>
    /// Where a dragged grenade will land (TDD_01 §3.5 "hold and drag: aim"): a pulsing ring of the blast radius on the
    /// ground at the local player's position plus the drag offset. One instanced draw while aiming.
    /// </summary>
    public sealed class GrenadeAimMarker : ITickable, System.IDisposable
    {
        static readonly int ColorId = Shader.PropertyToID("_FxColor");

        readonly PlayerStateTable _players;
        readonly System.Func<Vector2?> _aim;
        readonly float _radius;
        readonly Material _additive;
        readonly Mesh _ring;
        readonly Matrix4x4[] _matrix = new Matrix4x4[1];
        readonly Vector4[] _color = new Vector4[1];
        readonly MaterialPropertyBlock _props = new MaterialPropertyBlock();
        float _time;

        /// <param name="aim">Current drag offset in world XZ, or null when not aiming.</param>
        public GrenadeAimMarker(PlayerStateTable players, System.Func<Vector2?> aim, float radius, Material additive)
        {
            _players = players;
            _aim = aim;
            _radius = radius;
            _additive = additive;
            _ring = FxMeshes.Ring();
            _props.SetVectorArray(ColorId, _color);
        }

        public void Tick(float dt, uint tick)
        {
            _time += dt;
            Vector2? offset = _aim();
            int me = _players.Local.IsValid ? _players.Local.Value : -1;
            if (offset == null || me < 0 || _additive == null) return;
            var at = new Vector3(_players.X[me] + offset.Value.x, 0.06f, _players.Z[me] + offset.Value.y);
            _matrix[0] = Matrix4x4.TRS(at, Quaternion.identity, new Vector3(_radius, 1f, _radius));
            _color[0] = new Vector4(1f, 0.55f, 0.15f, 1.2f + 0.4f * Mathf.Sin(_time * 8f));
            _props.SetVectorArray(ColorId, _color);
            var rp = new RenderParams(_additive)
            {
                matProps = _props,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                worldBounds = new Bounds(at, new Vector3(_radius * 3f, 2f, _radius * 3f)),
            };
            Graphics.RenderMeshInstanced(rp, _ring, 0, _matrix, 1);
        }

        public void Dispose()
        {
            if (_ring != null) Object.Destroy(_ring);
        }
    }
}
