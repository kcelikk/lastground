using UnityEngine;

namespace LastGround.Data.Crowd
{
    /// <summary>
    /// How one zombie type looks (TDD_01 §8.5, §0.6 readability): which bodies it may use, its size, and an optional
    /// constant glow (e.g. an Exploder's pustules). Indexed by zombie TypeIndex in <see cref="CrowdVisualCatalog"/>.
    /// </summary>
    [System.Serializable]
    public struct CrowdTypeLook
    {
        /// <summary>Zombie definition id, for the Inspector.</summary>
        public string ZombieId;
        /// <summary>Body indices into CrowdVisualCatalog.Bodies; empty = any body.</summary>
        public int[] Bodies;
        public float Scale;
        public Color Glow;
        [Range(0f, 1f)] public float GlowStrength;
    }
}
