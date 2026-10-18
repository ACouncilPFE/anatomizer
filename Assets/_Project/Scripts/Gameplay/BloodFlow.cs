using UnityEngine;
using UnityEngine.Rendering;

namespace AIGame.Gameplay
{
    /// <summary>
    /// Red blood cells drifting and tumbling down the artery. They have no colliders (so they never
    /// block shots), but they slide around the ship's capsule instead of passing through it, and
    /// report each brush against the hull (CellBrushedShip) so ShipAudio can play a rub.
    /// Cells that reach the far end re-enter at the near end.
    /// </summary>
    public class BloodFlow : MonoBehaviour
    {
        [SerializeField] private int count = 70;
        [SerializeField] private float cellRadius = 0.55f;
        [SerializeField] private float minSpeed = 2.5f;
        [SerializeField] private float maxSpeed = 5.5f;
        [SerializeField, Range(0f, 1f)] private float maxRadialFraction = 0.75f;
        [SerializeField] private float hullClearance = 1f;     // capsule radius + cell radius
        [SerializeField] private float hullHalfLength = 0.6f;  // capsule's straight section, either side of centre

        /// <summary>A cell started rubbing against the ship: where, and how hard (0..1).</summary>
        public static event System.Action<Vector3, float> CellBrushedShip;

        private Transform ship;
        private Rigidbody shipBody;

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
            public Vector3 push;      // how far the ship has shoved this cell aside
            public bool touching;
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

        // Slide the cell around the capsule rather than through it, and report the first contact.
        private void PushOffHull(ref Cell c, Vector3 position)
        {
            Vector3 axis = ship.forward;
            float along = Mathf.Clamp(Vector3.Dot(position - ship.position, axis), -hullHalfLength, hullHalfLength);
            Vector3 nearest = ship.position + axis * along;
            Vector3 away = position - nearest;
            float distance = away.magnitude;

            if (distance < hullClearance)
            {
                Vector3 direction = distance > 0.001f ? away / distance : Random.onUnitSphere;
                c.push += direction * (hullClearance - distance);
                if (!c.touching)
                {
                    c.touching = true;
                    float speed = shipBody != null ? shipBody.velocity.magnitude : 0f;
                    CellBrushedShip?.Invoke(nearest + direction * 0.4f, Mathf.Clamp01(0.3f + speed / 40f));
                }
            }
            else if (distance > hullClearance + 0.5f)
            {
                c.touching = false;
            }
        }

        private void Update()
        {
            if (cells == null)
            {
                return;
            }

            float dt = Time.deltaTime;
            if (ship == null)
            {
                ShipController controller = FindObjectOfType<ShipController>();
                if (controller != null)
                {
                    ship = controller.transform;
                    shipBody = controller.GetComponent<Rigidbody>();
                }
            }

            for (int i = 0; i < cells.Length; i++)
            {
                ref Cell c = ref cells[i];

                // Blood moves fastest in the middle of the vessel and slowest near the wall.
                c.z += c.speed * (1.15f - c.radial * 0.6f) * dt;
                if (c.z > ArteryPath.MaxTravelZ)
                {
                    c.z = ArteryPath.MinTravelZ;
                    c.push = Vector3.zero;
                    c.touching = false;
                }
                c.angle += c.driftRate * dt;

                Vector3 center = ArteryPath.CenterAt(c.z);
                Vector2 radii = ArteryPath.RadiiAt(c.z);
                Vector3 position = center + new Vector3(
                    Mathf.Cos(c.angle) * radii.x * c.radial,
                    Mathf.Sin(c.angle) * radii.y * c.radial,
                    0f);

                c.push *= Mathf.Exp(-dt * 0.8f); // the flow slowly carries shoved cells back into line
                if (ship != null)
                {
                    PushOffHull(ref c, position + c.push);
                }
                c.transform.localPosition = position + c.push;
                c.baseRotation = Quaternion.AngleAxis(c.spinRate * dt, c.spinAxis) * c.baseRotation;
                c.transform.localRotation = c.baseRotation;
            }
        }
    }
}
