using System.Collections.Generic;
using LastGround.Core.Ids;
using LastGround.Core.Net.Protocol;
using LastGround.Core.Net.Session;
using LastGround.Data.Loot;
using LastGround.Data.Players;
using LastGround.Data.Zombies;
using LastGround.Gameplay.Crowd;
using LastGround.Gameplay.Loot;
using LastGround.Gameplay.Players;
using LastGround.Networking.Replication;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LastGround.Tests
{
    /// <summary>M5 loot: coin piles, team wallet, instanced medkits, claims and replication.</summary>
    public class LootTests
    {
        const float Dt = 1f / 30f;
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

        sealed class Rig
        {
            public readonly CrowdState Crowd = new CrowdState(64);
            public readonly PlayerStateTable Players = new PlayerStateTable { Local = new PlayerId(0) };
            public readonly PickupTable Table = new PickupTable(256);
            public readonly TeamWallet Wallet = new TeamWallet();
            public PickupRegistry Registry;
            public PlayerHealthSystem Health;
            public LootDefinition Loot;

            public void Kill(float x, float z)
            {
                int slot = Crowd.Spawn(0, x, z, 0f);
                Crowd.Despawn(slot, died: true);
            }

            public void Run(float seconds)
            {
                for (int t = 0; t < seconds * 30f; t++)
                {
                    Health.Tick(Dt, (uint)t);
                    Registry.Tick(Dt, (uint)t);
                }
            }

            public int ActiveOf(PickupType type)
            {
                int n = 0;
                for (int i = 0; i < Table.Capacity; i++) if (Table.Active[i] && Table.Type[i] == type) n++;
                return n;
            }

            public int First(PickupType type)
            {
                for (int i = 0; i < Table.Capacity; i++) if (Table.Active[i] && Table.Type[i] == type) return i;
                return -1;
            }
        }

        Rig Create(float coinChance, float medkitChance)
        {
            var rig = new Rig { Loot = Asset<LootDefinition>() };
            rig.Loot.MedkitChance = medkitChance;
            var walker = Asset<ZombieDefinition>();
            walker.CoinChance = coinChance;
            rig.Players.SetLocal(0f, 0f, 0f, 0f, 0f);
            rig.Players.PushRemote(new PlayerId(1), 20f, 0f, 0f, 0f, 0f, 0.0, 0.0);
            rig.Health = new PlayerHealthSystem(rig.Players, Asset<PlayerDefinition>());
            rig.Registry = new PickupRegistry(rig.Table, rig.Crowd, rig.Players, rig.Loot, walker, rig.Wallet, 4u) { Health = rig.Health };
            rig.Health.Tick(Dt, 0);
            return rig;
        }

        [Test]
        public void CoinDrops_InOneCell_MergeIntoOnePile()
        {
            Rig rig = Create(1f, 0f);
            for (int i = 0; i < 5; i++) rig.Kill(10.2f + i * 0.2f, 10.5f);
            rig.Kill(-10f, -10f);
            rig.Run(0.2f);
            Assert.AreEqual(0, rig.ActiveOf(PickupType.Coin), "piles wait for the merge window");
            rig.Run(0.5f);
            Assert.AreEqual(2, rig.ActiveOf(PickupType.Coin), "one pile per cell");
            int total = 0;
            for (int i = 0; i < rig.Table.Capacity; i++) if (rig.Table.Active[i]) total += rig.Table.Value[i];
            Assert.AreEqual(6, total);
        }

        [Test]
        public void Coins_PayTheWholeTeam_OnlyWhenCloseAndStanding()
        {
            Rig rig = Create(1f, 0f);
            rig.Kill(0.5f, 0.5f);
            rig.Run(0.6f);
            int coin = rig.First(PickupType.Coin);
            Assert.GreaterOrEqual(coin, 0);
            Assert.IsFalse(rig.Registry.Claim(1, coin), "player 1 is 20 m away");
            Assert.IsTrue(rig.Registry.Claim(0, coin));
            Assert.AreEqual(1, rig.Wallet.Coins);
            Assert.IsFalse(rig.Table.Active[coin], "team loot is gone once taken");
            Assert.IsFalse(rig.Registry.Claim(0, coin), "and cannot be taken twice");
        }

        [Test]
        public void Medkits_AreInstanced_EachPlayerTakesTheirOwnCopy()
        {
            Rig rig = Create(0f, 1f);
            rig.Kill(10f, 0f);
            rig.Run(Dt);
            int medkit = rig.First(PickupType.Medkit);
            Assert.GreaterOrEqual(medkit, 0);
            Assert.AreEqual(0b11, rig.Table.OwnerMask[medkit], "both players have a copy");

            rig.Players.SetLocal(10f, 0f, 0f, 0f, 0f);
            rig.Players.X[1] = 10f;
            Assert.IsFalse(rig.Registry.Claim(0, medkit), "full health: the copy stays on the ground");
            rig.Health.Damage(0, 50f);
            Assert.IsTrue(rig.Registry.Claim(0, medkit));
            Assert.AreEqual(80f, rig.Players.Health[0], 0.01f);
            Assert.IsTrue(rig.Table.Active[medkit], "player 1's copy is still there");
            Assert.IsFalse(rig.Table.VisibleTo(medkit, 0));
            Assert.IsTrue(rig.Table.VisibleTo(medkit, 1));
            Assert.IsFalse(rig.Registry.Claim(0, medkit), "player 0 already took theirs");
            rig.Health.Damage(1, 10f);
            Assert.IsTrue(rig.Registry.Claim(1, medkit));
            Assert.IsFalse(rig.Table.Active[medkit]);
        }

        [Test]
        public void Pickups_Expire()
        {
            Rig rig = Create(1f, 0f);
            rig.Kill(5f, 5f);
            rig.Run(0.6f);
            Assert.AreEqual(1, rig.ActiveOf(PickupType.Coin));
            rig.Run(rig.Loot.CoinLifetime);
            Assert.AreEqual(0, rig.ActiveOf(PickupType.Coin));
            Assert.AreEqual(0, rig.Wallet.Coins);
        }

        [Test]
        public void Coop_CoinsAndPickups_AreTheSameOnHostAndClient()
        {
            LogAssert.ignoreFailingMessages = true;
            var net = new TestNet();
            NetSession host = net.CreateSession("Host");
            Assert.IsTrue(host.StartHost());
            NetSession client = net.CreateSession("Client");
            client.Join("loopback", NetProtocol.DefaultGamePort);
            net.Step(0.5);
            Assert.AreEqual(SessionState.Connected, client.State);

            Rig rig = Create(1f, 1f);
            int me = client.LocalPlayer.Value;
            var clientTable = new PickupTable(256);
            var clientWallet = new TeamWallet();
            var hostSync = new LootSync(host, rig.Table, rig.Wallet, rig.Registry);
            var clientSync = new LootSync(client, clientTable, clientWallet, null);
            var clientPlayers = new PlayerStateTable { Local = client.LocalPlayer };

            void Tick(float dt)
            {
                rig.Registry.Tick(dt, 0);
                hostSync.Tick(dt, 0);
            }

            for (int i = 0; i < 10; i++) rig.Kill(i * 3f + 30f, 30f);
            net.Step(1.0, perTick: Tick);
            int spawned = 0;
            for (int i = 0; i < 256; i++)
            {
                Assert.AreEqual(rig.Table.Active[i], clientTable.Active[i], "slot " + i);
                if (!clientTable.Active[i]) continue;
                spawned++;
                Assert.AreEqual(rig.Table.Type[i], clientTable.Type[i]);
                Assert.AreEqual(rig.Table.Value[i], clientTable.Value[i]);
                Assert.AreEqual(rig.Table.X[i], clientTable.X[i], 0.05f);
            }
            Assert.Greater(spawned, 10, "coins and medkits reached the client");

            // The client walks onto a coin pile and claims it: everyone's wallet grows.
            int coin = rig.First(PickupType.Coin);
            rig.Players.X[me] = rig.Table.X[coin];
            rig.Players.Z[me] = rig.Table.Z[coin];
            clientSync.Claim(me, coin);
            net.Step(0.6, perTick: Tick);
            Assert.Greater(rig.Wallet.Coins, 0);
            Assert.AreEqual(rig.Wallet.Coins, clientWallet.Coins, "TDD_03 §36 M5: coin equal on all clients");
            Assert.IsFalse(clientTable.Active[coin]);
            hostSync.Dispose();
            clientSync.Dispose();
        }
    }
}
