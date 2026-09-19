using System.Collections.Generic;
using LastGround.Core.Ids;
using LastGround.Data.Loot;
using LastGround.Data.Map;
using LastGround.Data.Objectives;
using LastGround.Data.Zombies;
using LastGround.Gameplay.Crowd;
using LastGround.Gameplay.Director;
using LastGround.Gameplay.Loot;
using LastGround.Gameplay.Objectives;
using LastGround.Gameplay.Players;
using NUnit.Framework;
using UnityEngine;

namespace LastGround.Tests
{
    /// <summary>M7 map events (TDD_01 §12.2): anchored proximity events, time limits, region rotation, the map asset.</summary>
    public class MapEventTests
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
            public readonly RunStatus Status = new RunStatus();
            public readonly ObjectiveState State = new ObjectiveState();
            public readonly PickupTable Pickups = new PickupTable(64);
            public ObjectiveSystem System;

            public void Run(float seconds)
            {
                for (int t = 0; t < seconds * 30f; t++) System.Tick(Dt, (uint)t);
            }

            public void MoveTo(float x, float z) => Players.SetLocal(x, z, 0f, 0f, 0f);

            public int Coins()
            {
                int coins = 0;
                for (int i = 0; i < Pickups.Capacity; i++)
                    if (Pickups.Active[i] && Pickups.Type[i] == PickupType.Coin) coins += Pickups.Value[i];
                return coins;
            }
        }

        ObjectiveDefinition Event(ObjectiveKind kind, MapAnchorKind anchor)
        {
            var e = Asset<ObjectiveDefinition>();
            e.Kind = kind;
            e.Anchor = anchor;
            e.FirstDelay = 1f;
            e.Cooldown = 2f;
            e.CompletedShowTime = 5f;
            e.ArriveSeconds = 5f;
            e.HoldSeconds = 4f;
            e.Radius = 3f;
            return e;
        }

        Rig Create(params ObjectiveDefinition[] events)
        {
            var zones = Asset<MapZoneSet>();
            zones.Zones = new[]
            {
                new MapZoneSet.Zone { Id = "a", NameKey = "zone.a", Center = new Vector2(-30f, 0f), HalfSize = new Vector2(10f, 10f) },
                new MapZoneSet.Zone { Id = "b", NameKey = "zone.b", Center = new Vector2(30f, 0f), HalfSize = new Vector2(10f, 10f) },
            };
            var map = Asset<MapDefinition>();
            map.Anchors = new[]
            {
                new MapDefinition.Anchor { Id = "a_supply", Kind = MapAnchorKind.SupplyDrop, Region = 0, Position = new Vector2(-30f, 0f) },
                new MapDefinition.Anchor { Id = "b_supply", Kind = MapAnchorKind.SupplyDrop, Region = 1, Position = new Vector2(30f, 0f) },
                new MapDefinition.Anchor { Id = "b_generator", Kind = MapAnchorKind.Generator, Region = 1, Position = new Vector2(35f, 5f) },
            };
            var rig = new Rig();
            rig.MoveTo(0f, 0f);
            var registry = new PickupRegistry(rig.Pickups, new CrowdState(8), rig.Players, Asset<LootDefinition>(), Asset<ZombieDefinition>(),
                new TeamWallet(), 1u);
            rig.System = new ObjectiveSystem(rig.Crowd, rig.Players, zones, events, rig.Status, rig.State, 9u) { Loot = registry, Map = map };
            return rig;
        }

        [Test]
        public void SupplyDrop_IsAnnounced_ThenFillsOnlyWhileSomeoneStandsOnIt()
        {
            ObjectiveDefinition drop = Event(ObjectiveKind.SupplyDrop, MapAnchorKind.SupplyDrop);
            Rig rig = Create(drop);
            rig.Run(1.1f);
            Assert.AreEqual(ObjectivePhase.Announced, rig.State.Phase);
            Assert.AreEqual(ObjectiveKind.SupplyDrop, rig.State.Kind);
            Assert.IsTrue(rig.State.HasAnchor);
            Assert.AreEqual(5, rig.State.SecondsLeft, "arrival countdown");

            rig.Run(drop.ArriveSeconds + 0.1f);
            Assert.AreEqual(ObjectivePhase.Active, rig.State.Phase);
            rig.Run(2f);
            Assert.AreEqual(0, rig.State.Current, "nobody at the drop");

            rig.MoveTo(rig.State.AnchorX + 1f, rig.State.AnchorZ);
            rig.Run(2f);
            int half = rig.State.Current;
            Assert.Greater(half, 0);
            rig.MoveTo(0f, 0f);
            rig.Run(2f);
            Assert.Less(rig.State.Current, half, "progress fades while away");

            rig.MoveTo(rig.State.AnchorX, rig.State.AnchorZ);
            rig.Run(drop.HoldSeconds + 0.5f);
            Assert.AreEqual(ObjectivePhase.Completed, rig.State.Phase);
            Assert.AreEqual(drop.RewardCoinsPerPlayer, rig.Coins());
        }

        [Test]
        public void Generator_FailsWhenItsTimeRunsOut_AndKeepsHeldProgress()
        {
            ObjectiveDefinition generator = Event(ObjectiveKind.PowerGenerator, MapAnchorKind.Generator);
            generator.TimeLimit = 10f;
            generator.HoldSeconds = 6f;
            Rig rig = Create(generator);
            rig.Run(1.1f);
            Assert.AreEqual(ObjectivePhase.Active, rig.State.Phase);
            Assert.AreEqual(35f, rig.State.AnchorX);

            rig.MoveTo(35f, 5f);
            rig.Run(3f);
            int held = rig.State.Current;
            rig.MoveTo(0f, 0f);
            rig.Run(2f);
            Assert.AreEqual(held, rig.State.Current, "generators do not lose progress");
            rig.Run(6f);
            Assert.AreEqual(ObjectivePhase.Failed, rig.State.Phase);
            Assert.AreEqual(1, rig.System.Failed);
            Assert.AreEqual(0, rig.Coins(), "no reward on failure");
        }

        [Test]
        public void ConsecutiveEvents_MoveToAnotherRegion()
        {
            ObjectiveDefinition drop = Event(ObjectiveKind.SupplyDrop, MapAnchorKind.SupplyDrop);
            drop.ArriveSeconds = 0.1f;
            drop.TimeLimit = 1f;
            Rig rig = Create(drop);
            int last = -1;
            for (int round = 0; round < 6; round++)
            {
                // Wait for the next event (cooldown + show time), then let it time out.
                for (int guard = 0; guard < 200 && (rig.State.Phase == ObjectivePhase.None || rig.State.Phase == ObjectivePhase.Failed); guard++)
                    rig.Run(0.1f);
                Assert.AreNotEqual(last, rig.State.Zone, "round " + round);
                last = rig.State.Zone;
                rig.Run(1.2f);
                Assert.AreEqual(ObjectivePhase.Failed, rig.State.Phase);
            }
        }

        [Test]
        public void IndustrialMap_AnchorsAndInteractables_SitOnWalkableGround_InTheirRegions()
        {
            var map = UnityEditor.AssetDatabase.LoadAssetAtPath<MapDefinition>("Assets/ScriptableObjects/Maps/MAP_Industrial.asset");
            Assert.IsNotNull(map, "run the map builder");
            Assert.IsNotNull(map.NavGrid);
            Assert.IsNotNull(map.Minimap, "baked minimap");
            Assert.AreEqual(3, map.Regions.Zones.Length);
            using (var nav = LastGround.Gameplay.Navigation.NavGrid.FromAsset(map.NavGrid))
            {
                foreach (MapDefinition.Anchor a in map.Anchors)
                {
                    Assert.IsTrue(nav.IsWalkable(new Unity.Mathematics.float2(a.Position.x, a.Position.y)), a.Id + " walkable");
                    Assert.AreEqual(a.Region, map.RegionAt(a.Position.x, a.Position.y), a.Id + " region");
                }
                foreach (Vector2 spawn in map.PlayerSpawns)
                    Assert.IsTrue(nav.IsWalkable(new Unity.Mathematics.float2(spawn.x, spawn.y)), "player spawn " + spawn);
            }
            foreach (MapAnchorKind kind in new[] { MapAnchorKind.SupplyDrop, MapAnchorKind.Generator, MapAnchorKind.WeaponCache, MapAnchorKind.RescueSignal })
            {
                var regions = new HashSet<int>();
                foreach (MapDefinition.Anchor a in map.Anchors) if (a.Kind == kind) regions.Add(a.Region);
                Assert.GreaterOrEqual(regions.Count, 2, kind + " anchors in at least two regions (events rotate)");
            }
            Assert.Greater(map.Interactables.Length, 0);
            Assert.Greater(map.Lamps.Length, 20);
        }
    }
}
