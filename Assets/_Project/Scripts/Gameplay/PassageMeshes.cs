using System.Collections.Generic;
using UnityEngine;

namespace AIGame.Gameplay
{
    /// <summary>Builds the wall of a passage: an inward-facing tube shaped by its profile.</summary>
    public static class PassageMeshes
    {
        /// <summary>
        /// The passage's wall from end to end, facing inward, with the profile's relief pressed
        /// into it. Where the wall runs inside another passage of the network (at a fork) it is
        /// left out, opening the way through. doubleSided adds back faces (use that copy for the
        /// collider, so a fast ship can't slip through from either side).
        /// </summary>
        public static Mesh Wall(Passage passage, float ringsPerMeter, int sides, bool doubleSided)
        {
            return Wall(passage, ringsPerMeter, sides, doubleSided, out _);
        }

        /// <summary>As Wall, also returning the centreline point each vertex belongs to (to squeeze it).</summary>
        public static Mesh Wall(Passage passage, float ringsPerMeter, int sides, bool doubleSided, out Vector3[] centers)
        {
            int rings = Mathf.CeilToInt(passage.Length * ringsPerMeter) + 1;
            int columns = sides + 1; // duplicate seam column so UVs wrap cleanly
            Vector3[] vertices = new Vector3[rings * columns];
            Vector3[] normals = new Vector3[vertices.Length];
            Vector2[] uvs = new Vector2[vertices.Length];
            bool[] open = new bool[vertices.Length];
            centers = new Vector3[vertices.Length];
            float tile = passage.Profile.MetresPerTile;

            for (int ring = 0; ring < rings; ring++)
            {
                float s = Mathf.Lerp(0f, passage.Length, ring / (float)(rings - 1));
                for (int col = 0; col < columns; col++)
                {
                    float angle = col / (float)sides * Mathf.PI * 2f;
                    int i = ring * columns + col;
                    vertices[i] = WallPoint(passage, angle, s);
                    centers[i] = passage.CenterAt(s);
                    normals[i] = WallNormal(passage, angle, s);
                    uvs[i] = new Vector2(col / (float)sides, s / tile);
                    open[i] = PassageNetwork.InsideAnother(passage, vertices[i], 0.97f);
                }
            }

            List<int> triangles = new List<int>();
            for (int ring = 0; ring < rings - 1; ring++)
            {
                for (int col = 0; col < sides; col++)
                {
                    int current = ring * columns + col;
                    int next = current + 1;
                    int upper = current + columns;
                    int upperNext = upper + 1;

                    // The opening into another passage. Only drop a quad once it is entirely inside
                    // the other passage, so the two walls overlap a little at the edge of the
                    // opening rather than leaving a gap.
                    if (open[current] && open[next] && open[upper] && open[upperNext])
                    {
                        continue;
                    }

                    // Winding faces the inside of the tube.
                    triangles.Add(current); triangles.Add(upper); triangles.Add(next);
                    triangles.Add(next); triangles.Add(upper); triangles.Add(upperNext);

                    if (doubleSided)
                    {
                        triangles.Add(next); triangles.Add(upper); triangles.Add(current);
                        triangles.Add(upperNext); triangles.Add(upper); triangles.Add(next);
                    }
                }
            }

            Mesh mesh = new Mesh { name = passage.Name + (doubleSided ? " (collider)" : "") };
            mesh.indexFormat = vertices.Length > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateTangents(); // needed for the normal map
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector3 WallPoint(Passage passage, float angle, float s)
        {
            passage.Frame(s, out Vector3 center, out _, out Vector3 right, out Vector3 up);
            Vector2 radii = passage.RadiiAt(s);
            float relief = passage.Profile.Relief(angle, s);
            return center + right * (Mathf.Cos(angle) * radii.x * relief) + up * (Mathf.Sin(angle) * radii.y * relief);
        }

        private static Vector3 WallNormal(Passage passage, float angle, float s)
        {
            const float e = 0.01f;
            Vector3 along = WallPoint(passage, angle, s + e) - WallPoint(passage, angle, s - e);
            Vector3 around = WallPoint(passage, angle + e, s) - WallPoint(passage, angle - e, s);
            Vector3 n = Vector3.Cross(along, around);
            Vector3 center = passage.CenterAt(s);
            if (n.sqrMagnitude < 1e-10f)
            {
                // At the tip of an end dome the ring has shrunk to a point: face back into the passage.
                Vector3 tangent = passage.TangentAt(s);
                return s < passage.Length * 0.5f ? tangent : -tangent;
            }

            n.Normalize();
            Vector3 inward = center - WallPoint(passage, angle, s);
            return Vector3.Dot(n, inward) >= 0f ? n : -n;
        }
    }
}
