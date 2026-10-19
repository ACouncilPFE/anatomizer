using UnityEngine;
using UnityEngine.Rendering;

namespace AIGame.Gameplay
{
    /// <summary>
    /// What the ship looks like from outside: a white medical vessel shrunk to nano size.
    ///   - sleek teardrop hull, glossy white, with red ambulance stripes;
    ///   - a tinted glass canopy over the cockpit at the front;
    ///   - a glowing engine ring at the back that brightens with speed, and three tail fins
    ///     with red (port) and green (starboard) navigation lights;
    ///   - flashing red / white ambulance beacons on top;
    ///   - a medical emblem on both sides and the top.
    ///
    /// The emblem is a red heart with a white medical cross by default. A plain red cross is
    /// available too, but the Red Cross emblem is protected by the Geneva Conventions and
    /// games have been asked to remove it, so only use it for private builds.
    ///
    /// Replaces the old capsule mesh (its renderer is switched off; the ship's collider is
    /// untouched). Built entirely in code.
    /// </summary>
    [RequireComponent(typeof(ShipController))]
    public class MedicalVesselModel : MonoBehaviour
    {
        public enum EmblemStyle
        {
            HeartAndCross,
            RedCross
        }

        [SerializeField] private EmblemStyle emblem = EmblemStyle.HeartAndCross;
        [SerializeField] private float speedForFullEngine = 45f;

        private const float Length = 1.9f;
        private const float Radius = 0.36f;

        private Rigidbody rb;
        private Renderer engineCore;
        private Light engineLight;
        private Renderer beaconRed, beaconWhite;
        private Light beaconLight;
        private Color engineColor = new Color(0.3f, 0.75f, 1f);
        private Transform model;

        private void Start()
        {
            rb = GetComponent<Rigidbody>();
            HideOldCapsule();
            Build();
        }

        private void HideOldCapsule()
        {
            Transform old = transform.Find("Mesh");
            if (old != null && old.GetComponent<Renderer>() != null)
            {
                old.GetComponent<Renderer>().enabled = false;
            }
        }

        private void Build()
        {
            model = new GameObject("Medical Vessel").transform;
            model.SetParent(transform, false);

            Material white = Mat(new Color(0.93f, 0.95f, 0.97f), 0.85f, 0.15f, Color.black);
            Material red = Mat(new Color(0.85f, 0.06f, 0.08f), 0.7f, 0.1f, new Color(0.25f, 0.01f, 0.02f));
            Material dark = Mat(new Color(0.18f, 0.2f, 0.24f), 0.6f, 0.7f, Color.black);
            Material glass = Mat(new Color(0.15f, 0.55f, 0.7f), 0.97f, 0.5f, new Color(0.05f, 0.25f, 0.32f));

            // Hull: a teardrop, round at the nose and tapering toward the engine. The red
            // ambulance stripes and the emblems are painted into its texture, so they wrap the
            // curve instead of floating off it.
            Mesh hullMesh = ProceduralMeshes.DisplacedSphere("VesselHull", 48, 32, HullRadius);
            Material hullPaint = Mat(Color.white, 0.85f, 0.15f, Color.black);
            hullPaint.mainTexture = HullTexture(emblem, 1024, 512);
            Part("Hull", hullMesh, hullPaint, Vector3.zero, new Vector3(Radius * 2f, Radius * 2f, Length));

            // Cockpit canopy: tinted glass bubble on top of the nose.
            Part("Canopy", LesionMeshes.Sphere, glass, new Vector3(0f, 0.23f, 0.4f), new Vector3(0.38f, 0.26f, 0.6f));
            Mesh ring = Ring(0.5f, 0.06f);

            // Engine: dark nozzle ring with a glowing core.
            Part("Nozzle", ring, dark, new Vector3(0f, 0f, -0.86f), new Vector3(0.42f, 0.42f, 2.5f));
            engineCore = Part("EngineCore", LesionMeshes.Sphere, Mat(Color.black, 0.9f, 0f, engineColor), new Vector3(0f, 0f, -0.9f), new Vector3(0.3f, 0.3f, 0.16f)).GetComponent<Renderer>();
            engineLight = PointLight("EngineGlow", new Vector3(0f, 0f, -1.15f), engineColor, 1.5f, 3f);

            // Tail fins: one up, two angled down; red tips.
            Fin(0f, white, red);
            Fin(120f, white, red);
            Fin(-120f, white, red);

            // Navigation lights: red on the left (port), green on the right (starboard).
            Part("NavPort", LesionMeshes.Sphere, Mat(Color.black, 0.9f, 0f, new Color(1f, 0.05f, 0.05f) * 2f), new Vector3(-Radius * 0.95f, 0.02f, -0.15f), Vector3.one * 0.05f);
            Part("NavStarboard", LesionMeshes.Sphere, Mat(Color.black, 0.9f, 0f, new Color(0.1f, 1f, 0.2f) * 2f), new Vector3(Radius * 0.95f, 0.02f, -0.15f), Vector3.one * 0.05f);

            // Ambulance beacons on top, behind the canopy.
            Part("BeaconBase", LesionMeshes.Sphere, dark, new Vector3(0f, Radius * 0.92f, -0.12f), new Vector3(0.2f, 0.06f, 0.1f));
            beaconRed = Part("BeaconRed", LesionMeshes.Sphere, Mat(new Color(0.5f, 0f, 0f), 0.9f, 0f, Color.red), new Vector3(-0.06f, Radius * 0.98f, -0.12f), Vector3.one * 0.07f).GetComponent<Renderer>();
            beaconWhite = Part("BeaconWhite", LesionMeshes.Sphere, Mat(new Color(0.6f, 0.6f, 0.6f), 0.9f, 0f, Color.white), new Vector3(0.06f, Radius * 0.98f, -0.12f), Vector3.one * 0.07f).GetComponent<Renderer>();
            beaconLight = PointLight("BeaconLight", new Vector3(0f, Radius + 0.15f, -0.12f), Color.red, 0f, 4f);

        }

