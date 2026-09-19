using UnityEngine;

namespace LastGround.Data.Meta
{
    /// <summary>
    /// Character perk (TDD_01 §14.7): exactly one advantage and one drawback on different stats, so it changes the
    /// play style without adding power (net zero, checked by MetaRulesTests against <see cref="MetaCatalog.PerkCaps"/>).
    /// One perk is active at a time.
    /// </summary>
    [CreateAssetMenu(menuName = "LastGround/Meta/Perk")]
    public sealed class PerkDefinition : MetaItem
    {
        public StatModifier Plus;
        public StatModifier Minus;
    }
}
