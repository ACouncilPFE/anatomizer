using AIGame.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace AIGame.Gameplay
{
    /// <summary>
    /// Builds and runs Mission 2, Cardiac Maelstrom, inside the right side of the heart
    /// (see HeartLayout):
    ///   - the heartbeat (HeartBeat) drives everything: the atrium and ventricle walls squeeze,
    ///     blood surges forward on each beat, and the valves open and shut as timed gates;
    ///   - the problems to fix: a torn tricuspid leaflet (suture), clots in the chambers
    ///     (medicine) and scar tissue in the ventricle wall that makes the rhythm irregular (laser);
    ///   - darker blood (returning from the body), a deep purple-red haze, and the endoscope light.
    /// Lives on one object in the Mission_02_Heart scene.
    /// </summary>
    public class HeartEnvironment : MonoBehaviour
    {
        [SerializeField] private int textureSize = 512;
        [SerializeField] private float ringsPerMeter = 1.2f;
        [SerializeField] private int sides = 48;
        [SerializeField] private int bloodCellCount = 90;
        [SerializeField] private float surgePush = 16f; // m/s² of forward push at the height of a beat

        private HeartBeat heart;
        private Rigidbody ship;
        private HeartValve tricuspid;
        private HeartValve pulmonary;

        public HeartValve Tricuspid => tricuspid;
        public HeartValve Pulmonary => pulmonary;

        private void Awake()
        {
            heart = GetComponent<HeartBeat>();
            if (heart == null)
            {
                heart = gameObject.AddComponent<HeartBeat>();
            }

            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;
            HeartLayout.BuildMission2(heart);
            BuildWalls();
            ArteryEnvironment.ApplyAtmosphere(new Color(0.24f, 0.02f, 0.08f), new Color(0.2f, 0.05f, 0.08f), 0.011f);
        }

        private void Start()
        {
            GameObject flow = new GameObject("BloodFlow");
            flow.transform.SetParent(transform, false);
            flow.AddComponent<BloodFlow>().Configure(bloodCellCount, new Color(0.5f, 0.04f, 0.1f));

            tricuspid = new GameObject("Tricuspid Valve").AddComponent<HeartValve>();
            tricuspid.transform.SetParent(transform, false);
            tricuspid.Configure(HeartValve.Kind.Tricuspid, HeartLayout.Tricuspid, HeartLayout.TricuspidValveS);

            pulmonary = new GameObject("Pulmonary Valve").AddComponent<HeartValve>();
            pulmonary.transform.SetParent(transform, false);
            pulmonary.Configure(HeartValve.Kind.Pulmonary, HeartLayout.Pulmonary, HeartLayout.PulmonaryValveS);

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
        }

        private void OnDestroy()
        {
            BloodFlow.SpeedMultiplier = 1f;
        }

        private void Update()
        {
            // Blood crawls between beats and rushes on the beat.
            BloodFlow.SpeedMultiplier = 0.35f + 2.2f * heart.Surge;
        }

        // Each beat's surge carries the ship forward along whatever passage it is in.
        private void FixedUpdate()
        {
            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.InMission)
            {
                return;
            }

            if (ship == null)
            {
                ShipController controller = FindObjectOfType<ShipController>();
                ship = controller != null ? controller.GetComponent<Rigidbody>() : null;
                if (ship == null)
                {
                    return;
                }
            }

            if (PassageNetwork.Locate(ship.position, out Passage passage, out float s, out _, out _))
            {
                float push = surgePush * heart.Surge;
                // The ventricle's squeeze is the strongest.
                if (passage == HeartLayout.Ventricle || passage == HeartLayout.Pulmonary)
                {
                    push *= 1.4f;
                }
                ship.velocity += passage.TangentAt(s) * (push * Time.fixedDeltaTime);
            }

            // An irregular rhythm strains the heart until the scar tissue is ablated.
            if (heart.Arrhythmic)
            {
                GameManager.Instance?.AddStrain(0.0012f * Time.fixedDeltaTime);
            }
        }

        /// <summary>True when the ship is in a chamber that is squeezing hard right now.</summary>
        public bool PressureSurgeAtShip()
        {
            if (ship == null || !PassageNetwork.Locate(ship.position, out Passage passage, out _, out _, out _))
            {
                return false;
            }
            return heart.Surge > 0.7f && (passage == HeartLayout.Ventricle || passage == HeartLayout.Atrium);
        }

        private void PlaceProblems()
        {
            Transform holder = new GameObject("Problems").transform;
            holder.SetParent(transform, false);

            ArteryLesion leaflet = Place(holder, LesionType.LeakyValve, HeartLayout.Tricuspid, HeartLayout.TricuspidValveS - 0.6f, 0.9f);
            tricuspid.SetTear(leaflet);
            Place(holder, LesionType.Clot, HeartLayout.Atrium, 16f, 2.2f);
            Place(holder, LesionType.Clot, HeartLayout.Ventricle, 31f, 4.1f);
            Place(holder, LesionType.ScarTissue, HeartLayout.Ventricle, 17f, 1.1f);
            Place(holder, LesionType.ScarTissue, HeartLayout.Ventricle, 39f, 3.4f);
        }

        private static ArteryLesion Place(Transform parent, LesionType type, Passage passage, float s, float angle)
        {
            GameObject go = new GameObject(type + " in " + passage.Name);
            go.transform.SetParent(parent, false);
            ArteryLesion lesion = go.AddComponent<ArteryLesion>();
            lesion.Configure(type, passage, s, angle);
            return lesion;
        }

        private void BuildWalls()
        {
            Material vein = ArteryEnvironment.CreateLiningMaterial(textureSize, new Vector2(2f, 2f), 0.8f, new Color(0.72f, 0.5f, 0.62f), new Color(0.06f, 0.01f, 0.03f));
            Material chamber = ArteryEnvironment.CreateLiningMaterial(textureSize, new Vector2(3f, 3f), 0.85f, new Color(0.92f, 0.58f, 0.6f), new Color(0.1f, 0.01f, 0.03f));
            Material artery = ArteryEnvironment.CreateLiningMaterial(textureSize, new Vector2(2f, 2f), 0.78f, Color.white, new Color(0.1f, 0.01f, 0.025f));

            foreach (Passage passage in PassageNetwork.All)
            {
                GameObject host = new GameObject("Passage: " + passage.Name);
                host.transform.SetParent(transform, false);

                Mesh wall = PassageMeshes.Wall(passage, ringsPerMeter, sides, false, out Vector3[] centers);
                host.AddComponent<MeshFilter>().sharedMesh = wall;
                MeshRenderer r = host.AddComponent<MeshRenderer>();
                r.sharedMaterial = passage.Profile == PassageProfile.Vein ? vein
                    : passage.Profile == PassageProfile.HeartChamber ? chamber
                    : artery;
                r.shadowCastingMode = ShadowCastingMode.Off;

                MeshCollider wallCollider = host.AddComponent<MeshCollider>();
                wallCollider.sharedMesh = PassageMeshes.Wall(passage, ringsPerMeter * 0.5f, sides / 2, true);

                if (passage.Squeeze != null)
                {
                    host.AddComponent<PassageWallAnimator>().Bind(passage, wall, centers);
                }
            }
        }
    }
}
