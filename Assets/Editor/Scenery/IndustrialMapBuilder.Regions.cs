using LastGround.Data.Map;
using UnityEditor;
using UnityEngine;

namespace LastGround.EditorTools.Scenery
{
    /// <summary>The three combat regions of the industrial map (docs/reference/maps 02, 03, 04).</summary>
    static partial class IndustrialMapBuilder
    {
        const int Foundry = 0, GasStation = 1, Hospital = 2;

        /// <summary>
        /// Interactables (TDD_01 §12.3): barrels at chokepoints and gates so a shot can clear a queue, fuel tanks by the
        /// pumps and the foundry generator, one ammo crate per region and two medical stations.
        /// </summary>
        static void BuildInteractables()
        {
            Interactable(InteractableKind.ExplosiveBarrel, -8f, 50f);
            Interactable(InteractableKind.ExplosiveBarrel, -12f, 8.5f);
            Interactable(InteractableKind.ExplosiveBarrel, -44f, 22f);
            Interactable(InteractableKind.ExplosiveBarrel, 8f, 10f);
            Interactable(InteractableKind.ExplosiveBarrel, 24f, 36f);
            Interactable(InteractableKind.ExplosiveBarrel, -30f, -8.5f);
            Interactable(InteractableKind.ExplosiveBarrel, 30f, -9f);
            Interactable(InteractableKind.ExplosiveBarrel, -20f, -30f);
            Interactable(InteractableKind.ExplosiveBarrel, 22f, -30f);
            Interactable(InteractableKind.ExplosiveBarrel, 0f, 64f);
            Interactable(InteractableKind.FuelTank, 46f, 29f);
            Interactable(InteractableKind.FuelTank, 24f, 29f);
            Interactable(InteractableKind.FuelTank, -54f, 20f);
            Interactable(InteractableKind.AmmoCrate, -56f, 44f, 90f);
            Interactable(InteractableKind.AmmoCrate, 50f, 50f);
            Interactable(InteractableKind.AmmoCrate, -34f, -44f);
            Interactable(InteractableKind.AmmoCrate, 4f, -9f, 90f);
            Interactable(InteractableKind.MedStation, 8f, -46f);
            Interactable(InteractableKind.MedStation, -22f, 50f);
        }

        /// <summary>A closed roller door: a dark steel panel just in front of the wall.</summary>
        static void Door(float x, float z, float yaw)
        {
            Material steel = Surface("Metal053B");
            GameObject door = SceneryKit.Building(_root, "Door", new Vector2(x, z), new Vector2(4f, 0.15f), 3.2f, yaw, steel, steel);
            Object.DestroyImmediate(door.GetComponent<BoxCollider>());
        }

