using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityMeshSimplifier;

namespace LastGround.EditorTools.Scenery
{
    /// <summary>
    /// Turns the CC0 sources (Tools/Environment/fetch_cc0.py; not in git) into committed, mobile-sized assets under
    /// Assets/Art/Environment (D-020): each Poly Haven prop becomes one mesh (submesh per material) simplified to a
    /// triangle budget, URP Lit materials with its albedo and normal map resampled to 512 px, and a prefab with a box
    /// collider (nav baking, line of sight). ambientCG materials become tiling URP Lit materials at 1024 px.
    /// CLI: -executeMethod LastGround.EditorTools.Scenery.EnvironmentImport.BuildBatch
    /// </summary>
    public static class EnvironmentImport
    {
        const string PolyHaven = "Assets/ThirdParty/PolyHaven";
        const string AmbientCg = "Assets/ThirdParty/AmbientCG";
        public const string Output = "Assets/Art/Environment";
        const int PropTexture = 512;
        const int SurfaceTexture = 1024;

        /// <summary>Triangle budget per prop (default 1200); big set pieces get more.</summary>
        static readonly Dictionary<string, int> Budgets = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { "covered_car", 3500 }, { "overhead_crane", 6000 }, { "concrete_road_barrier_02", 1500 },
            { "modular_chainlink_fence", 2500 }, { "fire_hydrant", 1200 }, { "street_lamp_01", 1500 }, { "street_lamp_02", 1500 },
        };

        [MenuItem("LastGround/Environment/Build CC0 Assets")]
        public static void Build()
        {
            Ensure(Output + "/Props");
            Ensure(Output + "/Materials");
            Ensure(Output + "/Textures");
            foreach (string folder in Directory.GetDirectories(PolyHaven)) BuildProp(Path.GetFileName(folder));
            foreach (string folder in Directory.GetDirectories(AmbientCg)) BuildSurface(Path.GetFileName(folder));
            AssetDatabase.SaveAssets();
        }

