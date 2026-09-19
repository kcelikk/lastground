using System.Collections.Generic;
using System.IO;
using LastGround.Core.Net.Protocol;
using LastGround.Data.Meta;
using LastGround.Data.Upgrades;
using LastGround.Gameplay.Meta;
using LastGround.Gameplay.Upgrades;
using LastGround.Meta;
using LastGround.Save;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;

namespace LastGround.Tests
{
    /// <summary>M9 meta progression: no permanent power (D-005), unlocks, banking, badges, profile save and migration.</summary>
    public class MetaTests
    {
        string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "lg_meta_test_" + System.Guid.NewGuid().ToString("N"));
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        static MetaCatalog Catalog() => AssetDatabase.LoadAssetAtPath<MetaCatalog>("Assets/ScriptableObjects/Meta/META_Catalog.asset");

        [Test]
        public void EveryPerk_IsOneTradeOff_WithinItsCaps()
        {
            MetaCatalog catalog = Catalog();
            Assert.IsNotNull(catalog);
            Assert.GreaterOrEqual(catalog.Perks.Length, 3);
            var caps = new Dictionary<StatId, float>();
            foreach (MetaCatalog.PerkCap cap in catalog.PerkCaps) caps[cap.Stat] = cap.Max;
            foreach (PerkDefinition perk in catalog.Perks)
            {
                Assert.Greater(perk.Plus.Value, 0f, perk.Id + ": an advantage");
                Assert.Less(perk.Minus.Value, 0f, perk.Id + ": a drawback");
                Assert.AreNotEqual(perk.Plus.Stat, perk.Minus.Stat, perk.Id + ": different stats");
                Assert.IsTrue(caps.ContainsKey(perk.Plus.Stat) && caps.ContainsKey(perk.Minus.Stat), perk.Id + ": allowed stats only");
                Assert.LessOrEqual(perk.Plus.Value, caps[perk.Plus.Stat] + 1e-4f, perk.Id);
                Assert.LessOrEqual(-perk.Minus.Value, caps[perk.Minus.Stat] + 1e-4f, perk.Id);
            }
        }

        [Test]
        public void NoMetaUnlock_AddsPower_BeyondThePerkTradeOff()
        {
            MetaCatalog catalog = Catalog();
            // Characters: same base stats (no stat fields at all), only a perk slot and looks.
            foreach (CharacterDefinition c in catalog.Characters)
            {
                Assert.IsNotNull(c.DefaultPerk, c.Id);
                Assert.Greater(c.Outfits.Length, 0, c.Id);
                Assert.IsTrue(c.Outfits[0].OwnedByDefault, c.Id + ": default outfit owned");
            }
            // Loadouts: every one is the same tier (grenades within 1 of each other) and starts with a primary.
            int min = int.MaxValue, max = int.MinValue;
            foreach (LoadoutDefinition l in catalog.Loadouts)
            {
                Assert.IsNotNull(l.Primary, l.Id);
                Assert.AreEqual(Data.Weapons.WeaponSlot.Primary, l.Primary.Slot, l.Id);
                min = System.Math.Min(min, l.Grenades);
                max = System.Math.Max(max, l.Grenades);
            }
            Assert.LessOrEqual(max - min, 2, "loadouts trade grenades against the weapon, never stack power");
            // A build with a perk and nothing else: exactly the two perk modifiers.
            var build = new PlayerBuild(1);
            PerkDefinition perk = catalog.Perks[catalog.Perks.Length - 1];
            MetaApplier.ApplyPerk(new PlayerMeta { Perk = (byte)(catalog.Perks.Length - 1) }, build, catalog);
            for (int s = 0; s < (int)StatId.Count; s++)
            {
                var stat = (StatId)s;
                float expected = stat == perk.Plus.Stat ? perk.Plus.Value : stat == perk.Minus.Stat ? perk.Minus.Value : 0f;
                Assert.AreEqual(expected, build.Get(stat), 1e-4f, stat.ToString());
            }
            build.Clear();
            Assert.AreEqual(perk.Plus.Value, build.Get(perk.Plus.Stat), 1e-4f, "upgrades reset, the perk stays for the run");
        }

