using LastGround.Data.Combat;
using UnityEngine;

namespace LastGround.Data.Director
{
    /// <summary>
    /// Which zombies the director may spawn and when (TDD_01 §2.2, §9.6, §9.8): the spawn cards, elite modifiers and
    /// the elite pacing. Read-only at runtime.
    /// </summary>
    [CreateAssetMenu(menuName = "LastGround/Director/Spawn Deck")]
    public sealed class SpawnDeckDefinition : ScriptableObject
    {
        public SpawnCard[] Cards;

        [Header("Elites (TDD_01 §9.8)")]
        public EliteModifierDefinition[] EliteModifiers;
        public float EliteMinRunSeconds = 180f;
        /// <summary>At least this long between two elites.</summary>
        public float EliteMinInterval = 40f;
        /// <summary>Alive elites cap: base + per player.</summary>
        public int MaxElitesBase = 1;
        public int MaxElitesPerPlayer = 1;
    }
}
