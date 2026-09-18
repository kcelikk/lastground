using LastGround.Data.Director;
using LastGround.Data.Loot;
using LastGround.Data.Map;
using LastGround.Data.Objectives;
using LastGround.Data.Players;
using LastGround.Data.Presentation;
using LastGround.Data.Weapons;
using LastGround.Data.Zombies;
using UnityEditor;
using UnityEngine;

namespace LastGround.EditorTools.Setup
{
    /// <summary>
    /// Creates the combat and director data assets under Assets/ScriptableObjects (TDD_02 §26): assault rifle
    /// (TDD_01 §6.4), walker, base player, top-down camera, director profile, threat curve, player-count scaling. Only missing assets are created; existing ones keep hand-tuned values.
    /// </summary>
    static class CombatContentBuilder
    {
        const string Root = "Assets/ScriptableObjects";
        public const string WeaponPath = Root + "/Weapons/WPN_AssaultRifle.asset";
        public const string WalkerPath = Root + "/Zombies/ZMB_Walker.asset";
        public const string PlayerPath = Root + "/Players/PLR_Base.asset";
        public const string CameraPath = Root + "/Presentation/CAM_TopDown.asset";
        public const string DirectorPath = Root + "/Director/DIR_Default.asset";
        public const string ThreatPath = Root + "/Director/DIR_ThreatCurve.asset";
        public const string ScalingPath = Root + "/Director/DIR_PlayerCountScaling.asset";
        public const string LootPath = Root + "/Loot/LOOT_Default.asset";
        public const string ZonesPath = Root + "/Maps/MAP_Greybox_Zones.asset";
        public const string ClearAreaPath = Root + "/Objectives/OBJ_ClearArea.asset";

        public static void Build()
        {
            Ensure<WeaponDefinition>(WeaponPath, w =>
            {
                w.Id = "assault_rifle";
                w.DisplayNameKey = "weapon.assault_rifle";
                w.NetIndex = 0;
            });
            Ensure<ZombieDefinition>(WalkerPath, z => z.Id = "walker");
            Ensure<PlayerDefinition>(PlayerPath, null);
            Ensure<CameraProfile>(CameraPath, null);
            Ensure<DirectorProfile>(DirectorPath, null);
            Ensure<ThreatCurveDefinition>(ThreatPath, null);
            Ensure<PlayerCountScalingProfile>(ScalingPath, null);
            Ensure<LootDefinition>(LootPath, null);
            Ensure<ObjectiveDefinition>(ClearAreaPath, null);
            Ensure<MapZoneSet>(ZonesPath, z => z.Zones = new[]
            {
                // Greybox map (80 × 80 m, inner walls at z = ±20 with gaps).
                new MapZoneSet.Zone { Id = "north_yard", NameKey = "zone.north_yard", Center = new Vector2(0f, 30f), HalfSize = new Vector2(36f, 8f) },
                new MapZoneSet.Zone { Id = "south_yard", NameKey = "zone.south_yard", Center = new Vector2(0f, -30f), HalfSize = new Vector2(36f, 8f) },
                new MapZoneSet.Zone { Id = "west_lane", NameKey = "zone.west_lane", Center = new Vector2(-26f, 0f), HalfSize = new Vector2(12f, 17f) },
                new MapZoneSet.Zone { Id = "east_lane", NameKey = "zone.east_lane", Center = new Vector2(26f, 0f), HalfSize = new Vector2(12f, 17f) },
            });
        }

        static void Ensure<T>(string path, System.Action<T> init) where T : ScriptableObject
        {
            if (AssetDatabase.LoadAssetAtPath<T>(path) != null) return;
            ProjectSetup.EnsureFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'));
            var asset = ScriptableObject.CreateInstance<T>();
            init?.Invoke(asset);
            AssetDatabase.CreateAsset(asset, path);
        }
    }
}
