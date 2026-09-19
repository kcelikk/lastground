using LastGround.Core.Events;
using LastGround.Core.Net;
using LastGround.Core.Tick;
using LastGround.Data.Combat;
using LastGround.Data.Quality;
using LastGround.Gameplay.Projectiles;
using UnityEngine;
using UnityEngine.Rendering;

namespace LastGround.Rendering.Combat
{
    /// <summary>
    /// Blast and splash effects (TDD_01 §5.7, §6.5): an expanding additive ground flash sized to the blast radius, a
    /// shared particle system fed with Emit() for fire, smoke and sparks, camera trauma by distance, and a green splash
    /// where spit lands. Particle counts follow the quality preset. No GameObject per blast, no allocation.
    /// </summary>
    public sealed class ExplosionEffects : ITickable, System.IDisposable
    {
        const int MaxFlashes = 16;
        const float FlashLife = 0.45f;
        static readonly int ColorId = Shader.PropertyToID("_FxColor");

        struct Flash
        {
            public Vector3 Position;
            public float Radius;
            public float BornAt;
            public Color Color;
        }

        readonly IGameEventStream<ExplosionFx> _blasts;
        readonly ProjectileTable _projectiles;
        readonly TopDownCameraRig _camera;
        readonly System.Func<Vector2> _listener;
        readonly Material _additive;
        readonly Mesh _disc;
        readonly ParticleSystem _particles;
        readonly int _fireCount;
        readonly Flash[] _flashes = new Flash[MaxFlashes];
        readonly Matrix4x4[] _matrices = new Matrix4x4[MaxFlashes];
        readonly Vector4[] _colors = new Vector4[MaxFlashes];
        readonly MaterialPropertyBlock _props = new MaterialPropertyBlock();
        EventReader<ExplosionFx> _blastReader;
        EventReader<ProjectileEnded> _endReader;
        int _next;
        float _time;

        /// <param name="listener">Local player position (camera shake falls off with distance).</param>
        public ExplosionEffects(IGameEventStream<ExplosionFx> blasts, ProjectileTable projectiles, TopDownCameraRig camera,
            System.Func<Vector2> listener, QualityPresetDefinition preset, Transform parent, Material additive, Material particleMaterial)
        {
            _blasts = blasts;
            _blastReader = blasts.CreateReader();
            _projectiles = projectiles;
            if (projectiles != null) _endReader = projectiles.Ended.CreateReader();
            _camera = camera;
            _listener = listener;
            _additive = additive;
            _disc = FxMeshes.Disc();
            _fireCount = Mathf.Max(8, Mathf.RoundToInt(40 * preset.ParticleMultiplier));
            _particles = CreateParticles(parent, particleMaterial, Mathf.RoundToInt(400 * preset.ParticleMultiplier));
            _props.SetVectorArray(ColorId, _colors);
        }

        public void Tick(float dt, uint tick)
        {
            _time += dt;
            while (_blasts.TryRead(ref _blastReader, out ExplosionFx fx)) Blast(fx);
            if (_projectiles != null)
            {
                while (_projectiles.Ended.TryRead(ref _endReader, out ProjectileEnded end))
                {
                    ProjectileDefinition definition = _projectiles.DefinitionOf(end.Id);
                    if (definition == null || definition.Motion != ProjectileMotion.Straight) continue;
                    Emit(new Vector3(end.X, 0.8f, end.Z), definition.Color, 10, 2.5f, 0.35f);
                    AddFlash(new Vector3(end.X, 0.05f, end.Z), 0.9f, definition.Color);
                }
            }
            DrawFlashes();
        }

        public void Dispose()
        {
            if (_disc != null) Object.Destroy(_disc);
        }

        void Blast(in ExplosionFx fx)
        {
            var at = new Vector3(fx.X, 0.05f, fx.Z);
            Color fire = fx.Kind == ExplosionKind.Volatile ? new Color(1f, 0.35f, 0.08f) : new Color(1f, 0.55f, 0.15f);
            AddFlash(at, fx.Radius, fire);
            Emit(at + Vector3.up * 0.6f, fire, _fireCount, 7f, 0.55f);
            Emit(at + Vector3.up * 0.8f, new Color(0.18f, 0.16f, 0.15f), _fireCount / 2, 2.5f, 1.4f);
            if (_camera == null || _listener == null) return;
            float distance = Vector2.Distance(_listener(), new Vector2(fx.X, fx.Z));
            _camera.AddTrauma(Mathf.Clamp01(1f - distance / (fx.Radius * 4f)) * 0.8f);
        }

        void AddFlash(Vector3 at, float radius, Color color)
        {
            _flashes[_next] = new Flash { Position = at, Radius = radius, BornAt = _time, Color = color };
            _next = (_next + 1) % MaxFlashes;
        }

        void Emit(Vector3 at, Color color, int count, float speed, float lifetime)
        {
            var emit = new ParticleSystem.EmitParams
            {
                position = at,
                applyShapeToPosition = true,
                startColor = color,
                startLifetime = lifetime,
            };
            ParticleSystem.MainModule main = _particles.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
            _particles.Emit(emit, count);
        }

        void DrawFlashes()
        {
            int count = 0;
            for (int i = 0; i < MaxFlashes; i++)
            {
                float age = _time - _flashes[i].BornAt;
                if (_flashes[i].Radius <= 0f || age > FlashLife) continue;
                float k = age / FlashLife;
                float radius = _flashes[i].Radius * (0.55f + 0.55f * k);
                _matrices[count] = Matrix4x4.TRS(_flashes[i].Position, Quaternion.identity, new Vector3(radius, 1f, radius));
                Color c = _flashes[i].Color;
                _colors[count] = new Vector4(c.r, c.g, c.b, 2.2f * (1f - k) * (1f - k));
                count++;
            }
            if (count == 0 || _additive == null) return;
            _props.SetVectorArray(ColorId, _colors);
            var rp = new RenderParams(_additive)
            {
                matProps = _props,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                worldBounds = new Bounds(Vector3.zero, new Vector3(1000f, 10f, 1000f)),
            };
            Graphics.RenderMeshInstanced(rp, _disc, 0, _matrices, count);
        }

        static ParticleSystem CreateParticles(Transform parent, Material material, int maxParticles)
        {
            var go = new GameObject("FX_Explosions");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.maxParticles = Mathf.Max(60, maxParticles);
            main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.55f);
            main.gravityModifier = 0.6f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.4f;
            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.6f));
            ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
            color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.4f, 0.35f, 0.3f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            color.color = fade;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            ps.Play();
            return ps;
        }
    }
}
