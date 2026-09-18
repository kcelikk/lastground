using LastGround.Data.Players;
using LastGround.Data.Presentation;
using LastGround.Data.Weapons;
using LastGround.Data.Zombies;
using UnityEditor;
using UnityEngine;

namespace LastGround.EditorTools.Setup
{
    /// <summary>
    /// Creates the M4 combat data assets under Assets/ScriptableObjects (TDD_02 §26): assault rifle (TDD_01 §6.4),
    /// walker, base player, top-down camera. Only missing assets are created; existing ones keep hand-tuned values.
    /// </summary>
    static class CombatContentBuilder
    {
        const string Root = "Assets/ScriptableObjects";
        public const string WeaponPath = Root + "/Weapons/WPN_AssaultRifle.asset";
        public const string WalkerPath = Root + "/Zombies/ZMB_Walker.asset";
        public const string PlayerPath = Root + "/Players/PLR_Base.asset";
        public const string CameraPath = Root + "/Presentation/CAM_TopDown.asset";

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
