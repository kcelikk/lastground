using System.Collections.Generic;
using LastGround.Core.Events;
using LastGround.Core.Net.Protocol;
using LastGround.Core.Net.Session;
using LastGround.Data.Combat;
using LastGround.Gameplay.Combat;
using LastGround.Gameplay.Director;
using LastGround.Gameplay.Players;
using LastGround.Gameplay.Projectiles;
using LastGround.Networking.Replication;
using NUnit.Framework;
using Unity.Mathematics;

using UnityEngine;

namespace LastGround.Tests
{
    /// <summary>M6 over loopback: loadouts, grenade throws, projectile replay, blasts and director banners.</summary>
    public class CombatContentNetworkTests
    {
        const string CatalogPath = "Assets/ScriptableObjects/Combat/CMB_Catalog.asset";

        sealed class Rig
        {
            public TestNet Net;
            public NetSession Host, Client;
            public PlayerStateTable HostPlayers;
            public LoadoutTable HostLoadouts, ClientLoadouts;
            public LoadoutAuthority Authority;
            public LoadoutSync HostLoadoutSync, ClientLoadoutSync;
            public ProjectileTable HostProjectiles, ClientProjectiles;
            public ProjectileSystem Projectiles;
            public EventChannel<ExplosionFx> HostBlasts = new EventChannel<ExplosionFx>(32);
            public EventChannel<ExplosionFx> ClientBlasts = new EventChannel<ExplosionFx>(32);
            public CombatFxSync HostFx, ClientFx;
            public RunStatus HostStatus = new RunStatus(), ClientStatus = new RunStatus();
            public DirectorInfoSync HostInfo, ClientInfo;

            public void Tick(float dt)
            {
                Authority.Tick(dt, 0);
                Projectiles.Tick(dt, 0);
                HostLoadoutSync.Tick(dt, 0);
                HostFx.Tick(dt, 0);
                ClientFx.Tick(dt, 0);
                HostInfo.Tick(dt, 0);
            }
        }

        static Rig Create()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<CombatCatalog>(CatalogPath);
            var rig = new Rig { Net = new TestNet() };
            rig.Host = rig.Net.CreateSession("Host");
            Assert.IsTrue(rig.Host.StartHost());
            rig.Client = rig.Net.CreateSession("Client");
            rig.Client.Join("loopback", NetProtocol.DefaultGamePort);
            rig.Net.Step(0.5);
            Assert.AreEqual(SessionState.Connected, rig.Client.State);

            rig.HostPlayers = new PlayerStateTable { Local = rig.Host.LocalPlayer };
            rig.HostPlayers.SetLocal(0f, 0f, 0f, 0f, 0f);
            rig.HostPlayers.PushRemote(rig.Client.LocalPlayer, 3f, 0f, 0f, 0f, 0f, 0.0, 0.0);
            rig.HostLoadouts = new LoadoutTable(catalog.Weapons);
            rig.ClientLoadouts = new LoadoutTable(catalog.Weapons);
            rig.HostProjectiles = new ProjectileTable(catalog.Projectiles);
            rig.ClientProjectiles = new ProjectileTable(catalog.Projectiles);
            rig.Projectiles = new ProjectileSystem(rig.HostProjectiles, catalog.Projectiles, rig.HostPlayers, null, null, null);
            rig.Authority = new LoadoutAuthority(rig.HostLoadouts, rig.HostPlayers, catalog, rig.Projectiles);
            rig.HostLoadoutSync = new LoadoutSync(rig.Host, rig.HostLoadouts, rig.Authority);
            rig.ClientLoadoutSync = new LoadoutSync(rig.Client, rig.ClientLoadouts, null);
            rig.HostFx = new CombatFxSync(rig.Host, rig.HostProjectiles, rig.HostBlasts);
            rig.ClientFx = new CombatFxSync(rig.Client, rig.ClientProjectiles, rig.ClientBlasts);
            rig.HostInfo = new DirectorInfoSync(rig.Host, rig.HostStatus);
            rig.ClientInfo = new DirectorInfoSync(rig.Client, rig.ClientStatus);
            return rig;
        }

