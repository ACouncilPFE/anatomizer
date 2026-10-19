using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIGame.Gameplay
{
    /// <summary>Meshes generated in code: red blood cells and diseased cells (walls: PassageMeshes).</summary>
    public static class ProceduralMeshes
    {
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
