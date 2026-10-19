using UnityEngine;
using UnityEngine.Rendering;

namespace AIGame.Gameplay
{
    /// <summary>
    /// Dresses the artery so it looks like the inside of a real vessel on angioscopy footage:
    ///   - the passages of the mission (ArteryLayout: a sealed main artery that forks into two
    ///     branches), with folded, wet-looking lining, built from the same PassageNetwork the
    ///     pilot enforces, so the walls you see are the walls you're kept inside;
    ///   - procedural lining texture and normal map (mottled pink-red, dark branching vessels);
    ///   - red blood cells (proper biconcave discs) flowing down the vessel;
    ///   - damage to the wall to repair: tears, plaque and clots (ArteryLesionField);
    ///   - an endoscope-style light on the ship and a deep-red, darker atmosphere so the wet walls
    ///     catch highlights near you and fall away into the red haze.
    ///
    /// Sits on the tunnel object. Scenes built before this existed get it automatically: the
    /// bootstrap below finds the old "ArteryTubeMesh" object and upgrades it at load, hiding the
    /// old glow blobs and sphere "blood cells".
    /// </summary>
    [DisallowMultipleComponent]
    public class ArteryEnvironment : MonoBehaviour
    {
        public const string TunnelObjectName = "ArteryTubeMesh";

        [Header("Wall")]
        [SerializeField] private int textureSize = 512;
        [SerializeField] private float ringsPerMeter = 1.4f;
        [SerializeField] private int sides = 48;
        [SerializeField] private Vector2 textureTiling = new Vector2(3f, 3f);
        [SerializeField, Range(0f, 1f)] private float wetness = 0.78f;

        [Header("Blood")]
        [SerializeField] private int bloodCellCount = 70;

        [Header("Wall damage")]
        [SerializeField] private bool addLesions = true;

        [Header("Lighting")]
        [SerializeField] private bool addEndoscopeLight = true;
        [SerializeField] private float sceneLightIntensity = 0.3f; // dim the sun; the ship's light does the work

