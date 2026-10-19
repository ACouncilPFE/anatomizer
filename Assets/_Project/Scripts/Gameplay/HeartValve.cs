using UnityEngine;
using UnityEngine.Rendering;

namespace AIGame.Gameplay
{
    /// <summary>
    /// A heart valve across a passage: three pale leaflets that meet in the middle when shut and
    /// fold back against the wall when open, timed by the heartbeat. It is a timed gate: while it
    /// is shut the ship can't cross, and flying into it bumps you back to the side you came from.
    ///
    /// A torn leaflet (a LeakyValve lesion on this valve) never closes properly: one leaflet
    /// sags open, letting blood flow backwards, until it is sutured.
    /// </summary>
    public class HeartValve : MonoBehaviour
    {
        public enum Kind
        {
            Tricuspid,
            Pulmonary,
            Pylorus
        }

        private const int Leaflets = 3;
        private const int Segments = 8;

        private Passage passage;
        private float valveS;
        private Kind kind;
        private float openness = 1f;
        private int shipSide;
        private float lastBump = -999f;

        private Mesh[] meshes;
        private Vector3[][] vertexBuffers;
        private Rigidbody ship;
        private ArteryLesion tear;
        private System.Func<bool> openWhen;

        public Kind ValveKind => kind;
        public bool IsOpen => openness > 0.6f;
        public string DisplayName => kind == Kind.Tricuspid ? "Tricuspid valve" : kind == Kind.Pulmonary ? "Pulmonary valve" : "Pylorus";

        /// <summary>A gate timed by something other than the heartbeat (the pylorus, by the gut).</summary>
        public void Configure(Kind valveKind, Passage onPassage, float atS, System.Func<bool> isOpen)
        {
            openWhen = isOpen;
            Configure(valveKind, onPassage, atS);
        }

        public void Configure(Kind valveKind, Passage onPassage, float atS)
        {
            kind = valveKind;
            passage = onPassage;
            valveS = atS;
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;
            BuildLeaflets();
        }

        /// <summary>A torn leaflet on this valve, if any (set by HeartEnvironment).</summary>
        public void SetTear(ArteryLesion lesion) => tear = lesion;

        private bool ShouldBeOpen()
        {
            if (openWhen != null)
            {
                return openWhen();
            }
            HeartBeat heart = HeartBeat.Instance;
            if (heart == null)
            {
                return true;
            }
            return kind == Kind.Tricuspid ? heart.TricuspidOpen : heart.PulmonaryOpen;
        }

        private void Update()
        {
            float target = ShouldBeOpen() ? 1f : 0f;
            openness = Mathf.MoveTowards(openness, target, Time.deltaTime * 7f);
            UpdateLeaflets();
        }

        private void FixedUpdate()
        {
            if (ship == null)
            {
                ShipController controller = FindObjectOfType<ShipController>();
                ship = controller != null ? controller.GetComponent<Rigidbody>() : null;
                if (ship == null)
                {
                    return;
                }
            }

            float s = passage.Project(ship.position, out Vector2 local, out _);
            Vector2 r = passage.RadiiAt(valveS);
            bool beside = Mathf.Abs(local.x) < r.x * 1.1f && Mathf.Abs(local.y) < r.y * 1.1f;
            float offset = s - valveS;
            if (!beside || Mathf.Abs(offset) > 6f)
            {
                shipSide = 0;
                return;
            }

            if (Mathf.Abs(offset) > 1.3f)
            {
                shipSide = offset < 0f ? -1 : 1;
            }

            // Shut: nothing gets through. Push the ship back out to the side it came from.
            if (!IsOpen && shipSide != 0 && Mathf.Abs(offset) < 1.3f)
            {
                passage.Frame(valveS + shipSide * 1.3f, out Vector3 center, out Vector3 tangent, out Vector3 right, out Vector3 up);
                ship.position = center + right * local.x + up * local.y;
                float along = Vector3.Dot(ship.velocity, tangent);
                ship.velocity -= tangent * along * 1.6f; // stop, and bounce back a little
                if (Time.time - lastBump > 0.4f)
                {
                    lastBump = Time.time;
                    GameAudio.PlayAt(ProceduralAudio.HitWall, ship.position, 0.9f, 0.6f);
                }
            }
        }

