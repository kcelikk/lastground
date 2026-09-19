using LastGround.Core.Tick;
using LastGround.Data.Map;
using LastGround.Gameplay.Objectives;
using UnityEngine;
using UnityEngine.Rendering;

namespace LastGround.Rendering.Objectives
{
    /// <summary>
    /// Glowing outline of the active objective zone on the ground (TDD_01 §12.4): four additive edge strips, pulsing
    /// amber while active, green when completed. One instanced draw, no allocation.
    /// </summary>
    public sealed class ZoneMarker : ITickable, System.IDisposable
    {
        const float Width = 0.35f;
        const float Height = 0.05f;
        static readonly int ColorId = Shader.PropertyToID("_FxColor");

        readonly ObjectiveState _state;
        readonly MapZoneSet _zones;
        readonly Material _material;
        readonly Mesh _quad;
        readonly Matrix4x4[] _matrices = new Matrix4x4[4];
        readonly Vector4[] _colors = new Vector4[4];
        readonly MaterialPropertyBlock _props = new MaterialPropertyBlock();
        readonly Mesh _ring;
        float _time;

        public ZoneMarker(ObjectiveState state, MapZoneSet zones, Material additive)
        {
            _state = state;
            _zones = zones;
            _material = additive;
            _quad = CreateQuad();
            _ring = Combat.FxMeshes.Ring(0.08f);
            _props.SetVectorArray(ColorId, _colors);
        }

        public void Tick(float dt, uint tick)
        {
            _time += dt;
            if (_state.Phase == ObjectivePhase.None || _zones == null || (uint)_state.Zone >= (uint)_zones.Zones.Length) return;
            if (_state.HasAnchor)
            {
                DrawRing();
                return;
            }
            MapZoneSet.Zone zone = _zones.Zones[_state.Zone];
            float cx = zone.Center.x, cz = zone.Center.y, hx = zone.HalfSize.x, hz = zone.HalfSize.y;
            _matrices[0] = Edge(new Vector3(cx - hx, Height, cz + hz), new Vector3(cx + hx, Height, cz + hz));
            _matrices[1] = Edge(new Vector3(cx + hx, Height, cz + hz), new Vector3(cx + hx, Height, cz - hz));
            _matrices[2] = Edge(new Vector3(cx + hx, Height, cz - hz), new Vector3(cx - hx, Height, cz - hz));
            _matrices[3] = Edge(new Vector3(cx - hx, Height, cz - hz), new Vector3(cx - hx, Height, cz + hz));
            bool done = _state.Phase == ObjectivePhase.Completed;
            float pulse = 0.7f + 0.3f * Mathf.Sin(_time * 3f);
            Vector4 color = done ? new Vector4(0.3f, 1f, 0.4f, 1.6f) : new Vector4(1f, 0.65f, 0.15f, 1.2f * pulse);
            for (int i = 0; i < 4; i++) _colors[i] = color;
            _props.SetVectorArray(ColorId, _colors);
            var rp = new RenderParams(_material)
            {
                matProps = _props,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                worldBounds = new Bounds(new Vector3(cx, 0f, cz), new Vector3(hx * 2f + 2f, 2f, hz * 2f + 2f)),
            };
            Graphics.RenderMeshInstanced(rp, _quad, 0, _matrices, 4);
        }

        /// <summary>Event anchors (crate, generator, signal, hunted elite): a pulsing ring of the event radius.</summary>
        void DrawRing()
        {
            bool done = _state.Phase == ObjectivePhase.Completed;
            float radius = Mathf.Max(1.2f, _state.Radius);
            _matrices[0] = Matrix4x4.TRS(new Vector3(_state.AnchorX, Height, _state.AnchorZ), Quaternion.identity, new Vector3(radius, 1f, radius));
            float pulse = 0.7f + 0.3f * Mathf.Sin(_time * 4f);
            _colors[0] = done ? new Vector4(0.3f, 1f, 0.4f, 1.6f) : _state.Phase == ObjectivePhase.Announced
                ? new Vector4(1f, 0.35f, 0.2f, 1.4f * pulse) : new Vector4(1f, 0.65f, 0.15f, 1.4f * pulse);
            _props.SetVectorArray(ColorId, _colors);
            var rp = new RenderParams(_material)
            {
                matProps = _props,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                worldBounds = new Bounds(new Vector3(_state.AnchorX, 0f, _state.AnchorZ), new Vector3(radius * 3f, 2f, radius * 3f)),
            };
            Graphics.RenderMeshInstanced(rp, _ring, 0, _matrices, 1);
        }

        public void Dispose()
        {
            if (_ring != null) Object.Destroy(_ring);
            if (_quad != null) Object.Destroy(_quad);
        }

        static Matrix4x4 Edge(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            float length = d.magnitude;
            return Matrix4x4.TRS(from, Quaternion.LookRotation(d / Mathf.Max(1e-3f, length), Vector3.up), new Vector3(Width, 1f, length));
        }

        static Mesh CreateQuad()
        {
            var mesh = new Mesh { name = "ZoneEdgeQuad" };
            mesh.vertices = new[] { new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0f, 0f), new Vector3(0.5f, 0f, 1f), new Vector3(-0.5f, 0f, 1f) };
            // Constant brightness along the strip: uv.x fixed at the bright middle.
            mesh.uv = new[] { new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0f) };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.bounds = new Bounds(new Vector3(0f, 0f, 0.5f), new Vector3(1f, 0.1f, 1f));
            return mesh;
        }
    }
}
