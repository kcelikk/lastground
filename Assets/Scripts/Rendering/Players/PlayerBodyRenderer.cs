using LastGround.Core.Events;
using LastGround.Core.Tick;
using LastGround.Data.Crowd;
using LastGround.Data.Meta;
using LastGround.Data.Presentation;
using LastGround.Gameplay.Players;
using LastGround.Rendering.Combat;
using LastGround.Rendering.Crowd;
using UnityEngine;
using UnityEngine.Rendering;

namespace LastGround.Rendering.Players
{
    /// <summary>
    /// Player characters (M9, D-022): each player's baked Mixamo body drawn with the crowd's GPU-skinning shader,
    /// facing the aim. The clip follows the state: aimed run forwards or backwards by the move direction against the
    /// facing, firing while standing, the downed crawl, death, and emotes while standing still. Outfits tint the
    /// albedo (palette slots 8–11); a team-colour ring under the feet keeps players readable in every outfit
    /// (TDD_01 §14.7). Downed players pulse red, invulnerable ones blink. Fixed arrays: no allocation per frame.
    /// </summary>
    public sealed class PlayerBodyRenderer : ITickable, System.IDisposable
    {
        const float MoveThreshold = 0.6f;
        const float RingHeight = 0.05f;
        static readonly int AnimId = Shader.PropertyToID("_Anim");
        static readonly int GlowId = Shader.PropertyToID("_Glow");
        static readonly int BoneTexId = Shader.PropertyToID("_BoneTex");
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly int FxColorId = Shader.PropertyToID("_FxColor");

        readonly PlayerStateTable _players;
        readonly CrowdAnimationSet[] _bodies = new CrowdAnimationSet[PlayerStateTable.Max];
        readonly MaterialPropertyBlock[] _props = new MaterialPropertyBlock[PlayerStateTable.Max];
        readonly byte[] _clip = new byte[PlayerStateTable.Max];
        readonly float[] _clipSince = new float[PlayerStateTable.Max];
        readonly float[] _emoteUntil = new float[PlayerStateTable.Max];
        readonly byte[] _emoteClip = new byte[PlayerStateTable.Max];
        readonly Matrix4x4[] _matrix = new Matrix4x4[1];
        readonly Vector4[] _anim = new Vector4[1];
        readonly Vector4[] _glow = new Vector4[1];
        readonly Vector4[] _ringColor = new Vector4[1];
        readonly MaterialPropertyBlock _ringProps = new MaterialPropertyBlock();
        readonly Material _material;
        readonly Material _additive;
        readonly MetaCatalog _meta;
        readonly Mesh _ring;
        EventReader<PlayerEmote> _emotes;
        float _time;

        /// <param name="bodies">Body per player slot (null = that slot is not drawn by this renderer).</param>
        /// <param name="tints">Outfit tint per player slot.</param>
        public PlayerBodyRenderer(PlayerStateTable players, CrowdVisualCatalog catalog, CrowdAnimationSet[] bodies, Color[] tints,
            Material additive, MetaCatalog meta)
        {
            _players = players;
            _additive = additive;
            _meta = meta;
            _material = new Material(catalog.Material) { name = "Players (runtime)", enableInstancing = true };
            _material.EnableKeyword("LG_RIM");
            _ring = FxMeshes.Ring(0.1f);
            _emotes = players.Emotes.CreateReader();
            for (int p = 0; p < PlayerStateTable.Max; p++)
            {
                _bodies[p] = bodies[p];
                _clip[p] = 255;
                CrowdTints.SetPlayer(p, tints[p]);
                if (bodies[p] == null) continue;
                var props = new MaterialPropertyBlock();
                props.SetTexture(BoneTexId, bodies[p].BoneTexture);
                if (bodies[p].Albedo != null) props.SetTexture(BaseMapId, bodies[p].Albedo);
                props.SetVectorArray(AnimId, _anim);
                props.SetVectorArray(GlowId, _glow);
                _props[p] = props;
            }
        }

        public void Tick(float dt, uint tick)
        {
            _time += dt;
            while (_players.Emotes.TryRead(ref _emotes, out PlayerEmote e))
            {
                EmoteDefinition emote = _meta != null ? MetaCatalog.At(_meta.Emotes, e.Emote) : null;
                if (emote == null || (uint)e.Player >= PlayerStateTable.Max) continue;
                _emoteUntil[e.Player] = _time + emote.Seconds;
                _emoteClip[e.Player] = emote.Clip;
                _clip[e.Player] = 255; // restart the clip
            }
            for (int p = 0; p < PlayerStateTable.Max; p++)
            {
                if (!_players.Active[p] || _bodies[p] == null) continue;
                _players.GetDisplay(p, out float x, out float z, out float yaw);
                DrawRing(p, x, z);
                bool visible = !_players.Invulnerable[p] || Mathf.Repeat(_time * 8f, 1f) < 0.6f;
                if (visible) DrawBody(p, x, z, yaw);
            }
        }

