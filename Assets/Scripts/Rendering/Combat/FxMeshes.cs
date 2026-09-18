using UnityEngine;

namespace LastGround.Rendering.Combat
{
    /// <summary>
    /// Procedural meshes for the additive FX shader (LG/FX_Additive: brightness = across² × ends, from uv): a disc whose
    /// centre is bright and rim dark (blast flash, glow blob) and a ring bright on its middle line (aim marker).
    /// Both lie flat in XZ with radius 1.
    /// </summary>
    public static class FxMeshes
    {
        public static Mesh Disc(int segments = 24)
        {
            var vertices = new Vector3[segments + 1];
            var uv = new Vector2[segments + 1];
            var triangles = new int[segments * 3];
            vertices[0] = Vector3.zero;
            uv[0] = new Vector2(0.5f, 0.5f);
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                vertices[i + 1] = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                uv[i + 1] = new Vector2(0.5f, 0f);
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = 1 + (i + 1) % segments;
                triangles[i * 3 + 2] = 1 + i;
            }
            return Build("FxDisc", vertices, uv, triangles);
        }

        /// <param name="width">Ring width as a fraction of the radius.</param>
        public static Mesh Ring(float width = 0.12f, int segments = 48)
        {
            var vertices = new Vector3[segments * 3];
            var uv = new Vector2[segments * 3];
            var triangles = new int[segments * 12];
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                vertices[i * 3] = dir * (1f - width);
                vertices[i * 3 + 1] = dir;
                vertices[i * 3 + 2] = dir * (1f + width);
                uv[i * 3] = new Vector2(0.5f, 0f);
                uv[i * 3 + 1] = new Vector2(0.5f, 0.5f);
                uv[i * 3 + 2] = new Vector2(0.5f, 1f);
                int n = (i + 1) % segments;
                int t = i * 12;
                for (int band = 0; band < 2; band++)
                {
                    int a0 = i * 3 + band, a1 = a0 + 1, b0 = n * 3 + band, b1 = b0 + 1;
                    triangles[t++] = a0; triangles[t++] = b1; triangles[t++] = b0;
                    triangles[t++] = a0; triangles[t++] = a1; triangles[t++] = b1;
                }
            }
            return Build("FxRing", vertices, uv, triangles);
        }

        static Mesh Build(string name, Vector3[] vertices, Vector2[] uv, int[] triangles)
        {
            var mesh = new Mesh { name = name, vertices = vertices, uv = uv, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(2.5f, 0.1f, 2.5f));
            return mesh;
        }
    }
}
