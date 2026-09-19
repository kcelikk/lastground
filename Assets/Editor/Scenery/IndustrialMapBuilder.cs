using System.Collections.Generic;
using System.IO;
using LastGround.Data.Map;
using LastGround.EditorTools.Map;
using LastGround.EditorTools.Setup;
using UnityEditor;
using UnityEngine;

namespace LastGround.EditorTools.Scenery
{
    /// <summary>
    /// The industrial district map (TDD_01 §11, M7; art target docs/reference/maps): 200 × 200 m, a ring road and a
    /// cross of streets around three combat regions — Foundry Yard (NW, open concrete centre, halls, containers,
    /// gantry crane), Gas Station (NE, pump islands, shop, wrecks) and Hospital Yard (S, hospital, helipad for the
    /// M8 extraction, barrier lines). Every region has at least two exits; the centres stay open and props line the
    /// edges (§11.4). Outer blocks are backdrop behind a boundary wall. Writes the scene objects, the region set, the
    /// map definition (lamps, spawns, anchors, portals) and bakes the nav grid.
    /// Layout details live in the partial files (.Regions).
    /// </summary>
    static partial class IndustrialMapBuilder
    {
        public const float Size = 200f;
        public const string MapPath = "Assets/ScriptableObjects/Maps/MAP_Industrial.asset";
        public const string RegionsPath = "Assets/ScriptableObjects/Maps/MAP_Industrial_Regions.asset";
        public const string NavPath = "Assets/Art/Maps/Industrial_NavGrid.asset";
        const string MeshPath = "Assets/Art/Maps/Industrial_Meshes.asset";
        const string Props = EnvironmentImport.Output + "/Props/";
        const string Materials = EnvironmentImport.Output + "/Materials/";
        const float RoadHalf = 6f;
        const float Ring = 70f;

        static Transform _root;
        static readonly List<MapDefinition.Lamp> Lamps = new List<MapDefinition.Lamp>();
        static readonly List<MapDefinition.Anchor> Anchors = new List<MapDefinition.Anchor>();
        static readonly List<MapDefinition.Interactable> Interactables = new List<MapDefinition.Interactable>();

        /// <summary>Scene objects of interactables are named with this prefix plus their id (presentation finds them).</summary>
        public const string InteractablePrefix = "Interactable_";
        static readonly Color Sodium = new Color(1f, 0.62f, 0.3f);
        static readonly Color Cold = new Color(0.55f, 0.7f, 1f);

        public static GameObject Build()
        {
            Lamps.Clear();
            Anchors.Clear();
            Interactables.Clear();
            var root = new GameObject("Map_Industrial");
            _root = root.transform;

            DarkenProp("modular_chainlink_fence", new Color(0.38f, 0.38f, 0.38f));
            BuildGround();
            BuildRoads();
            BuildFoundry();
            BuildGasStation();
            BuildHospital();
            BuildBackdrop();
            BuildInteractables();

            SaveMeshes();
            MapZoneSet regions = SaveRegions();
            NavGridAsset nav = NavGridBaker.Bake(root, Size, NavPath);
            SaveMap(regions, nav);
            return root;
        }

        // ----- materials and props -----

        static Material Surface(string id) => AssetDatabase.LoadAssetAtPath<Material>(Materials + "SRF_" + id + ".mat")
                                               ?? throw new FileNotFoundException("run EnvironmentImport first: " + id);

        static GameObject Prefab(string id) => AssetDatabase.LoadAssetAtPath<GameObject>(Props + id + ".prefab")
                                               ?? throw new FileNotFoundException("run EnvironmentImport first: " + id);

        /// <summary>A tinted copy of a surface material (container colours, wet dark concrete).</summary>
        static Material Tinted(string id, string suffix, Color tint, float smoothness = -1f)
        {
            string path = Materials + "SRF_" + id + "_" + suffix + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Surface(id));
                AssetDatabase.CreateAsset(material, path);
            }
            material.CopyPropertiesFromMaterial(Surface(id));
            material.SetColor("_BaseColor", tint);
            if (smoothness >= 0f) material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Dark, wet cracked asphalt (maps/03 apron, junctions).</summary>
        static Material WetAsphalt() => Tinted("Asphalt033", "wet", new Color(0.62f, 0.62f, 0.64f), 0.5f);

