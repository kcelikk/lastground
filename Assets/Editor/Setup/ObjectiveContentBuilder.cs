using LastGround.Data.Map;
using LastGround.Data.Objectives;
using UnityEditor;
using UnityEngine;

namespace LastGround.EditorTools.Setup
{
    /// <summary>
    /// M7 map events (TDD_01 §12.2, §12.4) and the interactable balance, under Assets/ScriptableObjects. Clear Area is the
    /// M5 asset. Only missing assets are created; existing ones keep hand-tuned values. Unlock times follow §2.2
    /// (supply drops early, generator and rescue in the build-up, caches and hunts once elites exist).
    /// </summary>
    static class ObjectiveContentBuilder
    {
        const string Folder = "Assets/ScriptableObjects/Objectives";
        public const string InteractablesPath = "Assets/ScriptableObjects/Maps/MAP_Interactables.asset";

        public static ObjectiveDefinition[] Build()
        {
            var clearArea = AssetDatabase.LoadAssetAtPath<ObjectiveDefinition>(CombatContentBuilder.ClearAreaPath);
            var supply = Ensure("supply_drop", ObjectiveKind.SupplyDrop, MapAnchorKind.SupplyDrop, o =>
            {
                o.MinRunSeconds = 60f;
                o.Weight = 1.2f;
                o.ArriveSeconds = 15f;
                o.Radius = 2.5f;
                o.HoldSeconds = 3f;
                o.TimeLimit = 75f;
                o.RewardCoinsPerPlayer = 10;
                o.RewardMedkits = 1;
                o.RewardWeapon = true;
                o.RewardGrenades = 2;
            });
            var cache = Ensure("weapon_cache", ObjectiveKind.WeaponCache, MapAnchorKind.WeaponCache, o =>
            {
                o.MinRunSeconds = 200f;
                o.Weight = 0.8f;
                o.Radius = 2.5f;
                o.HoldSeconds = 2f;
                o.TimeLimit = 120f;
                o.RewardCoinsPerPlayer = 15;
                o.RewardMedkits = 0;
                o.RewardWeapon = true;
            });
            var generator = Ensure("power_generator", ObjectiveKind.PowerGenerator, MapAnchorKind.Generator, o =>
            {
                o.MinRunSeconds = 120f;
                o.Weight = 1f;
                o.Radius = 3f;
                o.HoldSeconds = 8f;
                o.PressureWhileHolding = true;
                o.TimeLimit = 90f;
                o.RewardCoinsPerPlayer = 12;
                o.RewardMedkits = 1;
                o.TurretSeconds = 60f;
            });
            var rescue = Ensure("rescue_signal", ObjectiveKind.RescueSignal, MapAnchorKind.RescueSignal, o =>
            {
                o.MinRunSeconds = 180f;
                o.Weight = 0.8f;
                o.Radius = 8f;
                o.HoldSeconds = 60f;
                o.PressureWhileHolding = true;
                o.TimeLimit = 150f;
                o.RewardCoinsPerPlayer = 20;
                o.RewardMedkits = 2;
                o.RewardOfferRarity = 2;
                o.RewardRespawn = true;
            });
            var hunt = Ensure("elite_hunt", ObjectiveKind.EliteHunt, MapAnchorKind.SupplyDrop, o =>
            {
                o.MinRunSeconds = 240f;
                o.Weight = 0.8f;
                o.Radius = 1.5f;
                o.TimeLimit = 120f;
                o.RewardCoinsPerPlayer = 15;
                o.RewardMedkits = 0;
                o.RewardOfferRarity = 3;
            });
            if (AssetDatabase.LoadAssetAtPath<InteractableProfile>(InteractablesPath) == null)
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<InteractableProfile>(), InteractablesPath);
            return new[] { clearArea, supply, cache, generator, rescue, hunt };
        }

        static ObjectiveDefinition Ensure(string id, ObjectiveKind kind, MapAnchorKind anchor, System.Action<ObjectiveDefinition> init)
        {
            string path = Folder + "/OBJ_" + id + ".asset";
            var asset = AssetDatabase.LoadAssetAtPath<ObjectiveDefinition>(path);
            if (asset != null) return asset;
            ProjectSetup.EnsureFolder(Folder);
            asset = ScriptableObject.CreateInstance<ObjectiveDefinition>();
            asset.Id = id;
            asset.Kind = kind;
            asset.Anchor = anchor;
            asset.TitleKey = "objective." + id + ".title";
            asset.CounterKey = "objective." + id + ".counter";
            asset.FirstDelay = 20f;
            asset.Cooldown = 25f;
            init(asset);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
    }
}
