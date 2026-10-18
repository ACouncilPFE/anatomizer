using UnityEngine;

namespace AIGame.Gameplay
{
    /// <summary>
    /// Makes a passage's wall mesh follow its live Squeeze, so a heart chamber visibly contracts and
    /// relaxes with each beat. Each vertex moves toward or away from its point on the centreline.
    /// Only the visible mesh moves; the collider stays at the relaxed size, and containment
    /// (which uses the live radii) keeps the ship clear of the squeezing wall.
    /// </summary>
    public class PassageWallAnimator : MonoBehaviour
    {
        private Passage passage;
        private Mesh mesh;
        private Vector3[] restVertices;
        private Vector3[] centers;
        private Vector3[] working;
        private float restScale = 1f;
        private float lastScale = -1f;

        public void Bind(Passage target, Mesh wall, Vector3[] ringCenters)
        {
            passage = target;
            mesh = wall;
            mesh.MarkDynamic();
            restVertices = wall.vertices;
            centers = ringCenters;
            working = new Vector3[restVertices.Length];
            restScale = passage.Squeeze != null ? Mathf.Max(passage.Squeeze(), 0.01f) : 1f;
        }

        private void LateUpdate()
        {
            if (passage == null || passage.Squeeze == null)
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
