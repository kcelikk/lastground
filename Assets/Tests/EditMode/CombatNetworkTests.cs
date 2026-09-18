using System.Collections.Generic;
using LastGround.Core.Events;
using LastGround.Core.Net.Protocol;
using LastGround.Core.Net.Session;
using LastGround.Data.Players;
using LastGround.Data.Weapons;
using LastGround.Data.Zombies;
using LastGround.Gameplay.Combat;
using LastGround.Gameplay.Crowd;
using LastGround.Gameplay.Navigation;
using LastGround.Gameplay.Players;
using LastGround.Gameplay.Zombies;
using LastGround.Networking.Replication;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

namespace LastGround.Tests
{
    /// <summary>
    /// M4 in miniature over loopback: a client shoots zombies it sees in its replica, the host validates the claims
    /// and kills them, the client receives the deaths; player vitals flow host → client.
    /// </summary>
    public class CombatNetworkTests
    {
        sealed class Rig : System.IDisposable
        {
            public TestNet Net;
            public NetSession Host, Client;
            public PlayerStateTable HostPlayers, ClientPlayers;
            public NavGrid Nav;
            public CrowdState Crowd;
            public ZombieWorld World;
            public CombatAuthority Authority;
            public PlayerHealthSystem Health;
            public PlayerSync HostSync, ClientSync;
            public PlayerVitalsSync HostVitals, ClientVitals;
            public HitClaimSync HostClaims, ClientClaims;
            public CrowdReplicationSender Sender;
            public CrowdReplica Replica;
            public CrowdReplicationReceiver Receiver;
            public WeaponController ClientWeapon;
            public CombatTests.ScriptedInput ClientInput = new CombatTests.ScriptedInput();
            public readonly List<Object> Assets = new List<Object>();
            uint _tick;

            public void Tick(float dt)
            {
                uint tick = _tick++;
                World.Tick(dt, tick);
                Authority.Tick(dt, tick);
                Health.Tick(dt, tick);
                ClientSync.Tick(dt, tick);
                HostSync.Tick(dt, tick);
                Sender.Tick(dt, tick);
                HostVitals.Tick(dt, tick);
                Receiver.Tick(dt, tick);
                ClientSync.Interpolate();
                HostSync.Interpolate();
                // The client's weapon runs per frame; two frames per sim tick.
                ClientWeapon.Tick(dt * 0.5f, tick);
                ClientWeapon.Tick(dt * 0.5f, tick);
                ClientClaims.Tick(dt, tick);
            }

            public void Dispose()
            {
                World.Dispose();
                Nav.Dispose();
                foreach (Object asset in Assets) Object.DestroyImmediate(asset);
            }
        }

        static Rig Create()
        {
            LogAssert.ignoreFailingMessages = true;
            var rig = new Rig { Net = new TestNet() };
            rig.Host = rig.Net.CreateSession("Host");
            Assert.IsTrue(rig.Host.StartHost());
            rig.Client = rig.Net.CreateSession("Client");
            rig.Client.Join("loopback", NetProtocol.DefaultGamePort);
            rig.Net.Step(0.5);
            Assert.AreEqual(SessionState.Connected, rig.Client.State);

            var rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
            var walker = ScriptableObject.CreateInstance<ZombieDefinition>();
            var player = ScriptableObject.CreateInstance<PlayerDefinition>();
            rig.Assets.Add(rifle);
            rig.Assets.Add(walker);
            rig.Assets.Add(player);

            rig.HostPlayers = new PlayerStateTable { Local = rig.Host.LocalPlayer };
            rig.ClientPlayers = new PlayerStateTable { Local = rig.Client.LocalPlayer };
            rig.HostPlayers.SetLocal(-20f, -20f, 0f, 0f, 0f);
            rig.ClientPlayers.SetLocal(0f, 0f, 0f, 0f, 0f);

            rig.Nav = NavGrid.Open(100);
            rig.Crowd = new CrowdState(64);
            rig.World = new ZombieWorld(rig.Crowd, rig.HostPlayers, rig.Nav, new ZombieTuning(), walker, 9u);
            rig.Health = new PlayerHealthSystem(rig.HostPlayers, player);
            rig.World.DamageSink = rig.Health;
            rig.Authority = new CombatAuthority(rig.World, rig.HostPlayers, rig.Nav, new[] { rifle }, 9u);
            rig.HostSync = new PlayerSync(rig.Host, rig.HostPlayers, player.MoveSpeed);
            rig.ClientSync = new PlayerSync(rig.Client, rig.ClientPlayers, player.MoveSpeed);
            rig.HostVitals = new PlayerVitalsSync(rig.Host, rig.HostPlayers);
            rig.ClientVitals = new PlayerVitalsSync(rig.Client, rig.ClientPlayers);
            rig.HostClaims = new HitClaimSync(rig.Host, rig.Authority);
            rig.ClientClaims = new HitClaimSync(rig.Client, null);
            rig.Sender = new CrowdReplicationSender(rig.Host, rig.Crowd, rig.HostPlayers, new ReplicationTuning());
            rig.Replica = new CrowdReplica(64);
            rig.Receiver = new CrowdReplicationReceiver(rig.Client, rig.Replica);
            rig.ClientWeapon = new WeaponController(rig.ClientPlayers, rig.ClientInput, rifle, rig.Replica, rig.Nav, 9u,
                rig.ClientClaims, null, rig.Replica);
            return rig;
        }

