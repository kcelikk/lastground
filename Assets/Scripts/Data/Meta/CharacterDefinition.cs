using UnityEngine;

namespace LastGround.Data.Meta
{
    /// <summary>
    /// Playable character (TDD_01 §14.7, D-022): its own body and outfits, same base stats as every other character
    /// (PlayerDefinition); one perk slot.
    /// </summary>
    [CreateAssetMenu(menuName = "LastGround/Meta/Character")]
    public sealed class CharacterDefinition : MetaItem
    {
        /// <summary>Player body id (baked like the crowd bodies, PlayerBodySource).</summary>
        public string BodyId;
        /// <summary>First outfit is the character's default look.</summary>
        public OutfitDefinition[] Outfits;
        /// <summary>Perk equipped on a fresh profile.</summary>
        public PerkDefinition DefaultPerk;
    }
}