        /// <summary>Grimy old brick (Bricks097, slightly darkened) for halls and shops.</summary>
        static Material OldBrick() => Tinted("Bricks097", "grimy", new Color(0.78f, 0.74f, 0.7f));

        /// <summary>Rusted dark sheet roof for halls (no painted blue remnants, maps/02).</summary>
        static Material RustRoof() => Tinted("Metal041B", "roof", new Color(0.42f, 0.38f, 0.35f), 0.25f);

        /// <summary>Dark tar/concrete flat roof (maps/03, 04).</summary>
        static Material FlatRoof() => Tinted("Concrete042A", "roof", new Color(0.55f, 0.55f, 0.56f));

        /// <summary>Darkens an imported prop's materials (the chain-link fence reads white against night asphalt).</summary>
        static void DarkenProp(string id, Color tint)
        {
            foreach (string guid in AssetDatabase.FindAssets("PRP_" + id + " t:Material", new[] { Materials.TrimEnd('/') }))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                material.SetColor("_BaseColor", tint);
                EditorUtility.SetDirty(material);
            }
        }

        static void Prop(string id, float x, float z, float yaw = 0f, float scale = 1f) =>
            SceneryKit.Prop(_root, Prefab(id), new Vector2(x, z), yaw, scale);

        static void Lamp(float x, float z, float radius, Color color, bool pole = true)
        {
            if (pole) Prop("street_lamp_01", x, z, 0f, 1.6f);
            Lamps.Add(new MapDefinition.Lamp { Position = new Vector2(x, z), Radius = radius, Color = color });
        }

        /// <summary>
        /// Places an interactable: explosive barrels (Poly Haven's "explosive" barrel), fuel tanks (propane tank ×2),
        /// ammo crates (military crate) and medical stations (medical box on a stand, cold light).
        /// </summary>
        static void Interactable(InteractableKind kind, float x, float z, float yaw = 0f)
        {
            GameObject go;
            switch (kind)
            {
                case InteractableKind.ExplosiveBarrel: go = SceneryKit.Prop(_root, Prefab("Barrel_01"), new Vector2(x, z), yaw); break;
                case InteractableKind.FuelTank: go = SceneryKit.Prop(_root, Prefab("propane_tank"), new Vector2(x, z), yaw, 2.2f); break;
                case InteractableKind.AmmoCrate: go = SceneryKit.Prop(_root, Prefab("old_military_crate"), new Vector2(x, z), yaw, 1.1f); break;
                default:
                    go = SceneryKit.Prop(_root, Prefab("utility_box_01"), new Vector2(x, z), yaw);
                    GameObject kit = SceneryKit.Prop(go.transform, Prefab("medical_box"), Vector2.zero, 0f, 1.2f);
                    kit.transform.localPosition = new Vector3(0f, 1.13f, 0f);
                    Lamps.Add(new MapDefinition.Lamp { Position = new Vector2(x, z), Radius = 4f, Color = new Color(0.4f, 1f, 0.55f) });
                    break;
            }
            // Destroyable ones toggle at runtime: not static-batched.
            if (kind == InteractableKind.ExplosiveBarrel || kind == InteractableKind.FuelTank)
                GameObjectUtility.SetStaticEditorFlags(go, 0);
            go.name = InteractablePrefix + Interactables.Count;
            Interactables.Add(new MapDefinition.Interactable { Kind = kind, Position = new Vector2(x, z), Yaw = yaw });
        }

        static void Anchor(string id, MapAnchorKind kind, int region, float x, float z) =>
            Anchors.Add(new MapDefinition.Anchor { Id = id, Kind = kind, Region = region, Position = new Vector2(x, z) });

        // ----- ground, roads, backdrop -----

        static void BuildGround()
        {
            SceneryKit.Ground(_root, "Ground_Dirt", new Rect(-Size / 2, -Size / 2, Size, Size), 0f, Surface("Ground103"), 6f);
        }

        static void BuildRoads()
        {
            Material road = Tinted("Road007", "wet", new Color(0.7f, 0.7f, 0.72f), 0.45f);
            Material patch = WetAsphalt();
            float w = RoadHalf * 2f;
            // Ring and the two cross streets (§11.2 loops: no dead ends); lane markings run along each street.
            SceneryKit.Road(_root, "Road_N", new Rect(-Ring - RoadHalf, Ring - RoadHalf, 2 * Ring + w, w), 0.01f, road, true);
            SceneryKit.Road(_root, "Road_S", new Rect(-Ring - RoadHalf, -Ring - RoadHalf, 2 * Ring + w, w), 0.01f, road, true);
            SceneryKit.Road(_root, "Road_W", new Rect(-Ring - RoadHalf, -Ring - RoadHalf, w, 2 * Ring + w), 0.011f, road, false);
            SceneryKit.Road(_root, "Road_E", new Rect(Ring - RoadHalf, -Ring - RoadHalf, w, 2 * Ring + w), 0.011f, road, false);
            SceneryKit.Road(_root, "Road_Cross_X", new Rect(-Ring, -RoadHalf, 2 * Ring, w), 0.012f, road, true);
            SceneryKit.Road(_root, "Road_Cross_Z", new Rect(-RoadHalf, 0f, w, Ring), 0.013f, road, false);
            // Plain asphalt over the junctions so the markings do not cross.
            foreach (var j in new[] { new Vector2(-Ring, Ring), new Vector2(Ring, Ring), new Vector2(-Ring, -Ring), new Vector2(Ring, -Ring),
                         new Vector2(-Ring, 0f), new Vector2(Ring, 0f), new Vector2(0f, 0f), new Vector2(0f, Ring) })
                SceneryKit.Ground(_root, "Road_Junction", new Rect(j.x - RoadHalf, j.y - RoadHalf, w, w), 0.015f, patch);

            // Street lamps every ~24 m along the streets; abandoned cars and barriers turn roads into flow lanes.
            for (float t = -60f; t <= 60f; t += 24f)
            {
                Lamp(t, Ring + RoadHalf + 1f, 11f, Sodium);
                Lamp(t, -Ring - RoadHalf - 1f, 11f, Sodium);
                Lamp(-Ring - RoadHalf - 1f, t, 11f, Sodium);
                Lamp(Ring + RoadHalf + 1f, t, 11f, Sodium);
            }
            Lamp(0f, 0f, 13f, Sodium, pole: false);
            Prop("covered_car", -52f, 1.5f, 80f);
            Prop("covered_car", 18f, -2.5f, 265f);
            Prop("covered_car", 2.5f, 42f, 10f);
            Prop("covered_car", -Ring + 2f, -30f, 175f);
            Prop("covered_car", Ring - 2f, 44f, 5f);
            Prop("concrete_road_barrier_02", -20f, 3f, 0f);
            Prop("concrete_road_barrier_02", -18f, 3f, 0f);
            Prop("concrete_road_barrier_02", 40f, -3f, 10f);
            Prop("old_tyre", 21f, -1f, 0f);
            Prop("trashbag", 4.5f, 55f, 30f);
            Prop("metal_trash_can", -5.2f, 20f, 90f);
            Prop("water_manhole_cover", 0f, 30f);
            Prop("water_manhole_cover", -30f, 0f);
        }

        static void BuildBackdrop()
        {
            Material brick = OldBrick(), plaster = Tinted("PaintedPlaster017", "grimy", new Color(0.62f, 0.6f, 0.56f));
            Material roof = RustRoof(), flat = FlatRoof();
            // Outside the ring: tall blocks the camera sees only at the edge; the boundary wall closes the nav grid.
            float edge = Ring + RoadHalf + 3f, depth = Size / 2 - edge;
            for (float t = -Size / 2 + 10f; t < Size / 2; t += 22f)
            {
                SceneryKit.Building(_root, "Backdrop_N", new Vector2(t, edge + depth / 2), new Vector2(18f, depth - 2f), 9f, 0f, brick, roof);
                SceneryKit.Building(_root, "Backdrop_S", new Vector2(t, -edge - depth / 2), new Vector2(18f, depth - 2f), 7f, 0f, plaster, flat);
                if (t > -edge && t < edge)
                {
                    SceneryKit.Building(_root, "Backdrop_W", new Vector2(-edge - depth / 2, t), new Vector2(depth - 2f, 18f), 8f, 0f, brick, flat);
                    SceneryKit.Building(_root, "Backdrop_E", new Vector2(edge + depth / 2, t), new Vector2(depth - 2f, 18f), 8f, 0f, plaster, roof);
                }
            }
        }

        // ----- saving -----

        static void SaveMeshes()
        {
            List<Mesh> meshes = SceneryKit.TakeMeshes();
            AssetDatabase.DeleteAsset(MeshPath);
            ProjectSetup.EnsureFolder(Path.GetDirectoryName(MeshPath).Replace('\\', '/'));
            var holder = ScriptableObject.CreateInstance<MapMeshBundle>();
            AssetDatabase.CreateAsset(holder, MeshPath);
            foreach (Mesh mesh in meshes) AssetDatabase.AddObjectToAsset(mesh, holder);
            AssetDatabase.ImportAsset(MeshPath);
        }

        static MapZoneSet SaveRegions()
        {
            var regions = AssetDatabase.LoadAssetAtPath<MapZoneSet>(RegionsPath);
            if (regions == null)
            {
                regions = ScriptableObject.CreateInstance<MapZoneSet>();
                ProjectSetup.EnsureFolder(Path.GetDirectoryName(RegionsPath).Replace('\\', '/'));
                AssetDatabase.CreateAsset(regions, RegionsPath);
            }
            ConceptArtImport.Import();
            regions.Zones = RegionTable();
            for (int i = 0; i < regions.Zones.Length; i++) regions.Zones[i].Concept = ConceptArtImport.RegionCard(regions.Zones[i].Id);
            EditorUtility.SetDirty(regions);
            return regions;
        }

        static void SaveMap(MapZoneSet regions, NavGridAsset nav)
        {
            var map = AssetDatabase.LoadAssetAtPath<MapDefinition>(MapPath);
            if (map == null)
            {
                map = ScriptableObject.CreateInstance<MapDefinition>();
                AssetDatabase.CreateAsset(map, MapPath);
            }
            map.Size = Size;
            map.NavGrid = nav;
            map.Regions = regions;
            map.Lamps = Lamps.ToArray();
            map.Anchors = Anchors.ToArray();
            map.Interactables = Interactables.ToArray();
            map.PlayerSpawns = new[] { new Vector2(-4f, -3f), new Vector2(4f, -3f), new Vector2(-4f, 3f), new Vector2(4f, 3f) };
            map.Portals = new[]
            {
                // Region indices: 0 foundry, 1 gas station, 2 hospital — joined by the streets.
                new MapDefinition.Portal { RegionA = 0, RegionB = 1, Position = new Vector2(0f, 35f) },
                new MapDefinition.Portal { RegionA = 0, RegionB = 2, Position = new Vector2(-35f, 0f) },
                new MapDefinition.Portal { RegionA = 1, RegionB = 2, Position = new Vector2(35f, 0f) },
            };
            map.MinimapRect = new Rect(-Size / 2, -Size / 2, Size, Size);
            EditorUtility.SetDirty(map);
            Debug.Log($"[MapBuilder] industrial: {map.Lamps.Length} lamps, {map.Anchors.Length} anchors, {regions.Zones.Length} regions");
        }
    }
}
