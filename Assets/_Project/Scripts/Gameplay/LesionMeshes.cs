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
        private static Mesh aneurysm;
        private static Mesh shard;
        private static Mesh sphere;
        private static Material aneurysmMaterial;
        private static Material metalMaterial;
        private static Material stentMaterial;
        private static Material balloonMaterial;
        private static Material scarTissueMaterial;

        /// <summary>Lumpy, fatty mound (unit size; scale it to taste).</summary>
        public static Mesh Plaque => plaque ??= ProceduralMeshes.DisplacedSphere("Plaque", 28, 20, d =>
            0.5f * (1f + 0.12f * Mathf.Sin(6f * d.x + 2f * d.y) + 0.08f * Mathf.Sin(9f * d.y - 3f * d.z + 1f) + 0.05f * Mathf.Sin(13f * d.z + d.x)));

        /// <summary>Irregular, stringy-looking clot mass.</summary>
        public static Mesh Clot => clot ??= ProceduralMeshes.DisplacedSphere("Clot", 32, 24, d =>
            0.5f * (1f + 0.18f * Mathf.Sin(7f * d.x - 4f * d.z) + 0.12f * Mathf.Sin(11f * d.y + 2f * d.x + 2f) + 0.06f * Mathf.Sin(17f * d.z + 5f * d.y)));

        public static Mesh Quad => quad ??= Gash(1f, 1f, 1, false);

        /// <summary>Smooth, swollen sac with a few faint ridges where the wall has stretched thin.</summary>
        public static Mesh Aneurysm => aneurysm ??= ProceduralMeshes.DisplacedSphere("Aneurysm", 32, 24, d =>
            0.5f * (1f + 0.04f * Mathf.Sin(8f * d.x + 3f * d.y) + 0.03f * Mathf.Sin(11f * d.z - 2f * d.x)));

        /// <summary>Jagged metal splinter (long along local Z once scaled).</summary>
        public static Mesh Shard => shard ??= ProceduralMeshes.DisplacedSphere("Shard", 12, 8, d =>
            0.5f * (0.8f + 0.35f * Mathf.Abs(Mathf.Sin(3f * d.x + 5f * d.y)) + 0.2f * Mathf.Abs(Mathf.Sin(7f * d.y - 2f * d.z))));

        public static Mesh Sphere => sphere ??= ProceduralMeshes.DisplacedSphere("Sphere", 24, 16, d => 0.5f);

        public static Material PlaqueMaterial   => plaqueMaterial   ??= Make("Plaque",   new Color(0.92f, 0.82f, 0.50f), 0.6f, new Color(0.10f, 0.08f, 0.02f));
        public static Material ClotMaterial     => clotMaterial     ??= Make("Clot",     new Color(0.30f, 0.03f, 0.05f), 0.75f, new Color(0.05f, 0f, 0.01f));
        public static Material TearRimMaterial  => tearRimMaterial  ??= Make("TearRim",  new Color(0.85f, 0.10f, 0.12f), 0.85f, new Color(0.45f, 0.02f, 0.04f));
        public static Material TearGashMaterial => tearGashMaterial ??= Make("TearGash", new Color(0.06f, 0f, 0.01f),    0.9f, new Color(0.08f, 0f, 0.01f));
        public static Material ScarMaterial     => scarMaterial     ??= Make("Scar",     new Color(0.88f, 0.55f, 0.55f), 0.4f, Color.black);
        public static Material StitchMaterial   => stitchMaterial   ??= Make("Stitch",   new Color(0.15f, 0.25f, 0.6f),  0.5f, new Color(0.1f, 0.2f, 0.5f));
        public static Material AneurysmMaterial => aneurysmMaterial ??= Make("Aneurysm", new Color(0.62f, 0.20f, 0.36f), 0.85f, new Color(0.12f, 0.01f, 0.04f));
        public static Material BalloonMaterial  => balloonMaterial  ??= Make("Balloon",  new Color(0.65f, 0.82f, 1f),    0.95f, new Color(0.08f, 0.12f, 0.2f));
        /// <summary>Pale, tough, fibrous scar in the heart muscle.</summary>
        public static Material ScarTissueMaterial => scarTissueMaterial ??= Make("ScarTissue", new Color(0.88f, 0.8f, 0.78f), 0.35f, new Color(0.05f, 0.06f, 0.08f));

        public static Material MetalMaterial
        {
            get
            {
                if (metalMaterial == null)
                {
                    metalMaterial = Make("ForeignMetal", new Color(0.55f, 0.57f, 0.6f), 0.75f, Color.black);
                    metalMaterial.SetFloat("_Metallic", 0.9f);
                }
                return metalMaterial;
            }
        }

        public static Material StentMaterial
        {
            get
            {
                if (stentMaterial == null)
                {
                    stentMaterial = Make("Stent", new Color(0.8f, 0.82f, 0.86f), 0.85f, new Color(0.08f, 0.08f, 0.1f));
                    stentMaterial.SetFloat("_Metallic", 1f);
                }
                return stentMaterial;
            }
        }

        /// <summary>
        /// A ring of plaque lining the passage around s0, narrowing it by `depth`
        /// (0 = no narrowing, 0.6 = only 40% of the width left) at its middle and blending into
        /// the wall at both ends. Vertices are relative to `origin`; faces point inward.
        /// Pass an existing mesh to update it in place as the blockage is opened.
        /// </summary>
        public static Mesh StenosisBand(Mesh mesh, Vector3 origin, Passage passage, float s0, float halfLength, float depth, int sides = 40, int rings = 18)
        {
            int columns = sides + 1;
            Vector3[] vertices = new Vector3[rings * columns];
            for (int ring = 0; ring < rings; ring++)
            {
                float t = ring / (float)(rings - 1) * 2f - 1f; // -1..1 along the band
                float s = s0 + t * halfLength;
                float bell = Mathf.Cos(t * Mathf.PI * 0.5f);
                bell *= bell;
                passage.Frame(s, out Vector3 center, out _, out Vector3 right, out Vector3 up);
                Vector2 radii = passage.RadiiAt(s) * 0.98f;
                for (int col = 0; col < columns; col++)
                {
                    float a = col / (float)sides * Mathf.PI * 2f;
                    float lumps = 1f + 0.18f * Mathf.Sin(a * 5f + s) + 0.1f * Mathf.Sin(a * 9f - 2f * s);
                    float scale = 1f - depth * bell * lumps;
                    vertices[ring * columns + col] = center + right * (Mathf.Cos(a) * radii.x * scale) + up * (Mathf.Sin(a) * radii.y * scale) - origin;
                }
            }

            if (mesh == null)
            {
                int[] triangles = new int[(rings - 1) * sides * 6];
                int k = 0;
                for (int ring = 0; ring < rings - 1; ring++)
                {
                    for (int col = 0; col < sides; col++)
                    {
                        int current = ring * columns + col;
                        int next = current + 1;
                        int upper = current + columns;
                        int upperNext = upper + 1;
                        triangles[k++] = current; triangles[k++] = upper; triangles[k++] = next;
                        triangles[k++] = next;    triangles[k++] = upper; triangles[k++] = upperNext;
                    }
                }
                mesh = new Mesh { name = "Stenosis" };
                mesh.vertices = vertices;
                mesh.triangles = triangles;
            }
            else
            {
                mesh.vertices = vertices;
            }

            mesh.RecalculateNormals();

            // The first and last columns are the same points; share their normals so no seam shows.
            Vector3[] normals = mesh.normals;
            for (int ring = 0; ring < rings; ring++)
            {
                int first = ring * columns;
                int last = first + sides;
                Vector3 n = (normals[first] + normals[last]).normalized;
                normals[first] = n;
                normals[last] = n;
            }
            mesh.normals = normals;

            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// A stent: thin zig-zag metal hoops hugging the passage wall around s0. Built
        /// relative to `origin`. Returns one mesh per hoop so they can appear one by one.
        /// </summary>
        public static Mesh[] StentHoops(Vector3 origin, Passage passage, float s0, float halfLength, int hoops, int sides = 40)
        {
            Mesh[] result = new Mesh[hoops];
            const float width = 0.14f;
            int columns = sides + 1;
            for (int h = 0; h < hoops; h++)
            {
                float s = s0 + Mathf.Lerp(-halfLength, halfLength, h / (float)Mathf.Max(hoops - 1, 1));
                passage.Frame(s, out Vector3 center, out Vector3 tangent, out Vector3 right, out Vector3 up);
                Vector2 radii = passage.RadiiAt(s) * 0.9f;
                Vector3[] vertices = new Vector3[columns * 2];
                for (int col = 0; col < columns; col++)
                {
                    float a = col / (float)sides * Mathf.PI * 2f;
                    // Zig-zag along the hoop, like the cut pattern of a real stent.
                    float zig = (col % 2 == 0 ? 1f : -1f) * 0.18f;
                    Vector3 p = center + right * (Mathf.Cos(a) * radii.x) + up * (Mathf.Sin(a) * radii.y) + tangent * zig - origin;
                    vertices[col * 2] = p - tangent * (width * 0.5f);
                    vertices[col * 2 + 1] = p + tangent * (width * 0.5f);
                }

                int[] triangles = new int[sides * 12];
                int k = 0;
                for (int col = 0; col < sides; col++)
                {
                    int a0 = col * 2, a1 = a0 + 1, b0 = a0 + 2, b1 = a0 + 3;
                    triangles[k++] = a0; triangles[k++] = a1; triangles[k++] = b0;
                    triangles[k++] = b0; triangles[k++] = a1; triangles[k++] = b1;
                    triangles[k++] = b0; triangles[k++] = a1; triangles[k++] = a0;
                    triangles[k++] = b1; triangles[k++] = a1; triangles[k++] = b0;
                }

                Mesh mesh = new Mesh { name = "StentHoop" };
                mesh.vertices = vertices;
                mesh.triangles = triangles;
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                result[h] = mesh;
            }
            return result;
        }

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
