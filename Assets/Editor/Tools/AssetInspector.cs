using System.Text;
using UnityEditor;
using UnityEngine;

namespace LastGround.EditorTools.Tools
{
    /// <summary>Logs mesh/material/bone/clip statistics of model files (asset acceptance, TDD_02 §26.4).</summary>
    public static class AssetInspector
    {
        public static void InspectBatch()
        {
            var report = new StringBuilder("[AssetInspector]\n");
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/ThirdParty" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                report.AppendLine(path);
                foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    switch (asset)
                    {
                        case Mesh mesh:
                            report.AppendLine($"  mesh {mesh.name}: {mesh.vertexCount} verts, {mesh.triangles.Length / 3} tris, {mesh.subMeshCount} submeshes, {mesh.bindposes.Length} bindposes, bounds {mesh.bounds.size}");
                            break;
                        case AnimationClip clip when !clip.name.StartsWith("__preview__"):
                            report.AppendLine($"  clip {clip.name}: {clip.length:0.00}s loop={clip.isLooping}");
                            break;
                        case Material material:
                            report.AppendLine($"  material {material.name} ({material.shader.name})");
                            break;
                        case GameObject go when go.transform.parent == null:
                            foreach (SkinnedMeshRenderer smr in go.GetComponentsInChildren<SkinnedMeshRenderer>())
                                report.AppendLine($"  skinned {smr.name}: {smr.bones.Length} bones, {smr.sharedMaterials.Length} materials, root {smr.rootBone?.name}");
                            break;
                    }
                }
            }
            Debug.Log(report.ToString());
            EditorApplication.Exit(0);
        }
    }
}