        [Test]
        public void ClientShots_KillZombies_OnTheHost_AndTheClientSeesTheCorpses()
        {
            using (Rig rig = Create())
            {
                rig.Net.Step(1.0, perTick: rig.Tick);
                // A column of zombies straight ahead of the client player.
                for (int i = 0; i < 5; i++) rig.World.Spawn(new float2(0f, 10f + i * 1.5f), 180f);
                rig.Net.Step(0.5, perTick: rig.Tick);
                Assert.AreEqual(5, rig.Replica.ActiveCount, "the client sees the zombies before shooting");

                var deaths = rig.Replica.Deaths.CreateReader();
                rig.ClientInput.Aim(0f, 1f, true);
                rig.Net.Step(4.0, perTick: rig.Tick);

                int clientDeaths = 0;
                while (rig.Replica.Deaths.TryRead(ref deaths, out _)) clientDeaths++;
                Debug.Log($"[Test] shots {rig.ClientWeapon.ShotsFired}, claims {rig.ClientWeapon.ClaimsSent}, accepted {rig.Authority.Accepted}, " +
                    $"rejected {rig.Authority.Rejected}, kills {rig.Authority.Kills}");
                Assert.AreEqual(5, rig.Authority.Kills, "all five die");
                Assert.AreEqual(5, clientDeaths, "and the client gets five corpses");
                Assert.AreEqual(0, rig.Replica.ActiveCount);
                Assert.Greater(rig.Authority.Accepted, 5 * 2);
                Assert.LessOrEqual(rig.Authority.Rejected, rig.Authority.Accepted / 4,
                    "stale claims after a kill are expected, cheating-level rejections are not");
                Assert.AreEqual(0, rig.Authority.CountOf(HitClaimVerdict.FarFromTarget));
                Assert.AreEqual(0, rig.Authority.CountOf(HitClaimVerdict.RateLimited));
            }
        }

        [Test]
        public void Vitals_ReachTheClient_WithHurtEvents()
        {
            using (Rig rig = Create())
            {
                rig.Net.Step(1.0, perTick: rig.Tick);
                int me = rig.Client.LocalPlayer.Value;
                Assert.AreEqual(100f, rig.ClientPlayers.Health[me], 0.05f);

                EventReader<PlayerHurt> hurt = rig.ClientPlayers.Hurt.CreateReader();
                rig.Health.Damage(me, 30f);
                rig.Net.Step(0.3, perTick: rig.Tick);
                Assert.AreEqual(70f, rig.ClientPlayers.Health[me], 0.05f);
                Assert.IsTrue(rig.ClientPlayers.Hurt.TryRead(ref hurt, out PlayerHurt h));
                Assert.AreEqual(me, h.Player);
                Assert.AreEqual(30f, h.Amount, 0.1f);

                rig.Health.Damage(me, 80f);
                rig.Net.Step(0.3, perTick: rig.Tick);
                Assert.IsTrue(rig.ClientPlayers.Dead[me]);
                Assert.IsTrue(rig.ClientPlayers.Hurt.TryRead(ref hurt, out h));
                Assert.IsTrue(h.Died);
            }
        }

        [Test]
        public void Firing_IsVisibleToOtherPlayers()
        {
            using (Rig rig = Create())
            {
                rig.Net.Step(1.0, perTick: rig.Tick);
                int client = rig.Client.LocalPlayer.Value;
                rig.ClientInput.Aim(1f, 0f, true);
                rig.Net.Step(0.3, perTick: rig.Tick);
                Assert.IsTrue(rig.HostPlayers.Firing[client], "the host draws the client's tracers");
                rig.ClientInput.Aim(1f, 0f, false);
                rig.Net.Step(0.5, perTick: rig.Tick);
                Assert.IsFalse(rig.HostPlayers.Firing[client]);
            }
        }
    }
}
