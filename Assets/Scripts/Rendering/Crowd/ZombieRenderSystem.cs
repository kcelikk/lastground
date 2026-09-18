using System;
using LastGround.Core.Events;
using LastGround.Core.Net;
using LastGround.Core.Tick;
using LastGround.Data.Crowd;
using LastGround.Data.Quality;
using LastGround.Gameplay.Crowd;
using UnityEngine;
using UnityEngine.Rendering;

namespace LastGround.Rendering.Crowd
{
    /// <summary>
    /// Draws the crowd and its corpses with GPU skinning + instancing (TDD_02 §21.3, D-018): one draw per
    /// (body, LOD) batch, no per-entity GameObjects. Culls against the camera's ground footprint, picks LODs by
    /// distance to the focus point, and enforces the preset's visible cap by dropping the farthest entities first.
    /// Allocation-free per frame. Partial <c>.Looks</c>: body and size per zombie type, glow for elites and status effects.
    /// </summary>
    public sealed partial class ZombieRenderSystem : ITickable, IDisposable
    {
        const int LodCount = 3;
        const float FootprintMargin = 2.5f;
        const float SinkDuration = 1.5f;
        const float SinkDepth = 0.6f;
        const int MaxPerDraw = 1023;
        const float HitFlashDuration = 0.12f;

        static readonly int AnimId = Shader.PropertyToID("_Anim");
        static readonly int GlowId = Shader.PropertyToID("_Glow");
        static readonly int BoneTexId = Shader.PropertyToID("_BoneTex");
        static readonly int TintsId = Shader.PropertyToID("_LGCrowdTints");

        struct Batch
        {
            public Mesh Mesh;
            public Matrix4x4[] Matrices;
            public Vector4[] Anim;
            public Vector4[] Glow;
            public MaterialPropertyBlock Props;
            public int Count;
        }

        readonly ICrowdRenderSource _source;
        readonly CrowdVisualCatalog _catalog;
        readonly Camera _camera;
        readonly QualityPresetDefinition _preset;
        readonly IGameEventStream<CrowdDeath> _deaths;
        readonly IGameEventStream<CrowdHit> _hits;
        readonly float[] _flashUntil;
        EventReader<CrowdHit> _hitReader;
        readonly Material _material;
        readonly Batch[] _batches;
        readonly GroundFootprint _footprint = new GroundFootprint();
        readonly CorpseBuffer _corpses;
        readonly int[] _candidates;
        readonly float[] _candidateDistance;
        EventReader<CrowdDeath> _deathReader;
        float _time;

        public ZombieRenderSystem(ICrowdRenderSource source, CrowdVisualCatalog catalog, Camera camera,
            QualityPresetDefinition preset, IGameEventStream<CrowdDeath> deaths, IGameEventStream<CrowdHit> hits = null)
        {
            _source = source;
            _catalog = catalog;
            _camera = camera;
            _preset = preset;
            _deaths = deaths;
            if (deaths != null) _deathReader = deaths.CreateReader();
            _hits = hits;
            if (hits != null) _hitReader = hits.CreateReader();
            _flashUntil = new float[source.Capacity];

            _material = new Material(catalog.Material) { name = "Crowd (runtime)", enableInstancing = true };
            if (preset.RimLight) _material.EnableKeyword("LG_RIM");
            else _material.DisableKeyword("LG_RIM");

            _corpses = new CorpseBuffer(preset.CorpseCap);
            int perBatch = Math.Min(MaxPerDraw, source.Capacity + Math.Max(0, preset.CorpseCap));
            _batches = new Batch[catalog.Bodies.Length * LodCount];
            for (int body = 0; body < catalog.Bodies.Length; body++)
            {
                for (int lod = 0; lod < LodCount; lod++)
                {
                    CrowdAnimationSet set = catalog.Bodies[body];
                    var props = new MaterialPropertyBlock();
                    props.SetTexture(BoneTexId, set.BoneTexture);
                    var anim = new Vector4[perBatch];
                    var glow = new Vector4[perBatch];
                    props.SetVectorArray(AnimId, anim);
                    props.SetVectorArray(GlowId, glow);
                    _batches[body * LodCount + lod] = new Batch
                    {
                        Mesh = set.Lods[Mathf.Min(lod, set.Lods.Length - 1)],
                        Matrices = new Matrix4x4[perBatch],
                        Anim = anim,
                        Glow = glow,
                        Props = props,
                    };
                }
            }
            _candidates = new int[source.Capacity];
            _candidateDistance = new float[source.Capacity];
            Shader.SetGlobalVectorArray(TintsId, TintPalette);
        }

