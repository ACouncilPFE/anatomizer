using UnityEngine;
using UnityEngine.Rendering;

namespace AIGame.Gameplay
{
    /// <summary>
    /// Dresses the artery so it looks like the inside of a real vessel on angioscopy footage:
    ///   - a sealed tube (both ends close into domes) with folded, wet-looking lining, built from
    ///     ArteryPath so the walls you see are the walls the pilot enforces;
    ///   - procedural lining texture and normal map (mottled pink-red, dark branching vessels);
    ///   - red blood cells (proper biconcave discs) flowing down the vessel;
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

            if (addEndoscopeLight)
            {
                AddEndoscopeLight();
            }

            foreach (Light light in FindObjectsOfType<Light>())
            {
                if (light.type == LightType.Directional)
                {
                    light.intensity = Mathf.Min(light.intensity, sceneLightIntensity);
                }
            }
        }

        /// <summary>Builds the sealed tube mesh, its collider and the lining material.</summary>
        public void BuildWall()
        {
            // The mesh is in ArteryPath coordinates, so the tunnel must sit at the origin.
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one;

            MeshFilter filter = GetOrAdd<MeshFilter>();
            MeshRenderer meshRenderer = GetOrAdd<MeshRenderer>();
            filter.sharedMesh = ProceduralMeshes.ArteryWall(ringsPerMeter, sides, false);
            meshRenderer.sharedMaterial = CreateWallMaterial();
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;

            MeshCollider wallCollider = GetOrAdd<MeshCollider>();
            wallCollider.convex = false;
            wallCollider.sharedMesh = ProceduralMeshes.ArteryWall(ringsPerMeter * 0.5f, sides / 2, true);
        }

        private Material CreateWallMaterial()
        {
            ProceduralTextures.ArteryWall(textureSize, out Texture2D albedo, out Texture2D normal);

            Material mat = new Material(Shader.Find("Standard")) { name = "ArteryWall_Procedural" };
            mat.mainTexture = albedo;
            mat.mainTextureScale = textureTiling;
            mat.color = Color.white;
            mat.SetTexture("_BumpMap", normal);
            mat.SetFloat("_BumpScale", 0.9f);
            mat.EnableKeyword("_NORMALMAP");
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Glossiness", wetness);
            // A faint inner glow keeps distant walls from going pure black inside the fog.
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(0.10f, 0.01f, 0.025f));
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
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.22f, 0.05f, 0.07f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.30f, 0.03f, 0.07f);
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.016f;
        }

        private static void AddEndoscopeLight()
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

        private T GetOrAdd<T>() where T : Component
        {
            T component = GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
        }
    }
}
