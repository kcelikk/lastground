using UnityEngine;

namespace LastGround.Data.Meta
{
    /// <summary>Character outfit (TDD_01 §14.7 skin/outfit): a colour treatment of its body; the team-colour ring stays readable.</summary>
    [CreateAssetMenu(menuName = "LastGround/Meta/Outfit")]
    public sealed class OutfitDefinition : MetaItem
    {
        /// <summary>Multiplies the body albedo.</summary>
        public Color Tint = Color.white;
    }
}
