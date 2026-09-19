using LastGround.Core.Random;
using LastGround.Data.Upgrades;

namespace LastGround.Gameplay.Upgrades
{
    /// <summary>
    /// Builds a player's offer (TDD_01 §7.4): upgrades below their stack cap, weighted, no repeats; each choice rolls
    /// its own rarity (60/28/10/2). Every player has an independent stream (hash(runSeed, player, "upgrade")), so
    /// teammates see different offers. Allocation-free.
    /// </summary>
    public sealed class OfferGenerator
    {
        readonly UpgradeCatalog _catalog;
        readonly uint _seed;
        readonly bool[] _taken;

        public OfferGenerator(UpgradeCatalog catalog, uint runSeed)
        {
            _catalog = catalog;
            _seed = runSeed;
            _taken = new bool[catalog.Upgrades.Length];
        }

        /// <param name="minRarity">Objective rewards raise every card to at least this rarity.</param>
        public UpgradeOffer Generate(PlayerBuild build, int player, ushort offerId, UpgradeRarity minRarity = UpgradeRarity.Common)
        {
            var rng = DeterministicRandom.ForStream(_seed, "upgrade", Hash32.Combine((uint)player, offerId));
            var offer = new UpgradeOffer { Id = offerId };
            System.Array.Clear(_taken, 0, _taken.Length);
            int choices = System.Math.Min(UpgradeOffer.Choices, _catalog.ChoicesPerOffer);
            for (int c = 0; c < choices; c++)
            {
                int upgrade = PickUpgrade(build, ref rng);
                if (upgrade < 0) break;
                _taken[upgrade] = true;
                UpgradeRarity rarity = RollRarity(ref rng);
                offer.Set(c, upgrade, rarity < minRarity ? minRarity : rarity);
                offer.Count++;
            }
            return offer;
        }

        int PickUpgrade(PlayerBuild build, ref DeterministicRandom rng)
        {
            float total = 0f;
            for (int i = 0; i < _catalog.Upgrades.Length; i++)
                if (Eligible(build, i)) total += _catalog.Upgrades[i].Weight;
            if (total <= 0f) return -1;
            float roll = rng.NextFloat() * total;
            for (int i = 0; i < _catalog.Upgrades.Length; i++)
            {
                if (!Eligible(build, i)) continue;
                roll -= _catalog.Upgrades[i].Weight;
                if (roll <= 0f) return i;
            }
            for (int i = _catalog.Upgrades.Length - 1; i >= 0; i--) if (Eligible(build, i)) return i;
            return -1;
        }

        bool Eligible(PlayerBuild build, int i)
        {
            UpgradeDefinition u = _catalog.Upgrades[i];
            return !_taken[i] && u != null && u.Weight > 0f && build.StacksOf(i) < u.MaxStacks;
        }

        UpgradeRarity RollRarity(ref DeterministicRandom rng)
        {
            float[] weights = _catalog.RarityWeights;
            float total = 0f;
            for (int i = 0; i < weights.Length; i++) total += weights[i];
            float roll = rng.NextFloat() * total;
            for (int i = 0; i < weights.Length; i++)
            {
                roll -= weights[i];
                if (roll <= 0f) return (UpgradeRarity)i;
            }
            return UpgradeRarity.Common;
        }
    }
}
