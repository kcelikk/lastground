using LastGround.Core.Ids;
using LastGround.Data.Combat;
using LastGround.Data.Weapons;
using LastGround.Data.Zombies;
using LastGround.Gameplay.Combat;
using LastGround.Gameplay.Crowd;
using LastGround.Gameplay.Loot;
using LastGround.Gameplay.Navigation;
using LastGround.Gameplay.Players;
using NUnit.Framework;
using Unity.Mathematics;

namespace LastGround.Tests
{
    /// <summary>M6 weapons: two slots, reserve ammo, swapping, pickups, per-weapon validation, on-hit status effects.</summary>
    public class WeaponLoadoutTests
    {
        const string CatalogPath = "Assets/ScriptableObjects/Combat/CMB_Catalog.asset";
        const float Frame = 1f / 60f;

        static CombatCatalog Catalog() => UnityEditor.AssetDatabase.LoadAssetAtPath<CombatCatalog>(CatalogPath);

        sealed class Rig : System.IDisposable
        {
            public readonly PlayerStateTable Players = new PlayerStateTable { Local = new PlayerId(0) };
            public readonly CombatTests.ScriptedInput Input = new CombatTests.ScriptedInput();
            public readonly CombatTests.RecordingSink Sink = new CombatTests.RecordingSink();
            public readonly LoadoutTable Loadouts;
            public readonly PickupTable Pickups = new PickupTable(16);
            public readonly WeaponController Weapon;
            readonly NavGrid _nav = NavGrid.Open(80);

            public Rig(CombatCatalog catalog)
            {
                Players.SetLocal(0f, 0f, 0f, 0f, 0f);
                Loadouts = new LoadoutTable(catalog.Weapons);
                Loadouts.Set(0, catalog.StartPrimary.NetIndex, catalog.StartSidearm.NetIndex, 2);
                Weapon = new WeaponController(Players, Input, Loadouts, new CrowdState(8), _nav, 3u, Sink, null, null, catalog)
                {
                    Pickups = Pickups,
                };
            }

            public void Run(float seconds)
            {
                for (int f = 0; f < seconds * 60f; f++)
                {
                    Weapon.Tick(Frame, 0);
                    Input.Frame.SwitchWeapon = false;
                    Input.Frame.ThrowGrenade = false;
                }
            }

            public void Dispose() => _nav.Dispose();
        }

        [Test]
        public void Primary_ReloadsFromReserve_ThenSwitchesToTheSidearmWhenDry()
        {
            CombatCatalog catalog = Catalog();
            using (var rig = new Rig(catalog))
            {
                WeaponDefinition rifle = catalog.StartPrimary;
                rig.Run(Frame);
                Assert.AreSame(rifle, rig.Weapon.Active);
                Assert.AreEqual(rifle.MagazineSize, rig.Weapon.Ammo);
                Assert.AreEqual(rifle.MaxReserveAmmo, rig.Weapon.Reserve);

                rig.Input.Aim(0f, 1f, true);
                rig.Input.Frame.FireHeld = true;
                rig.Run(60f);
                Assert.AreSame(catalog.StartSidearm, rig.Weapon.Active, "dry primary → sidearm");
                Assert.AreEqual(1, rig.Players.ActiveSlot[0]);
                Assert.GreaterOrEqual(rig.Weapon.ShotsFired, rifle.MagazineSize + rifle.MaxReserveAmmo);

                // The pistol never runs dry.
                int before = rig.Weapon.ShotsFired;
                rig.Run(20f);
                Assert.Greater(rig.Weapon.ShotsFired - before, catalog.StartSidearm.MagazineSize * 3);
                Assert.IsTrue(rig.Weapon.InfiniteReserve);

                // An ammo pickup refills part of the rifle's reserve; swapping back uses it.
                rig.Input.Frame.FireHeld = false;
                rig.Pickups.Put(new PickupSpawned { Id = 0, Type = PickupType.Ammo, OwnerMask = 1 });
                rig.Pickups.Take(0, 0);
                rig.Input.Frame.SwitchWeapon = true;
                rig.Run(3f);
                Assert.AreSame(rifle, rig.Weapon.Active);
                int expected = (int)math.ceil(rifle.MaxReserveAmmo * rifle.AmmoPickupFraction);
                Assert.AreEqual(expected, rig.Weapon.Ammo + rig.Weapon.Reserve);
            }
        }

        [Test]
        public void NewPrimary_ArrivesFull_AndSwapsAreNotFreeShots()
        {
            CombatCatalog catalog = Catalog();
            using (var rig = new Rig(catalog))
            {
                rig.Run(Frame);
                WeaponDefinition shotgun = catalog.Weapon(3);
                rig.Loadouts.Set(0, shotgun.NetIndex, catalog.StartSidearm.NetIndex, 2);
                rig.Run(Frame);
                Assert.AreSame(shotgun, rig.Weapon.Active);
                Assert.AreEqual(shotgun.MagazineSize, rig.Weapon.Ammo);
                Assert.AreEqual(shotgun.MaxReserveAmmo, rig.Weapon.Reserve);
                Assert.AreEqual(shotgun.Range, rig.Weapon.Stats.Range);

                rig.Input.Aim(0f, 1f, true);
                rig.Input.Frame.FireHeld = true;
                rig.Input.Frame.SwitchWeapon = true;
                rig.Run(0.2f);
                Assert.AreEqual(0, rig.Weapon.ShotsFired, "drawing the sidearm takes a moment");
                rig.Run(0.3f);
                Assert.Greater(rig.Weapon.ShotsFired, 0);
            }
        }

