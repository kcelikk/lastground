using System;
using System.Collections.Generic;
using System.IO;
using LastGround.Data.Crowd;
using UnityEditor;
using UnityEngine;
using UnityMeshSimplifier;

namespace LastGround.EditorTools.Crowd
{
    /// <summary>
    /// Bakes skinned models into GPU-animated crowd bodies (TDD_02 §21.3, D-018):
    /// all skinned meshes of a model → one mesh in root space (bone slot indices in UV1, weights in UV2),
    /// three LODs by quadric simplification, and a bone-matrix texture with every clip sampled at 30 fps.
    /// Loop clips are made in-place by removing the root bone's horizontal travel. Humanoid bodies (Mixamo) sample
    /// separate motion files through their avatar (<see cref="PoseSampler"/>) and get their own albedo
    /// (<see cref="BodyTextureBaker"/>; UDIM tiles packed side by side, UVs remapped).
    /// Menu: LastGround/Crowd/Bake Bodies · CLI: -executeMethod LastGround.EditorTools.Crowd.CrowdBaker.BakeAllBatch
    /// </summary>
    public static class CrowdBaker
    {
        const string OutputDir = "Assets/Art/Crowd/Baked";
        const float FrameRate = 30f;
        static readonly int[] LodVertexTargets = { 1900, 800, 300 };

        [MenuItem("LastGround/Crowd/Bake Bodies")]
        public static void BakeAll()
        {
            Directory.CreateDirectory(OutputDir);
            var sets = new List<CrowdAnimationSet>();
            foreach (CrowdBodySource source in CrowdBodySource.All)
                sets.Add(Bake(source));
            CrowdCatalogBuilder.Build(sets.ToArray());
            AssetDatabase.SaveAssets();
        }

