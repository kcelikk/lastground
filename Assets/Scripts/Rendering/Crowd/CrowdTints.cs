using UnityEngine;

namespace LastGround.Rendering.Crowd
{
    /// <summary>
    /// The global tint palette of the crowd shader (16 entries): 0–7 subtle zombie variety, 8–11 the outfit tint of
    /// players 0–3 (M9). One shared array so both renderers can update their part.
    /// </summary>
    public static class CrowdTints
    {
        public const int PlayerBase = 8;
        static readonly int TintsId = Shader.PropertyToID("_LGCrowdTints");

        static readonly Vector4[] Palette =
        {
            new Vector4(1f, 1f, 1f, 1f), new Vector4(0.85f, 0.95f, 0.85f, 1f), new Vector4(0.9f, 0.85f, 0.8f, 1f),
            new Vector4(0.75f, 0.8f, 0.9f, 1f), new Vector4(1f, 0.9f, 0.9f, 1f), new Vector4(0.8f, 0.8f, 0.75f, 1f),
            new Vector4(0.95f, 1f, 0.8f, 1f), new Vector4(0.7f, 0.75f, 0.7f, 1f),
            Vector4.one, Vector4.one, Vector4.one, Vector4.one, Vector4.one, Vector4.one, Vector4.one, Vector4.one,
        };

        public static void SetPlayer(int player, Color tint)
        {
            if ((uint)player >= 4) return;
            Palette[PlayerBase + player] = new Vector4(tint.r, tint.g, tint.b, 1f);
            Apply();
        }

        public static void Apply() => Shader.SetGlobalVectorArray(TintsId, Palette);
    }
}
