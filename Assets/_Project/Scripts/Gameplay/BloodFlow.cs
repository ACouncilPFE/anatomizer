using UnityEngine;
using UnityEngine.Rendering;

namespace AIGame.Gameplay
{
    /// <summary>
    /// Red blood cells drifting and tumbling down the artery. Purely visual: no colliders, so they
    /// never block shots or the ship. Cells that reach the far end re-enter at the near end.
    /// </summary>
    public class BloodFlow : MonoBehaviour
    {
        [SerializeField] private int count = 70;
        [SerializeField] private float cellRadius = 0.55f;
        [SerializeField] private float minSpeed = 2.5f;
        [SerializeField] private float maxSpeed = 5.5f;
        [SerializeField, Range(0f, 1f)] private float maxRadialFraction = 0.75f;

        private struct Cell
        {
            public Transform transform;
            public float z;
            public float angle;
            public float radial;
            public float speed;
            public float driftRate;
            public Vector3 spinAxis;
            public float spinRate;
            public Quaternion baseRotation;
        }

        private Cell[] cells;

        public void Configure(int cellCount)
        {
            count = cellCount;
        }

        private void Start()
        {
            Mesh mesh = ProceduralMeshes.BiconcaveDisc(cellRadius);
            Material mat = new Material(Shader.Find("Standard")) { name = "RedBloodCell" };
            mat.color = new Color(0.74f, 0.06f, 0.10f);
            mat.SetFloat("_Glossiness", 0.55f);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(0.09f, 0f, 0.01f));

            cells = new Cell[count];
            for (int i = 0; i < count; i++)
            {
                GameObject go = new GameObject("RedBloodCell_" + i);
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;

                float scale = Random.Range(0.85f, 1.15f);
                go.transform.localScale = Vector3.one * scale;

                cells[i] = new Cell
                {
                    transform = go.transform,
                    z = Random.Range(ArteryPath.MinTravelZ, ArteryPath.MaxTravelZ),
                    angle = Random.Range(0f, Mathf.PI * 2f),
                    radial = Mathf.Sqrt(Random.value) * maxRadialFraction,
                    speed = Random.Range(minSpeed, maxSpeed),
                    driftRate = Random.Range(-0.15f, 0.15f),
                    spinAxis = Random.onUnitSphere,
                    spinRate = Random.Range(15f, 60f),
                    baseRotation = Random.rotation,
                };
            }
        }

        private void Update()
        {
            if (cells == null)
            {
                return;
            }

            float dt = Time.deltaTime;
            for (int i = 0; i < cells.Length; i++)
            {
                ref Cell c = ref cells[i];

                // Blood moves fastest in the middle of the vessel and slowest near the wall.
                c.z += c.speed * (1.15f - c.radial * 0.6f) * dt;
                if (c.z > ArteryPath.MaxTravelZ)
                {
                    c.z = ArteryPath.MinTravelZ;
                }
                c.angle += c.driftRate * dt;

                Vector3 center = ArteryPath.CenterAt(c.z);
                Vector2 radii = ArteryPath.RadiiAt(c.z);
                c.transform.localPosition = center + new Vector3(
                    Mathf.Cos(c.angle) * radii.x * c.radial,
                    Mathf.Sin(c.angle) * radii.y * c.radial,
                    0f);
                c.baseRotation = Quaternion.AngleAxis(c.spinRate * dt, c.spinAxis) * c.baseRotation;
                c.transform.localRotation = c.baseRotation;
            }
        }
    }
}