        public static void BakeAllBatch()
        {
            try
            {
                BakeAll();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        static CrowdAnimationSet Bake(CrowdBodySource source)
        {
            if (source.Humanoid)
            {
                if (!MixamoImport.PrepareCharacter(source.ModelPath))
                    throw new FileNotFoundException(source.ModelPath + " (run Tools/Mixamo/mixamo_fetch.py)");
            }
            else
            {
                EnsureReadable(source.ModelPath);
            }
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(source.ModelPath)
                        ?? throw new FileNotFoundException(source.ModelPath);
            GameObject root = UnityEngine.Object.Instantiate(model);
            try
            {
                root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                root.transform.localScale = Vector3.one;

                var clips = LoadClips(source);
                var clipList = new List<AnimationClip>(clips.Count);
                foreach (var entry in clips) clipList.Add(entry.clip);
                using var sampler = new PoseSampler(root, clipList, source.Humanoid);
                // Sample the first clip at t=0 so the bind geometry matches the animated skeleton scale.
                sampler.Sample(0, 0f);

                SkinnedMeshRenderer[] renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>();
                var slots = new List<(Transform bone, Matrix4x4 bindToRoot)>();
                int tiles = source.DiffuseTiles != null ? Mathf.Max(1, source.DiffuseTiles.Length) : 1;
                Mesh combined = Combine(root.transform, renderers, slots, out float height, 1f / tiles, source.MaterialTiles);
                float scale = source.TargetHeight / height;
                ScaleMesh(combined, scale);

                Transform motionBone = FindMotionBone(renderers);
                var set = ScriptableObject.CreateInstance<CrowdAnimationSet>();
                set.Id = source.Id;
                set.FrameRate = FrameRate;
                set.BoneCount = slots.Count;
                set.Height = source.TargetHeight;
                set.BoneTexture = BakeBones(root, sampler, clips, slots, motionBone, scale, out CrowdClip[] table, out int totalFrames, out List<Matrix4x4[]> frames);
                float error = Validate(root, sampler, renderers, combined, clips, table, frames, scale);
                float upright = Upright(combined, frames[table[0].StartFrame]);
                if (upright < 0.6f)
                    throw new InvalidOperationException($"{source.Id}: walk pose is not upright (height {upright:0.00} of target)");
                if (error > 0.02f)
                    throw new InvalidOperationException($"{source.Id}: GPU skinning differs from Unity skinning by {error * 100f:0.0} cm");
                set.BoneTexture.name = source.Id + "_Bones";
                set.Clips = table;
                set.TotalFrames = totalFrames;
                set.Lods = BuildLods(combined, source.Id);
                if (source.DiffuseTiles != null) set.Albedo = BodyTextureBaker.Bake(source, OutputDir);

                string path = OutputDir + "/" + source.Id + ".asset";
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(set, path);
                AssetDatabase.AddObjectToAsset(set.BoneTexture, set);
                foreach (Mesh lod in set.Lods) AssetDatabase.AddObjectToAsset(lod, set);
                AssetDatabase.ImportAsset(path);

                Debug.Log($"[CrowdBaker] {source.Id}: {slots.Count} bone slots, {totalFrames} frames, " +
                          $"texture {set.BoneTexture.width}x{set.BoneTexture.height}, LOD verts " +
                          $"{set.Lods[0].vertexCount}/{set.Lods[1].vertexCount}/{set.Lods[2].vertexCount}, scale {scale:0.###}, " +
                          $"skinning error {error * 1000f:0.0} mm, walk height {upright:0.00}");
                return AssetDatabase.LoadAssetAtPath<CrowdAnimationSet>(path);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        static List<(CrowdBodySource.ClipSource source, AnimationClip clip)> LoadClips(CrowdBodySource source)
        {
            if (source.Humanoid)
            {
                var motions = new List<(CrowdBodySource.ClipSource, AnimationClip)>();
                foreach (CrowdBodySource.ClipSource clipSource in source.Clips)
                {
                    AnimationClip motion = MixamoImport.PrepareMotion(clipSource.Name, clipSource.Loop);
                    if (motion != null) motions.Add((clipSource, motion));
                    else Debug.LogWarning($"[CrowdBaker] {source.Id}: motion '{clipSource.Name}' missing, {clipSource.Id} falls back to Walk");
                }
                if (motions.Count == 0) throw new InvalidOperationException(source.Id + ": no motions found");
                return motions;
            }

            var byName = new Dictionary<string, AnimationClip>(StringComparer.Ordinal);
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(source.ModelPath))
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
                    byName[clip.name] = clip;
            }

            var result = new List<(CrowdBodySource.ClipSource, AnimationClip)>();
            foreach (CrowdBodySource.ClipSource clipSource in source.Clips)
            {
                if (byName.TryGetValue(clipSource.Name, out AnimationClip clip)) result.Add((clipSource, clip));
                else Debug.LogWarning($"[CrowdBaker] {source.Id}: clip '{clipSource.Name}' not found, {clipSource.Id} falls back to Walk");
            }
            if (result.Count == 0) throw new InvalidOperationException(source.Id + ": no clips found");
            return result;
        }

        /// <summary>Merges all skinned meshes into root space. Bone slots are (bone, bind matrix) pairs.</summary>
        /// <param name="uScale">Horizontal UV scale: UDIM tiles packed side by side in one texture.</param>
        /// <param name="materialTiles">Material-name fragments by tile (multi-material bodies); null = UDIM or one texture.</param>
        static Mesh Combine(Transform root, SkinnedMeshRenderer[] renderers, List<(Transform, Matrix4x4)> slots, out float height,
            float uScale = 1f, string[] materialTiles = null)
        {
            var positions = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var weights = new List<BoneWeight>();
            var indices = new List<int>();

            foreach (SkinnedMeshRenderer smr in renderers)
            {
                Mesh mesh = smr.sharedMesh;
                Matrix4x4 meshToRoot = root.worldToLocalMatrix * smr.transform.localToWorldMatrix;
                Matrix4x4[] bindposes = mesh.bindposes;
                var slotOf = new int[smr.bones.Length];
                for (int b = 0; b < smr.bones.Length; b++)
                {
                    // Maps root-space bind vertices into the bone's space: bindpose · meshFromRoot.
                    Matrix4x4 bindToRoot = bindposes[b] * meshToRoot.inverse;
                    slotOf[b] = FindOrAddSlot(slots, smr.bones[b], bindToRoot);
                }

                int offset = positions.Count;
                var tileOf = new int[mesh.vertexCount];
                if (materialTiles != null)
                {
                    for (int s = 0; s < mesh.subMeshCount; s++)
                    {
                        Material material = s < smr.sharedMaterials.Length ? smr.sharedMaterials[s] : null;
                        // Matched against "renderer/material" (Mixamo material names can be unrelated, e.g. Akai_MAT).
                        string key = smr.name + "/" + (material != null ? material.name : string.Empty);
                        int tile = materialTiles.Length - 1;
                        for (int k = 0; k < materialTiles.Length; k++)
                            if (key.IndexOf(materialTiles[k], StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                tile = k;
                                break;
                            }
                        foreach (int index in mesh.GetTriangles(s)) tileOf[index] = tile;
                    }
                }
                Vector3[] v = mesh.vertices;
                Vector3[] n = mesh.normals;
                Vector2[] uv = mesh.uv;
                BoneWeight[] w = mesh.boneWeights;
                for (int i = 0; i < v.Length; i++)
                {
                    positions.Add(meshToRoot.MultiplyPoint3x4(v[i]));
                    normals.Add(meshToRoot.MultiplyVector(n.Length > 0 ? n[i] : Vector3.up).normalized);
                    uvs.Add(uv.Length > 0 ? new Vector2((uv[i].x + tileOf[i]) * uScale, uv[i].y) : Vector2.zero);
                    BoneWeight bw = w[i];
                    bw.boneIndex0 = slotOf[bw.boneIndex0];
                    bw.boneIndex1 = slotOf[bw.boneIndex1];
                    bw.boneIndex2 = slotOf[bw.boneIndex2];
                    bw.boneIndex3 = slotOf[bw.boneIndex3];
                    weights.Add(bw);
                }
                for (int s = 0; s < mesh.subMeshCount; s++)
                    foreach (int index in mesh.GetTriangles(s)) indices.Add(index + offset);
            }

            float minY = float.MaxValue, maxY = float.MinValue;
            foreach (Vector3 p in positions)
            {
                minY = Mathf.Min(minY, p.y);
                maxY = Mathf.Max(maxY, p.y);
            }
            height = Mathf.Max(0.01f, maxY - minY);

            var combined = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            combined.SetVertices(positions);
            combined.SetNormals(normals);
            combined.SetUVs(0, uvs);
            combined.SetTriangles(indices, 0);
            combined.boneWeights = weights.ToArray();
            var bind = new Matrix4x4[slots.Count];
            for (int i = 0; i < bind.Length; i++) bind[i] = slots[i].Item2;
            combined.bindposes = bind;
            return combined;
        }

        static int FindOrAddSlot(List<(Transform bone, Matrix4x4 bind)> slots, Transform bone, Matrix4x4 bind)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].bone == bone && Approximately(slots[i].bind, bind)) return i;
            }
            slots.Add((bone, bind));
            return slots.Count - 1;
        }

