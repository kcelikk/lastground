using LastGround.Core.Events;
using LastGround.Core.Net;
using LastGround.Core.Pooling;
using LastGround.Core.Tick;
using LastGround.Gameplay.Combat;
using UnityEngine;
using UnityEngine.Rendering;

namespace LastGround.Rendering.Combat
{
    /// <summary>
    /// Tracers and muzzle flashes (TDD_01 §6.5, TDD_02 §20.2): instanced stretched quads in a ring buffer, one draw
    /// call, no GameObjects. Tracers live 0.07 s, flashes 0.05 s; the preset's TracerCap bounds both.
    /// </summary>
    public sealed class TracerSystem : ITickable, System.IDisposable
    {
        const float TracerLife = 0.07f;
        const float FlashLife = 0.05f;
        const float TracerWidth = 0.07f;
        const float FlashLength = 0.9f;
        const float FlashWidth = 0.5f;
        const float Height = 1.15f;
        static readonly int ColorId = Shader.PropertyToID("_FxColor");
        static readonly Vector4 TracerColor = new Vector4(1f, 0.82f, 0.45f, 1.6f);
        static readonly Vector4 FlashColor = new Vector4(1f, 0.7f, 0.3f, 2.4f);

        struct Streak
        {
            public Matrix4x4 Matrix;
            public float BornAt;
            public float Life;
            public Vector4 Color;
        }

        readonly IGameEventStream<ShotFired> _shots;
        readonly RingBuffer<Streak> _streaks;
        readonly Material _material;
        readonly Mesh _quad;
        readonly Matrix4x4[] _matrices;
        readonly Vector4[] _colors;
        readonly MaterialPropertyBlock _props = new MaterialPropertyBlock();
        EventReader<ShotFired> _reader;
        float _time;

        public TracerSystem(IGameEventStream<ShotFired> shots, Material material, int cap)
        {
            _shots = shots;
            _reader = shots.CreateReader();
            _material = material;
            cap = Mathf.Max(8, cap);
            _streaks = new RingBuffer<Streak>(cap);
            _matrices = new Matrix4x4[cap];
            _colors = new Vector4[cap];
            _props.SetVectorArray(ColorId, _colors);
            _quad = CreateQuad();
        }

        public void Tick(float dt, uint tick)
        {
            _time += dt;
            while (_shots.TryRead(ref _reader, out ShotFired shot)) Add(shot);
            while (_streaks.Count > 0 && _time - _streaks[0].BornAt > _streaks[0].Life) _streaks.TryPopOldest(out _);

            int count = 0;
            for (int i = 0; i < _streaks.Count; i++)
            {
                ref Streak s = ref _streaks[i];
                float fade = 1f - Mathf.Clamp01((_time - s.BornAt) / s.Life);
                if (fade <= 0f) continue;
                _matrices[count] = s.Matrix;
                Vector4 c = s.Color;
                c.w *= fade;
                _colors[count] = c;
                count++;
            }
            if (count == 0) return;
            _props.SetVectorArray(ColorId, _colors);
            var rp = new RenderParams(_material)
            {
                matProps = _props,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                worldBounds = new Bounds(Vector3.zero, new Vector3(1000f, 20f, 1000f)),
            };
            Graphics.RenderMeshInstanced(rp, _quad, 0, _matrices, count);
        }

        public void Dispose()
        {
            if (_quad != null) Object.Destroy(_quad);
        }

        void Add(in ShotFired shot)
        {
            var from = new Vector3(shot.OriginX, Height, shot.OriginZ);
            var to = new Vector3(shot.EndX, Height, shot.EndZ);
            Vector3 d = to - from;
            float length = d.magnitude;
            if (length < 0.05f) return;
            Quaternion rotation = Quaternion.LookRotation(d / length, Vector3.up);
            _streaks.PushOverwrite(new Streak
            {
                Matrix = Matrix4x4.TRS(from, rotation, new Vector3(TracerWidth, 1f, length)),
                BornAt = _time,
                Life = TracerLife,
                Color = TracerColor,
            });
            if (!shot.FirstPellet) return;
            _streaks.PushOverwrite(new Streak
            {
                Matrix = Matrix4x4.TRS(from - d / length * 0.1f, rotation, new Vector3(FlashWidth, 1f, FlashLength)),
                BornAt = _time,
                Life = FlashLife,
                Color = FlashColor,
            });
        }

        /// <summary>Unit quad lying on XZ from z = 0 (tail, uv.x = 0) to z = 1 (head), centred on x.</summary>
        static Mesh CreateQuad()
        {
            var mesh = new Mesh { name = "TracerQuad" };
            mesh.vertices = new[] { new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0f, 0f), new Vector3(0.5f, 0f, 1f), new Vector3(-0.5f, 0f, 1f) };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.bounds = new Bounds(new Vector3(0f, 0f, 0.5f), new Vector3(1f, 0.1f, 1f));
            return mesh;
        }
    }
}
