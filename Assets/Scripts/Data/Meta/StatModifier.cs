using LastGround.Data.Upgrades;

namespace LastGround.Data.Meta
{
    /// <summary>One stat change of a perk (same units as upgrades: flat for MaxHealth, % for *Pct stats).</summary>
    [System.Serializable]
    public struct StatModifier
    {
        public StatId Stat;
        public float Value;
    }
}