        static bool Approximately(Matrix4x4 a, Matrix4x4 b)
        {
            for (int i = 0; i < 16; i++)
                if (Mathf.Abs(a[i] - b[i]) > 1e-4f) return false;
            return true;
        }

        static void ScaleMesh(Mesh mesh, float scale)
        {
            Vector3[] v = mesh.vertices;
            for (int i = 0; i < v.Length; i++) v[i] *= scale;
            mesh.vertices = v;
        }

        static Transform FindMotionBone(SkinnedMeshRenderer[] renderers)
        {
            foreach (SkinnedMeshRenderer smr in renderers)
                if (smr.rootBone != null) return smr.rootBone;
            return null;
        }

        /// <summary>
        /// Texture layout: x = slot * 3 + row (rows 0..2 of the 3x4 skinning matrix), y = global frame.
        /// Skinning matrix in scaled root space: S · (root⁻¹ · bone · bind) · S⁻¹.
        /// </summary>
        static Texture2D BakeBones(GameObject root, PoseSampler sampler, List<(CrowdBodySource.ClipSource source, AnimationClip clip)> clips,
            List<(Transform bone, Matrix4x4 bind)> slots, Transform motionBone, float scale,
            out CrowdClip[] table, out int totalFrames, out List<Matrix4x4[]> frames)
        {
            table = new CrowdClip[clips.Count];
            frames = new List<Matrix4x4[]>();
            Matrix4x4 s = Matrix4x4.Scale(Vector3.one * scale);
            Matrix4x4 sInv = Matrix4x4.Scale(Vector3.one / scale);
            Matrix4x4 rootInv = root.transform.worldToLocalMatrix;

            for (int c = 0; c < clips.Count; c++)
            {
                (CrowdBodySource.ClipSource source, AnimationClip clip) = clips[c];
                int count = Mathf.Max(2, Mathf.RoundToInt(clip.length * FrameRate) + (source.Loop ? 0 : 1));
                table[c] = new CrowdClip { Id = source.Id, StartFrame = frames.Count, FrameCount = count, Loop = source.Loop };

                sampler.Sample(c, 0f);
                Vector3 motionStart = motionBone != null ? rootInv.MultiplyPoint3x4(motionBone.position) : Vector3.zero;
                for (int f = 0; f < count; f++)
                {
                    float t = source.Loop ? clip.length * f / count : Mathf.Min(clip.length, f / FrameRate);
                    sampler.Sample(c, t);

                    // In-place: cancel horizontal root travel for locomotion loops; the simulation moves the entity.
                    Matrix4x4 inPlace = Matrix4x4.identity;
                    if (source.Loop && motionBone != null)
                    {
                        Vector3 travel = rootInv.MultiplyPoint3x4(motionBone.position) - motionStart;
                        inPlace = Matrix4x4.Translate(new Vector3(-travel.x, 0f, -travel.z));
                    }

                    var matrices = new Matrix4x4[slots.Count];
                    for (int b = 0; b < slots.Count; b++)
                        matrices[b] = s * inPlace * rootInv * slots[b].bone.localToWorldMatrix * slots[b].bind * sInv;
                    frames.Add(matrices);
                }
            }

            totalFrames = frames.Count;
            int width = slots.Count * 3;
            var texture = new Texture2D(width, totalFrames, TextureFormat.RGBAHalf, false, true)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color[width * totalFrames];
            for (int f = 0; f < totalFrames; f++)
            {
                for (int b = 0; b < slots.Count; b++)
                {
                    Matrix4x4 m = frames[f][b];
                    int x = b * 3;
                    pixels[f * width + x] = new Color(m.m00, m.m01, m.m02, m.m03);
                    pixels[f * width + x + 1] = new Color(m.m10, m.m11, m.m12, m.m13);
                    pixels[f * width + x + 2] = new Color(m.m20, m.m21, m.m22, m.m23);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return texture;
        }

        /// <summary>
        /// Compares our texture skinning against Unity's own skinning (SkinnedMeshRenderer.BakeMesh) on a mid frame of
        /// the death clip (not in-place adjusted). Returns the largest vertex distance in metres.
        /// </summary>
        static float Validate(GameObject root, PoseSampler sampler, SkinnedMeshRenderer[] renderers, Mesh combined,
            List<(CrowdBodySource.ClipSource source, AnimationClip clip)> clips, CrowdClip[] table, List<Matrix4x4[]> frames, float scale)
        {
            int index = Array.FindIndex(table, c => !c.Loop);
            if (index < 0) return 0f;
            int local = table[index].FrameCount / 2;
            sampler.Sample(index, local / FrameRate);
            Matrix4x4[] matrices = frames[table[index].StartFrame + local];

            // Compare against Unity's 4-bone skinning; the active quality level may use fewer weights.
            SkinWeights previousWeights = QualitySettings.skinWeights;
            QualitySettings.skinWeights = SkinWeights.FourBones;

            Vector3[] bind = combined.vertices;
            BoneWeight[] weights = combined.boneWeights;
            var baked = new Mesh();
            float maxError = 0f;
            int offset = 0;
            foreach (SkinnedMeshRenderer smr in renderers)
            {
                float smrError = 0f;
                smr.BakeMesh(baked, true);
                Matrix4x4 meshToRoot = root.transform.worldToLocalMatrix * smr.transform.localToWorldMatrix;
                Vector3[] expected = baked.vertices;
                for (int i = 0; i < expected.Length; i++)
                {
                    Vector3 reference = meshToRoot.MultiplyPoint3x4(expected[i]) * scale;
                    BoneWeight w = weights[offset + i];
                    Vector3 v = bind[offset + i];
                    float sum = w.weight0 + w.weight1 + w.weight2 + w.weight3;
                    if (sum <= 0f) sum = 1f;
                    Vector3 skinned = (matrices[w.boneIndex0].MultiplyPoint3x4(v) * w.weight0
                                     + matrices[w.boneIndex1].MultiplyPoint3x4(v) * w.weight1
                                     + matrices[w.boneIndex2].MultiplyPoint3x4(v) * w.weight2
                                     + matrices[w.boneIndex3].MultiplyPoint3x4(v) * w.weight3) / sum;
                    smrError = Mathf.Max(smrError, Vector3.Distance(reference, skinned));
                }
                var perVertex = smr.sharedMesh.GetBonesPerVertex();
                int maxInfluences = 0;
                foreach (byte count in perVertex) maxInfluences = Mathf.Max(maxInfluences, count);
                Debug.Log($"[CrowdBaker]   {smr.name}: {expected.Length} verts, max {maxInfluences} bones/vertex, skinWeights {QualitySettings.skinWeights}, max error {smrError * 1000f:0.0} mm");
                maxError = Mathf.Max(maxError, smrError);
                offset += expected.Length;
            }
            UnityEngine.Object.DestroyImmediate(baked);
            QualitySettings.skinWeights = previousWeights;
            return maxError;
        }

        /// <summary>Skinned height of the first walk frame relative to the bind height (≈ 1 when standing).</summary>
        static float Upright(Mesh combined, Matrix4x4[] matrices)
        {
            Vector3[] bind = combined.vertices;
            BoneWeight[] weights = combined.boneWeights;
            float minY = float.MaxValue, maxY = float.MinValue, bindMin = float.MaxValue, bindMax = float.MinValue;
            for (int i = 0; i < bind.Length; i += 7)
            {
                BoneWeight w = weights[i];
                Vector3 p = matrices[w.boneIndex0].MultiplyPoint3x4(bind[i]) * w.weight0 + matrices[w.boneIndex1].MultiplyPoint3x4(bind[i]) * w.weight1
                          + matrices[w.boneIndex2].MultiplyPoint3x4(bind[i]) * w.weight2 + matrices[w.boneIndex3].MultiplyPoint3x4(bind[i]) * w.weight3;
                minY = Mathf.Min(minY, p.y);
                maxY = Mathf.Max(maxY, p.y);
                bindMin = Mathf.Min(bindMin, bind[i].y);
                bindMax = Mathf.Max(bindMax, bind[i].y);
            }
            return (maxY - minY) / Mathf.Max(0.01f, bindMax - bindMin);
        }

        static Mesh[] BuildLods(Mesh combined, string id)
        {
            var lods = new Mesh[LodVertexTargets.Length];
            for (int i = 0; i < lods.Length; i++)
            {
                int target = LodVertexTargets[i];
                Mesh lod = combined.vertexCount <= target ? UnityEngine.Object.Instantiate(combined) : Simplify(combined, target);
                lods[i] = ToGpuSkinnedMesh(lod, id + "_LOD" + i);
            }
            return lods;
        }

        /// <summary>
        /// Quadric simplification towards a vertex budget. UV seams split vertices, so the requested quality is lowered
        /// until the result fits (at most 10 % over), or the simplifier stops making progress.
        /// </summary>
        static Mesh Simplify(Mesh source, int target)
        {
            // Dense scans cut into many UV islands tear apart when simplified island by island: weld by position
            // first (UV seams may smear slightly), then simplify the connected surface.
            if (source.vertexCount > target * 4) return SimplifyOnce(Weld(source), target);
            Mesh result = SimplifyOnce(source, target);
            if (result.vertexCount <= target * 1.5f) return result;
            Mesh welded = SimplifyOnce(Weld(source), target);
            return welded.vertexCount < result.vertexCount ? welded : result;
        }

        /// <summary>Merges vertices at the same position (keeping the first one's UV and weights); drops degenerate triangles.</summary>
        static Mesh Weld(Mesh source)
        {
            Vector3[] v = source.vertices;
            Vector3[] n = source.normals;
            Vector2[] uv = source.uv;
            BoneWeight[] w = source.boneWeights;
            int[] tris = source.triangles;
            var map = new int[v.Length];
            var index = new Dictionary<Vector3Int, int>();
            var pos = new List<Vector3>();
            var nrm = new List<Vector3>();
            var tex = new List<Vector2>();
            var wts = new List<BoneWeight>();
            for (int i = 0; i < v.Length; i++)
            {
                var key = Vector3Int.RoundToInt(v[i] * 2000f);
                if (!index.TryGetValue(key, out int k))
                {
                    k = pos.Count;
                    index[key] = k;
                    pos.Add(v[i]);
                    nrm.Add(n.Length > 0 ? n[i] : Vector3.up);
                    tex.Add(uv.Length > 0 ? uv[i] : Vector2.zero);
                    wts.Add(w[i]);
                }
                map[i] = k;
            }
            var outTris = new List<int>(tris.Length);
            for (int t = 0; t < tris.Length; t += 3)
            {
                int a = map[tris[t]], b = map[tris[t + 1]], c = map[tris[t + 2]];
                if (a == b || b == c || a == c) continue;
                outTris.Add(a);
                outTris.Add(b);
                outTris.Add(c);
            }
            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(pos);
            mesh.SetNormals(nrm);
            mesh.SetUVs(0, tex);
            mesh.SetTriangles(outTris, 0);
            mesh.boneWeights = wts.ToArray();
            mesh.bindposes = source.bindposes;
            return mesh;
        }

        static Mesh SimplifyOnce(Mesh source, int target)
        {
            float quality = Mathf.Clamp01((float)target / source.vertexCount);
            Mesh best = null;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                var simplifier = new MeshSimplifier
                {
                    SimplificationOptions = new SimplificationOptions
                    {
                        PreserveBorderEdges = false,
                        PreserveUVSeamEdges = false,
                        PreserveUVFoldoverEdges = false,
                        PreserveSurfaceCurvature = false,
                        EnableSmartLink = true,
                        VertexLinkDistance = 1e-4,
                        MaxIterationCount = 200,
                        Agressiveness = 7.0,
                    },
                };
                simplifier.Initialize(source);
                simplifier.SimplifyMesh(quality);
                Mesh result = simplifier.ToMesh();
                if (best == null || result.vertexCount < best.vertexCount) best = result;
                if (result.vertexCount <= target * 1.1f) break;
                quality *= Mathf.Clamp((float)target / result.vertexCount, 0.3f, 0.9f);
            }
            return best;
        }

        /// <summary>Moves skin data into UV channels so the mesh draws as a static, instanceable mesh.</summary>
        static Mesh ToGpuSkinnedMesh(Mesh source, string name)
        {
            BoneWeight[] weights = source.boneWeights;
            var boneIndices = new List<Vector4>(weights.Length);
            var boneWeights = new List<Vector4>(weights.Length);
            foreach (BoneWeight w in weights)
            {
                boneIndices.Add(new Vector4(w.boneIndex0, w.boneIndex1, w.boneIndex2, w.boneIndex3));
                float sum = w.weight0 + w.weight1 + w.weight2 + w.weight3;
                if (sum <= 0f) sum = 1f;
                boneWeights.Add(new Vector4(w.weight0, w.weight1, w.weight2, w.weight3) / sum);
            }

            var mesh = new Mesh
            {
                name = name,
                indexFormat = source.vertexCount > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16,
            };
            mesh.SetVertices(source.vertices);
            mesh.SetNormals(source.normals);
            mesh.SetUVs(0, source.uv);
            mesh.SetUVs(1, boneIndices);
            mesh.SetUVs(2, boneWeights);
            mesh.SetTriangles(source.triangles, 0);
            // Animated vertices leave the bind-pose box; give culling generous bounds.
            Bounds bounds = source.bounds;
            bounds.Expand(new Vector3(1.2f, 0.4f, 1.2f));
            mesh.bounds = bounds;
            mesh.UploadMeshData(false);
            return mesh;
        }

        static void EnsureReadable(string modelPath)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
            if (importer == null || (importer.isReadable && importer.animationType == ModelImporterAnimationType.Generic)) return;
            importer.isReadable = true;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.SaveAndReimport();
        }
    }
}
