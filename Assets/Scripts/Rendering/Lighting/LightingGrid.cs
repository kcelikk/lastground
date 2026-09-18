using System;
using UnityEngine;

namespace LastGround.Rendering.Lighting
{
    /// <summary>
    /// Lighting-grid prototype (TDD_02 §21.2): a low-resolution top-down light texture that crowd shaders sample
    /// by world XZ, so zombies under a lamp are lit warm and those in the dark fall off — one texture read, no lights.
    /// M2 builds it at runtime from lamp positions; M7/M11 bake it per region in the editor (LightingGridBaker).
    /// </summary>
    public sealed class LightingGrid : IDisposable
    {
        static readonly int GridId = Shader.PropertyToID("_LGLightGrid");
        static readonly int RectId = Shader.PropertyToID("_LGLightGridRect");

        public readonly struct Lamp
        {
            public readonly Vector2 Position;
            public readonly float Radius;
            public readonly Color Color;

            public Lamp(Vector2 position, float radius, Color color)
            {
                Position = position;
                Radius = radius;
                Color = color;
            }
        }

        /// <summary>Sodium-vapour orange (TDD_01 §0.4: 2700–3200 K).</summary>
        public static readonly Color Sodium = new Color(1f, 0.55f, 0.22f);

        readonly Texture2D _texture;

        public LightingGrid(Vector2 min, float size, int resolution, Lamp[] lamps)
        {
            _texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false, true)
            {
                name = "LightingGrid",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            var pixels = new Color32[resolution * resolution];
            float cell = size / resolution;
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    var world = new Vector2(min.x + (x + 0.5f) * cell, min.y + (y + 0.5f) * cell);
                    Color sum = Color.black;
                    foreach (Lamp lamp in lamps)
                    {
                        float d = Vector2.Distance(world, lamp.Position) / lamp.Radius;
                        if (d >= 1f) continue;
                        float falloff = (1f - d * d);
                        sum += lamp.Color * (falloff * falloff);
                    }
                    pixels[y * resolution + x] = new Color(Mathf.Min(1f, sum.r), Mathf.Min(1f, sum.g), Mathf.Min(1f, sum.b), 1f);
                }
            }
            _texture.SetPixels32(pixels);
            _texture.Apply(false, true);

            Shader.SetGlobalTexture(GridId, _texture);
            Shader.SetGlobalVector(RectId, new Vector4(min.x, min.y, 1f / size, 1f / size));
        }

        /// <summary>Six lamps on a ring plus one at the centre: the greybox arena layout of TDD_01 §0.5.</summary>
        public static Lamp[] GreyboxLamps()
        {
            var lamps = new Lamp[7];
            lamps[0] = new Lamp(Vector2.zero, 9f, Sodium * 0.7f);
            for (int i = 0; i < 6; i++)
            {
                float angle = i * Mathf.PI / 3f;
                lamps[i + 1] = new Lamp(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 18f, 11f, Sodium);
            }
            return lamps;
        }

        public void Dispose()
        {
            if (_texture != null) UnityEngine.Object.Destroy(_texture);
            Shader.SetGlobalTexture(GridId, Texture2D.blackTexture);
        }
    }
}
