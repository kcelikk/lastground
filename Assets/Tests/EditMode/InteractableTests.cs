using System.Collections.Generic;
using LastGround.Core.Ids;
using LastGround.Data.Combat;
using LastGround.Data.Map;
using LastGround.Gameplay.Interactables;
using LastGround.Gameplay.Players;
using LastGround.Gameplay.Zombies;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace LastGround.Tests
{
    /// <summary>M7 map interactables: barrels and fuel tanks (shots, chains, respawn) and medical stations.</summary>
    public class InteractableTests
    {
        readonly List<Object> _assets = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object a in _assets) Object.DestroyImmediate(a);
            _assets.Clear();
        }

        /// <summary>Records explosions and feeds them back as blasts, like the host explosion system does.</summary>
        sealed class ChainSink : IExplosionSink
        {
            public InteractableSystem System;
            public readonly List<float2> Centers = new List<float2>();

            public void Explode(float2 center, in ExplosionSpec spec, ExplosionKind kind, int sourcePlayer)
            {
                Assert.AreEqual(ExplosionKind.Barrel, kind);
                Assert.AreEqual(-1, sourcePlayer, "environment blasts are nobody's kills");
                Centers.Add(center);
                System.OnBlast(center, spec.Radius, spec.Damage);
            }
        }

        InteractableTable Table(params MapDefinition.Interactable[] items)
        {
            var map = ScriptableObject.CreateInstance<MapDefinition>();
            _assets.Add(map);
            map.Interactables = items;
            return new InteractableTable(map);
        }

        static MapDefinition.Interactable Item(InteractableKind kind, float x, float z) =>
            new MapDefinition.Interactable { Kind = kind, Position = new Vector2(x, z) };

        InteractableProfile Profile()
        {
            var profile = ScriptableObject.CreateInstance<InteractableProfile>();
            _assets.Add(profile);
            return profile;
        }

        static PlayerStateTable Player(float x, float z)
        {
            var players = new PlayerStateTable { Local = new PlayerId(0) };
            players.SetLocal(x, z, 0f, 0f, 0f);
            players.Health[0] = players.MaxHealth[0];
            return players;
        }

        [Test]
        public void Barrel_ShotDown_ChainsToItsNeighbour_AndComesBack()
        {
            InteractableProfile profile = Profile();
            InteractableTable table = Table(Item(InteractableKind.ExplosiveBarrel, 0f, 10f), Item(InteractableKind.ExplosiveBarrel, 1.5f, 10f),
                Item(InteractableKind.ExplosiveBarrel, 30f, 10f), Item(InteractableKind.MedStation, 0f, -10f));
            var sink = new ChainSink();
            var system = new InteractableSystem(table, profile, Player(0f, 0f), null, sink);
            sink.System = system;
            var changes = table.Changed.CreateReader();

            system.HitInteractable(0, 0, profile.BarrelHealth * 0.5f);
            Assert.IsTrue(table.Intact[0], "half its health");
            system.HitInteractable(0, 0, profile.BarrelHealth);
            Assert.IsFalse(table.Intact[0]);
            Assert.IsFalse(table.Intact[1], "the neighbour is inside the blast");
            Assert.IsTrue(table.Intact[2], "the far barrel is not");
            Assert.AreEqual(2, system.Explosions);
            Assert.AreEqual(2, sink.Centers.Count);
            int published = 0;
            while (table.Changed.TryRead(ref changes, out InteractableChanged _)) published++;
            Assert.AreEqual(2, published);

            system.HitInteractable(0, 0, 999f);
            Assert.AreEqual(2, system.Explosions, "a blown barrel ignores shots");
            system.HitInteractable(0, 3, 999f);
            Assert.IsTrue(table.Intact[3], "stations are not explosive");

            for (float t = 0f; t < profile.RespawnSeconds + 1f; t += 0.5f) system.Tick(0.5f, 0);
            Assert.IsTrue(table.Intact[0] && table.Intact[1], "barrels come back");
        }

        [Test]
        public void Shots_FromFarAway_OrFromADownedPlayer_AreRejected()
        {
            InteractableProfile profile = Profile();
            InteractableTable table = Table(Item(InteractableKind.FuelTank, 0f, 80f));
            PlayerStateTable players = Player(0f, 0f);
            var system = new InteractableSystem(table, profile, players, null, null);

            system.HitInteractable(0, 0, 999f);
            Assert.IsTrue(table.Intact[0], "out of claim range");
            system.HitInteractable(1, 0, 999f);
            Assert.IsTrue(table.Intact[0], "inactive shooter");
            system.HitInteractable(0, 7, 999f);

            players.SetLocal(0f, 70f, 0f, 0f, 0f);
            players.Life[0] = PlayerLife.Downed;
            system.HitInteractable(0, 0, 999f);
            Assert.IsTrue(table.Intact[0], "downed players cannot shoot");
            players.Life[0] = PlayerLife.Alive;
            system.HitInteractable(0, 0, profile.TankHealth - 1f);
            Assert.IsTrue(table.Intact[0], "tanks are tougher than barrels");
            system.HitInteractable(0, 0, 1f);
            Assert.IsFalse(table.Intact[0]);
        }

        [Test]
        public void MedStation_HealsPlayersInRange_FromARechargingPool()
        {
            InteractableProfile profile = Profile();
            InteractableTable table = Table(Item(InteractableKind.MedStation, 0f, 0f));
            PlayerStateTable players = Player(10f, 0f);
            var system = new InteractableSystem(table, profile, players, null, null);

            players.Health[0] = 10f;
            system.Tick(1f, 0);
            Assert.AreEqual(10f, players.Health[0], 1e-3f, "out of range");

            players.SetLocal(1f, 0f, 0f, 0f, 0f);
            system.Tick(1f, 0);
            Assert.AreEqual(10f + profile.MedHealPerSecond, players.Health[0], 1e-3f);
            for (int i = 0; i < 20; i++) system.Tick(1f, 0);
            Assert.AreEqual(players.MaxHealth[0], players.Health[0], 1e-3f, "stops at full health");

            // Drain the pool: heal three full bars' worth, then check that it runs dry and recharges.
            float healedBefore = system.Healed;
            for (int round = 0; round < 3; round++)
            {
                players.Health[0] = 1f;
                for (int i = 0; i < 10; i++) system.Tick(1f, 0);
            }
            Assert.LessOrEqual(system.Healed - healedBefore, profile.MedCharge + 30f * profile.MedRechargePerSecond + 1f, "pool limit");
            Assert.Less(table.Charge[0], 0.2f, "nearly empty");
            players.SetLocal(10f, 0f, 0f, 0f, 0f);
            for (int i = 0; i < 60; i++) system.Tick(1f, 0);
            Assert.Greater(table.Charge[0], 0.7f, "recharges while unused");
        }
    }
}