        private void Update()
        {
            if (rb == null || engineCore == null)
            {
                return;
            }

            // Engine glow follows speed.
            float speed01 = Mathf.Clamp01(rb.velocity.magnitude / speedForFullEngine);
            float flicker = 1f + Mathf.Sin(Time.time * 37f) * 0.05f;
            engineCore.material.SetColor("_EmissionColor", engineColor * Mathf.Lerp(0.8f, 3f, speed01) * flicker);
            engineLight.intensity = Mathf.Lerp(0.6f, 2.2f, speed01) * flicker;

            // Ambulance beacons: red and white alternating, twice a second.
            bool redPhase = Mathf.Repeat(Time.time * 2f, 1f) < 0.5f;
            beaconRed.material.SetColor("_EmissionColor", Color.red * (redPhase ? 4f : 0.2f));
            beaconWhite.material.SetColor("_EmissionColor", Color.white * (redPhase ? 0.2f : 3f));
            beaconLight.color = redPhase ? new Color(1f, 0.1f, 0.1f) : new Color(1f, 0.95f, 0.9f);
            beaconLight.intensity = 0.9f;
        }

        // ─── Pieces ────────────────────────────────────────────────────────────────

        private void Fin(float roll, Material body, Material tip)
        {
            Transform pivot = new GameObject("FinPivot").transform;
            pivot.SetParent(model, false);
            pivot.localPosition = new Vector3(0f, 0f, -0.62f);
            pivot.localRotation = Quaternion.Euler(0f, 0f, roll);
            // Root sits just inside the tapering tail, so the fin grows out of the hull.
            Primitive(PrimitiveType.Cube, "Fin", body, pivot, new Vector3(0f, 0.25f, -0.05f), new Vector3(0.022f, 0.16f, 0.3f));
            Primitive(PrimitiveType.Cube, "FinTip", tip, pivot, new Vector3(0f, 0.33f, -0.09f), new Vector3(0.028f, 0.03f, 0.2f));
        }

        private Transform Part(string name, Mesh mesh, Material material, Vector3 position, Vector3 scale)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(model, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return go.transform;
        }

        private static Transform Primitive(PrimitiveType type, string name, Material material, Transform parent, Vector3 position, Vector3 scale)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            DestroyImmediate(go.GetComponent<Collider>()); // must go now: the ship has a dynamic Rigidbody
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            Renderer r = go.GetComponent<Renderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return go.transform;
        }

