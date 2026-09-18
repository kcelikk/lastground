using LastGround.Core.Events;
using LastGround.Data.Upgrades;
using LastGround.Gameplay.Players;

namespace LastGround.Gameplay.Upgrades
{
    /// <summary>A pick was applied to a player's build (HUD toast, stat refresh, sound).</summary>
    public struct BuildChanged
    {
        public int Player;
        public int Upgrade;
        public UpgradeRarity Rarity;
    }

    /// <summary>All four players' builds, identical on every device.</summary>
    public sealed class TeamBuilds
    {
        readonly PlayerBuild[] _builds = new PlayerBuild[PlayerStateTable.Max];

        public readonly EventChannel<BuildChanged> Changed = new EventChannel<BuildChanged>(32);

        public TeamBuilds(UpgradeCatalog catalog)
        {
            Catalog = catalog;
            int count = catalog != null && catalog.Upgrades != null ? catalog.Upgrades.Length : 0;
            for (int p = 0; p < _builds.Length; p++) _builds[p] = new PlayerBuild(count);
        }

        public UpgradeCatalog Catalog { get; }

        public PlayerBuild Of(int player) => _builds[player];

        public void Apply(int player, int upgrade, UpgradeRarity rarity)
        {
            if ((uint)player >= (uint)_builds.Length || Catalog == null) return;
            _builds[player].Apply(Catalog, upgrade, rarity);
            Changed.Publish(new BuildChanged { Player = player, Upgrade = upgrade, Rarity = rarity });
        }
    }
}