        [Test]
        public void Loadouts_Replicate_AndClientGrenadeFliesOnBothDevices()
        {
            Rig rig = Create();
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<CombatCatalog>(CatalogPath);
            rig.Net.Step(0.3, perTick: rig.Tick);
            int client = rig.Client.LocalPlayer.Value;
            Assert.AreEqual(catalog.StartPrimary.NetIndex, rig.ClientLoadouts.Primary[client]);
            Assert.AreEqual(catalog.StartSidearm.NetIndex, rig.ClientLoadouts.Sidearm[client]);
            Assert.AreEqual(catalog.StartGrenades, rig.ClientLoadouts.Grenades[client]);
            Assert.AreEqual(rig.HostLoadouts.Primary[rig.Host.LocalPlayer.Value], rig.ClientLoadouts.Primary[rig.Host.LocalPlayer.Value]);

            var launched = rig.ClientProjectiles.Launched.CreateReader();
            var ended = rig.ClientProjectiles.Ended.CreateReader();
            rig.ClientLoadoutSync.Throw(client, new float2(3f, 8f));
            rig.Net.Step(0.2, perTick: rig.Tick);
            Assert.AreEqual(1, rig.Authority.Thrown);
            Assert.AreEqual(catalog.StartGrenades - 1, rig.ClientLoadouts.Grenades[client], "the stock drops everywhere");
            Assert.IsTrue(rig.ClientProjectiles.Launched.TryRead(ref launched, out ProjectileLaunched launch));
            Assert.AreEqual(client, launch.Owner);
            Assert.AreEqual(8f, launch.EndZ, 0.05f);
            Assert.AreEqual(rig.HostProjectiles.Age[launch.Id], rig.ClientProjectiles.Age[launch.Id], 0.1f, "the client replays the same moment");

            rig.Net.Step(1.5, perTick: rig.Tick);
            Assert.IsTrue(rig.ClientProjectiles.Ended.TryRead(ref ended, out ProjectileEnded end));
            Assert.AreEqual(8f, end.Z, 0.05f);
            Assert.IsFalse(rig.ClientProjectiles.Active[launch.Id]);

            // A fourth throw fails once the stock is gone.
            for (int i = 0; i < 4; i++)
            {
                rig.ClientLoadoutSync.Throw(client, new float2(0f, 5f));
                rig.Net.Step(1.0, perTick: rig.Tick);
            }
            Assert.AreEqual(catalog.StartGrenades, rig.Authority.Thrown);
            Assert.AreEqual(0, rig.ClientLoadouts.Grenades[client]);
        }

        [Test]
        public void Blasts_AndAnnouncements_ReachTheClient()
        {
            Rig rig = Create();
            var blasts = rig.ClientBlasts.CreateReader();
            var banners = rig.ClientStatus.Announcements.CreateReader();
            rig.HostBlasts.Publish(new ExplosionFx { X = 4f, Z = -2f, Radius = 3.5f, Kind = ExplosionKind.Exploder });
            rig.HostStatus.Announcements.Publish(new DirectorAnnouncement { Kind = AnnouncementKind.EliteSpawned, ZombieType = 2, Elite = 4 });
            rig.Net.Step(0.2, perTick: rig.Tick);
            Assert.IsTrue(rig.ClientBlasts.TryRead(ref blasts, out ExplosionFx fx));
            Assert.AreEqual(ExplosionKind.Exploder, fx.Kind);
            Assert.AreEqual(3.5f, fx.Radius, 0.05f);
            Assert.AreEqual(4f, fx.X, 0.05f);
            Assert.IsTrue(rig.ClientStatus.Announcements.TryRead(ref banners, out DirectorAnnouncement a));
            Assert.AreEqual(AnnouncementKind.EliteSpawned, a.Kind);
            Assert.AreEqual(2, a.ZombieType);
            Assert.AreEqual(4, a.Elite);
        }
    }
}