        private Light PointLight(string name, Vector3 position, Color color, float intensity, float range)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(model, false);
            go.transform.localPosition = position;
            Light light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            return light;
        }

        // A thin band around the Z axis, unit diameter (scale it to fit), facing outward.
        private static Mesh Ring(float radius, float width, int segments = 48)
        {
            int columns = segments + 1;
            Vector3[] vertices = new Vector3[columns * 2];
            Vector3[] normals = new Vector3[vertices.Length];
            for (int i = 0; i < columns; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                Vector3 outward = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                Vector3 p = outward * radius;
                vertices[i * 2] = p + new Vector3(0f, 0f, -width * 0.5f);
                vertices[i * 2 + 1] = p + new Vector3(0f, 0f, width * 0.5f);
                normals[i * 2] = outward;
                normals[i * 2 + 1] = outward;
            }

            int[] triangles = new int[segments * 6];
            int k = 0;
            for (int i = 0; i < segments; i++)
            {
                int a0 = i * 2, a1 = a0 + 1, b0 = a0 + 2, b1 = a0 + 3;
                triangles[k++] = a0; triangles[k++] = a1; triangles[k++] = b0;
                triangles[k++] = a1; triangles[k++] = b1; triangles[k++] = b0;
            }

            Mesh mesh = new Mesh { name = "Ring" };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material Mat(Color color, float gloss, float metallic, Color emission)
        {
            Material m = new Material(Shader.Find("Standard"));
            m.color = color;
            m.SetFloat("_Glossiness", gloss);
            m.SetFloat("_Metallic", metallic);
            if (emission != Color.black)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission);
            }
            return m;
        }

        // ─── Emblem ────────────────────────────────────────────────────────────────

        // Hull shape on the unit sphere: round nose, tapering tail.
        private static float HullRadius(Vector3 d)
        {
            float rear = Mathf.Max(0f, -d.z);
            return 0.5f * (1f - 0.22f * rear * rear);
        }

        private struct Badge
        {
            public Vector3 center, normal, right, up;
        }

        /// <summary>
        /// The hull's paint job, in the hull mesh's UV layout (DisplacedSphere: u around the Y axis,
        /// v from the top pole down). White, with two red ambulance stripes and an emblem on each
        /// side and on top, each wrapped onto the curved surface.
        /// </summary>
        private static Texture2D HullTexture(EmblemStyle style, int w, int h)
        {
            Texture2D badge = EmblemTexture(style, 256);
            Color white = new Color(0.93f, 0.95f, 0.97f);
            Color red = new Color(0.85f, 0.06f, 0.08f);
            const float badgeRadius = 0.13f;

            Badge[] badges =
            {
                new Badge { center = new Vector3(-Radius, 0f, -0.3f), normal = Vector3.left, right = Vector3.back, up = Vector3.up },
                new Badge { center = new Vector3(Radius, 0f, -0.3f), normal = Vector3.right, right = Vector3.forward, up = Vector3.up },
                new Badge { center = new Vector3(0f, Radius, -0.42f), normal = Vector3.up, right = Vector3.right, up = Vector3.forward },
            };

            Color[] pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                float theta = y / (float)(h - 1) * Mathf.PI;
                for (int x = 0; x < w; x++)
                {
                    float phi = x / (float)(w - 1) * Mathf.PI * 2f;
                    Vector3 d = new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi), Mathf.Cos(theta), Mathf.Sin(theta) * Mathf.Sin(phi));
                    float r = HullRadius(d);
                    Vector3 p = new Vector3(d.x * r * Radius * 2f, d.y * r * Radius * 2f, d.z * r * Length);

                    Color c = white;
                    if (Mathf.Abs(p.z - 0.12f) < 0.035f || Mathf.Abs(p.z + 0.08f) < 0.045f)
                    {
                        c = red;
                    }

                    foreach (Badge b in badges)
                    {
                        Vector3 local = p - b.center;
                        if (Vector3.Dot(d, b.normal) < 0.5f)
                        {
                            continue;
                        }
                        float bu = Vector3.Dot(local, b.right) / badgeRadius;
                        float bv = Vector3.Dot(local, b.up) / badgeRadius;
                        if (bu * bu + bv * bv <= 1f)
                        {
                            Color s = badge.GetPixelBilinear((bu + 1f) * 0.5f, (bv + 1f) * 0.5f);
                            c = Color.Lerp(c, s, s.a);
                        }
                    }

                    pixels[y * w + x] = c;
                }
            }

            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "VesselPaint", wrapMode = TextureWrapMode.Clamp, anisoLevel = 8 };
            tex.SetPixels(pixels);
            tex.Apply(true);
            return tex;
        }

        /// <summary>A round badge: white disc with a red rim, and the emblem in the middle.</summary>
        public static Texture2D EmblemTexture(EmblemStyle style, int size)
        {
            Color white = new Color(0.97f, 0.97f, 0.97f, 1f);
            Color red = new Color(0.86f, 0.05f, 0.08f, 1f);
            Color clear = new Color(0f, 0f, 0f, 0f);
            Color[] pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // -1..1 across the badge, y up.
                    float u = (x + 0.5f) / size * 2f - 1f;
                    float v = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);

                    Color c;
                    if (r > 1f)
                    {
                        c = clear;
                    }
                    else if (r > 0.88f)
                    {
                        c = red; // rim
                    }
                    else if (style == EmblemStyle.RedCross)
                    {
                        bool cross = (Mathf.Abs(u) < 0.18f && Mathf.Abs(v) < 0.6f) || (Mathf.Abs(v) < 0.18f && Mathf.Abs(u) < 0.6f);
                        c = cross ? red : white;
                    }
                    else
                    {
                        // Heart: (x^2 + y^2 - 1)^3 - x^2 y^3 <= 0, scaled and centred.
                        float hx = u / 0.62f;
                        float hy = (v + 0.08f) / 0.62f;
                        float q = hx * hx + hy * hy - 1f;
                        bool heart = q * q * q - hx * hx * hy * hy * hy <= 0f;
                        // White medical cross inside the heart.
                        float cx = u;
                        float cy = v - 0.02f;
                        bool cross = (Mathf.Abs(cx) < 0.08f && Mathf.Abs(cy) < 0.26f) || (Mathf.Abs(cy) < 0.08f && Mathf.Abs(cx) < 0.26f);
                        c = heart ? (cross ? white : red) : white;
                    }
                    pixels[y * size + x] = c;
                }
            }

            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "MedicalEmblem", wrapMode = TextureWrapMode.Clamp, anisoLevel = 4 };
            tex.SetPixels(pixels);
            tex.Apply(true);
            return tex;
        }
    }
}
