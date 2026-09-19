using LastGround.Core.Events;
using LastGround.Core.Tick;
using LastGround.Data.Boss;
using LastGround.Gameplay.Boss;
using LastGround.Gameplay.Crowd;
using LastGround.Rendering.Combat;
using UnityEngine;
using UnityEngine.Rendering;

namespace LastGround.Rendering.Boss
{
    /// <summary>
    /// Boss presentation on every device (TDD_01 §10, D-021): ground telegraphs from each replicated attack windup —
    /// a filling red circle and the shock ring after the slam, the charge lane, the debris landing circle with the
    /// debris arcing in — plus the glowing tumour on its back (the weak point) and camera shake on impacts. The body
    /// itself is an ordinary crowd instance. Additive instanced draws with fixed arrays: no allocation per frame.
    /// </summary>
    public sealed class BossView : ITickable, System.IDisposable
    {
        const int MaxTelegraphs = 4;
        const float GroundHeight = 0.06f;
        /// <summary>Tumour position relative to the body: behind the shoulders at 3.5× scale.</summary>
        const float TumourBack = 1.1f;
        const float TumourHeight = 4.6f;
        static readonly int ColorId = Shader.PropertyToID("_FxColor");
        static readonly Color Danger = new Color(1f, 0.18f, 0.08f);
        static readonly Color Tumour = new Color(1f, 0.35f, 0.12f);

        struct Telegraph
        {
            public bool Active;
            public BossAttackStarted Attack;
            public float StartedAt;
            public bool Landed;
        }

        readonly BossState _state;
        readonly BossDefinition _definition;
        readonly ICrowdRenderSource _crowd;
        readonly TopDownCameraRig _camera;
        readonly Material _material;
        readonly Mesh _disc;
        readonly Mesh _ring;
        readonly Mesh _lane;
        readonly Mesh _chunk;
        readonly Telegraph[] _telegraphs = new Telegraph[MaxTelegraphs];
        readonly Matrix4x4[] _matrix = new Matrix4x4[1];
        readonly Vector4[] _color = new Vector4[1];
        readonly MaterialPropertyBlock _props = new MaterialPropertyBlock();
        EventReader<BossAttackStarted> _attacks;
        bool _wasStunned;
        float _time;

        public BossView(BossState state, BossDefinition definition, ICrowdRenderSource crowd, TopDownCameraRig camera, Material additive,
            Material chunk)
        {
            _state = state;
            _definition = definition;
            _crowd = crowd;
            _camera = camera;
            _material = additive;
            _chunkMaterial = chunk;
            _attacks = state.Attacks.CreateReader();
            _disc = FxMeshes.Disc();
            _ring = FxMeshes.Ring(0.08f);
            _lane = Lane();
            _chunk = Chunk();
        }

        readonly Material _chunkMaterial;

        public void Tick(float dt, uint tick)
        {
            _time += dt;
            while (_state.Attacks.TryRead(ref _attacks, out BossAttackStarted a)) Add(a);
            if (!_state.Active)
            {
                for (int i = 0; i < _telegraphs.Length; i++) _telegraphs[i].Active = false;
                return;
            }
            if (_state.Stunned && !_wasStunned) _camera?.AddTrauma(0.45f);
            _wasStunned = _state.Stunned;
            for (int i = 0; i < _telegraphs.Length; i++) if (_telegraphs[i].Active) Draw(ref _telegraphs[i]);
            DrawTumour();
        }

        void Add(in BossAttackStarted attack)
        {
            int free = 0;
            for (int i = 0; i < _telegraphs.Length; i++)
            {
                if (_telegraphs[i].Active) continue;
                free = i;
                break;
            }
            _telegraphs[free] = new Telegraph { Active = true, Attack = attack, StartedAt = _time - attack.Elapsed };
        }

        void Draw(ref Telegraph t)
        {
            if (_definition == null || t.Attack.Attack >= _definition.Attacks.Length)
            {
                t.Active = false;
                return;
            }
            BossAttackDefinition a = _definition.Attacks[t.Attack.Attack];
            float age = _time - t.StartedAt;
            float fill = Mathf.Clamp01(age / a.TelegraphSeconds);
            bool landed = age >= a.TelegraphSeconds;
            if (landed && !t.Landed)
            {
                t.Landed = true;
                if (a.Kind == BossAttackKind.GroundSlam) _camera?.AddTrauma(0.7f);
                else if (a.Kind == BossAttackKind.PropThrow) _camera?.AddTrauma(0.35f);
            }
            float pulse = 0.75f + 0.25f * Mathf.Sin(_time * 18f);
            var origin = new Vector3(t.Attack.OriginX, GroundHeight, t.Attack.OriginZ);
            switch (a.Kind)
            {
                case BossAttackKind.GroundSlam:
                    if (!landed)
                    {
                        Emit(_disc, origin, a.Radius * fill, Danger, 0.35f + 0.5f * fill);
                        Emit(_ring, origin, a.Radius, Danger, 1.4f * pulse);
                        break;
                    }
                    float ring = a.Radius + (age - a.TelegraphSeconds) * a.RingSpeed;
                    if (ring > a.RingMaxRadius)
                    {
                        t.Active = false;
                        break;
                    }
                    Emit(_ring, origin, ring, new Color(1f, 0.55f, 0.2f), 1.8f * (1f - ring / a.RingMaxRadius) + 0.3f);
                    break;
                case BossAttackKind.Charge:
                    if (landed && age > a.TelegraphSeconds + a.Length / Mathf.Max(1f, a.ChargeSpeed))
                    {
                        t.Active = false;
                        break;
                    }
                    float yaw = Mathf.Atan2(t.Attack.DirX, t.Attack.DirZ) * Mathf.Rad2Deg;
                    _matrix[0] = Matrix4x4.TRS(origin, Quaternion.Euler(0f, yaw, 0f), new Vector3(a.Radius * 2f, 1f, a.Length));
                    Draw(_lane, Danger, (landed ? 0.6f : 0.4f + 0.8f * fill) * pulse);
                    break;
                case BossAttackKind.PropThrow:
                    var target = new Vector3(t.Attack.TargetX, GroundHeight, t.Attack.TargetZ);
                    if (landed)
                    {
                        float after = age - a.TelegraphSeconds;
                        if (after > 0.4f) t.Active = false;
                        else Emit(_disc, target, a.Radius * (1f + after), new Color(1f, 0.6f, 0.3f), 1.5f * (1f - after / 0.4f));
                        break;
                    }
                    Emit(_disc, target, a.Radius * fill, Danger, 0.3f + 0.5f * fill);
                    Emit(_ring, target, a.Radius, Danger, 1.3f * pulse);
                    DrawDebris(origin, target, a, age);
                    break;
                default:
                    if (landed)
                    {
                        t.Active = false;
                        break;
                    }
                    // Summon scream: a red shock pulse around the boss while it roars.
                    Emit(_ring, origin, 2f + 6f * fill, Danger, 1.2f * (1f - fill));
                    break;
            }
        }

