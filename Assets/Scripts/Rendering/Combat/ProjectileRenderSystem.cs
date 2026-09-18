using LastGround.Core.Tick;
using LastGround.Data.Combat;
using LastGround.Gameplay.Projectiles;
using UnityEngine;
using UnityEngine.Rendering;

namespace LastGround.Rendering.Combat
{
    /// <summary>
    /// Flying projectiles (TDD_01 §6.3): grenades as small lit bodies on their arc with a blinking fuse light, spit as
    /// glowing blobs. Each device draws its own <see cref="ProjectileTable"/> (host: simulated, client: replayed).
    /// Two instanced draws at most; no GameObjects, no allocation.
    /// </summary>
    public sealed class ProjectileRenderSystem : ITickable, System.IDisposable
    {
        static readonly int ColorId = Shader.PropertyToID("_FxColor");

        readonly ProjectileTable _table;
        readonly Mesh _body;
        readonly Material _bodyMaterial;
        readonly Material _glowMaterial;
        readonly Mesh _disc;
        readonly Camera _camera;
        readonly Matrix4x4[] _bodies;
        readonly Matrix4x4[] _glows;
        readonly Vector4[] _colors;
        readonly MaterialPropertyBlock _props = new MaterialPropertyBlock();
        float _time;

        public ProjectileRenderSystem(ProjectileTable table, Camera camera, Mesh body, Material bodyMaterial, Material glowAdditive)
        {
            _table = table;
            _camera = camera;
            _body = body;
            _bodyMaterial = bodyMaterial;
            _glowMaterial = glowAdditive;
            _disc = FxMeshes.Disc(16);
            _bodies = new Matrix4x4[table.Capacity];
            _glows = new Matrix4x4[table.Capacity];
            _colors = new Vector4[table.Capacity];
            _props.SetVectorArray(ColorId, _colors);
        }

        public void Tick(float dt, uint tick)
        {
            _time += dt;
            // Glow discs (flat, normal +Y) turn their face to the camera so blobs read as round.
            Quaternion facing = _camera != null ? Quaternion.FromToRotation(Vector3.up, -_camera.transform.forward) : Quaternion.identity;
            int bodies = 0, glows = 0;
            for (int id = 0; id < _table.Capacity; id++)
            {
                if (!_table.Active[id]) continue;
                ProjectileDefinition definition = _table.DefinitionOf(id);
                if (definition == null) continue;
                Unity.Mathematics.float2 ground = _table.PositionOf(id);
                var position = new Vector3(ground.x, _table.HeightOf(id), ground.y);
                if (definition.Motion == ProjectileMotion.Arc)
                {
                    float size = definition.Size;
                    _bodies[bodies++] = Matrix4x4.TRS(position, Quaternion.Euler(_time * 400f, _time * 250f, 0f), new Vector3(size, size * 1.2f, size));
                    bool landed = _table.Age[id] >= _table.Duration[id];
                    float blink = Mathf.Repeat(_time * (landed ? 10f : 4f), 1f) < 0.5f ? 1.6f : 0.3f;
                    _glows[glows] = Matrix4x4.TRS(position + Vector3.up * size, facing, Vector3.one * size * 1.6f);
                    _colors[glows++] = new Vector4(1f, 0.15f, 0.05f, blink);
                }
                else
                {
                    Color c = definition.Color;
                    float wobble = 1f + 0.15f * Mathf.Sin(_time * 25f + id);
                    _glows[glows] = Matrix4x4.TRS(position, facing, Vector3.one * definition.Size * 2.2f * wobble);
                    _colors[glows++] = new Vector4(c.r, c.g, c.b, 1.4f);
                }
            }
            var bounds = new Bounds(Vector3.zero, new Vector3(1000f, 30f, 1000f));
            if (bodies > 0 && _body != null && _bodyMaterial != null)
            {
                var rp = new RenderParams(_bodyMaterial) { shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false, worldBounds = bounds };
                Graphics.RenderMeshInstanced(rp, _body, 0, _bodies, bodies);
            }
            if (glows > 0 && _glowMaterial != null)
            {
                _props.SetVectorArray(ColorId, _colors);
                var rp = new RenderParams(_glowMaterial)
                {
                    matProps = _props, shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false, worldBounds = bounds,
                };
                Graphics.RenderMeshInstanced(rp, _disc, 0, _glows, glows);
            }
        }

        public void Dispose()
        {
            if (_disc != null) Object.Destroy(_disc);
        }
    }
}