        [Test]
        public void PlayerMeta_PacksIntoTheRosterValue()
        {
            var meta = new PlayerMeta { Character = 1, Outfit = 3, Perk = 4, Loadout = 2, Title = 6, OwnedWeapons = 0b10101 };
            PlayerMeta back = PlayerMeta.Unpack(meta.Pack());
            Assert.AreEqual(1, back.Character);
            Assert.AreEqual(3, back.Outfit);
            Assert.AreEqual(4, back.Perk);
            Assert.AreEqual(2, back.Loadout);
            Assert.AreEqual(6, back.Title);
            Assert.AreEqual(0b10101, back.OwnedWeapons);
            PlayerMeta none = PlayerMeta.Unpack(PlayerMeta.Default.Pack());
            Assert.AreEqual(PlayerMeta.None, none.Perk);
            Assert.AreEqual(PlayerMeta.None, none.Title);
            Assert.AreEqual(PlayerMeta.None, PlayerMeta.Unpack(0UL).Loadout, "old clients without meta");
        }

        [Test]
        public void Unlock_ChecksPriceAndRequirement_AndEquipsOnlyOwned()
        {
            MetaCatalog catalog = Catalog();
            var profile = new MetaProfile(new ProfileData(), catalog);
            var unlocks = new UnlockService(profile);
            WeaponUnlock sniper = System.Array.Find(catalog.Weapons, w => w.Id == "sniper");
            LoadoutDefinition marksman = System.Array.Find(catalog.Loadouts, l => l.Id == "marksman");

            Assert.IsFalse(profile.CanUse(marksman), "the loadout needs its weapon");
            Assert.IsFalse(profile.Equip(marksman));
            Assert.AreEqual(UnlockResult.NotEnoughScrap, unlocks.Unlock(sniper));
            profile.Data.Scrap = sniper.Price + 10;
            Assert.AreEqual(UnlockResult.Unlocked, unlocks.Unlock(sniper));
            Assert.AreEqual(10, profile.Scrap);
            Assert.AreEqual(UnlockResult.AlreadyOwned, unlocks.Unlock(sniper));
            Assert.IsTrue(profile.Equip(marksman));
            Assert.AreEqual(System.Array.IndexOf(catalog.Loadouts, marksman), profile.LoadoutIndex);
            Assert.AreNotEqual(0, profile.OwnedWeaponMask & (1 << System.Array.IndexOf(catalog.Weapons, sniper)));
            Assert.AreEqual(UnlockResult.NotForSale, unlocks.Unlock(catalog.Titles[0]), "titles are earned");

            CharacterDefinition survivor = catalog.Characters[1];
            Assert.IsFalse(profile.Equip(survivor));
            Assert.AreEqual(0, profile.CharacterIndex, "falls back to an owned character");
        }

        [Test]
        public void Banking_AddsScrapStatsAndBadges_OnlyOnce()
        {
            MetaCatalog catalog = Catalog();
            var profile = new MetaProfile(new ProfileData(), catalog);
            var progression = new MetaProgressionService(profile);
            BankReport report = progression.Bank(new RunSummary
            {
                BankedCoins = 420, Extracted = true, MaxThreat = 5, SurvivalSeconds = 1300f, Kills = 900, Revives = 3, BossKills = 1,
            });
            Assert.AreEqual((int)(420 * catalog.ScrapPerCoin), report.ScrapGained);
            Assert.AreEqual(report.ScrapGained, profile.Scrap);
            Assert.AreEqual(1, profile.Data.Stats.Extractions);
            Assert.AreEqual(5, profile.Data.Stats.MaxExtractThreat);
            var earned = new HashSet<string>();
            foreach (TitleDefinition t in report.NewBadges) earned.Add(t.Id);
            Assert.IsTrue(earned.SetEquals(new[] { "first_extraction", "boss_slayer", "threat_five", "twenty_minutes" }));

            BankReport again = progression.Bank(new RunSummary { BankedCoins = 10, MaxThreat = 2, SurvivalSeconds = 60f });
            Assert.AreEqual(0, again.NewBadges.Count, "badges are earned once");
            Assert.AreEqual(1, profile.Data.Stats.Wipes);
            Assert.AreEqual(1300, profile.Data.Stats.BestSurvivalSeconds, "best is a maximum");
            Assert.IsTrue(profile.Equip(catalog.Titles[0]), "an earned title can be worn");
        }