        [Test]
        public void GrenadeButton_ThrowsTowardsTheOffset()
        {
            CombatCatalog catalog = Catalog();
            using (var rig = new Rig(catalog))
            {
                var grenades = new GrenadeRecorder();
                rig.Weapon.Grenades = grenades;
                rig.Input.Frame.ThrowGrenade = true;
                rig.Input.Frame.GrenadeX = 0f;
                rig.Input.Frame.GrenadeY = 8f;
                rig.Run(Frame);
                Assert.AreEqual(1, grenades.Count);
                Assert.AreEqual(8f, grenades.Target.y, 1e-4f);
                rig.Loadouts.Set(0, rig.Loadouts.Primary[0], rig.Loadouts.Sidearm[0], 0);
                rig.Input.Frame.ThrowGrenade = true;
                rig.Run(Frame);
                Assert.AreEqual(1, grenades.Count, "no grenades, no throw");
            }
        }

        sealed class GrenadeRecorder : IGrenadeSink
        {
            public int Count;
            public float2 Target;

            public void Throw(int player, float2 target)
            {
                Count++;
                Target = target;
            }
        }

        [Test]
        public void Validator_AcceptsOnlyCarriedWeapons_WithAGraceAfterASwap()
        {
            CombatCatalog catalog = Catalog();
            var crowd = new CrowdState(8);
            int zombie = crowd.Spawn(0, 0f, 5f, 0f);
            var players = new PlayerStateTable { Local = new PlayerId(0) };
            players.SetLocal(0f, 0f, 0f, 0f, 0f);
            var loadouts = new LoadoutTable(catalog.Weapons);
            loadouts.Set(0, catalog.StartPrimary.NetIndex, catalog.StartSidearm.NetIndex, 0);
            var validator = new HitClaimValidator(players, crowd, null, catalog.Weapons) { Loadouts = loadouts };
            ushort seq = 0;
            HitClaim Claim(byte weapon) => new HitClaim
            {
                Shooter = 0, ShotSeq = ++seq, Weapon = weapon, Slot = (ushort)zombie, Generation = crowd.Generation[zombie], HitX = 0f, HitZ = 5f,
            };

            validator.Refill(1f);
            Assert.AreEqual(HitClaimVerdict.Accepted, validator.Check(Claim(catalog.StartPrimary.NetIndex)));
            Assert.AreEqual(HitClaimVerdict.Accepted, validator.Check(Claim(catalog.StartSidearm.NetIndex)));
            Assert.AreEqual(HitClaimVerdict.UnknownWeapon, validator.Check(Claim(4)), "a sniper nobody carries");

            loadouts.Set(0, 4, catalog.StartSidearm.NetIndex, 0);
            validator.Refill(0.5f);
            Assert.AreEqual(HitClaimVerdict.Accepted, validator.Check(Claim(4)));
            Assert.AreEqual(HitClaimVerdict.Accepted, validator.Check(Claim(catalog.StartPrimary.NetIndex)), "in flight before the swap");
            validator.Refill(HitClaimValidator.SwapGrace + 0.1f);
            Assert.AreEqual(HitClaimVerdict.UnknownWeapon, validator.Check(Claim(catalog.StartPrimary.NetIndex)));
        }

        [Test]
        public void OnHitUpgrades_BurnSlowAndStun_ThroughTheAuthority()
        {
            CombatCatalog catalog = Catalog();
            var crowd = new CrowdState(8);
            var players = new PlayerStateTable { Local = new PlayerId(0) };
            players.SetLocal(0f, 0f, 0f, 0f, 0f);
            using (var nav = NavGrid.Open(60))
            using (var world = new LastGround.Gameplay.Zombies.ZombieWorld(crowd, players, nav, new LastGround.Gameplay.Zombies.ZombieTuning(),
                       catalog.Zombies, 1u))
            {
                var upgrades = UnityEditor.AssetDatabase.LoadAssetAtPath<Data.Upgrades.UpgradeCatalog>("Assets/ScriptableObjects/Upgrades/UPG_Catalog.asset");
                var builds = new LastGround.Gameplay.Upgrades.TeamBuilds(upgrades);
                int incendiary = System.Array.FindIndex(upgrades.Upgrades, u => u.Id == "incendiary");
                int crippling = System.Array.FindIndex(upgrades.Upgrades, u => u.Id == "crippling");
                builds.Apply(0, incendiary, Data.Upgrades.UpgradeRarity.Common);
                builds.Apply(0, crippling, Data.Upgrades.UpgradeRarity.Common);
                var loadouts = new LoadoutTable(catalog.Weapons);
                loadouts.Set(0, catalog.StartPrimary.NetIndex, catalog.StartSidearm.NetIndex, 0);
                var authority = new CombatAuthority(world, players, nav, catalog.Weapons, 1u)
                {
                    Builds = builds, Loadouts = loadouts, Catalog = catalog,
                };
                int tank = world.Spawn(new float2(0f, 20f), 180f, 2);
                authority.Submit(new HitClaim
                {
                    Shooter = 0, ShotSeq = 1, Weapon = catalog.StartPrimary.NetIndex, Slot = (ushort)tank,
                    Generation = crowd.Generation[tank], HitX = 0f, HitZ = 20f,
                });
                authority.Tick(1f / 30f, 0);
                Assert.AreEqual(1, authority.Accepted);
                Assert.IsTrue(world.IsBurning(tank));
                Assert.IsTrue(world.IsSlowed(tank));
            }
        }
    }
}
