using AIGame.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace AIGame.Gameplay
{
    /// <summary>
    /// Builds and runs Mission 3, Gut Run (see GutLayout):
    ///   - peristalsis waves squeeze the oesophagus and intestine and carry the ship along;
    ///   - the stomach's acid pool burns the hull (boost shields with H to resist it);
    ///   - the pylorus is a timed gate out of the stomach;
    ///   - good bacteria drift about (don't shoot them), food particles flow down the gut;
    ///   - problems: bleeding ulcers and a swallowed object in the stomach, polyps and a blockage
    ///     in the intestine.
    /// Lives on one object in the Mission_03_Gut scene.
    /// </summary>
    public class GutEnvironment : MonoBehaviour
    {
        [SerializeField] private int textureSize = 512;
        [SerializeField] private float ringsPerMeter = 1.2f;
        [SerializeField] private int sides = 48;
        [SerializeField] private float acidDamagePerSecond = 6f;
        [SerializeField] private int goodBacteria = 30;

        private GutMotility motility;
        private HeartValve pylorus;
        private Rigidbody ship;
        private ShipHealth hull;
        private ShipSystems systems;
        private AudioSource sizzle;

        public GutMotility Motility => motility;
        public HeartValve PylorusGate => pylorus;
        /// <summary>True while the ship is down in the stomach acid.</summary>
        public bool ShipInAcid { get; private set; }
        /// <summary>True when a peristalsis wave is about to catch the ship.</summary>
        public bool WaveBehindShip { get; private set; }

        private void Awake()
        {
            motility = GetComponent<GutMotility>();
            if (motility == null)
            {
                motility = gameObject.AddComponent<GutMotility>();
            }

            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;
            GutLayout.BuildMission3(motility);
            BuildWalls();
            // Warm, murky haze, lit by the scope: endoscopy footage of the gut is pink-orange.
            ArteryEnvironment.ApplyAtmosphere(new Color(0.22f, 0.1f, 0.06f), new Color(0.22f, 0.13f, 0.1f), 0.012f);
        }

        private void Start()
        {
            GameObject flow = new GameObject("FoodParticles");
            flow.transform.SetParent(transform, false);
            BloodFlow particles = flow.AddComponent<BloodFlow>();
            particles.Configure(45, new Color(0.5f, 0.38f, 0.18f));
            particles.UseMesh(LesionMeshes.Clot, 0.45f);

            pylorus = new GameObject("Pylorus Gate").AddComponent<HeartValve>();
            pylorus.transform.SetParent(transform, false);
            pylorus.Configure(HeartValve.Kind.Pylorus, GutLayout.Pylorus, GutLayout.PylorusGateS, () => motility.PylorusOpen);

            BuildAcidPool();
            PlaceBacteria();
            PlaceProblems();

            ArteryEnvironment.AddEndoscopeLight();
            ArteryEnvironment.UseHazeBackground();
            foreach (Light light in FindObjectsOfType<Light>())
            {
                if (light.type == LightType.Directional)
                {
                    light.intensity = Mathf.Min(light.intensity, 0.25f);
                }
            }

            sizzle = gameObject.AddComponent<AudioSource>();
            sizzle.clip = ProceduralAudio.Scrape;
            sizzle.loop = true;
            sizzle.pitch = 1.9f;
            sizzle.volume = 0f;
            sizzle.spatialBlend = 0f;
            sizzle.Play();
        }

        private void FixedUpdate()
        {
            if (ship == null)
            {
                ShipController controller = FindObjectOfType<ShipController>();
                if (controller == null)
                {
                    return;
                }
                ship = controller.GetComponent<Rigidbody>();
                hull = controller.GetComponent<ShipHealth>();
                systems = controller.GetComponent<ShipSystems>();
            }

            bool playing = GameManager.Instance == null || GameManager.Instance.CurrentState == GameState.InMission;
            ShipInAcid = false;
            WaveBehindShip = false;
            if (!playing || !PassageNetwork.Locate(ship.position, out Passage passage, out float s, out _, out _))
            {
                return;
            }

            // A peristalsis wave around the ship carries it along at the wave's speed.
            if (passage.SqueezeAt != null)
            {
                float squeeze = passage.SqueezeAt(s);
                if (squeeze < 0.9f)
                {
                    Vector3 tangent = passage.TangentAt(s);
                    float along = Vector3.Dot(ship.velocity, tangent);
                    float grip = Mathf.InverseLerp(0.9f, 0.6f, squeeze);
                    ship.velocity += tangent * ((motility.WaveSpeed * 1.4f - along) * grip * 6f * Time.fixedDeltaTime);
                }
                WaveBehindShip = motility.WaveComing(s);
            }

            // Stomach acid eats at the hull; the shields keep most of it out when boosted.
            if (passage == GutLayout.Stomach && ship.position.y < GutLayout.AcidLevel)
            {
                ShipInAcid = true;
                float leak = systems != null ? systems.ShieldLeak : 1f;
                hull?.ApplyDamage(acidDamagePerSecond * leak * Time.fixedDeltaTime);
            }
        }

        private void Update()
        {
            if (sizzle != null)
            {
                sizzle.volume = Mathf.MoveTowards(sizzle.volume, ShipInAcid ? 0.5f * GameAudio.MasterVolume : 0f, Time.deltaTime * 3f);
            }
        }

        private void BuildWalls()
        {
            Material esophagus = ArteryEnvironment.CreateLiningMaterial(textureSize, new Vector2(2.5f, 2.5f), 0.85f, new Color(1f, 0.82f, 0.8f), new Color(0.09f, 0.04f, 0.04f));
            Material stomach = ArteryEnvironment.CreateLiningMaterial(textureSize, new Vector2(3f, 3f), 0.9f, new Color(1f, 0.72f, 0.58f), new Color(0.1f, 0.05f, 0.03f));
            Material intestine = ArteryEnvironment.CreateLiningMaterial(textureSize, new Vector2(4f, 4f), 0.9f, new Color(1f, 0.74f, 0.55f), new Color(0.1f, 0.06f, 0.03f));

            foreach (Passage passage in PassageNetwork.All)
            {
                GameObject host = new GameObject("Passage: " + passage.Name);
                host.transform.SetParent(transform, false);

                Mesh wall = PassageMeshes.Wall(passage, ringsPerMeter, sides, false, out Vector3[] centers, out float[] vertexS);
                host.AddComponent<MeshFilter>().sharedMesh = wall;
                MeshRenderer r = host.AddComponent<MeshRenderer>();
                r.sharedMaterial = passage.Profile == PassageProfile.Stomach ? stomach
                    : passage.Profile == PassageProfile.Intestine ? intestine
                    : esophagus;
                r.shadowCastingMode = ShadowCastingMode.Off;

                MeshCollider wallCollider = host.AddComponent<MeshCollider>();
                wallCollider.sharedMesh = PassageMeshes.Wall(passage, ringsPerMeter * 0.5f, sides / 2, true);

                if (passage.SqueezeAt != null || passage.Squeeze != null)
                {
                    host.AddComponent<PassageWallAnimator>().Bind(passage, wall, centers, vertexS);
                }
            }
        }

        // A glowing yellow-green pool across the bottom of the stomach, bubbling.
        private void BuildAcidPool()
        {
            Material acid = new Material(Shader.Find("Standard")) { name = "StomachAcid" };
            acid.color = new Color(0.55f, 0.62f, 0.12f);
            acid.SetFloat("_Glossiness", 0.95f);
            acid.EnableKeyword("_EMISSION");
            acid.SetColor("_EmissionColor", new Color(0.22f, 0.27f, 0.03f));

            Passage stomach = GutLayout.Stomach;
            Vector3 middle = stomach.CenterAt(stomach.Length * 0.5f);
            Vector3 center = new Vector3(middle.x, GutLayout.AcidLevel, middle.z);
            for (int side = 0; side < 2; side++)
            {
                // One face from above, one from below, so it shows either way.
                GameObject surface = GameObject.CreatePrimitive(PrimitiveType.Quad);
                surface.name = side == 0 ? "Acid surface" : "Acid surface (underside)";
                Destroy(surface.GetComponent<Collider>());
                surface.transform.SetParent(transform, false);
                surface.transform.position = center;
                surface.transform.rotation = Quaternion.Euler(side == 0 ? 90f : -90f, 0f, 0f);
                surface.transform.localScale = new Vector3(70f, 70f, 1f);
                surface.GetComponent<Renderer>().sharedMaterial = acid;
            }

            GameObject bubbles = new GameObject("Acid bubbles");
            bubbles.transform.SetParent(transform, false);
            bubbles.transform.position = center + Vector3.up * 0.2f;
            bubbles.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            ParticleSystem ps = bubbles.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 2.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
            main.startColor = new Color(0.8f, 0.9f, 0.3f, 0.8f);
            main.maxParticles = 300;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 60f;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(30f, 30f, 0.1f);
            ParticleSystemRenderer psr = bubbles.GetComponent<ParticleSystemRenderer>();
            psr.sharedMaterial = LesionMeshes.BloodParticleMaterial;
            ps.Play();
        }

        private void PlaceBacteria()
        {
            Transform holder = new GameObject("Good bacteria").transform;
            holder.SetParent(transform, false);
            for (int i = 0; i < goodBacteria; i++)
            {
                // Mostly in the intestine, where gut flora lives; a few in the stomach.
                Passage passage = i < goodBacteria * 0.8f ? GutLayout.Intestine : GutLayout.Stomach;
                float s = Random.Range(passage.MinTravel + 8f, passage.MaxTravel - 4f);
                GameObject go = new GameObject("Bacterium");
                go.transform.SetParent(holder, false);
                go.AddComponent<GoodBacteria>().Configure(passage, s);
            }
        }

        private void PlaceProblems()
        {
            Transform holder = new GameObject("Problems").transform;
            holder.SetParent(transform, false);
            Passage stomach = GutLayout.Stomach;
            Passage intestine = GutLayout.Intestine;
            // Ulcers and the swallowed object sit on the stomach's upper walls, above the acid.
            Place(holder, LesionType.Ulcer, stomach, stomach.Length * 0.3f, 1.3f);
            Place(holder, LesionType.Ulcer, stomach, stomach.Length * 0.62f, 2.0f);
            Place(holder, LesionType.ForeignObject, stomach, stomach.Length * 0.48f, 0.4f);
            Place(holder, LesionType.Polyp, intestine, intestine.Length * 0.25f, 3.6f);
            Place(holder, LesionType.Blockage, intestine, intestine.Length * 0.5f, 0f);
            Place(holder, LesionType.Polyp, intestine, intestine.Length * 0.78f, 0.9f);
        }

        private static void Place(Transform parent, LesionType type, Passage passage, float s, float angle)
        {
            GameObject go = new GameObject(type + " in " + passage.Name);
            go.transform.SetParent(parent, false);
            go.AddComponent<ArteryLesion>().Configure(type, passage, s, angle);
        }
    }
}
