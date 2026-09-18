using UnityEngine;

namespace LastGround.Data.Crowd
{
    /// <summary>
    /// All crowd bodies plus the shared material and tint palette (TDD_02 §21.3 variety: bodies × tints × scale).
    /// One material is shared by every body (single atlas); per-instance data selects frame and tint.
    /// </summary>
    [CreateAssetMenu(menuName = "LastGround/Crowd/Visual Catalog")]
    public sealed class CrowdVisualCatalog : ScriptableObject
    {
        public CrowdAnimationSet[] Bodies;
        public Material Material;

        /// <summary>Target rendered height in metres (TDD_01 §0.2: zombie ≈ player ≈ 1.8 m).</summary>
        public float TargetHeight = 1.8f;

        [Range(0f, 0.2f)] public float ScaleVariation = 0.08f;
    }
}