        private static readonly string[] LegacyDressing = { "ArteryGlow_", "BloodCell_" };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) => UpgradeScene();
            UpgradeScene();
        }

        private static void UpgradeScene()
        {
            if (FindObjectOfType<ArteryEnvironment>() != null)
            {
                return;
            }

            GameObject tunnel = GameObject.Find(TunnelObjectName);
            if (tunnel != null)
            {
                tunnel.AddComponent<ArteryEnvironment>();
            }
        }

        private void Awake()
        {
            BuildWall();
            HideLegacyDressing();
            ApplyAtmosphere();
        }

        private void Start()
        {
            if (GetComponentInChildren<BloodFlow>() == null)
            {
                GameObject flow = new GameObject("BloodFlow");
                flow.transform.SetParent(transform, false);
                flow.AddComponent<BloodFlow>().Configure(bloodCellCount);
            }

            if (addLesions && GetComponentInChildren<ArteryLesionField>() == null)
            {
                GameObject lesions = new GameObject("Lesions");
                lesions.transform.SetParent(transform, false);
                lesions.AddComponent<ArteryLesionField>();
            }

            if (addEndoscopeLight)
            {
                AddEndoscopeLight();
            }

            // Anything that isn't wall shows as deep-red haze rather than sky.
            UseHazeBackground();

            foreach (Light light in FindObjectsOfType<Light>())
            {
                if (light.type == LightType.Directional)
                {
                    light.intensity = Mathf.Min(light.intensity, sceneLightIntensity);
                }
            }
        }

        /// <summary>
        /// Builds Mission 1's passages (the main artery and its two branches, see ArteryLayout),
        /// then a wall mesh, collider and lining for each. The main artery's wall goes on this
        /// object; each branch gets a child object.
        /// </summary>
        public void BuildWall()
        {
            // Passage meshes are in world coordinates, so the tunnel must sit at the origin.
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one;

            ArteryLayout.BuildMission1();
            Material lining = CreateLiningMaterial(textureSize, textureTiling, wetness, Color.white, new Color(0.10f, 0.01f, 0.025f));

            for (int i = 0; i < PassageNetwork.All.Count; i++)
            {
                Passage passage = PassageNetwork.All[i];
                GameObject host = gameObject;
                if (i > 0)
                {
                    string childName = "Passage: " + passage.Name;
                    Transform existing = transform.Find(childName);
                    host = existing != null ? existing.gameObject : new GameObject(childName);
                    host.transform.SetParent(transform, false);
                }

                MeshFilter filter = GetOrAdd<MeshFilter>(host);
                MeshRenderer meshRenderer = GetOrAdd<MeshRenderer>(host);
                filter.sharedMesh = PassageMeshes.Wall(passage, ringsPerMeter, sides, false);
                meshRenderer.sharedMaterial = lining;
                meshRenderer.shadowCastingMode = ShadowCastingMode.Off;

                MeshCollider wallCollider = GetOrAdd<MeshCollider>(host);
                wallCollider.convex = false;
                wallCollider.sharedMesh = PassageMeshes.Wall(passage, ringsPerMeter * 0.5f, sides / 2, true);
            }
        }

        /// <summary>
        /// The wet lining used for vessel and chamber walls: the procedural texture and normal map,
        /// tinted (white = artery pink-red). emission is a faint glow so far walls aren't pure black.
        /// </summary>
        public static Material CreateLiningMaterial(int textureSize, Vector2 textureTiling, float wetness, Color tint, Color emission)
        {
            ProceduralTextures.ArteryWall(textureSize, out Texture2D albedo, out Texture2D normal);

            Material mat = new Material(Shader.Find("Standard")) { name = "Lining_Procedural" };
            mat.mainTexture = albedo;
            mat.mainTextureScale = textureTiling;
            mat.color = tint;
            mat.SetTexture("_BumpMap", normal);
            mat.SetFloat("_BumpScale", 0.9f);
            mat.EnableKeyword("_NORMALMAP");
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Glossiness", wetness);
            // A faint inner glow keeps distant walls from going pure black inside the fog.
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emission);
            return mat;
        }

        private void HideLegacyDressing()
        {
            Transform root = transform.parent != null ? transform.parent : transform;
            foreach (Transform child in root)
            {
                foreach (string prefix in LegacyDressing)
                {
                    if (child.name.StartsWith(prefix))
                    {
                        child.gameObject.SetActive(false);
                    }
                }
            }
        }

        /// <summary>Deep-red haze and low ambient light, like looking down a vessel with a scope.</summary>
        public static void ApplyAtmosphere()
        {
            ApplyAtmosphere(new Color(0.30f, 0.03f, 0.07f), new Color(0.22f, 0.05f, 0.07f), 0.016f);
        }

        public static void ApplyAtmosphere(Color fog, Color ambient, float density)
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = ambient;
            RenderSettings.fog = true;
            RenderSettings.fogColor = fog;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = density;
        }

        /// <summary>Haze-coloured background for every scene camera, so gaps never show sky.</summary>
        public static void UseHazeBackground()
        {
            foreach (Camera cam in FindObjectsOfType<Camera>())
            {
                if (cam.targetTexture == null || cam.farClipPlane > 50f)
                {
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = RenderSettings.fogColor;
                }
            }
        }

        public static void AddEndoscopeLight()
        {
            ShipController ship = FindObjectOfType<ShipController>();
            if (ship == null || ship.transform.Find("Endoscope Light") != null)
            {
                return;
            }

            // Main beam: a warm white spot ahead of the nose, like a catheter camera's light.
            GameObject beam = new GameObject("Endoscope Light");
            beam.transform.SetParent(ship.transform, false);
            beam.transform.localPosition = new Vector3(0f, 0.25f, 0.8f);
            Light spot = beam.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.range = 55f;
            spot.spotAngle = 100f;
            spot.intensity = 1.7f;
            spot.color = new Color(1f, 0.93f, 0.86f);
            spot.shadows = LightShadows.None;

            // Soft fill so the walls right beside the ship aren't black.
            GameObject fill = new GameObject("Endoscope Fill");
            fill.transform.SetParent(ship.transform, false);
            Light point = fill.AddComponent<Light>();
            point.type = LightType.Point;
            point.range = 12f;
            point.intensity = 0.9f;
            point.color = new Color(1f, 0.85f, 0.8f);
            point.shadows = LightShadows.None;
        }

        private static T GetOrAdd<T>(GameObject host) where T : Component
        {
            T component = host.GetComponent<T>();
            return component != null ? component : host.AddComponent<T>();
        }
    }
}
