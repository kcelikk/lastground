using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LastGround.EditorTools.Scenery
{
    /// <summary>
    /// Procedural building blocks for map scenery (TDD_01 §11): ground patches, buildings (walls + roof), shipping
    /// containers and prop placement. UVs are in world metres divided by a tile size, so tiling textures keep their real
    /// scale on every size. Everything is static (batching) and solid geometry gets a box collider for the nav bake.
    /// </summary>
    static class SceneryKit
    {
        public const float WallTile = 3f;
        public const float RoofTile = 4f;
        public const float GroundTile = 4f;

        /// <summary>A flat ground rectangle at <paramref name="y"/> (layer small offsets to avoid z-fighting).</summary>
        public static GameObject Ground(Transform parent, string name, Rect area, float y, Material material, float tile = GroundTile)
        {
            var mesh = new Mesh { name = name };
            float x0 = area.xMin, x1 = area.xMax, z0 = area.yMin, z1 = area.yMax;
            mesh.vertices = new[] { new Vector3(x0, y, z0), new Vector3(x0, y, z1), new Vector3(x1, y, z1), new Vector3(x1, y, z0) };
            mesh.uv = new[] { new Vector2(x0, z0) / tile, new Vector2(x0, z1) / tile, new Vector2(x1, z1) / tile, new Vector2(x1, z0) / tile };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            return Place(parent, name, mesh, new[] { material }, collider: false);
        }

        /// <summary>
        /// A road strip whose texture spans the full width once (lane markings stay centred) and repeats along its
        /// length every road width. <paramref name="alongX"/> picks the running direction.
        /// </summary>
        public static GameObject Road(Transform parent, string name, Rect area, float y, Material material, bool alongX)
        {
            var mesh = new Mesh { name = name };
            float x0 = area.xMin, x1 = area.xMax, z0 = area.yMin, z1 = area.yMax;
            float width = alongX ? area.height : area.width, length = (alongX ? area.width : area.height) / width;
            mesh.vertices = new[] { new Vector3(x0, y, z0), new Vector3(x0, y, z1), new Vector3(x1, y, z1), new Vector3(x1, y, z0) };
            mesh.uv = alongX
                ? new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, length), new Vector2(0f, length) }
                : new[] { new Vector2(0f, 0f), new Vector2(0f, length), new Vector2(1f, length), new Vector2(1f, 0f) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            return Place(parent, name, mesh, new[] { material }, collider: false);
        }

        /// <summary>
        /// A closed building: four walls (UV by metre) and a roof, centred at <paramref name="center"/>, rotated about Y.
        /// </summary>
        public static GameObject Building(Transform parent, string name, Vector2 center, Vector2 size, float height, float yaw,
            Material walls, Material roof)
        {
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var wallTris = new List<int>();
            var roofTris = new List<int>();
            float hx = size.x * 0.5f, hz = size.y * 0.5f;
            Vector3[] corners = { new Vector3(-hx, 0f, -hz), new Vector3(-hx, 0f, hz), new Vector3(hx, 0f, hz), new Vector3(hx, 0f, -hz) };
            for (int side = 0; side < 4; side++)
            {
                Vector3 a = corners[side], b = corners[(side + 1) % 4];
                float length = Vector3.Distance(a, b);
                int start = vertices.Count;
                vertices.Add(a);
                vertices.Add(a + Vector3.up * height);
                vertices.Add(b + Vector3.up * height);
                vertices.Add(b);
                uvs.Add(new Vector2(0f, 0f));
                uvs.Add(new Vector2(0f, height / WallTile));
                uvs.Add(new Vector2(length / WallTile, height / WallTile));
                uvs.Add(new Vector2(length / WallTile, 0f));
                // Outward faces: the corners run clockwise seen from above.
                wallTris.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
            }
            int top = vertices.Count;
            foreach (Vector3 c in corners)
            {
                vertices.Add(c + Vector3.up * height);
                uvs.Add(new Vector2(c.x, c.z) / RoofTile);
            }
            roofTris.AddRange(new[] { top, top + 1, top + 2, top, top + 2, top + 3 });

            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(wallTris, 0);
            mesh.SetTriangles(roofTris, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            GameObject go = Place(parent, name, mesh, new[] { walls, roof }, collider: true);
            go.transform.SetPositionAndRotation(new Vector3(center.x, 0f, center.y), Quaternion.Euler(0f, yaw, 0f));
            return go;
        }

        /// <summary>A 6.1 × 2.6 × 2.4 m shipping container (corrugated sides, tinted material).</summary>
        public static GameObject Container(Transform parent, string name, Vector2 center, float yaw, Material material, bool twentyFoot = true)
        {
            float length = twentyFoot ? 6.1f : 12.2f;
            return Building(parent, name, center, new Vector2(length, 2.44f), 2.6f, yaw, material, material);
        }

        /// <summary>Instantiates a prop prefab lying on the ground (y = 0) with yaw and uniform scale.</summary>
        public static GameObject Prop(Transform parent, GameObject prefab, Vector2 position, float yaw, float scale = 1f)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(new Vector3(position.x, 0f, position.y), Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = Vector3.one * scale;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        /// <summary>A flat decal quad slightly above ground (helipad, road marks) without a collider.</summary>
        public static GameObject Decal(Transform parent, string name, Vector2 center, float size, float yaw, Material material)
        {
            var mesh = new Mesh { name = name };
            float h = size * 0.5f;
            mesh.vertices = new[] { new Vector3(-h, 0f, -h), new Vector3(-h, 0f, h), new Vector3(h, 0f, h), new Vector3(h, 0f, -h) };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateNormals();
            GameObject go = Place(parent, name, mesh, new[] { material }, collider: false);
            go.transform.SetPositionAndRotation(new Vector3(center.x, 0.03f, center.y), Quaternion.Euler(0f, yaw, 0f));
            return go;
        }

        static readonly List<Mesh> Meshes = new List<Mesh>();

        /// <summary>Meshes created since the last call (saved into the map mesh asset by the builder).</summary>
        public static List<Mesh> TakeMeshes()
        {
            var result = new List<Mesh>(Meshes);
            Meshes.Clear();
            return result;
        }

        static GameObject Place(Transform parent, string name, Mesh mesh, Material[] materials, bool collider)
        {
            Meshes.Add(mesh);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = materials;
            if (collider) go.AddComponent<BoxCollider>();
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }
    }
}
