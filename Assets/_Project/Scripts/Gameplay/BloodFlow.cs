using UnityEngine;
using UnityEngine.Rendering;

namespace AIGame.Gameplay
{
    /// <summary>
    /// Red blood cells drifting and tumbling down the passages of the network (shared out by
    /// length). They have no colliders (so they never
    /// block shots), but they slide around the ship's capsule instead of passing through it, and
    /// report each brush against the hull (CellBrushedShip) so ShipAudio can play a rub.
    /// Cells that reach the far end of their passage re-enter at its start.
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

        /// <summary>Scales how fast all blood moves (the heart sets this with each beat's surge).</summary>
        public static float SpeedMultiplier = 1f;

        [SerializeField] private Color cellColor = new Color(0.74f, 0.06f, 0.10f);

        /// <summary>A cell started rubbing against the ship: where, and how hard (0..1).</summary>
        public static event System.Action<Vector3, float> CellBrushedShip;

        private Transform ship;
        private Rigidbody shipBody;

        private struct Cell
        {
            public Transform transform;
            public Passage passage;
            public float s;
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

        private Mesh customMesh;
        private float customScale = 1f;

        /// <summary>Float something other than red blood cells (e.g. food particles in the gut), at a size multiplier.</summary>
        public void UseMesh(Mesh mesh, float scale = 1f)
        {
            customMesh = mesh;
            customScale = scale;
        }

        /// <summary>Blood returning from the body (in veins and the right heart) is darker.</summary>
        public void Configure(int cellCount, Color color)
        {
            count = cellCount;
            cellColor = color;
        }

        private void Start()
        {
            Mesh mesh = customMesh != null ? customMesh : ProceduralMeshes.BiconcaveDisc(cellRadius);
            Material mat = new Material(Shader.Find("Standard")) { name = "RedBloodCell" };
            mat.color = cellColor;
            mat.SetFloat("_Glossiness", 0.55f);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(0.09f, 0f, 0.01f));

            if (PassageNetwork.All.Count == 0)
            {
                return;
            }

            float totalLength = 0f;
            foreach (Passage p in PassageNetwork.All)
            {
                totalLength += p.MaxTravel - p.MinTravel;
            }

            cells = new Cell[count];
            for (int i = 0; i < count; i++)
            {
                // Pick a passage in proportion to its length, so the blood is spread evenly.
                float pick = Random.value * totalLength;
                Passage passage = PassageNetwork.Main;
                foreach (Passage p in PassageNetwork.All)
                {
                    float span = p.MaxTravel - p.MinTravel;
                    if (pick <= span)
                    {
                        passage = p;
                        break;
                    }
                    pick -= span;
                }

                GameObject go = new GameObject("RedBloodCell_" + i);
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;

                float scale = Random.Range(0.85f, 1.15f) * customScale;
                go.transform.localScale = Vector3.one * scale;

                cells[i] = new Cell
                {
                    transform = go.transform,
                    passage = passage,
                    s = Random.Range(passage.MinTravel, passage.MaxTravel),
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
                c.s += c.speed * (1.15f - c.radial * 0.6f) * SpeedMultiplier * dt;
                if (c.s > c.passage.MaxTravel)
                {
                    c.s = c.passage.MinTravel;
                    c.push = Vector3.zero;
                    c.touching = false;
                }
                c.angle += c.driftRate * dt;

                Vector3 position = c.passage.PointAt(c.s, c.angle, c.radial);

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
