using System.IO;
using UnityEditor;
using UnityEngine;

namespace LastGround.EditorTools.Crowd
{
    /// <summary>
    /// Import settings for the Mixamo sources (not in git; Tools/Mixamo/mixamo_fetch.py): characters and motions as
    /// Humanoid (motions retarget onto every body's own proportions), readable meshes, embedded textures extracted next
    /// to the character so the body atlas can be built on the CPU, loop flags and baked-in root motion for clips.
    /// </summary>
    static class MixamoImport
    {
        public const string Root = "Assets/ThirdParty/Mixamo/";
        public const string Characters = Root + "Characters/";
        public const string Animations = Root + "Animations/";

        /// <summary>Configures a character FBX; returns false when the file is missing.</summary>
        public static bool PrepareCharacter(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) return false;
            bool changed = false;
            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                changed = true;
            }
            if (!importer.isReadable)
            {
                importer.isReadable = true;
                changed = true;
            }
            if (importer.importAnimation)
            {
                importer.importAnimation = false;
                changed = true;
            }
            if (changed) importer.SaveAndReimport();
            EnsureHumanAvatar(path, importer);

            string textures = TextureFolder(path);
            if (!AssetDatabase.IsValidFolder(textures))
            {
                AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(textures));
                importer.ExtractTextures(textures);
                AssetDatabase.Refresh();
                foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { textures }))
                    MakeReadable(AssetDatabase.GUIDToAssetPath(guid));
                importer.SaveAndReimport();
            }
            return true;
        }

        /// <summary>Configures a motion FBX: Humanoid, one clip, loop flag, root motion baked into the pose.</summary>
        public static AnimationClip PrepareMotion(string path, bool loop)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) return null;
            bool changed = importer.animationType != ModelImporterAnimationType.Human;
            if (changed)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.SaveAndReimport();
            }
            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
            bool dirty = changed;
            for (int i = 0; i < clips.Length; i++)
            {
                ModelImporterClipAnimation c = clips[i];
                if (c.loopTime == loop && c.lockRootRotation && c.lockRootHeightY && c.lockRootPositionXZ && c.keepOriginalPositionY
                    && !c.keepOriginalOrientation) continue;
                c.loopTime = loop;
                c.lockRootRotation = true;
                c.lockRootHeightY = true;
                c.lockRootPositionXZ = true;
                // Body orientation, not the file's: Mixamo motions carry a -90° X root that lays the body flat.
                c.keepOriginalOrientation = false;
                c.keepOriginalPositionY = true;
                c.keepOriginalPositionXZ = true;
                clips[i] = c;
                dirty = true;
            }
            if (dirty)
            {
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
            }
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__", System.StringComparison.Ordinal)) return clip;
            return null;
        }

        /// <summary>Unity's automatic mapping fails on some Mixamo rigs (Mutant: no left fingers); map by Mixamo names.</summary>
        static void EnsureHumanAvatar(string path, ModelImporter importer)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var animator = model != null ? model.GetComponent<Animator>() : null;
            if (animator != null && animator.avatar != null && animator.avatar.isHuman) return;

            var names = new System.Collections.Generic.Dictionary<string, Transform>(System.StringComparer.Ordinal);
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true)) names[t.name] = t;
            var human = new System.Collections.Generic.List<HumanBone>();
            foreach (var pair in MixamoToHuman)
            {
                if (!names.ContainsKey("mixamorig:" + pair.Key)) continue;
                var bone = new HumanBone { boneName = "mixamorig:" + pair.Key, humanName = pair.Value };
                bone.limit.useDefaultValues = true;
                human.Add(bone);
            }
            var skeleton = new System.Collections.Generic.List<SkeletonBone>();
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
                skeleton.Add(new SkeletonBone { name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale });

            HumanDescription description = importer.humanDescription;
            description.human = human.ToArray();
            description.skeleton = skeleton.ToArray();
            importer.humanDescription = description;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.SaveAndReimport();
            model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            animator = model.GetComponent<Animator>();
            Debug.Log($"[Mixamo] {Path.GetFileName(path)}: manual human mapping ({human.Count} bones) → " +
                      (animator != null && animator.avatar != null && animator.avatar.isHuman ? "human ok" : "STILL NOT HUMAN"));
        }

        static readonly System.Collections.Generic.KeyValuePair<string, string>[] MixamoToHuman =
        {
            Pair("Hips", "Hips"), Pair("Spine", "Spine"), Pair("Spine1", "Chest"), Pair("Spine2", "UpperChest"),
            Pair("Neck", "Neck"), Pair("Head", "Head"),
            Pair("LeftUpLeg", "LeftUpperLeg"), Pair("LeftLeg", "LeftLowerLeg"), Pair("LeftFoot", "LeftFoot"), Pair("LeftToeBase", "LeftToes"),
            Pair("RightUpLeg", "RightUpperLeg"), Pair("RightLeg", "RightLowerLeg"), Pair("RightFoot", "RightFoot"), Pair("RightToeBase", "RightToes"),
            Pair("LeftShoulder", "LeftShoulder"), Pair("LeftArm", "LeftUpperArm"), Pair("LeftForeArm", "LeftLowerArm"), Pair("LeftHand", "LeftHand"),
            Pair("RightShoulder", "RightShoulder"), Pair("RightArm", "RightUpperArm"), Pair("RightForeArm", "RightLowerArm"), Pair("RightHand", "RightHand"),
        };

        static System.Collections.Generic.KeyValuePair<string, string> Pair(string mixamo, string human) =>
            new System.Collections.Generic.KeyValuePair<string, string>(mixamo, human);

        public static string TextureFolder(string characterPath) =>
            Path.GetDirectoryName(characterPath).Replace('\\', '/') + "/" + Path.GetFileNameWithoutExtension(characterPath) + "_Textures";

        static void MakeReadable(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null || importer.isReadable) return;
            importer.isReadable = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        /// <summary>Import report: meshes, vertices, materials, textures, bones (menu + batch).</summary>
        [MenuItem("LastGround/Crowd/Inspect Mixamo Sources")]
        public static void Inspect()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { Characters.TrimEnd('/') }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                PrepareCharacter(path);
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var sb = new System.Text.StringBuilder();
                sb.Append("[Mixamo] ").Append(Path.GetFileNameWithoutExtension(path));
                foreach (SkinnedMeshRenderer smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    sb.Append(" | ").Append(smr.name).Append(' ').Append(smr.sharedMesh.vertexCount).Append("v ")
                      .Append(smr.sharedMesh.subMeshCount).Append("sub ").Append(smr.bones.Length).Append("bones");
                    foreach (Material m in smr.sharedMaterials)
                    {
                        Texture t = m != null ? m.mainTexture : null;
                        sb.Append(" [").Append(m != null ? m.name : "null").Append(':')
                          .Append(t != null ? t.name + " " + t.width + "x" + t.height : "no tex").Append(']');
                    }
                }
                var animator = model.GetComponent<Animator>();
                sb.Append(" | avatar ").Append(animator != null && animator.avatar != null && animator.avatar.isHuman ? "human ok" : "NOT HUMAN");
                Debug.Log(sb.ToString());
            }
        }

        public static void InspectBatch()
        {
            try
            {
                Inspect();
                EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }
    }
}
