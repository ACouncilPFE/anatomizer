using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIGame.Gameplay
{
    /// <summary>Meshes generated in code: the sealed artery, red blood cells and diseased cells.</summary>
    public static class ProceduralMeshes
    {
        // ─── Artery wall ────────────────────────────────────────────────────────────

        /// <summary>
        /// The whole sealed vessel from ArteryPath.StartZ to EndZ, facing inward. The lining has
        /// soft longitudinal folds and faint rings, like the inside of a real artery, and the ends
        /// close into domes. doubleSided adds back faces (use that copy for the collider, so a fast
        /// ship can't slip through from either side).
        /// </summary>
        public static Mesh ArteryWall(float ringsPerMeter, int sides, bool doubleSided)
        {
            int rings = Mathf.CeilToInt((ArteryPath.EndZ - ArteryPath.StartZ) * ringsPerMeter) + 1;
            int columns = sides + 1; // duplicate seam column so UVs wrap cleanly
            Vector3[] vertices = new Vector3[rings * columns];
            Vector3[] normals = new Vector3[vertices.Length];
            Vector2[] uvs = new Vector2[vertices.Length];

            // About one texture repeat per 33 m of length and per trip round the wall (~33 m).
            const float uvMetresPerTile = 33f;

            for (int ring = 0; ring < rings; ring++)
            {
                float z = Mathf.Lerp(ArteryPath.StartZ, ArteryPath.EndZ, ring / (float)(rings - 1));
                for (int col = 0; col < columns; col++)
                {
                    float angle = col / (float)sides * Mathf.PI * 2f;
                    int i = ring * columns + col;
                    vertices[i] = WallPoint(angle, z);
                    normals[i] = WallNormal(angle, z);
                    uvs[i] = new Vector2(col / (float)sides, (z - ArteryPath.StartZ) / uvMetresPerTile);
                }
            }

            int quads = (rings - 1) * sides;
            int[] triangles = new int[quads * (doubleSided ? 12 : 6)];
            int t = 0;
            for (int ring = 0; ring < rings - 1; ring++)
            {
                for (int col = 0; col < sides; col++)
                {
                    int current = ring * columns + col;
                    int next = current + 1;
                    int upper = current + columns;
                    int upperNext = upper + 1;

                    // Winding faces the inside of the tube.
                    triangles[t++] = current; triangles[t++] = upper;     triangles[t++] = next;
                    triangles[t++] = next;    triangles[t++] = upper;     triangles[t++] = upperNext;

                    if (doubleSided)
                    {
                        triangles[t++] = next;      triangles[t++] = upper; triangles[t++] = current;
                        triangles[t++] = upperNext; triangles[t++] = upper; triangles[t++] = next;
                    }
                }
            }

            Mesh mesh = new Mesh { name = doubleSided ? "ArteryTube_Collider" : "ArteryTube" };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateTangents(); // needed for the normal map
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector3 WallPoint(float angle, float z)
        {
            Vector3 center = ArteryPath.CenterAt(z);
            Vector2 radii = ArteryPath.RadiiAt(z);
            // Folds run along the vessel and twist slowly; rings are faint and irregular.
            float relief = 1f
                + 0.030f * Mathf.Sin(angle * 7f + z * 0.12f)
                + 0.018f * Mathf.Sin(angle * 13f - z * 0.21f + 1.3f)
                + 0.012f * Mathf.Sin(z * 0.9f + Mathf.Sin(angle * 3f) * 1.5f);
            return center + new Vector3(Mathf.Cos(angle) * radii.x * relief, Mathf.Sin(angle) * radii.y * relief, 0f);
        }

        private static Vector3 WallNormal(float angle, float z)
        {
            const float e = 0.01f;
            Vector3 alongZ = WallPoint(angle, z + e) - WallPoint(angle, z - e);
            Vector3 around = WallPoint(angle + e, z) - WallPoint(angle - e, z);
            Vector3 n = Vector3.Cross(alongZ, around);
            if (n.sqrMagnitude < 1e-10f)
            {
                // At the tip of an end dome the ring has shrunk to a point: face back into the vessel.
                return z < 0f ? Vector3.forward : Vector3.back;
            }

            n.Normalize();
            Vector3 inward = ArteryPath.CenterAt(z) - WallPoint(angle, z);
            return Vector3.Dot(n, inward) >= 0f ? n : -n;
        }

        // ─── Cells ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// A red blood cell: a disc pinched in the middle on both faces (the Evans-Fung biconcave
        /// profile). radius is the disc's outer radius; it lies in the XZ plane.
        /// </summary>
        public static Mesh BiconcaveDisc(float radius, int segments = 32, int rings = 12)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            int rows = rings * 2 + 1; // top pole .. rim .. bottom pole

            for (int row = 0; row < rows; row++)
            {
                float theta = row / (float)(rows - 1) * Mathf.PI; // 0 top, pi bottom
                float rho = Mathf.Sin(theta);                       // 0 at poles, 1 at the rim
                float sign = theta < Mathf.PI * 0.5f ? 1f : -1f;
                float rho2 = rho * rho;
                float half = 0.5f * Mathf.Sqrt(Mathf.Max(0f, 1f - rho2)) * (0.207f + 2.003f * rho2 - 1.123f * rho2 * rho2);
                for (int s = 0; s <= segments; s++)
                {
                    float phi = s / (float)segments * Mathf.PI * 2f;
                    vertices.Add(new Vector3(Mathf.Cos(phi) * rho * radius, sign * half * radius, Mathf.Sin(phi) * rho * radius));
                    uvs.Add(new Vector2(s / (float)segments, row / (float)(rows - 1)));
                }
            }

            return Assemble("RedBloodCell", vertices, uvs, rows, segments);
        }

        /// <summary>
        /// A sphere whose radius in each direction comes from radiusAt(direction). Used for the
        /// lumpy infected cell and the spiky malignant one.
        /// </summary>
        public static Mesh DisplacedSphere(string name, int segments, int rows, Func<Vector3, float> radiusAt)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            for (int row = 0; row < rows; row++)
            {
                float theta = row / (float)(rows - 1) * Mathf.PI;
                for (int s = 0; s <= segments; s++)
                {
                    float phi = s / (float)segments * Mathf.PI * 2f;
                    Vector3 dir = new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi), Mathf.Cos(theta), Mathf.Sin(theta) * Mathf.Sin(phi));
                    vertices.Add(dir * radiusAt(dir));
                    uvs.Add(new Vector2(s / (float)segments, row / (float)(rows - 1)));
                }
            }

            return Assemble(name, vertices, uvs, rows, segments);
        }

        // Rows of (segments + 1) vertices from top pole to bottom pole -> outward-facing triangles.
        private static Mesh Assemble(string name, List<Vector3> vertices, List<Vector2> uvs, int rows, int segments)
        {
            int columns = segments + 1;
            List<int> triangles = new List<int>();
            for (int row = 0; row < rows - 1; row++)
            {
                for (int s = 0; s < segments; s++)
                {
                    int a = row * columns + s;
                    int b = a + 1;
                    int c = a + columns;
                    int d = c + 1;
                    triangles.Add(a); triangles.Add(b); triangles.Add(c);
                    triangles.Add(b); triangles.Add(d); triangles.Add(c);
                }
            }

            Mesh mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
