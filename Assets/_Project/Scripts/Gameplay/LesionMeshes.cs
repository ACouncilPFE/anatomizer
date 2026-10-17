using UnityEngine;

namespace AIGame.Gameplay
{
    /// <summary>Shared meshes and materials for ArteryLesion, built once on first use.</summary>
    public static class LesionMeshes
    {
        private static Mesh plaque;
        private static Mesh clot;
        private static Mesh quad;
        private static Material plaqueMaterial;
        private static Material clotMaterial;
        private static Material tearRimMaterial;
        private static Material tearGashMaterial;
        private static Material scarMaterial;
        private static Material stitchMaterial;
        private static Material bloodParticleMaterial;

        /// <summary>Lumpy, fatty mound (unit size; scale it to taste).</summary>
        public static Mesh Plaque => plaque ??= ProceduralMeshes.DisplacedSphere("Plaque", 28, 20, d =>
            0.5f * (1f + 0.12f * Mathf.Sin(6f * d.x + 2f * d.y) + 0.08f * Mathf.Sin(9f * d.y - 3f * d.z + 1f) + 0.05f * Mathf.Sin(13f * d.z + d.x)));

        /// <summary>Irregular, stringy-looking clot mass.</summary>
        public static Mesh Clot => clot ??= ProceduralMeshes.DisplacedSphere("Clot", 32, 24, d =>
            0.5f * (1f + 0.18f * Mathf.Sin(7f * d.x - 4f * d.z) + 0.12f * Mathf.Sin(11f * d.y + 2f * d.x + 2f) + 0.06f * Mathf.Sin(17f * d.z + 5f * d.y)));

        public static Mesh Quad => quad ??= Gash(1f, 1f, 1, false);

        public static Material PlaqueMaterial   => plaqueMaterial   ??= Make("Plaque",   new Color(0.92f, 0.82f, 0.50f), 0.6f, new Color(0.10f, 0.08f, 0.02f));
        public static Material ClotMaterial     => clotMaterial     ??= Make("Clot",     new Color(0.30f, 0.03f, 0.05f), 0.75f, new Color(0.05f, 0f, 0.01f));
        public static Material TearRimMaterial  => tearRimMaterial  ??= Make("TearRim",  new Color(0.85f, 0.10f, 0.12f), 0.85f, new Color(0.45f, 0.02f, 0.04f));
        public static Material TearGashMaterial => tearGashMaterial ??= Make("TearGash", new Color(0.06f, 0f, 0.01f),    0.9f, new Color(0.08f, 0f, 0.01f));
        public static Material ScarMaterial     => scarMaterial     ??= Make("Scar",     new Color(0.88f, 0.55f, 0.55f), 0.4f, Color.black);
        public static Material StitchMaterial   => stitchMaterial   ??= Make("Stitch",   new Color(0.15f, 0.25f, 0.6f),  0.5f, new Color(0.1f, 0.2f, 0.5f));

        public static Material BloodParticleMaterial
        {
            get
            {
                if (bloodParticleMaterial == null)
                {
                    // Alpha-blended and always included in builds, so the soft dot reads as a blob.
                    bloodParticleMaterial = new Material(Shader.Find("Sprites/Default")) { name = "BloodParticle" };
                    bloodParticleMaterial.mainTexture = ProceduralTextures.SoftDot(32);
                    bloodParticleMaterial.color = Color.white;
                }
                return bloodParticleMaterial;
            }
        }

        /// <summary>
        /// A ragged lens-shaped slit in the XY plane (long along Y), facing +Z (into the vessel).
        /// segments is the number of jagged steps along each edge.
        /// </summary>
        public static Mesh Gash(float length, float width, int segments, bool jagged = true)
        {
            int rows = segments + 1;
            Vector3[] vertices = new Vector3[rows * 2];
            Vector2[] uvs = new Vector2[vertices.Length];
            for (int i = 0; i < rows; i++)
            {
                float t = i / (float)segments;
                float y = (t - 0.5f) * length;
                float half = jagged
                    ? 0.5f * width * Mathf.Pow(Mathf.Sin(Mathf.PI * t), 0.7f) * (0.75f + 0.5f * Mathf.PerlinNoise(t * 9f, length))
                    : 0.5f * width;
                float wobble = jagged ? (Mathf.PerlinNoise(t * 5f, 3.3f) - 0.5f) * width * 0.4f : 0f;
                vertices[i * 2] = new Vector3(wobble - half, y, 0f);
                vertices[i * 2 + 1] = new Vector3(wobble + half, y, 0f);
                uvs[i * 2] = new Vector2(0f, t);
                uvs[i * 2 + 1] = new Vector2(1f, t);
            }

            int[] triangles = new int[segments * 6];
            int k = 0;
            for (int i = 0; i < segments; i++)
            {
                int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
                triangles[k++] = a; triangles[k++] = b; triangles[k++] = c;
                triangles[k++] = b; triangles[k++] = d; triangles[k++] = c;
            }

            Vector3[] normals = new Vector3[vertices.Length];
            for (int i = 0; i < normals.Length; i++)
            {
                normals[i] = Vector3.forward;
            }

            Mesh mesh = new Mesh { name = jagged ? "Gash" : "Quad" };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.normals = normals;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material Make(string name, Color color, float gloss, Color emission)
        {
            Material mat = new Material(Shader.Find("Standard")) { name = name };
            mat.color = color;
            mat.SetFloat("_Glossiness", gloss);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emission);
            return mat;
        }
    }
}
