using LastGround.Data.Upgrades;

namespace LastGround.Gameplay.Upgrades
{
    /// <summary>
    /// One player's run build (TDD_01 §7.1, §7.7): the upgrades taken and the stat totals they add. Run-scoped and
    /// never saved (D-005). Every device rebuilds every player's build from BuildChanged (upgrade id + rarity), so no
    /// stat values travel over the network. <see cref="Version"/> changes on every pick so users can cache.
    /// </summary>
    public sealed class PlayerBuild
    {
        readonly float[] _totals = new float[(int)StatId.Count];
        readonly byte[] _stacks;

        public PlayerBuild(int upgradeCount)
        {
            _stacks = new byte[upgradeCount];
        }

        public int Version { get; private set; }
        public int Picks { get; private set; }

        public float Get(StatId stat) => _totals[(int)stat];
        public int StacksOf(int upgrade) => (uint)upgrade < (uint)_stacks.Length ? _stacks[upgrade] : 0;

        public void Apply(UpgradeCatalog catalog, int upgrade, UpgradeRarity rarity)
        {
            if ((uint)upgrade >= (uint)catalog.Upgrades.Length) return;
            UpgradeDefinition definition = catalog.Upgrades[upgrade];
            _totals[(int)definition.Stat] += definition.ValueFor(rarity);
            if (_stacks[upgrade] < byte.MaxValue) _stacks[upgrade]++;
            Picks++;
            Version++;
        }

        public void Clear()
        {
            System.Array.Clear(_totals, 0, _totals.Length);
            System.Array.Clear(_stacks, 0, _stacks.Length);
            Picks = 0;
            Version++;
        }
    }
}