        public static void BuildBatch()
        {
            try
            {
                Build();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        static void BuildProp(string id)
        {
            string fbx = $"{PolyHaven}/{id}/{id}_1k.fbx";
            var importer = AssetImporter.GetAtPath(fbx) as ModelImporter;
            if (importer == null) throw new FileNotFoundException(fbx);
            if (!importer.isReadable || importer.materialImportMode == ModelImporterMaterialImportMode.None)
            {
                importer.isReadable = true;
                // Materials are imported only for their names (they pick the texture set of each submesh).
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.importAnimation = false;
                importer.SaveAndReimport();
            }
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            Mesh mesh = Combine(model, out int sourceTriangles, out List<string> materialNames);
            FixUnits(mesh);
            int budget = Budgets.TryGetValue(id, out int b) ? b : 1200;
            if (sourceTriangles > budget) mesh = Simplify(mesh, budget);
            mesh.name = id;
            mesh.RecalculateBounds();
            string meshPath = $"{Output}/Props/{id}_mesh.asset";
            AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(mesh, meshPath);

            var materials = new Material[materialNames.Count];
            for (int m = 0; m < materials.Length; m++) materials[m] = PropMaterial(id, materialNames[m], m);
            var go = new GameObject(id);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = materials;
            var box = go.AddComponent<BoxCollider>();
            box.center = mesh.bounds.center;
            box.size = mesh.bounds.size;
            PrefabUtility.SaveAsPrefabAsset(go, $"{Output}/Props/{id}.prefab");
            UnityEngine.Object.DestroyImmediate(go);
            Debug.Log($"[Environment] {id}: {sourceTriangles} → {mesh.triangles.Length / 3} tris, size {mesh.bounds.size.x:0.00}×{mesh.bounds.size.y:0.00}×{mesh.bounds.size.z:0.00} m");
        }

        /// <summary>
        /// All mesh renderers of the model in Unity space (Y up, metres), one submesh per source material (a fence has
        /// posts and wire), in first-seen order.
        /// </summary>
        static Mesh Combine(GameObject model, out int triangles, out List<string> materialNames)
        {
            var groups = new List<List<CombineInstance>>();
            materialNames = new List<string>();
            triangles = 0;
            foreach (MeshRenderer renderer in model.GetComponentsInChildren<MeshRenderer>(true))
            {
                var filter = renderer.GetComponent<MeshFilter>();
                Mesh source = filter != null ? filter.sharedMesh : null;
                if (source == null) continue;
                Material[] shared = renderer.sharedMaterials;
                for (int s = 0; s < source.subMeshCount; s++)
                {
                    string name = s < shared.Length && shared[s] != null ? shared[s].name : "default";
                    int group = materialNames.IndexOf(name);
                    if (group < 0)
                    {
                        group = materialNames.Count;
                        materialNames.Add(name);
                        groups.Add(new List<CombineInstance>());
                    }
                    // The asset root carries the FBX axis/unit conversion (Z-up, centimetres): keep it.
                    groups[group].Add(new CombineInstance
                    {
                        mesh = source, subMeshIndex = s, transform = filter.transform.localToWorldMatrix,
                    });
                    triangles += (int)source.GetIndexCount(s) / 3;
                }
            }
            var parts = new CombineInstance[groups.Count];
            for (int g = 0; g < groups.Count; g++)
            {
                var part = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                part.CombineMeshes(groups[g].ToArray(), true, true);
                parts[g] = new CombineInstance { mesh = part, transform = Matrix4x4.identity };
            }
            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.CombineMeshes(parts, false, false);
            return mesh;
        }

        /// <summary>Some sources are authored in centimetres (a 1 m tyre imports as 1 cm): scale anything under 20 cm by 100.</summary>
        static void FixUnits(Mesh mesh)
        {
            mesh.RecalculateBounds();
            Vector3 size = mesh.bounds.size;
            if (Mathf.Max(size.x, Mathf.Max(size.y, size.z)) >= 0.2f) return;
            Vector3[] v = mesh.vertices;
            for (int i = 0; i < v.Length; i++) v[i] *= 100f;
            mesh.vertices = v;
            mesh.RecalculateBounds();
        }

        /// <summary>Lowers quality until the triangle budget is met (10 % slack); borders are kept only on the first try.</summary>
        static Mesh Simplify(Mesh mesh, int budget)
        {
            int triangles = mesh.triangles.Length / 3;
            float quality = (float)budget / triangles;
            Mesh best = mesh;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                Mesh result = SimplifyOnce(mesh, quality, attempt == 0);
                int count = result.triangles.Length / 3;
                if (count < best.triangles.Length / 3) best = result;
                if (count <= budget * 1.1f) break;
                quality *= Mathf.Clamp((float)budget / count, 0.3f, 0.9f);
            }
            if (best.triangles.Length / 3 <= budget * 1.5f) return best;
            // Many separate UV islands block the collapse: weld by position and retry (UV seams may smear slightly).
            Mesh welded = SimplifyOnce(Weld(mesh), (float)budget / triangles, false);
            return welded.triangles.Length < best.triangles.Length ? welded : best;
        }

        static Mesh Weld(Mesh source)
        {
            Vector3[] v = source.vertices;
            Vector2[] uv = source.uv;
            var map = new int[v.Length];
            var index = new Dictionary<Vector3Int, int>();
            var positions = new List<Vector3>();
            var uvs = new List<Vector2>();
            for (int i = 0; i < v.Length; i++)
            {
                var key = Vector3Int.RoundToInt(v[i] * 1000f);
                if (!index.TryGetValue(key, out int k))
                {
                    k = positions.Count;
                    index[key] = k;
                    positions.Add(v[i]);
                    uvs.Add(uv.Length > 0 ? uv[i] : Vector2.zero);
                }
                map[i] = k;
            }
            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(positions);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = source.subMeshCount;
            for (int s = 0; s < source.subMeshCount; s++)
            {
                int[] tris = source.GetTriangles(s);
                var output = new List<int>(tris.Length);
                for (int t = 0; t < tris.Length; t += 3)
                {
                    int a = map[tris[t]], b = map[tris[t + 1]], c = map[tris[t + 2]];
                    if (a == b || b == c || a == c) continue;
                    output.Add(a);
                    output.Add(b);
                    output.Add(c);
                }
                mesh.SetTriangles(output, s);
            }
            mesh.RecalculateNormals();
            return mesh;
        }

        static Mesh SimplifyOnce(Mesh mesh, float quality, bool preserveBorders)
        {
            var simplifier = new MeshSimplifier
            {
                SimplificationOptions = new SimplificationOptions
                {
                    PreserveBorderEdges = preserveBorders,
                    PreserveUVSeamEdges = false,
                    PreserveUVFoldoverEdges = true,
                    PreserveSurfaceCurvature = true,
                    EnableSmartLink = true,
                    VertexLinkDistance = 1e-4,
                    MaxIterationCount = 200,
                    Agressiveness = 7.0,
                },
            };
            simplifier.Initialize(mesh);
            simplifier.SimplifyMesh(Mathf.Clamp01(quality));
            Mesh result = simplifier.ToMesh();
            result.RecalculateNormals();
            result.RecalculateTangents();
            return result;
        }

        /// <summary>
        /// Material of one texture set. Poly Haven names sets "&lt;id&gt;_&lt;set&gt;_diff_1k"; the FBX material name carries
        /// the set (e.g. "modular_chainlink_fence_wire"). A set with an alpha map (wire) is alpha-clipped.
        /// </summary>
        static Material PropMaterial(string id, string materialName, int index)
        {
            string folder = $"{PolyHaven}/{id}/textures";
            string set = materialName.StartsWith(id, StringComparison.Ordinal) ? materialName.Substring(id.Length) : "";
            string prefix = id + set + "_";
            string diffuse = Find(folder, prefix + "diff_") ?? Find(folder, "_diff_");
            string normal = Find(folder, prefix + "nor_gl_") ?? Find(folder, "_nor_gl_");
            string alpha = Find(folder, prefix + "alpha_");
            string suffix = index == 0 ? "" : "_" + index;
            var material = LitMaterial($"{Output}/Materials/PRP_{id}{suffix}.mat");
            if (diffuse != null)
                material.SetTexture("_BaseMap", Resample(diffuse, $"{Output}/Textures/{id}{suffix}_albedo." + (alpha != null ? "png" : "jpg"), PropTexture, false, alpha));
            if (normal != null)
            {
                material.SetTexture("_BumpMap", Resample(normal, $"{Output}/Textures/{id}{suffix}_normal.png", PropTexture, true));
                material.EnableKeyword("_NORMALMAP");
            }
            if (alpha != null)
            {
                material.SetFloat("_AlphaClip", 1f);
                material.SetFloat("_Cutoff", 0.5f);
                material.EnableKeyword("_ALPHATEST_ON");
                material.SetFloat("_Cull", 0f);
            }
            material.SetFloat("_Smoothness", 0.25f);
            EditorUtility.SetDirty(material);
            return material;
        }

        static void BuildSurface(string id)
        {
            string folder = $"{AmbientCg}/{id}";
            string color = Find(folder, "_Color."), normal = Find(folder, "_NormalGL.");
            var material = LitMaterial($"{Output}/Materials/SRF_{id}.mat");
            if (color != null) material.SetTexture("_BaseMap", Resample(color, $"{Output}/Textures/{id}_albedo.jpg", SurfaceTexture, false));
            if (normal != null)
            {
                material.SetTexture("_BumpMap", Resample(normal, $"{Output}/Textures/{id}_normal.png", SurfaceTexture, true));
                material.EnableKeyword("_NORMALMAP");
            }
            material.SetFloat("_Smoothness", 0.15f);
            EditorUtility.SetDirty(material);
            Debug.Log($"[Environment] surface {id}");
        }

        static Material LitMaterial(string path)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { enableInstancing = true };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static string Find(string folder, string marker)
        {
            if (!Directory.Exists(folder)) return null;
            foreach (string file in Directory.GetFiles(folder))
            {
                if (file.EndsWith(".meta", StringComparison.Ordinal)) continue;
                if (Path.GetFileName(file).IndexOf(marker, StringComparison.Ordinal) >= 0) return file.Replace('\\', '/');
            }
            return null;
        }

        /// <summary>Reads a source texture on the CPU, box-filters it to <paramref name="size"/>, saves a PNG and configures it.</summary>
        /// <param name="alphaSource">Optional grey alpha map written into the output's alpha channel (PNG).</param>
        static Texture2D Resample(string source, string output, int size, bool normalMap, string alphaSource = null)
        {
            Color[] alpha = null;
            int alphaWidth = 0, alphaHeight = 0;
            if (alphaSource != null)
            {
                Texture2D a = ReadableSource(alphaSource, false);
                alpha = a.GetPixels();
                alphaWidth = a.width;
                alphaHeight = a.height;
            }
            Texture2D input = ReadableSource(source, normalMap);
            Color[] src = input.GetPixels();
            int w = input.width, h = input.height, target = Mathf.Min(size, Mathf.Max(w, h));
            var pixels = new Color[target * target];
            int step = Mathf.Max(1, w / target);
            for (int y = 0; y < target; y++)
            {
                for (int x = 0; x < target; x++)
                {
                    Color sum = Color.clear;
                    int sx0 = x * w / target, sy0 = y * h / target, n = 0;
                    for (int dy = 0; dy < step; dy += Mathf.Max(1, step / 2))
                    for (int dx = 0; dx < step; dx += Mathf.Max(1, step / 2))
                    {
                        sum += src[Mathf.Min(h - 1, sy0 + dy) * w + Mathf.Min(w - 1, sx0 + dx)];
                        n++;
                    }
                    Color c = sum / n;
                    c.a = alpha == null ? 1f : alpha[Mathf.Min(alphaHeight - 1, y * alphaHeight / target) * alphaWidth + Mathf.Min(alphaWidth - 1, x * alphaWidth / target)].r;
                    pixels[y * target + x] = c;
                }
            }
            var texture = new Texture2D(target, target, TextureFormat.RGBA32, false, normalMap);
            texture.SetPixels(pixels);
            texture.Apply(false);
            // Albedo as JPG (small in git), normal maps as PNG (no block artefacts in the vectors).
            File.WriteAllBytes(output, normalMap || alpha != null ? texture.EncodeToPNG() : texture.EncodeToJPG(90));
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(output);
            var outImporter = (TextureImporter)AssetImporter.GetAtPath(output);
            outImporter.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            outImporter.sRGBTexture = !normalMap;
            outImporter.alphaIsTransparency = alpha != null;
            outImporter.mipmapEnabled = true;
            outImporter.maxTextureSize = size;
            outImporter.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "Android", overridden = true, maxTextureSize = size, format = TextureImporterFormat.ASTC_6x6,
            });
            outImporter.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(output);
        }

        static Texture2D ReadableSource(string source, bool linear)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(source);
            if (!importer.isReadable || importer.textureType != TextureImporterType.Default || importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.isReadable = true;
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = !linear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 4096;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(source);
        }

        static void Ensure(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            Ensure(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
