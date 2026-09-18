using LastGround.Core.Tick;
using LastGround.Gameplay.Crowd;
using UnityEngine;
using UnityEngine.Rendering;

namespace LastGround.Rendering
{
    /// <summary>
    /// Draws a crowd with GPU instancing (one draw per 1023 instances), no GameObjects per entity.
    /// M1 placeholder look (capsules); VAT animation, LODs and culling come in M2 (TDD_02 §21.3).
    /// </summary>
    public sealed class CrowdRenderer : ITickable
    {
        const int BatchSize = 1023;

        readonly ICrowdRenderSource _source;
        readonly Mesh _mesh;
        readonly RenderParams _params;
        readonly Matrix4x4[] _matrices;
        readonly Vector3 _scale;
        readonly float _heightOffset;

        public CrowdRenderer(ICrowdRenderSource source, Mesh mesh, Material material, float height)
        {
            _source = source;
            _mesh = mesh;
            _matrices = new Matrix4x4[source.Capacity];
            _scale = new Vector3(0.7f, height * 0.5f, 0.7f);
            _heightOffset = height * 0.5f;
            _params = new RenderParams(material)
            {
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                worldBounds = new Bounds(Vector3.zero, new Vector3(1000f, 100f, 1000f)),
            };
        }

        public int DrawnLastFrame { get; private set; }

        public void Tick(float dt, uint tick)
        {
            bool[] alive = _source.Alive;
            float[] xs = _source.X;
            float[] zs = _source.Z;
            float[] yaws = _source.Yaw;
            int count = 0;
            for (int i = 0; i < _source.Capacity; i++)
            {
                if (!alive[i]) continue;
                _matrices[count++] = Matrix4x4.TRS(
                    new Vector3(xs[i], _heightOffset, zs[i]),
                    Quaternion.Euler(0f, yaws[i], 0f),
                    _scale);
            }

            for (int start = 0; start < count; start += BatchSize)
                Graphics.RenderMeshInstanced(_params, _mesh, 0, _matrices, Mathf.Min(BatchSize, count - start), start);
            DrawnLastFrame = count;
        }
    }
}
