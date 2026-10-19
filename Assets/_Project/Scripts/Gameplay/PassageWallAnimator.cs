using UnityEngine;

namespace AIGame.Gameplay
{
    /// <summary>
    /// Makes a passage's wall mesh follow its live Squeeze (a heart chamber contracting and relaxing
    /// with each beat) and SqueezeAt (peristalsis waves travelling along the gut). Each vertex moves toward or away from its point on the centreline.
    /// Only the visible mesh moves; the collider stays at the relaxed size, and containment
    /// (which uses the live radii) keeps the ship clear of the squeezing wall.
    /// </summary>
    public class PassageWallAnimator : MonoBehaviour
    {
        private Passage passage;
        private Mesh mesh;
        private Vector3[] restVertices;
        private Vector3[] centers;
        private float[] vertexS;
        private Vector3[] working;
        private float restScale = 1f;
        private float lastScale = -1f;

        public void Bind(Passage target, Mesh wall, Vector3[] ringCenters, float[] ringS = null)
        {
            passage = target;
            vertexS = ringS;
            mesh = wall;
            mesh.MarkDynamic();
            restVertices = wall.vertices;
            centers = ringCenters;
            working = new Vector3[restVertices.Length];
            restScale = passage.Squeeze != null ? Mathf.Max(passage.Squeeze(), 0.01f) : 1f;
            if (passage.SqueezeAt != null && vertexS != null)
            {
                // The mesh was built mid-wave; work out each vertex's rest offset.
                for (int i = 0; i < restVertices.Length; i++)
                {
                    float k = Mathf.Max(passage.SqueezeAt(vertexS[i]), 0.01f);
                    restVertices[i] = centers[i] + (restVertices[i] - centers[i]) / k;
                }
            }
        }

        private void LateUpdate()
        {
            if (passage == null)
            {
                return;
            }

            if (passage.SqueezeAt != null && vertexS != null)
            {
                // Waves move every frame: move every vertex.
                float whole = passage.Squeeze != null ? passage.Squeeze() / restScale : 1f;
                for (int i = 0; i < restVertices.Length; i++)
                {
                    float k = whole * passage.SqueezeAt(vertexS[i]);
                    working[i] = centers[i] + (restVertices[i] - centers[i]) * k;
                }
                mesh.vertices = working;
                mesh.RecalculateBounds();
                return;
            }

            if (passage.Squeeze == null)
            {
                return;
            }

            float scale = passage.Squeeze() / restScale;
            if (Mathf.Abs(scale - lastScale) < 0.002f)
            {
                return;
            }
            lastScale = scale;

            for (int i = 0; i < restVertices.Length; i++)
            {
                working[i] = centers[i] + (restVertices[i] - centers[i]) * scale;
            }
            mesh.vertices = working;
            mesh.RecalculateBounds();
        }
    }
}