        [Test]
        public void Profile_SavesAndLoads_ThroughTheStore()
        {
            var service = new SaveService(new JsonFileSaveStore(_dir));
            service.Profile.Scrap = 1234;
            service.Profile.Owned.Add("sniper");
            service.Profile.Outfits.Add(new OutfitChoice { Character = "ranger", Outfit = "ranger_night" });
            service.SaveNow();

            var reloaded = new SaveService(new JsonFileSaveStore(_dir));
            Assert.AreEqual(1234, reloaded.Profile.Scrap);
            CollectionAssert.Contains(reloaded.Profile.Owned, "sniper");
            Assert.AreEqual("ranger_night", reloaded.Profile.Outfits[0].Outfit);
            Assert.AreEqual(ProfileData.CurrentVersion, reloaded.Profile.Version);
        }

        [Test]
        public void Migrator_UpgradesStepByStep_AndNeverOverwritesANewerProfile()
        {
            // An imagined v1 → v2 → v3 chain: v1 stored "Coins", v2 renamed it to "Scrap", v3 added a list.
            var migrator = new ProfileMigrator()
                .Add(1, json =>
                {
                    json["Scrap"] = json["Coins"];
                    json.Remove("Coins");
                })
                .Add(2, json => json["Badges"] = new JArray("veteran"));
            JObject old = JObject.Parse("{\"Version\":1,\"Coins\":77}");
            Assert.AreEqual(ProfileMigrator.Outcome.Migrated, migrator.Migrate(old, 3));
            Assert.AreEqual(3, old.Value<int>("Version"));
            Assert.AreEqual(77, old.Value<int>("Scrap"));
            Assert.AreEqual("veteran", old["Badges"][0].ToString());
            Assert.AreEqual(ProfileMigrator.Outcome.Unsupported, new ProfileMigrator().Migrate(JObject.Parse("{\"Version\":1}"), 2));

            // A profile from a newer build: loaded, never written back.
            var store = new JsonFileSaveStore(_dir);
            string future = "{\"Version\":" + (ProfileData.CurrentVersion + 1) + ",\"Scrap\":999,\"Owned\":[\"x\"]}";
            store.Write(SaveService.ProfileKey, future);
            var service = new SaveService(store);
            Assert.IsTrue(service.ProfileReadOnly);
            Assert.AreEqual(999, service.Profile.Scrap);
            service.Profile.Scrap = 1;
            service.SaveNow();
            Assert.IsTrue(store.TryRead(SaveService.ProfileKey, out string kept));
            Assert.AreEqual(future, kept);
        }

        [Test]
        public void MetaSelection_TravelsInTheRoster_AndUpdatesInTheLobby()
        {
            var net = new TestNet();
            var host = net.CreateSession("Host");
            Assert.IsTrue(host.StartHost());
            var client = net.CreateSession("Client");
            ulong first = new PlayerMeta { Character = 1, Perk = 2, Loadout = 0, Title = PlayerMeta.None }.Pack();
            client.SetLocalMeta(first);
            client.Join("loopback", NetProtocol.DefaultGamePort);
            net.Step(0.5);
            Assert.AreEqual(first, host.Players[1].Meta, "sent with the join request");

            ulong second = new PlayerMeta { Character = 0, Perk = 4, Loadout = 3, Title = 1 }.Pack();
            client.SetLocalMeta(second);
            net.Step(0.3);
            Assert.AreEqual(second, host.Players[1].Meta);
            Assert.AreEqual(second, client.Players[1].Meta, "the roster brings it back to everyone");
            host.SetLocalMeta(first);
            net.Step(0.3);
            Assert.AreEqual(first, client.Players[0].Meta);
        }
    }
}
