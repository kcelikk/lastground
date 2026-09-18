using System.IO;
using LastGround.Data.Crowd;
using UnityEditor;
using UnityEngine;

namespace LastGround.EditorTools.Crowd
{
    /// <summary>
    /// Diagnostic contact sheet of the baked crowd bodies: every body × LOD0/1/2 skinned on the CPU from its bone
    /// texture at a walk frame, drawn with an unlit textured material from the front and from the game camera angle.
    /// Needs a graphics device (batch mode without -nographics). CLI: -executeMethod
    /// LastGround.EditorTools.Crowd.CrowdPreview.RenderBatch -lgOut &lt;png&gt;
    /// </summary>
    public static class CrowdPreview
    {
        const int Cell = 256;

        public static void RenderBatch()
        {
            string output = "CrowdPreview.png";
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-lgOut") output = args[i + 1];
            try
            {
                Render(output);
                EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        public static void Render(string output)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CrowdVisualCatalog>(CrowdCatalogBuilder.CatalogPath);
            CrowdAnimationSet[] bodies = catalog.Bodies;
            int columns = 6, rows = bodies.Length;
            var sheet = new Texture2D(Cell * columns, Cell * rows, TextureFormat.RGB24, false);
            var target = new RenderTexture(Cell, Cell, 24);
            var cameraGo = new GameObject("PreviewCamera");
            var camera = cameraGo.AddComponent<Camera>();
            camera.targetTexture = target;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.35f, 0.33f, 0.3f);
            camera.orthographic = true;
            camera.orthographicSize = 1.1f;
            var meshGo = new GameObject("PreviewMesh");
            var filter = meshGo.AddComponent<MeshFilter>();
            var renderer = meshGo.AddComponent<MeshRenderer>();
            var material = new Material(Shader.Find("Unlit/Texture"));
            renderer.sharedMaterial = material;

            for (int b = 0; b < rows; b++)
            {
                CrowdAnimationSet set = bodies[b];
                material.mainTexture = set.Albedo != null ? set.Albedo : catalog.Material.GetTexture("_BaseMap");
                CrowdClip walk = set.GetClip(CrowdClipId.Walk);
                int frame = walk.StartFrame + walk.FrameCount / 3;
                for (int lod = 0; lod < 3; lod++)
                {
                    Mesh skinned = Skin(set.Lods[lod], set.BoneTexture, frame);
                    filter.sharedMesh = skinned;
                    for (int view = 0; view < 2; view++)
                    {
                        // Front view, then the top-down game camera (pitch 55°).
                        Quaternion rotation = view == 0 ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.Euler(55f, 0f, 0f);
                        camera.transform.SetPositionAndRotation(new Vector3(0f, 0.9f, 0f) - rotation * Vector3.forward * 5f, rotation);
                        camera.Render();
                        RenderTexture.active = target;
                        sheet.ReadPixels(new Rect(0, 0, Cell, Cell), (lod * 2 + view) * Cell, (rows - 1 - b) * Cell);
                        RenderTexture.active = null;
                    }
                    Object.DestroyImmediate(skinned);
                }
            }
            sheet.Apply();
            File.WriteAllBytes(output, sheet.EncodeToPNG());
            Object.DestroyImmediate(cameraGo);
            Object.DestroyImmediate(meshGo);
            Object.DestroyImmediate(material);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(sheet);
            Debug.Log("[CrowdPreview] " + output);
        }

        /// <summary>CPU version of the crowd shader's skinning: 3 texels per bone per frame, 4 weights.</summary>
        static Mesh Skin(Mesh lod, Texture2D bones, int frame)
        {
            var indices = new System.Collections.Generic.List<Vector4>();
            var weights = new System.Collections.Generic.List<Vector4>();
            lod.GetUVs(1, indices);
            lod.GetUVs(2, weights);
            Vector3[] v = lod.vertices;
            var result = new Vector3[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                Vector3 p = Vector3.zero;
                for (int k = 0; k < 4; k++)
                {
                    float w = weights[i][k];
                    if (w <= 0f) continue;
                    int bone = (int)indices[i][k];
                    Color r0 = bones.GetPixel(bone * 3, frame), r1 = bones.GetPixel(bone * 3 + 1, frame), r2 = bones.GetPixel(bone * 3 + 2, frame);
                    p += new Vector3(r0.r * v[i].x + r0.g * v[i].y + r0.b * v[i].z + r0.a,
                                     r1.r * v[i].x + r1.g * v[i].y + r1.b * v[i].z + r1.a,
                                     r2.r * v[i].x + r2.g * v[i].y + r2.b * v[i].z + r2.a) * w;
                }
                result[i] = p;
            }
            var mesh = new Mesh { indexFormat = lod.indexFormat };
            mesh.SetVertices(result);
            mesh.SetUVs(0, lod.uv);
            mesh.SetTriangles(lod.triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