        // ─── Leaflets ──────────────────────────────────────────────────────────────

        private void BuildLeaflets()
        {
            Material tissue = new Material(Shader.Find("Standard")) { name = "ValveLeaflet" };
            // Thin, pale pink tissue: lighter than the wall so it reads, but not glaring.
            tissue.color = new Color(0.78f, 0.52f, 0.54f);
            tissue.SetFloat("_Glossiness", 0.55f);
            tissue.EnableKeyword("_EMISSION");
            tissue.SetColor("_EmissionColor", new Color(0.06f, 0.02f, 0.025f));

            meshes = new Mesh[Leaflets];
            vertexBuffers = new Vector3[Leaflets][];
            for (int l = 0; l < Leaflets; l++)
            {
                GameObject go = new GameObject("Leaflet " + l);
                go.transform.SetParent(transform, false);
                Mesh mesh = new Mesh { name = "Leaflet" };
                mesh.MarkDynamic();

                // Two faces (front and back) with their own vertices, so both sides light properly.
                int perSide = Segments + 2; // arc points + tip
                vertexBuffers[l] = new Vector3[perSide * 2];
                mesh.vertices = vertexBuffers[l];
                int[] triangles = new int[Segments * 6];
                int k = 0;
                for (int i = 0; i < Segments; i++)
                {
                    int tip = Segments + 1;
                    triangles[k++] = i; triangles[k++] = i + 1; triangles[k++] = tip;
                    triangles[k++] = perSide + i + 1; triangles[k++] = perSide + i; triangles[k++] = perSide + tip;
                }
                mesh.triangles = triangles;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = tissue;
                r.shadowCastingMode = ShadowCastingMode.Off;
                meshes[l] = mesh;
            }
            UpdateLeaflets();
        }

        private void UpdateLeaflets()
        {
            if (meshes == null)
            {
                return;
            }

            passage.Frame(valveS, out Vector3 center, out Vector3 tangent, out Vector3 right, out Vector3 up);
            Vector2 radii = passage.RadiiAt(valveS) * 0.9f; // rooted just inside the ridged wall
            // Blood pushes the leaflets open downstream (+tangent).
            for (int l = 0; l < Leaflets; l++)
            {
                float open = openness;
                if (l == 0 && tear != null && !tear.IsTreated)
                {
                    open = Mathf.Max(open, 0.55f); // torn: sags, never seals
                }

                float a0 = (l / (float)Leaflets) * Mathf.PI * 2f;
                float a1 = ((l + 1) / (float)Leaflets) * Mathf.PI * 2f;
                float mid = (a0 + a1) * 0.5f;
                Vector3 wallMid = center + right * (Mathf.Cos(mid) * radii.x) + up * (Mathf.Sin(mid) * radii.y);

                // Closed: the tip meets the others in the middle, bulging slightly back.
                // Open: the leaflet swings downstream until it lies against the wall.
                Vector3 closedTip = center - tangent * 0.4f;
                Vector3 openTip = Vector3.Lerp(center, wallMid, 0.8f) + tangent * (Mathf.Min(radii.x, radii.y) * 0.9f);
                Vector3 tip = Vector3.Lerp(closedTip, openTip, open);

                Vector3[] v = vertexBuffers[l];
                int perSide = Segments + 2;
                Vector3 normalOffset = tangent * 0.03f;
                for (int i = 0; i <= Segments; i++)
                {
                    float a = Mathf.Lerp(a0, a1, i / (float)Segments);
                    Vector3 p = center + right * (Mathf.Cos(a) * radii.x) + up * (Mathf.Sin(a) * radii.y);
                    v[i] = p - normalOffset;
                    v[perSide + i] = p + normalOffset;
                }
                v[Segments + 1] = tip - normalOffset;
                v[perSide + Segments + 1] = tip + normalOffset;

                meshes[l].vertices = v;
                meshes[l].RecalculateNormals();
                meshes[l].RecalculateBounds();
            }
        }
    }
}
