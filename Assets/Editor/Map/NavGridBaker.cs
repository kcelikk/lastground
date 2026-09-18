using System.IO;
using LastGround.Data.Map;
using UnityEditor;
using UnityEngine;

namespace LastGround.EditorTools.Map
{
    /// <summary>
    /// Bakes a 1 m walkability grid from the colliders under a map root (TDD_01 §8.3). A cell is blocked when a box
    /// the size of the cell, inflated by the zombie radius, overlaps any collider. Replaces NavMesh sampling: no
    /// extra package, same result for flat single-level maps.
    /// </summary>
    static class NavGridBaker
    {
        const float AgentRadius = 0.35f;

        public static NavGridAsset Bake(GameObject mapRoot, float size, string assetPath)
        {
            Physics.SyncTransforms();
            int cells = Mathf.CeilToInt(size) + 2;
            var origin = new Vector2(-cells * 0.5f, -cells * 0.5f);
            var data = new byte[cells * cells];
            var halfExtents = new Vector3(0.5f + AgentRadius, 1f, 0.5f + AgentRadius);
            int blocked = 0;
            for (int y = 0; y < cells; y++)
            {
                for (int x = 0; x < cells; x++)
                {
                    var centre = new Vector3(origin.x + x + 0.5f, 1f, origin.y + y + 0.5f);
                    bool hit = Physics.CheckBox(centre, halfExtents, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                    data[y * cells + x] = hit ? (byte)0 : (byte)1;
                    if (hit) blocked++;
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(assetPath) ?? "Assets");
            var asset = AssetDatabase.LoadAssetAtPath<NavGridAsset>(assetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<NavGridAsset>();
                AssetDatabase.CreateAsset(asset, assetPath);
            }
            asset.Width = cells;
            asset.Height = cells;
            asset.CellSize = 1f;
            asset.Origin = origin;
            asset.Cells = data;
            EditorUtility.SetDirty(asset);
            Debug.Log($"[NavGridBaker] {mapRoot.name}: {cells}x{cells} cells, {blocked} blocked → {assetPath}");
            return asset;
        }
    }
}
