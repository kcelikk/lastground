using LastGround.Data.Upgrades;

namespace LastGround.Gameplay.Upgrades
{
    /// <summary>Three upgrade choices for one player (TDD_01 §7.4). Fixed fields: no arrays, copied by value.</summary>
    public struct UpgradeOffer
    {
        public const int Choices = 3;

        public ushort Id;
        public byte Count;
        public byte Upgrade0, Upgrade1, Upgrade2;
        public UpgradeRarity Rarity0, Rarity1, Rarity2;

        public int UpgradeAt(int i) => i == 0 ? Upgrade0 : i == 1 ? Upgrade1 : Upgrade2;
        public UpgradeRarity RarityAt(int i) => i == 0 ? Rarity0 : i == 1 ? Rarity1 : Rarity2;

        public void Set(int i, int upgrade, UpgradeRarity rarity)
        {
            if (i == 0) { Upgrade0 = (byte)upgrade; Rarity0 = rarity; }
            else if (i == 1) { Upgrade1 = (byte)upgrade; Rarity1 = rarity; }
            else { Upgrade2 = (byte)upgrade; Rarity2 = rarity; }
        }
    }
}
