using UnityEditor;
using UnityEngine;

namespace LastGround.EditorTools.Map
{
    /// <summary>
    /// M3 greybox arena (TDD_03 §36 M3, TDD_01 §0.5/§11.4): 80×80 m walled yard, two inner wall lines with 8 m
    /// chokepoints, containers and pillars towards the edges, an open centre. Box colliders exist only so the
    /// NavGridBaker (and later line-of-sight raycasts) can see the geometry.
    /// </summary>
    static class GreyboxMapBuilder
    {
        public const float Size = 80f;
        const float WallHeight = 3f;
        const float WallThickness = 1f;

        public static GameObject Build(Material wall, Material prop)
        {
            var root = new GameObject("Map_Greybox");
            float half = Size * 0.5f;

            // Outer walls.
            Box(root, "Wall_N", new Vector3(0f, WallHeight * 0.5f, half), new Vector3(Size + WallThickness, WallHeight, WallThickness), wall);
            Box(root, "Wall_S", new Vector3(0f, WallHeight * 0.5f, -half), new Vector3(Size + WallThickness, WallHeight, WallThickness), wall);
            Box(root, "Wall_E", new Vector3(half, WallHeight * 0.5f, 0f), new Vector3(WallThickness, WallHeight, Size), wall);
            Box(root, "Wall_W", new Vector3(-half, WallHeight * 0.5f, 0f), new Vector3(WallThickness, WallHeight, Size), wall);

            // Inner wall lines at z = ±20 with two 8 m gaps each (chokepoints the horde has to flow through).
            foreach (float z in new[] { -20f, 20f })
            {
                Segment(root, -half + 1f, -16f, z, wall);
                Segment(root, -8f, 8f, z, wall);
                Segment(root, 16f, half - 1f, z, wall);
            }

            // Containers (6 × 2.6 × 2.5) and pillars along the edges; the centre stays open (~25 m).
            Box(root, "Container_1", new Vector3(-28f, 1.3f, 8f), new Vector3(6f, 2.6f, 2.5f), prop);
            Box(root, "Container_2", new Vector3(-28f, 1.3f, 3f), new Vector3(6f, 2.6f, 2.5f), prop);
            Box(root, "Container_3", new Vector3(27f, 1.3f, -6f), new Vector3(2.5f, 2.6f, 6f), prop);
            Box(root, "Container_4", new Vector3(30f, 1.3f, 10f), new Vector3(6f, 2.6f, 2.5f), prop);
            Box(root, "Container_5", new Vector3(-12f, 1.3f, 30f), new Vector3(6f, 2.6f, 2.5f), prop);
            Box(root, "Container_6", new Vector3(14f, 1.3f, -31f), new Vector3(6f, 2.6f, 2.5f), prop);
            Box(root, "Truck", new Vector3(-20f, 1.4f, -30f), new Vector3(2.6f, 2.8f, 6f), prop);
            for (int i = 0; i < 6; i++)
            {
                float angle = i * Mathf.PI / 3f + 0.4f;
                Box(root, "Pillar_" + i, new Vector3(Mathf.Cos(angle) * 15f, 1.5f, Mathf.Sin(angle) * 11f), new Vector3(1.2f, 3f, 1.2f), prop);
            }
            return root;
        }

        static void Segment(GameObject root, float fromX, float toX, float z, Material material)
        {
            float length = toX - fromX;
            Box(root, "InnerWall", new Vector3((fromX + toX) * 0.5f, WallHeight * 0.5f, z), new Vector3(length, WallHeight, WallThickness), material);
        }

        static void Box(GameObject root, string name, Vector3 position, Vector3 size, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = position;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        }
    }
}
