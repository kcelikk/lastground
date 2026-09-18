using System.Collections.Generic;
using LastGround.Core.Ids;
using LastGround.Core.Net.Protocol;
using LastGround.Core.Net.Session;
using LastGround.Data.Upgrades;
using LastGround.Data.Weapons;
using LastGround.Gameplay.Crowd;
using LastGround.Gameplay.Players;
using LastGround.Gameplay.Upgrades;
using LastGround.Networking.Replication;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LastGround.Tests
{
    /// <summary>M5 team XP, level-ups, offers, builds and their replication.</summary>
    public class ProgressionTests
    {
        readonly List<Object> _assets = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object a in _assets) Object.DestroyImmediate(a);
            _assets.Clear();
        }

        T Asset<T>() where T : ScriptableObject
        {
            var a = ScriptableObject.CreateInstance<T>();
            _assets.Add(a);
            return a;
        }

        UpgradeCatalog Catalog()
        {
            var catalog = Asset<UpgradeCatalog>();
            var stats = new[] { StatId.DamagePct, StatId.FireRatePct, StatId.MagazinePct, StatId.MaxHealth, StatId.Pierce };
            catalog.Upgrades = new UpgradeDefinition[stats.Length];
            for (int i = 0; i < stats.Length; i++)
            {
                var u = Asset<UpgradeDefinition>();
                u.Id = "u" + i;
                u.Stat = stats[i];
                u.Values = new[] { 10f, 20f, 30f, 40f };
                u.MaxStacks = 2;
                catalog.Upgrades[i] = u;
            }
            return catalog;
        }

        sealed class RecordingOffers : IOfferSink
        {
            public readonly List<(int player, UpgradeOffer offer)> Offers = new List<(int, UpgradeOffer)>();
            public void Deliver(int player, in UpgradeOffer offer) => Offers.Add((player, offer));
        }

        static PlayerStateTable TwoPlayers()
        {
            var players = new PlayerStateTable { Local = new PlayerId(0) };
            players.SetLocal(0f, 0f, 0f, 0f, 0f);
            players.PushRemote(new PlayerId(1), 5f, 0f, 0f, 0f, 0f, 0.0, 0.0);
            return players;
        }

        static void Kill(CrowdState crowd, int count)
        {
            for (int i = 0; i < count; i++)
            {
                int slot = crowd.Spawn(0, 0f, 0f, 0f);
                crowd.Despawn(slot, died: true);
            }
        }

        [Test]
        public void Kills_FillOneTeamPool_AndEveryoneGetsAnOffer_OnLevelUp()
        {
            var curve = Asset<LevelCurveDefinition>();
            var crowd = new CrowdState(64);
            PlayerStateTable players = TwoPlayers();
            var builds = new TeamBuilds(Catalog());
            var offers = new RecordingOffers();
            var progress = new TeamProgress(crowd, players, builds, curve, 1, 42u) { Offers = offers };

            int needed = curve.XpForLevel(1, 2);
            Kill(crowd, needed - 1);
            progress.Tick(0.033f, 0);
            Assert.AreEqual(1, progress.Level);
            Assert.AreEqual(needed - 1, progress.Xp);
            Assert.AreEqual(0, offers.Offers.Count);

            Kill(crowd, 1);
            progress.Tick(0.033f, 1);
            Assert.AreEqual(2, progress.Level);
            Assert.AreEqual(2, offers.Offers.Count, "both players level up together (D-002)");
            Assert.AreEqual(0, offers.Offers[0].player);
            Assert.AreEqual(1, offers.Offers[1].player);
            Assert.AreEqual(3, offers.Offers[0].offer.Count);
            Assert.AreNotEqual(offers.Offers[0].offer.Id, offers.Offers[1].offer.Id);
            Assert.Greater(curve.XpForLevel(2, 2), needed, "levels get longer");
        }

        [Test]
        public void Offers_AreDeterministicPerPlayer_AndNeverRepeatAChoice()
        {
            UpgradeCatalog catalog = Catalog();
            var generator = new OfferGenerator(catalog, 7u);
            var build = new PlayerBuild(catalog.Upgrades.Length);
            UpgradeOffer a = generator.Generate(build, 0, 5);
            UpgradeOffer again = generator.Generate(build, 0, 5);
            Assert.AreEqual(a.Upgrade0, again.Upgrade0);
            Assert.AreEqual(a.Rarity1, again.Rarity1);
            Assert.AreNotEqual(a.Upgrade0, a.Upgrade1);
            Assert.AreNotEqual(a.Upgrade1, a.Upgrade2);
            Assert.AreNotEqual(a.Upgrade0, a.Upgrade2);

            int differ = 0;
            for (ushort id = 1; id < 40; id++)
            {
                UpgradeOffer p0 = generator.Generate(build, 0, id);
                UpgradeOffer p1 = generator.Generate(build, 1, id);
                if (p0.Upgrade0 != p1.Upgrade0 || p0.Upgrade1 != p1.Upgrade1) differ++;
            }
            Assert.Greater(differ, 20, "teammates roll their own offers");
        }

        [Test]
        public void Rarity_FollowsTheWeights()
        {
            UpgradeCatalog catalog = Catalog();
            var generator = new OfferGenerator(catalog, 3u);
            var build = new PlayerBuild(catalog.Upgrades.Length);
            var counts = new int[4];
            for (ushort id = 1; id < 3001; id++) counts[(int)generator.Generate(build, 0, id).Rarity0]++;
            Assert.That(counts[0], Is.InRange(1650, 1950), "common ~60 %");
            Assert.That(counts[1], Is.InRange(700, 990), "rare ~28 %");
            Assert.That(counts[2], Is.InRange(220, 390), "epic ~10 %");
            Assert.That(counts[3], Is.InRange(25, 110), "legendary ~2 %");
        }

        [Test]
        public void Choosing_AppliesTheBuild_ValidatesTheOffer_AndRespectsStackCaps()
        {
            UpgradeCatalog catalog = Catalog();
            var curve = Asset<LevelCurveDefinition>();
            var crowd = new CrowdState(512);
            PlayerStateTable players = TwoPlayers();
            var builds = new TeamBuilds(catalog);
            var offers = new RecordingOffers();
            var progress = new TeamProgress(crowd, players, builds, curve, 1, 9u) { Offers = offers };
            progress.AddXp(curve.XpForLevel(1, 2));
            UpgradeOffer offer = offers.Offers[0].offer;

            progress.Choose(0, (ushort)(offer.Id + 99), 0);
            Assert.AreEqual(0, builds.Of(0).Picks, "unknown offer id is ignored");
            progress.Choose(0, offer.Id, 1);
            Assert.AreEqual(1, builds.Of(0).Picks);
            Assert.AreEqual(1, builds.Of(0).StacksOf(offer.Upgrade1));
            Assert.AreEqual(0, builds.Of(1).Picks, "only the chooser's build changes");
            progress.Choose(0, offer.Id, 1);
            Assert.AreEqual(1, builds.Of(0).Picks, "an offer is used once");

            // Two stacks max: the upgrade stops being offered.
            builds.Apply(1, 0, UpgradeRarity.Common);
            builds.Apply(1, 0, UpgradeRarity.Common);
            var generator = new OfferGenerator(catalog, 1u);
            for (ushort id = 1; id < 60; id++)
            {
                UpgradeOffer o = generator.Generate(builds.Of(1), 1, id);
                for (int i = 0; i < o.Count; i++) Assert.AreNotEqual(0, o.UpgradeAt(i));
            }
        }

        [Test]
        public void WeaponStats_FollowTheBuild()
        {
            var rifle = Asset<WeaponDefinition>();
            UpgradeCatalog catalog = Catalog();
            var build = new PlayerBuild(catalog.Upgrades.Length);
            build.Apply(catalog, 0, UpgradeRarity.Epic);      // +30 % damage
            build.Apply(catalog, 1, UpgradeRarity.Common);    // +10 % fire rate
            build.Apply(catalog, 2, UpgradeRarity.Legendary); // +40 % magazine
            build.Apply(catalog, 4, UpgradeRarity.Common);    // +10 pierce (test value)
            WeaponStats stats = WeaponStats.From(rifle, build);
            Assert.AreEqual(rifle.Damage * 1.3f, stats.Damage, 1e-3f);
            Assert.AreEqual(rifle.FireRate * 1.1f, stats.FireRate, 1e-3f);
            Assert.AreEqual(42, stats.MagazineSize);
            Assert.AreEqual(rifle.Penetration + 10, stats.Penetration);
        }

        [Test]
        public void CoopNeverPauses_SoloPausesOnlyWithTheSetting()
        {
            Assert.IsFalse(LevelUpPause.ShouldPause(2, true, true), "TDD_03 §36 M5: level-up does not pause co-op");
            Assert.IsFalse(LevelUpPause.ShouldPause(4, true, true));
            Assert.IsTrue(LevelUpPause.ShouldPause(1, true, true));
            Assert.IsFalse(LevelUpPause.ShouldPause(1, true, false));
            Assert.IsFalse(LevelUpPause.ShouldPause(1, false, true));
        }

        [Test]
        public void Coop_XpAndBuilds_AreIdenticalOnHostAndClient()
        {
            LogAssert.ignoreFailingMessages = true;
            var net = new TestNet();
            NetSession host = net.CreateSession("Host");
            Assert.IsTrue(host.StartHost());
            NetSession client = net.CreateSession("Client");
            client.Join("loopback", NetProtocol.DefaultGamePort);
            net.Step(0.5);
            Assert.AreEqual(SessionState.Connected, client.State);

            UpgradeCatalog catalog = Catalog();
            var curve = Asset<LevelCurveDefinition>();
            var crowd = new CrowdState(512);
            var hostPlayers = new PlayerStateTable { Local = host.LocalPlayer };
            hostPlayers.SetLocal(0f, 0f, 0f, 0f, 0f);
            hostPlayers.PushRemote(client.LocalPlayer, 5f, 0f, 0f, 0f, 0f, 0.0, 0.0);
            var hostBuilds = new TeamBuilds(catalog);
            var clientBuilds = new TeamBuilds(catalog);
            var hostXp = new TeamXp();
            var clientXp = new TeamXp();
            var hostOffers = new LocalOffers(host.LocalPlayer.Value);
            var clientOffers = new LocalOffers(client.LocalPlayer.Value);
            var progress = new TeamProgress(crowd, hostPlayers, hostBuilds, curve, 1, 5u);
            var hostSync = new ProgressionSync(host, hostXp, hostBuilds, hostOffers, progress);
            var clientSync = new ProgressionSync(client, clientXp, clientBuilds, clientOffers, null);
            progress.Offers = hostSync;
            hostOffers.Choices = progress;
            clientOffers.Choices = clientSync;

            void Tick(float dt)
            {
                progress.Tick(dt, 0);
                hostSync.Tick(dt, 0);
            }

            for (int level = 0; level < 4; level++)
            {
                Kill(crowd, curve.XpForLevel(progress.Level, 2));
                net.Step(0.5, perTick: Tick);
                Assert.IsTrue(clientOffers.HasOffer, "the client gets its own offer");
                Assert.IsTrue(hostOffers.HasOffer);
                clientOffers.Choose(level % 3);
                hostOffers.Choose((level + 1) % 3);
                net.Step(0.5, perTick: Tick);
            }
            Kill(crowd, 7);
            net.Step(0.6, perTick: Tick);

            Assert.AreEqual(5, progress.Level);
            Assert.AreEqual(hostXp.Level, clientXp.Level, "TDD_03 §36 M5: XP equal on all clients");
            Assert.AreEqual(hostXp.Xp, clientXp.Xp);
            Assert.AreEqual(hostXp.XpToNext, clientXp.XpToNext);
            for (int p = 0; p < 2; p++)
            {
                Assert.AreEqual(4, hostBuilds.Of(p).Picks, "player " + p);
                Assert.AreEqual(hostBuilds.Of(p).Picks, clientBuilds.Of(p).Picks);
                for (int s = 0; s < (int)StatId.Count; s++)
                    Assert.AreEqual(hostBuilds.Of(p).Get((StatId)s), clientBuilds.Of(p).Get((StatId)s), "player " + p + " stat " + (StatId)s);
            }
            hostSync.Dispose();
            clientSync.Dispose();
        }
    }
}
