using UnityEngine;

namespace AIGame.Gameplay
{
    /// <summary>
    /// Textures generated in code, so the artery and cells need no imported art.
    ///
    /// The artery wall is modelled on angioscopy footage: a wet, mottled pink-red lining crossed
    /// by darker branching vessels (the vasa vasorum), with fine cell-scale speckle. Every texture
    /// tiles seamlessly, because the wall wraps all the way round.
    /// </summary>
    public static class ProceduralTextures
    {
        /// <summary>Albedo and matching normal map for the artery lining.</summary>
        public static void ArteryWall(int size, out Texture2D albedo, out Texture2D normal)
        {
            Color baseLight = new Color(0.90f, 0.55f, 0.55f);
            Color baseDeep  = new Color(0.70f, 0.24f, 0.27f);
            Color veinLarge = new Color(0.30f, 0.02f, 0.08f);
            Color veinSmall = new Color(0.45f, 0.05f, 0.12f);

            float[] height = new float[size * size];
            Color[] colors = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Domain warp gives the veins an organic meander instead of straight noise ridges.
                    float wx = x + (Fbm(x, y, size, 3f, 3, 11.3f) - 0.5f) * size * 0.18f;
                    float wy = y + (Fbm(x, y, size, 3f, 3, 47.1f) - 0.5f) * size * 0.18f;

                    float mottling = Fbm(x, y, size, 5f, 4, 3.7f);
                    float speckle  = Tileable(x, y, size, 64f, 91.7f);
                    float large    = Vein(wx, wy, size, 4f, 0.86f, 23.9f);
                    float small    = Vein(wx * 1.7f, wy * 1.7f, size, 9f, 0.90f, 71.2f) * (1f - large);

                    Color c = Color.Lerp(baseDeep, baseLight, Mathf.SmoothStep(0.25f, 0.75f, mottling));
                    c *= 0.94f + speckle * 0.12f;
                    c = Color.Lerp(c, veinLarge, large * 0.9f);
                    c = Color.Lerp(c, veinSmall, small * 0.75f);
                    c.a = 1f;

                    int i = y * size + x;
                    colors[i] = c;
                    height[i] = mottling * 0.35f + large * 0.55f + small * 0.25f + speckle * 0.08f;
                }
            }

            albedo = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "ArteryWall_Albedo", wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
            albedo.SetPixels(colors);
            albedo.Apply(true);

            normal = NormalFromHeight(height, size, 3.5f, "ArteryWall_Normal");
        }

        /// <summary>Infected cell skin: sickly yellow-green with olive-brown blotches and pale pustules.</summary>
        public static Texture2D InfectedCell(int size)
        {
            Color body     = new Color(0.64f, 0.70f, 0.22f);
            Color blotch   = new Color(0.36f, 0.30f, 0.08f);
            Color pustule  = new Color(0.90f, 0.88f, 0.45f);
            Color[] colors = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float b = Mathf.SmoothStep(0.52f, 0.66f, Fbm(x, y, size, 4f, 4, 5.1f));
                    float p = Mathf.SmoothStep(0.70f, 0.78f, Tileable(x, y, size, 14f, 33.3f));
                    Color c = Color.Lerp(body, blotch, b * 0.8f);
                    c = Color.Lerp(c, pustule, p * 0.7f);
                    c.a = 1f;
                    colors[y * size + x] = c;
                }
            }

            return Build(colors, size, "InfectedCell_Albedo");
        }

        /// <summary>Malignant cell skin: near-black purple with angry red veining.</summary>
        public static Texture2D MalignantCell(int size)
        {
            Color body     = new Color(0.22f, 0.06f, 0.24f);
            Color bruise   = new Color(0.38f, 0.10f, 0.42f);
            Color vein     = new Color(0.62f, 0.06f, 0.12f);
            Color[] colors = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float m = Fbm(x, y, size, 4f, 4, 8.8f);
                    float v = Vein(x, y, size, 5f, 0.9f, 19.4f);
                    Color c = Color.Lerp(body, bruise, m);
                    c = Color.Lerp(c, vein, v * 0.9f);
                    c.a = 1f;
                    colors[y * size + x] = c;
                }
            }

            return Build(colors, size, "MalignantCell_Albedo");
        }

        // ─── Noise helpers ──────────────────────────────────────────────────────────

        // Perlin noise that repeats every `size` pixels in both directions (a tile blend of four
        // shifted samples). freq is the number of noise cells across the tile.
        private static float Tileable(float x, float y, int size, float freq, float seed)
        {
            x = Mathf.Repeat(x, size);
            y = Mathf.Repeat(y, size);
            float s = freq / size;
            float a = Mathf.PerlinNoise(x * s + seed,          y * s + seed);
            float b = Mathf.PerlinNoise((x - size) * s + seed, y * s + seed);
            float c = Mathf.PerlinNoise(x * s + seed,          (y - size) * s + seed);
            float d = Mathf.PerlinNoise((x - size) * s + seed, (y - size) * s + seed);
            float u = x / size;
            float v = y / size;
            float n = a * (1f - u) * (1f - v) + b * u * (1f - v) + c * (1f - u) * v + d * u * v;
            // The blend flattens contrast mid-tile; stretch it back out around 0.5.
            return Mathf.Clamp01((n - 0.5f) * 1.6f + 0.5f);
        }

        private static float Fbm(float x, float y, int size, float freq, int octaves, float seed)
        {
            float sum = 0f;
            float amplitude = 0.5f;
            float total = 0f;
            for (int o = 0; o < octaves; o++)
            {
                sum += Tileable(x, y, size, freq, seed + o * 17.7f) * amplitude;
                total += amplitude;
                freq *= 2f;
                amplitude *= 0.5f;
            }
            return sum / total;
        }

        // Thin bright lines where noise crosses 0.5: ridged noise thresholded into vessel-like strands.
        private static float Vein(float x, float y, int size, float freq, float threshold, float seed)
        {
            float ridge = 1f - Mathf.Abs(Tileable(x, y, size, freq, seed) * 2f - 1f);
            return Mathf.SmoothStep(threshold, 1f, ridge);
        }

        private static Texture2D NormalFromHeight(float[] height, int size, float strength, string name)
        {
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float left  = height[y * size + (x + size - 1) % size];
                    float right = height[y * size + (x + 1) % size];
                    float down  = height[((y + size - 1) % size) * size + x];
                    float up    = height[((y + 1) % size) * size + x];
                    Vector3 n = new Vector3((left - right) * strength, (down - up) * strength, 1f).normalized;
                    pixels[y * size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                }
            }

            // Linear (not sRGB) so the shader reads the vectors back unchanged.
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true, true) { name = name, wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
            tex.SetPixels(pixels);
            tex.Apply(true);
            return tex;
        }

        private static Texture2D Build(Color[] colors, int size, string name)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Repeat };
            tex.SetPixels(colors);
            tex.Apply(true);
            return tex;
        }
    }
}
