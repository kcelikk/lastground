using LastGround.Core.Tick;
using LastGround.Gameplay.Extraction;
using LastGround.Rendering.Combat;
using UnityEngine;
using UnityEngine.Rendering;

namespace LastGround.Rendering.Boss
{
    /// <summary>
    /// Landing zone on the ground (TDD_01 §0.x cold blue extraction light, D-021 without a helicopter model): a pulsing
    /// blue ring of the zone radius over a dim blue light pool, four flares flickering on its edge, landing lights
    /// chasing round it, and — while the team holds the zone — the shadow of rotor blades sweeping over it, growing as
    /// the helicopter "comes down" with the hold progress. No allocation per frame.
    /// </summary>
    public sealed class ExtractionZoneView : ITickable, System.IDisposable
    {
        const float Height = 0.07f;
        const int LandingLights = 8;
        static readonly int ColorId = Shader.PropertyToID("_FxColor");
        static readonly Color Blue = new Color(0.35f, 0.65f, 1f);
        static readonly Color Flare = new Color(0.6f, 0.85f, 1f);

        readonly ExtractionState _state;
        readonly Material _additive;
        readonly Material _shadowMaterial;
        readonly Mesh _disc;
        readonly Mesh _ring;
        readonly Mesh _rotor;
        readonly Matrix4x4[] _matrix = new Matrix4x4[1];
        readonly Vector4[] _color = new Vector4[1];
        readonly MaterialPropertyBlock _props = new MaterialPropertyBlock();
        float _time;
        float _rotorAngle;

        /// <param name="shadow">Alpha-blended FX material (vertex colour) for the rotor shadow.</param>
        public ExtractionZoneView(ExtractionState state, Material additive, Material shadow)
        {
            _state = state;
            _additive = additive;
            _shadowMaterial = shadow;
            _disc = FxMeshes.Disc();
            _ring = FxMeshes.Ring(0.06f);
            _rotor = Rotor();
        }

        public void Tick(float dt, uint tick)
        {
            _time += dt;
            if (_state.Phase != ExtractionPhase.Open && _state.Phase != ExtractionPhase.Extracted) return;
            var centre = new Vector3(_state.X, Height, _state.Z);
            float r = _state.Radius;
            float pulse = 0.75f + 0.25f * Mathf.Sin(_time * 3f);

            Emit(_disc, centre, r * 1.3f, Blue, 0.18f);
            Emit(_ring, centre, r, Blue, (_state.Holding ? 1.6f : 1.1f) * pulse);
            // Progress: an inner ring grows from the centre towards the edge.
            if (_state.Progress > 0f) Emit(_ring, centre, Mathf.Max(0.3f, r * _state.Progress), Flare, 1.4f);

            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f + Mathf.PI * 0.25f;
                var p = centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                float flicker = 0.7f + 0.3f * Mathf.PerlinNoise(_time * 9f, i * 3.1f);
                Emit(_disc, p, 1.1f, Flare, 1.6f * flicker);
            }
            for (int i = 0; i < LandingLights; i++)
            {
                float a = i * Mathf.PI * 2f / LandingLights;
                float chase = Mathf.Repeat(_time * 1.5f - i / (float)LandingLights, 1f);
                var p = centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (r * 0.8f);
                Emit(_disc, p, 0.35f, Blue, chase > 0.85f ? 2f : 0.25f);
            }

            if (!_state.Holding && _state.Phase != ExtractionPhase.Extracted) return;
            if (_shadowMaterial == null) return;
            // Rotor shadow: spins faster and grows as the hold completes.
            float progress = _state.Phase == ExtractionPhase.Extracted ? 1f : _state.Progress;
            _rotorAngle += dt * (400f + 500f * progress);
            float size = Mathf.Lerp(5f, 7.5f, progress);
            _matrix[0] = Matrix4x4.TRS(centre + Vector3.up * 0.01f, Quaternion.Euler(0f, _rotorAngle, 0f), new Vector3(size, 1f, size));
            var rp = new RenderParams(_shadowMaterial) { shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false };
            Graphics.RenderMeshInstanced(rp, _rotor, 0, _matrix, 1);
        }

        void Emit(Mesh mesh, Vector3 position, float radius, Color color, float intensity)
        {
            _matrix[0] = Matrix4x4.TRS(position, Quaternion.identity, new Vector3(radius, 1f, radius));
            _color[0] = new Vector4(color.r, color.g, color.b, intensity);
            _props.SetVectorArray(ColorId, _color);
            var rp = new RenderParams(_additive)
            {
                matProps = _props,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                worldBounds = new Bounds(position, new Vector3(radius * 2f + 1f, 2f, radius * 2f + 1f)),
            };
            Graphics.RenderMeshInstanced(rp, mesh, 0, _matrix, 1);
        }

        public void Dispose()
        {
            if (_disc != null) Object.Destroy(_disc);
            if (_ring != null) Object.Destroy(_ring);
            if (_rotor != null) Object.Destroy(_rotor);
        }

        /// <summary>Four soft blades (two crossed strips, unit radius) with a dark vertex colour for the alpha-blended FX shader.</summary>
        static Mesh Rotor()
        {
            var vertices = new Vector3[8];
            var uv = new Vector2[8];
            var colors = new Color[8];
            var triangles = new int[12];
            for (int b = 0; b < 2; b++)
            {
                Quaternion q = Quaternion.Euler(0f, b * 90f, 0f);
                int v = b * 4;
                vertices[v] = q * new Vector3(-0.06f, 0f, -1f);
                vertices[v + 1] = q * new Vector3(0.06f, 0f, -1f);
                vertices[v + 2] = q * new Vector3(0.06f, 0f, 1f);
                vertices[v + 3] = q * new Vector3(-0.06f, 0f, 1f);
                uv[v] = new Vector2(0f, 0f);
                uv[v + 1] = new Vector2(1f, 0f);
                uv[v + 2] = new Vector2(1f, 1f);
                uv[v + 3] = new Vector2(0f, 1f);
                for (int k = 0; k < 4; k++) colors[v + k] = new Color(0f, 0f, 0f, 0.7f);
                int t = b * 6;
                triangles[t] = v; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
                triangles[t + 3] = v; triangles[t + 4] = v + 3; triangles[t + 5] = v + 2;
            }
            var mesh = new Mesh { name = "RotorShadow", vertices = vertices, uv = uv, colors = colors, triangles = triangles };
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(2f, 0.1f, 2f));
            return mesh;
        }
    }
}