        /// <summary>Weathered yellow "H" decal (generated texture Assets/Art/Maps/Helipad.png), alpha-clipped.</summary>
        static Material HelipadMaterial()
        {
            const string path = "Assets/Art/Maps/Helipad.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { enableInstancing = true };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Maps/Helipad.png"));
            material.SetColor("_BaseColor", new Color(0.8f, 0.78f, 0.7f));
            material.SetFloat("_AlphaClip", 1f);
            material.SetFloat("_Cutoff", 0.5f);
            material.EnableKeyword("_ALPHATEST_ON");
            material.SetFloat("_Smoothness", 0.3f);
            EditorUtility.SetDirty(material);
            return material;
        }

        static MapZoneSet.Zone[] RegionTable() => new[]
        {
            new MapZoneSet.Zone { Id = "foundry_yard", NameKey = "region.foundry_yard", Center = new Vector2(-35f, 35f), HalfSize = new Vector2(29f, 29f), DangerLevel = 2 },
            new MapZoneSet.Zone { Id = "gas_station", NameKey = "region.gas_station", Center = new Vector2(35f, 35f), HalfSize = new Vector2(29f, 29f), DangerLevel = 2 },
            new MapZoneSet.Zone { Id = "hospital_yard", NameKey = "region.hospital_yard", Center = new Vector2(0f, -35f), HalfSize = new Vector2(64f, 29f), DangerLevel = 3 },
        };

        /// <summary>Foundry yard (maps/02): open wet concrete centre, halls with roller doors north and west, two
        /// containers and a gantry crane in the middle, pipe/barrel clutter at the edges, gates to both streets.</summary>
        static void BuildFoundry()
        {
            // Wet dark concrete (maps/02): darker tint, higher smoothness for the moonlight sheen.
            SceneryKit.Ground(_root, "Foundry_Concrete", new Rect(-64f, 6f, 58f, 58f), 0.02f, Tinted("Concrete042A", "wet", new Color(0.72f, 0.72f, 0.76f), 0.55f));
            Material brick = OldBrick(), roof = RustRoof();
            SceneryKit.Building(_root, "Foundry_Hall_N", new Vector2(-38f, 58f), new Vector2(46f, 11f), 8f, 0f, brick, roof);
            SceneryKit.Building(_root, "Foundry_Hall_W", new Vector2(-58.5f, 30f), new Vector2(10f, 34f), 7f, 0f, brick, roof);
            Door(-46f, 52.4f, 0f);
            Door(-30f, 52.4f, 0f);
            Door(-53.4f, 36f, 90f);

            Material red = Tinted("CorrugatedSteel007B", "red", new Color(0.72f, 0.3f, 0.22f));
            Material blue = Tinted("CorrugatedSteel007B", "blue", new Color(0.3f, 0.42f, 0.6f));
            SceneryKit.Container(_root, "Container_Red", new Vector2(-42f, 30f), 20f, red);
            SceneryKit.Container(_root, "Container_Blue", new Vector2(-25f, 38f), -12f, blue);
            // Gantry crane (maps/02): the beam rides on two steel legs 6.5 m up.
            Material steel = Surface("Metal053B");
            SceneryKit.Building(_root, "Crane_Leg_W", new Vector2(-44f, 44f), new Vector2(0.8f, 0.8f), 6.5f, 0f, steel, steel);
            SceneryKit.Building(_root, "Crane_Leg_E", new Vector2(-24f, 44f), new Vector2(0.8f, 0.8f), 6.5f, 0f, steel, steel);
            GameObject crane = SceneryKit.Prop(_root, Prefab("overhead_crane"), new Vector2(-34f, 44f), 0f, 1.65f);
            crane.transform.position += Vector3.up * 6.5f;
            Object.DestroyImmediate(crane.GetComponent<BoxCollider>());

            // Edge clutter (TDD_01 §11.4: centre open, edges dense).
            Prop("Barrel_02", -44.5f, 27.5f, 30f);
            Prop("Barrel_02", -43.6f, 26.7f, 10f);
            Prop("barrel_03", -23f, 35.5f, 70f);
            Prop("old_military_crate", -61f, 12f, 90f);
            Prop("wooden_crate_01", -50f, 50f, 15f);
            Prop("wooden_crate_01", -49f, 49.2f, 40f);
            Prop("hand_truck", -20f, 51f, 200f);
            Prop("portable_generator", -52f, 16f, 60f);
            Prop("propane_tank", -51f, 18f);
            Prop("utility_box_02", -13f, 52.5f, 180f);
            Prop("exterior_aircon_unit", -18f, 52.4f, 180f);
            for (float z = 8f; z < 48f; z += 8.1f) Prop("modular_chainlink_fence", -8f, z, 90f);
            Lamp(-36f, 34f, 12f, Sodium, pole: false);
            Lamp(-46f, 50f, 7f, Sodium, pole: false);
            Lamp(-20f, 16f, 9f, Cold);
            Anchor("foundry_supply", MapAnchorKind.SupplyDrop, Foundry, -33f, 24f);
            Anchor("foundry_generator", MapAnchorKind.Generator, Foundry, -49f, 14f);
            Anchor("foundry_cache", MapAnchorKind.WeaponCache, Foundry, -30f, 51f);
        }

        /// <summary>Gas station (maps/03): two pump islands on an open wet asphalt apron, shop to the north, wrecks
        /// and tyres around, open to both streets.</summary>
        static void BuildGasStation()
        {
            SceneryKit.Ground(_root, "Gas_Apron", new Rect(6f, 6f, 58f, 58f), 0.02f, WetAsphalt());
            Material brick = OldBrick(), plaster = Tinted("PaintedPlaster017", "grimy", new Color(0.62f, 0.6f, 0.56f)), flat = FlatRoof();
            // Brick shop with a lit front (maps/03) and a plaster service garage with a roller door.
            SceneryKit.Building(_root, "Gas_Shop", new Vector2(40f, 56f), new Vector2(30f, 12f), 5f, 0f, brick, flat);
            SceneryKit.Building(_root, "Gas_Garage", new Vector2(58f, 36f), new Vector2(10f, 18f), 5.5f, 0f, plaster, flat);
            Door(52.9f, 36f, 90f);
            Prop("exterior_aircon_unit", 34f, 56f, 0f);
            Prop("exterior_aircon_unit", 46f, 57f, 90f);

            // Two raised pump islands with a curb (maps/03: no canopy, lamps light the apron).
            Material curb = Tinted("Concrete034", "curb", new Color(0.45f, 0.44f, 0.4f));
            foreach (float x in new[] { 27f, 43f })
            {
                GameObject island = SceneryKit.Building(_root, "Gas_Island", new Vector2(x, 30f), new Vector2(3f, 11f), 0.18f, 0f, curb, curb);
                Object.DestroyImmediate(island.GetComponent<BoxCollider>());
                Prop("utility_box_02", x, 27.5f, 90f, 1.2f);
                Prop("utility_box_02", x, 32.5f, 90f, 1.2f);
                Prop("metal_trash_can", x + 0.9f, 34.8f);
            }
            Prop("covered_car", 22f, 44f, 100f);
            Prop("covered_car", 50f, 16f, 20f);
            Prop("old_tyre", 54f, 22f);
            Prop("old_tyre", 54.6f, 22.5f, 40f);
            Prop("Barrel_01", 47f, 45f);
            Prop("Barrel_01", 47.8f, 45.6f, 50f);
            Prop("metal_trash_can", 28f, 50.6f);
            Prop("fire_hydrant", 8f, 48f, 90f);
            Prop("metal_jerrycan_green", 45f, 28f, 30f);
            Prop("security_light", 40f, 49.8f, 180f);
            Lamp(12f, 30f, 11f, Sodium);
            Lamp(58f, 14f, 11f, Sodium);
            Lamp(35f, 34f, 9f, Cold, pole: false);
            Lamp(40f, 48f, 7f, Sodium, pole: false);
            Anchor("gas_supply", MapAnchorKind.SupplyDrop, GasStation, 20f, 20f);
            Anchor("gas_rescue", MapAnchorKind.RescueSignal, GasStation, 35f, 38f);
            Anchor("gas_cache", MapAnchorKind.WeaponCache, GasStation, 60f, 21f);
        }

        /// <summary>Hospital yard (maps/04): hospital block on the south edge, open yard with low barrier lines,
        /// helipad (extraction LZ for M8) to the east, parked ambulances and medical supplies.</summary>
        static void BuildHospital()
        {
            SceneryKit.Ground(_root, "Hospital_Yard", new Rect(-64f, -64f, 128f, 58f), 0.02f, Tinted("Concrete034", "dark", new Color(0.52f, 0.52f, 0.54f), 0.35f));
            SceneryKit.Ground(_root, "Hospital_Forecourt", new Rect(-40f, -48f, 80f, 10f), 0.025f, Tinted("Tiles107", "dirty", new Color(0.5f, 0.5f, 0.48f)), 2f);
            Material plaster = Tinted("PaintedPlaster017", "hospital", new Color(0.78f, 0.78f, 0.76f)), flat = FlatRoof();
            SceneryKit.Building(_root, "Hospital_Main", new Vector2(0f, -56f), new Vector2(80f, 14f), 6f, 0f, plaster, flat);
            SceneryKit.Building(_root, "Hospital_Wing", new Vector2(-54f, -40f), new Vector2(18f, 14f), 5f, 0f, plaster, flat);
            Prop("exterior_aircon_unit", -20f, -48.8f, 0f);
            Prop("exterior_aircon_unit", 12f, -48.8f, 0f);

            for (float x = -30f; x <= 30f; x += 12f)
            {
                Prop("concrete_road_barrier_02", x, -26f, 0f);
                Prop("concrete_road_barrier_02", x + 1.7f, -26f, 0f);
            }
            Prop("covered_car", -44f, -20f, 90f);
            Prop("covered_car", -44f, -14f, 90f);
            Prop("covered_car", 20f, -40f, 180f);
            Prop("medical_box", -6f, -44f, 20f);
            Prop("medical_box", 5f, -45f, 70f);
            Prop("portable_generator", 50f, -46f, 0f);
            Prop("propane_tank", 48.5f, -47f);
            Prop("trashbag", -28f, -46f, 10f);
            Prop("metal_trash_can", -32f, -47f, 0f);
            Lamp(-15f, -40f, 10f, Cold, pole: false);
            Lamp(15f, -40f, 10f, Cold, pole: false);
            // Helipad on a dark slab: the M8 extraction landing zone.
            SceneryKit.Ground(_root, "Helipad_Slab", new Rect(30f, -38f, 20f, 20f), 0.027f, WetAsphalt());
            SceneryKit.Decal(_root, "Helipad_Mark", new Vector2(40f, -28f), 17f, 0f, HelipadMaterial());
            Lamp(40f, -28f, 12f, Sodium, pole: false);
            Lamp(-40f, -18f, 9f, Sodium);
            Anchor("hospital_extraction", MapAnchorKind.Extraction, Hospital, 40f, -28f);
            Anchor("hospital_rescue", MapAnchorKind.RescueSignal, Hospital, -10f, -34f);
            Anchor("hospital_supply", MapAnchorKind.SupplyDrop, Hospital, -40f, -34f);
            Anchor("hospital_generator", MapAnchorKind.Generator, Hospital, 47f, -43.5f);
        }
    }
}
