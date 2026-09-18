using UnityEngine;

namespace LastGround.Data.Quality
{
    /// <summary>
    /// Per-tier presentation budget (TDD_02 §21.9). Gameplay counts never depend on this; only local visual cost.
    /// The URP asset of the matching Unity quality level supplies render scale, MSAA, HDR and shadows.
    /// </summary>
    [CreateAssetMenu(menuName = "LastGround/Quality/Preset")]
    public sealed class QualityPresetDefinition : ScriptableObject
    {
        public string Id = "medium";

        /// <summary>Index into QualitySettings (0 LOW, 1 MEDIUM, 2 HIGH).</summary>
        public int QualityLevel = 1;

        public int TargetFps = 60;

        [Header("Crowd")]
        public float Lod0Distance = 12f;
        public float Lod1Distance = 24f;
        public int MaxVisibleCrowd = 250;
        public bool RimLight = true;

        [Header("Carnage")]
        public int CorpseCap = 50;
        public float CorpseLifetime = 12f;
        public int BloodSplatCap = 96;
        [Range(0f, 1f)] public float ParticleMultiplier = 0.8f;
    }
}
