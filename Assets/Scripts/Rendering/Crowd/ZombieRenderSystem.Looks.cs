using LastGround.Data.Crowd;
using LastGround.Gameplay.Crowd;
using UnityEngine;

namespace LastGround.Rendering.Crowd
{
    /// <summary>
    /// Readability of types and states (TDD_01 §0.6, §9.8): each zombie type draws from its own bodies at its own size;
    /// a per-instance glow (rim-weighted emissive in the shader) marks elites (their modifier colour, slow pulse),
    /// burning (orange flicker), a lit Exploder fuse (fast red blink) and stuns (pale). Priority: fuse, fire, elite,
    /// stun, the type's constant glow.
    /// </summary>
    public sealed partial class ZombieRenderSystem
    {
        static readonly Color BurnColor = new Color(1f, 0.45f, 0.1f);
        static readonly Color FuseColor = new Color(1f, 0.25f, 0.05f);
        static readonly Color StunColor = new Color(0.75f, 0.85f, 1f);

        /// <summary>Elite glow colours by modifier id (index 0 unused). Null = a default gold.</summary>
        public Color[] EliteGlows { get; set; }

        int BodyOf(byte type, uint hash)
        {
            CrowdTypeLook[] looks = _catalog.TypeLooks;
            if (looks != null && type < looks.Length && looks[type].Bodies != null && looks[type].Bodies.Length > 0)
            {
                int[] bodies = looks[type].Bodies;
                int body = bodies[(int)(hash % (uint)bodies.Length)];
                if ((uint)body < (uint)_catalog.Bodies.Length) return body;
            }
            return CrowdVariety.Body(hash, _catalog.Bodies.Length);
        }

        /// <summary>Seconds since this slot's animation state changed, for types that start clips with the state; else -1.</summary>
        float ClipAge(int slot, byte type, byte state)
        {
            CrowdTypeLook[] looks = _catalog.TypeLooks;
            if (looks == null || type >= looks.Length || !looks[type].ClipsFromStateStart) return -1f;
            // Stored as state + 1 so a fresh slot (0) always counts as a change.
            if (_stateShown[slot] != state + 1)
            {
                _stateShown[slot] = (byte)(state + 1);
                _stateSince[slot] = _time;
            }
            return _time - _stateSince[slot];
        }

        float ScaleOf(byte type)
        {
            CrowdTypeLook[] looks = _catalog.TypeLooks;
            return looks != null && type < looks.Length && looks[type].Scale > 0f ? looks[type].Scale : 1f;
        }

        Vector4 GlowOf(int slot, byte type)
        {
            byte flags = _source.FlagBits[slot];
            if ((flags & CrowdFlags.Priming) != 0)
                return Glow(FuseColor, Mathf.Repeat(_time * 6f, 1f) < 0.5f ? 1.3f : 0.15f);
            if ((flags & CrowdFlags.Burning) != 0)
                return Glow(BurnColor, 0.55f + 0.35f * Mathf.PerlinNoise(_time * 12f, slot * 0.37f));
            byte elite = _source.Elites[slot];
            if (elite != 0)
            {
                Color color = EliteGlows != null && elite < EliteGlows.Length ? EliteGlows[elite] : new Color(1f, 0.8f, 0.3f);
                return Glow(color, 0.55f + 0.2f * Mathf.Sin(_time * 3f + slot));
            }
            if ((flags & CrowdFlags.Stunned) != 0) return Glow(StunColor, 0.4f);
            CrowdTypeLook[] looks = _catalog.TypeLooks;
            if (looks != null && type < looks.Length && looks[type].GlowStrength > 0f) return Glow(looks[type].Glow, looks[type].GlowStrength);
            return Vector4.zero;
        }

        static Vector4 Glow(Color color, float strength) => new Vector4(color.r, color.g, color.b, strength);
    }
}