        /// <summary>Instances drawn last frame (crowd + corpses), for stats and the benchmark.</summary>
        public int DrawnLastFrame { get; private set; }
        public int CorpseCount => _corpses.Count;

        static readonly Vector4[] TintPalette =
        {
            new Vector4(1f, 1f, 1f, 1f), new Vector4(0.85f, 0.95f, 0.85f, 1f), new Vector4(0.9f, 0.85f, 0.8f, 1f),
            new Vector4(0.75f, 0.8f, 0.9f, 1f), new Vector4(1f, 0.9f, 0.9f, 1f), new Vector4(0.8f, 0.8f, 0.75f, 1f),
            new Vector4(0.95f, 1f, 0.8f, 1f), new Vector4(0.7f, 0.75f, 0.7f, 1f),
        };

        public void Tick(float dt, uint tick)
        {
            _time += dt;
            _footprint.Update(_camera, FootprintMargin);
            for (int i = 0; i < _batches.Length; i++) _batches[i].Count = 0;

            CollectDeaths();
            CollectHits();
            int drawn = AddCrowd();
            drawn += AddCorpses();
            Draw();
            DrawnLastFrame = drawn;
        }

        public void Dispose()
        {
            UnityEngine.Object.Destroy(_material);
        }

        void CollectDeaths()
        {
            if (_deaths == null) return;
            while (_deaths.TryRead(ref _deathReader, out CrowdDeath death))
                _corpses.Add(death.Slot, death.X, death.Z, death.Yaw, _time, death.Type);
            _corpses.Expire(_time, _preset.CorpseLifetime + SinkDuration);
        }

        void CollectHits()
        {
            if (_hits == null) return;
            while (_hits.TryRead(ref _hitReader, out CrowdHit hit))
                if ((uint)hit.Slot < (uint)_flashUntil.Length) _flashUntil[hit.Slot] = _time + HitFlashDuration;
        }

        int AddCrowd()
        {
            bool[] alive = _source.Alive;
            float[] xs = _source.X;
            float[] zs = _source.Z;
            Vector2 focus = _footprint.Focus;
            int count = 0;
            for (int i = 0; i < _source.Capacity; i++)
            {
                if (!alive[i] || !_footprint.Contains(xs[i], zs[i])) continue;
                float dx = xs[i] - focus.x;
                float dz = zs[i] - focus.y;
                _candidates[count] = i;
                _candidateDistance[count] = dx * dx + dz * dz;
                count++;
            }

            int cap = _preset.MaxVisibleCrowd;
            if (count > cap)
            {
                // Nearest first; far entities at the footprint edge are the ones left out (TDD_03 §I.4).
                Array.Sort(_candidateDistance, _candidates, 0, count);
                count = cap;
            }

            float lod0 = _preset.Lod0Distance * _preset.Lod0Distance;
            float lod1 = _preset.Lod1Distance * _preset.Lod1Distance;
            byte[] anim = _source.AnimState;
            float[] yaws = _source.Yaw;
            byte[] types = _source.Types;
            for (int c = 0; c < count; c++)
            {
                int slot = _candidates[c];
                float d2 = _candidateDistance[c];
                int lod = d2 < lod0 ? 0 : d2 < lod1 ? 1 : 2;
                float flash = Mathf.Clamp01((_flashUntil[slot] - _time) / HitFlashDuration);
                Emit(slot, types[slot], xs[slot], 0f, zs[slot], yaws[slot], lod, (CrowdClipId)anim[slot], -1f, flash, GlowOf(slot, types[slot]));
            }
            return count;
        }

