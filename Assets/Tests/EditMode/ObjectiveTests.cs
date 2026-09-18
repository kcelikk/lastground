using System.Collections.Generic;
using LastGround.Core.Ids;
using LastGround.Core.Net.Protocol;
using LastGround.Core.Net.Session;
using LastGround.Data.Loot;
using LastGround.Data.Map;
using LastGround.Data.Objectives;
using LastGround.Data.Players;
using LastGround.Data.Zombies;
using LastGround.Gameplay.Crowd;
using LastGround.Gameplay.Director;
using LastGround.Gameplay.Loot;
using LastGround.Gameplay.Objectives;
using LastGround.Gameplay.Players;
using LastGround.Networking.Replication;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LastGround.Tests
{
    /// <summary>M5 "Clear the area" objectives (TDD_01 §12.4, D-019).</summary>
    public class ObjectiveTests
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

        MapZoneSet Zones()
        {
            var zones = Asset<MapZoneSet>();
            zones.Zones = new[]
            {
                new MapZoneSet.Zone { Id = "a", NameKey = "zone.a", Center = new Vector2(0f, 30f), HalfSize = new Vector2(10f, 5f) },
                new MapZoneSet.Zone { Id = "b", NameKey = "zone.b", Center = new Vector2(0f, -30f), HalfSize = new Vector2(10f, 5f) },
            };
            return zones;
        }

        sealed class Rig
        {
            public readonly CrowdState Crowd = new CrowdState(64);
            public readonly PlayerStateTable Players = new PlayerStateTable { Local = new PlayerId(0) };
            public readonly RunStatus Status = new RunStatus();
            public readonly ObjectiveState State = new ObjectiveState();
            public ObjectiveSystem System;
            public ObjectiveDefinition Definition;
            public MapZoneSet Zones;
            public PickupTable Pickups = new PickupTable(64);

            public void Kill(float x, float z)
            {
                int slot = Crowd.Spawn(0, x, z, 0f);
                Crowd.Despawn(slot, died: true);
            }

            public void Run(float seconds)
            {
                for (int t = 0; t < seconds * 30f; t++) System.Tick(Dt, (uint)t);
            }

            public void KillInActiveZone(int count)
            {
                Vector2 c = Zones.Zones[State.Zone].Center;
                for (int i = 0; i < count; i++) Kill(c.x + (i % 5) - 2f, c.y);
            }
        }

        Rig Create(int players = 1, int threat = 1)
        {
            var rig = new Rig { Definition = Asset<ObjectiveDefinition>(), Zones = Zones() };
            rig.Status.Threat = threat;
            rig.Players.SetLocal(0f, 0f, 0f, 0f, 0f);
            for (int p = 1; p < players; p++) rig.Players.PushRemote(new PlayerId((byte)p), 0f, 0f, 0f, 0f, 0f, 0.0, 0.0);
            var loot = Asset<LootDefinition>();
            var registry = new PickupRegistry(rig.Pickups, new CrowdState(8), rig.Players, loot, Asset<ZombieDefinition>(), new TeamWallet(), 1u);
            rig.System = new ObjectiveSystem(rig.Crowd, rig.Players, rig.Zones, rig.Definition, rig.Status, rig.State, 3u) { Loot = registry };
            return rig;
        }

        [Test]
        public void ClearTheArea_CountsOnlyKillsInsideTheZone_AndRewards()
        {
            Rig rig = Create();
            rig.Run(rig.Definition.FirstDelay - 1f);
            Assert.AreEqual(ObjectivePhase.None, rig.State.Phase);
            rig.Run(1.1f);
            Assert.AreEqual(ObjectivePhase.Active, rig.State.Phase);
            Assert.AreEqual(rig.Definition.TargetBase, rig.State.Target);

            rig.Kill(0f, 0f); // outside every zone
            rig.KillInActiveZone(10);
            rig.Run(Dt);
            Assert.AreEqual(10, rig.State.Current, "only kills inside the zone count");

            rig.KillInActiveZone(rig.State.Target - 10);
            rig.Run(Dt);
            Assert.AreEqual(ObjectivePhase.Completed, rig.State.Phase);
            Assert.AreEqual(1, rig.System.Completed);
            int coins = 0, medkits = 0;
            for (int i = 0; i < rig.Pickups.Capacity; i++)
            {
                if (!rig.Pickups.Active[i]) continue;
                if (rig.Pickups.Type[i] == PickupType.Coin) coins += rig.Pickups.Value[i];
                else medkits++;
            }
            Assert.AreEqual(rig.Definition.RewardCoinsPerPlayer, coins, "reward pile in the zone");
            Assert.AreEqual(rig.Definition.RewardMedkits, medkits);
        }

        [Test]
        public void NextObjective_ComesAfterTheCooldown_InAnotherZone()
        {
            Rig rig = Create();
            rig.Run(rig.Definition.FirstDelay + 0.1f);
            int first = rig.State.Zone;
            ushort instance = rig.State.Instance;
            rig.KillInActiveZone(rig.State.Target);
            rig.Run(rig.Definition.CompletedShowTime + 0.1f);
            Assert.AreEqual(ObjectivePhase.None, rig.State.Phase, "panel hides between objectives");
            rig.Run(rig.Definition.Cooldown + 0.1f);
            Assert.AreEqual(ObjectivePhase.Active, rig.State.Phase);
            Assert.AreNotEqual(instance, rig.State.Instance);
            Assert.AreNotEqual(first, rig.State.Zone, "never the same zone twice in a row");
            Assert.AreEqual(0, rig.State.Current);
        }

        [Test]
        public void Target_ScalesWithPlayersAndThreat()
        {
            Rig rig = Create(players: 3, threat: 3);
            rig.Run(rig.Definition.FirstDelay + 0.1f);
            int expected = Mathf.RoundToInt(rig.Definition.TargetBase * (1f + 2 * rig.Definition.TargetPerExtraPlayer)) + 2 * rig.Definition.TargetPerThreat;
            Assert.AreEqual(expected, rig.State.Target);
        }

        [Test]
        public void Coop_ObjectiveState_ReachesTheClient()
        {
            LogAssert.ignoreFailingMessages = true;
            var net = new TestNet();
            NetSession host = net.CreateSession("Host");
            Assert.IsTrue(host.StartHost());
            NetSession client = net.CreateSession("Client");
            client.Join("loopback", NetProtocol.DefaultGamePort);
            net.Step(0.5);

            Rig rig = Create();
            var clientState = new ObjectiveState();
            var hostSync = new ObjectiveSync(host, rig.State);
            var clientSync = new ObjectiveSync(client, clientState);
            net.Step(rig.Definition.FirstDelay + 0.5f, perTick: dt => { rig.System.Tick(dt, 0); hostSync.Tick(dt, 0); });
            rig.KillInActiveZone(7);
            net.Step(0.6, perTick: dt => { rig.System.Tick(dt, 0); hostSync.Tick(dt, 0); });
            Assert.AreEqual(rig.State.Instance, clientState.Instance);
            Assert.AreEqual(rig.State.Zone, clientState.Zone);
            Assert.AreEqual(7, clientState.Current);
            Assert.AreEqual(rig.State.Target, clientState.Target);
            Assert.AreEqual(ObjectivePhase.Active, clientState.Phase);
            hostSync.Dispose();
            clientSync.Dispose();
        }
    }
}
