using LastGround.Core.Events;
using LastGround.Core.Net;
using LastGround.Core.Pooling;
using LastGround.Core.Tick;
using LastGround.Data.Quality;
using LastGround.Gameplay.Crowd;
using UnityEngine;
using UnityEngine.Rendering;

namespace LastGround.Rendering.Carnage
{
    /// <summary>
    /// Blood layers 1 and 2 (TDD_02 §21.5): L1 = one shared particle system fed with Emit() per death,
    /// L2 = instanced ground splats in a ring buffer that fade out. L3 (accumulation texture) comes with M11.
    /// </summary>
    public sealed class BloodSystem : ITickable, System.IDisposable
    {
        const float SplatLife = 25f;
        const float SplatFade = 5f;
        static readonly int ParamsId = Shader.PropertyToID("_Splat");

        struct Splat
        {
            public Matrix4x4 Matrix;
            public float BornAt;
            public float Seed;
        }

        readonly IGameEventStream<CrowdDeath> _deaths;
        readonly ParticleSystem _burst;
        readonly int _burstCount;
        readonly RingBuffer<Splat> _splats;
        readonly Mesh _quad;
        readonly Material _splatMaterial;
        readonly Matrix4x4[] _matrices;
        readonly Vector4[] _params;
        readonly MaterialPropertyBlock _props = new MaterialPropertyBlock();
        EventReader<CrowdDeath> _reader;
        float _time;
        uint _seed = 1;

        public BloodSystem(IGameEventStream<CrowdDeath> deaths, QualityPresetDefinition preset, Transform parent,
            Material particleMaterial, Material splatMaterial)
        {
            _deaths = deaths;
            _reader = deaths.CreateReader();
            _burstCount = Mathf.Max(3, Mathf.RoundToInt(14 * preset.ParticleMultiplier));
            _burst = CreateBurst(parent, particleMaterial, Mathf.RoundToInt(500 * preset.ParticleMultiplier));

            int cap = Mathf.Max(0, preset.BloodSplatCap);
            _splats = new RingBuffer<Splat>(Mathf.Max(1, cap));
            SplatsEnabled = cap > 0;
            _splatMaterial = splatMaterial;
            _quad = CreateQuad();
            _matrices = new Matrix4x4[Mathf.Max(1, cap)];
            _params = new Vector4[Mathf.Max(1, cap)];
            _props.SetVectorArray(ParamsId, _params);
        }

        public bool SplatsEnabled { get; }

        public void Tick(float dt, uint tick)
        {
            _time += dt;
            while (_deaths.TryRead(ref _reader, out CrowdDeath death))
                OnDeath(death);

            if (!SplatsEnabled || _splats.Count == 0) return;
            while (_splats.Count > 0 && _time - _splats[0].BornAt > SplatLife) _splats.TryPopOldest(out _);

            int count = _splats.Count;
            for (int i = 0; i < count; i++)
            {
                ref Splat s = ref _splats[i];
                float age = _time - s.BornAt;
                float fade = age < SplatLife - SplatFade ? 1f : Mathf.Clamp01((SplatLife - age) / SplatFade);
                float grow = Mathf.Clamp01(age * 4f);
                _matrices[i] = s.Matrix;
                _params[i] = new Vector4(fade, s.Seed, grow, 0f);
            }
            if (count == 0) return;
            _props.SetVectorArray(ParamsId, _params);
            var rp = new RenderParams(_splatMaterial)
            {
                matProps = _props,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                worldBounds = new Bounds(Vector3.zero, new Vector3(1000f, 10f, 1000f)),
            };
            Graphics.RenderMeshInstanced(rp, _quad, 0, _matrices, count);
        }

        public void Dispose()
        {
            if (_quad != null) Object.Destroy(_quad);
        }

        void OnDeath(in CrowdDeath death)
        {
            var emit = new ParticleSystem.EmitParams { position = new Vector3(death.X, 1f, death.Z), applyShapeToPosition = true };
            _burst.Emit(emit, _burstCount);

            if (!SplatsEnabled) return;
            _seed = _seed * 1664525u + 1013904223u;
            float size = 0.9f + (_seed >> 24) / 255f * 0.9f;
            float angle = ((_seed >> 8) & 1023u) / 1023f * 360f;
            _splats.PushOverwrite(new Splat
            {
                Matrix = Matrix4x4.TRS(new Vector3(death.X, 0.015f, death.Z), Quaternion.Euler(0f, angle, 0f), new Vector3(size, 1f, size)),
                BornAt = _time,
                Seed = (_seed & 1023u) / 1023f,
            });
        }

        static ParticleSystem CreateBurst(Transform parent, Material material, int maxParticles)
        {
            var go = new GameObject("FX_BloodBurst");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.maxParticles = Mathf.Max(50, maxParticles);
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.18f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.45f, 0.02f, 0.02f), new Color(0.25f, 0f, 0f));
            main.gravityModifier = 2.2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.25f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            ps.Play();
            return ps;
        }

        static Mesh CreateQuad()
        {
            var mesh = new Mesh { name = "BloodSplatQuad" };
            mesh.vertices = new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(-0.5f, 0f, 0.5f) };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(1f, 0.1f, 1f));
            return mesh;
        }
    }
}