        int AddCorpses()
        {
            Vector2 focus = _footprint.Focus;
            float lod0 = _preset.Lod0Distance * _preset.Lod0Distance;
            float lod1 = _preset.Lod1Distance * _preset.Lod1Distance;
            int drawn = 0;
            for (int i = 0; i < _corpses.Count; i++)
            {
                ref CorpseBuffer.Corpse corpse = ref _corpses[i];
                if (!_footprint.Contains(corpse.X, corpse.Z)) continue;
                float age = _time - corpse.DiedAt;
                float sink = age > _preset.CorpseLifetime ? (age - _preset.CorpseLifetime) / SinkDuration * SinkDepth : 0f;
                float dx = corpse.X - focus.x;
                float dz = corpse.Z - focus.y;
                float d2 = dx * dx + dz * dz;
                int lod = d2 < lod0 ? 0 : d2 < lod1 ? 1 : 2;
                if (Emit(corpse.Slot, corpse.Type, corpse.X, -sink, corpse.Z, corpse.Yaw, lod, CrowdClipId.Death, age, 0f, Vector4.zero)) drawn++;
            }
            return drawn;
        }

        /// <summary>Adds one instance. <paramref name="deathAge"/> ≥ 0 plays the death clip once and holds the last frame.</summary>
        bool Emit(int slot, byte type, float x, float y, float z, float yaw, int lod, CrowdClipId clipId, float deathAge, float flash,
            Vector4 glow)
        {
            uint hash = CrowdVariety.Hash(slot);
            int body = BodyOf(type, hash);
            ref Batch batch = ref _batches[body * LodCount + lod];
            if (batch.Count >= batch.Matrices.Length) return false;

            CrowdAnimationSet set = _catalog.Bodies[body];
            CrowdClip clip = set.GetClip(clipId);
            float frame;
            if (deathAge >= 0f)
            {
                frame = Mathf.Min(deathAge * set.FrameRate, clip.FrameCount - 1);
            }
            else
            {
                frame = (_time * CrowdVariety.Speed(hash) + CrowdVariety.Phase(hash) * clip.Length(set.FrameRate)) * set.FrameRate;
                frame %= clip.FrameCount;
            }
            int a = (int)frame;
            int b = clip.Loop ? (a + 1) % clip.FrameCount : Mathf.Min(a + 1, clip.FrameCount - 1);

            float scale = CrowdVariety.Scale(hash, _catalog.ScaleVariation) * ScaleOf(type);
            batch.Matrices[batch.Count] = Matrix4x4.TRS(new Vector3(x, y, z), Quaternion.Euler(0f, yaw, 0f), new Vector3(scale, scale, scale));
            batch.Anim[batch.Count] = new Vector4(clip.StartFrame + a, clip.StartFrame + b, frame - a, CrowdVariety.Tint(hash) + flash * 0.99f);
            batch.Glow[batch.Count] = glow;
            batch.Count++;
            return true;
        }

        void Draw()
        {
            for (int i = 0; i < _batches.Length; i++)
            {
                ref Batch batch = ref _batches[i];
                if (batch.Count == 0) continue;
                batch.Props.SetVectorArray(AnimId, batch.Anim);
                batch.Props.SetVectorArray(GlowId, batch.Glow);
                var rp = new RenderParams(_material)
                {
                    matProps = batch.Props,
                    shadowCastingMode = ShadowCastingMode.Off,
                    receiveShadows = false,
                    worldBounds = new Bounds(Vector3.zero, new Vector3(1000f, 50f, 1000f)),
                };
                Graphics.RenderMeshInstanced(rp, batch.Mesh, 0, batch.Matrices, batch.Count);
            }
        }
    }
}
