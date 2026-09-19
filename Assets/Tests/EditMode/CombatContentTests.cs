using LastGround.Data.Combat;
using LastGround.Data.Director;
using LastGround.Data.Weapons;
using NUnit.Framework;
using UnityEditor;

namespace LastGround.Tests
{
    /// <summary>The M6 combat catalog: array index == wire id, starting loadout valid, spawn deck complete.</summary>
    public class CombatContentTests
    {
        const string CatalogPath = "Assets/ScriptableObjects/Combat/CMB_Catalog.asset";
        const string DeckPath = "Assets/ScriptableObjects/Director/DIR_SpawnDeck.asset";

        static CombatCatalog Catalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CombatCatalog>(CatalogPath);
            Assert.IsNotNull(catalog, CatalogPath);
            return catalog;
        }

        [Test]
        public void Catalog_IndicesMatchWireIds()
        {
            CombatCatalog catalog = Catalog();
            Assert.AreEqual(6, catalog.Weapons.Length);
            for (int i = 0; i < catalog.Weapons.Length; i++) Assert.AreEqual(i, catalog.Weapons[i].NetIndex, catalog.Weapons[i].Id);
            Assert.AreEqual(6, catalog.Zombies.Length, "5 types + the M8 boss body");
            for (int i = 0; i < catalog.Zombies.Length; i++) Assert.AreEqual(i, catalog.Zombies[i].TypeIndex, catalog.Zombies[i].Id);
            for (int i = 0; i < catalog.Projectiles.Length; i++) Assert.AreEqual(i, catalog.Projectiles[i].NetIndex, catalog.Projectiles[i].Id);
            for (int i = 0; i < catalog.Elites.Length; i++) Assert.AreEqual(i + 1, catalog.Elites[i].NetIndex, catalog.Elites[i].Id);
        }

        [Test]
        public void Catalog_StartingLoadoutIsPrimaryPlusInfiniteSidearm()
        {
            CombatCatalog catalog = Catalog();
            Assert.AreEqual(WeaponSlot.Primary, catalog.StartPrimary.Slot);
            Assert.AreEqual(WeaponSlot.Sidearm, catalog.StartSidearm.Slot);
            Assert.IsTrue(catalog.StartSidearm.InfiniteReserve);
            Assert.IsTrue(catalog.Grenade.Explosion.IsValid);
            Assert.LessOrEqual(catalog.StartGrenades, catalog.MaxGrenades);
        }

        [Test]
        public void Catalog_ShotgunFiresPellets()
        {
            WeaponDefinition shotgun = Catalog().Weapon(3);
            Assert.AreEqual(WeaponFireMode.Pellet, shotgun.FireMode);
            Assert.AreEqual(8, shotgun.PelletCount);
            Assert.AreEqual(8, shotgun.MaxClaimsPerShot);
        }

        [Test]
        public void SpawnDeck_HasEveryZombieTypeAndPositiveCosts()
        {
            var deck = AssetDatabase.LoadAssetAtPath<SpawnDeckDefinition>(DeckPath);
            Assert.IsNotNull(deck);
            CombatCatalog catalog = Catalog();
            // Every type but the boss body (driven by BossController, never spawned by the deck).
            Assert.AreEqual(catalog.Zombies.Length - 1, deck.Cards.Length);
            foreach (SpawnCard card in deck.Cards) Assert.AreNotEqual(Data.Zombies.ZombieBehaviour.Boss, card.Zombie.Behaviour);
            foreach (SpawnCard card in deck.Cards)
            {
                Assert.IsNotNull(card.Zombie);
                Assert.Greater(card.Cost, 0);
                Assert.IsNotNull(card.Weight);
                Assert.AreSame(card.Zombie, catalog.Zombie(card.Zombie.TypeIndex));
            }
            Assert.AreEqual(catalog.Elites.Length, deck.EliteModifiers.Length);
        }
    }
}