        /// <summary>The ripped-up chunk flies in for the last part of the telegraph on a high arc.</summary>
        void DrawDebris(Vector3 origin, Vector3 target, BossAttackDefinition a, float age)
        {
            float flightStart = a.TelegraphSeconds - a.FlightSeconds;
            if (age < flightStart || _chunkMaterial == null) return;
            float k = Mathf.Clamp01((age - flightStart) / Mathf.Max(0.05f, a.FlightSeconds));
            // A low arc: a high one passes right under the top-down camera and fills the screen.
            Vector3 p = Vector3.Lerp(origin + Vector3.up * 3f, target, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 2.5f);
            _matrix[0] = Matrix4x4.TRS(p, Quaternion.Euler(age * 240f, age * 170f, 0f), new Vector3(0.9f, 0.7f, 0.8f));
            var rp = new RenderParams(_chunkMaterial) { shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false };
            Graphics.RenderMeshInstanced(rp, _chunk, 0, _matrix, 1);
        }

        void DrawTumour()
        {
            int slot = _state.Slot;
            if ((uint)slot >= (uint)_crowd.Capacity || !_crowd.Alive[slot]) return;
            float yaw = _crowd.Yaw[slot] * Mathf.Deg2Rad;
            var back = new Vector3(-Mathf.Sin(yaw), 0f, -Mathf.Cos(yaw));
            var p = new Vector3(_crowd.X[slot], TumourHeight, _crowd.Z[slot]) + back * TumourBack;
            float beat = 0.6f + 0.4f * Mathf.Pow(Mathf.Abs(Mathf.Sin(_time * 2.6f)), 3f);
            Emit(_disc, p, 0.9f, Tumour, 1.1f * beat);
        }

        void Emit(Mesh mesh, Vector3 position, float radius, Color color, float intensity)
        {
            _matrix[0] = Matrix4x4.TRS(position, Quaternion.identity, new Vector3(radius, 1f, radius));
            Draw(mesh, color, intensity);
        }

        void Draw(Mesh mesh, Color color, float intensity)
        {
            _color[0] = new Vector4(color.r, color.g, color.b, intensity);
            _props.SetVectorArray(ColorId, _color);
            var rp = new RenderParams(_material)
            {
                matProps = _props,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                worldBounds = new Bounds(_matrix[0].GetColumn(3), new Vector3(60f, 20f, 60f)),
            };
            Graphics.RenderMeshInstanced(rp, mesh, 0, _matrix, 1);
        }

        public void Dispose()
        {
            if (_disc != null) Object.Destroy(_disc);
            if (_ring != null) Object.Destroy(_ring);
            if (_lane != null) Object.Destroy(_lane);
            if (_chunk != null) Object.Destroy(_chunk);
        }

        /// <summary>Unit lane from the origin forward (+Z), 1 wide: bright along its middle, fading at the far end.</summary>
        static Mesh Lane()
        {
            var mesh = new Mesh { name = "BossLane" };
            mesh.vertices = new[] { new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0f, 0f), new Vector3(0.5f, 0f, 1f), new Vector3(-0.5f, 0f, 1f) };
            mesh.uv = new[] { new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.9f), new Vector2(0f, 0.9f) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.bounds = new Bounds(new Vector3(0f, 0f, 0.5f), new Vector3(1f, 0.1f, 1f));
            return mesh;
        }

        /// <summary>A rough concrete chunk (irregular box).</summary>
        static Mesh Chunk()
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Mesh source = cube.GetComponent<MeshFilter>().sharedMesh;
            var mesh = Object.Instantiate(source);
            mesh.name = "BossDebris";
            Vector3[] v = mesh.vertices;
            for (int i = 0; i < v.Length; i++) v[i] = Vector3.Scale(v[i], new Vector3(1f + 0.25f * Mathf.Sin(i * 1.7f), 0.8f, 1f + 0.2f * Mathf.Cos(i * 2.3f)));
            mesh.vertices = v;
            mesh.RecalculateNormals();
            Object.Destroy(cube);
            return mesh;
        }
    }
}