        void DrawBody(int p, float x, float z, float yaw)
        {
            CrowdAnimationSet set = _bodies[p];
            byte state = State(p, yaw, out float speedScale);
            if (state != _clip[p])
            {
                _clip[p] = state;
                _clipSince[p] = _time;
            }
            CrowdClip clip = set.GetClip((CrowdClipId)state);
            float frame = (_time - _clipSince[p]) * set.FrameRate * speedScale;
            frame = clip.Loop ? frame % clip.FrameCount : Mathf.Min(frame, clip.FrameCount - 1);
            int a = (int)frame;
            int b = clip.Loop ? (a + 1) % clip.FrameCount : Mathf.Min(a + 1, clip.FrameCount - 1);

            _matrix[0] = Matrix4x4.TRS(new Vector3(x, 0f, z), Quaternion.Euler(0f, yaw, 0f), Vector3.one);
            _anim[0] = new Vector4(clip.StartFrame + a, clip.StartFrame + b, frame - a, CrowdTints.PlayerBase + p);
            _glow[0] = _players.IsDowned(p)
                ? new Vector4(1f, 0.1f, 0.08f, 0.35f + 0.25f * Mathf.Sin(_time * 8f))
                : _players.IsDead(p) ? new Vector4(0f, 0f, 0f, 0f) : Vector4.zero;
            MaterialPropertyBlock props = _props[p];
            props.SetVectorArray(AnimId, _anim);
            props.SetVectorArray(GlowId, _glow);
            var rp = new RenderParams(_material)
            {
                matProps = props,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                worldBounds = new Bounds(new Vector3(x, 1f, z), new Vector3(4f, 4f, 4f)),
            };
            Graphics.RenderMeshInstanced(rp, set.Lods[0], 0, _matrix, 1);
        }

        /// <summary>Animation state from life, movement against facing, firing and emotes.</summary>
        byte State(int p, float yaw, out float speedScale)
        {
            speedScale = 1f;
            if (_players.IsDead(p)) return (byte)CrowdClipId.Death;
            if (_players.IsDowned(p)) return (byte)CrowdClipId.Crawl;
            float vx = _players.VelX[p], vz = _players.VelZ[p];
            float speed = Mathf.Sqrt(vx * vx + vz * vz);
            if (speed > MoveThreshold)
            {
                _emoteUntil[p] = 0f;
                float rad = yaw * Mathf.Deg2Rad;
                float along = (vx * Mathf.Sin(rad) + vz * Mathf.Cos(rad)) / speed;
                // The run clips are authored at ~4.5 m/s: play faster or slower with the real speed.
                speedScale = Mathf.Clamp(speed / 4.5f, 0.6f, 1.5f);
                return along < -0.3f ? (byte)CrowdClipId.Run : (byte)CrowdClipId.Walk;
            }
            if (_time < _emoteUntil[p]) return _emoteClip[p];
            return _players.Firing[p] ? (byte)CrowdClipId.Attack : (byte)CrowdClipId.Idle;
        }

        void DrawRing(int p, float x, float z)
        {
            if (_additive == null) return;
            Color c = PlayerSlotColors.Of(p);
            _matrix[0] = Matrix4x4.TRS(new Vector3(x, RingHeight, z), Quaternion.identity, new Vector3(0.75f, 1f, 0.75f));
            _ringColor[0] = new Vector4(c.r, c.g, c.b, _players.IsDead(p) || _players.Disconnected[p] ? 0.3f : 1.2f);
            _ringProps.SetVectorArray(FxColorId, _ringColor);
            var rp = new RenderParams(_additive)
            {
                matProps = _ringProps,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                worldBounds = new Bounds(new Vector3(x, 0f, z), new Vector3(2f, 1f, 2f)),
            };
            Graphics.RenderMeshInstanced(rp, _ring, 0, _matrix, 1);
        }

        public void Dispose()
        {
            if (_material != null) Object.Destroy(_material);
            if (_ring != null) Object.Destroy(_ring);
        }
    }
}
