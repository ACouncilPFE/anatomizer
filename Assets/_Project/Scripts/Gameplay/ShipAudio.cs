using AIGame.AI;
using AIGame.Core;
using UnityEngine;

namespace AIGame.Gameplay
{
    /// <summary>
    /// The ship's own sounds:
    ///   - blood rushing past the hull, louder and higher the faster you go;
    ///   - the patient's heartbeat underneath everything;
    ///   - a squishy rub whenever a blood cell slides along the capsule (BloodFlow reports these)
    ///     or a bad cell bumps into it, and a rubbery scrape while you grind along the wall;
    ///   - the surgical beam's hum, pitched per tool, while it is held.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class ShipAudio : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float flowVolume = 0.45f;
        [SerializeField, Range(0f, 1f)] private float heartbeatVolume = 0.35f;
        [SerializeField, Range(0f, 1f)] private float rubVolume = 0.6f;
        [SerializeField, Range(0f, 1f)] private float scrapeVolume = 0.5f;
        [SerializeField, Range(0f, 1f)] private float beamVolume = 0.35f;
        [SerializeField] private float speedForFullFlow = 45f;

        private Rigidbody rb;
        private ShipPilot pilot;
        private SurgicalBeam beam;
        private AudioSource flow;
        private AudioSource heart;
        private AudioSource scrape;
        private AudioSource hum;
        private float scrapeLevel;
        private float contactLevel;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            flow = MakeLoop("Flow", ProceduralAudio.Flow);
            heart = MakeLoop("Heartbeat", ProceduralAudio.Heartbeat);
            scrape = MakeLoop("Scrape", ProceduralAudio.Scrape);
            hum = MakeLoop("Beam Hum", ProceduralAudio.BeamHum);
        }

        private void OnEnable()
        {
            BloodFlow.CellBrushedShip += OnCellBrushed;
        }

        private void OnDisable()
        {
            BloodFlow.CellBrushedShip -= OnCellBrushed;
        }

        private void Update()
        {
            if (pilot == null) pilot = GetComponent<ShipPilot>();
            if (beam == null) beam = GetComponent<SurgicalBeam>();

            bool playing = GameManager.Instance == null || GameManager.Instance.CurrentState == GameState.InMission;
            float dt = Time.unscaledDeltaTime;

            // Flow: a low rush at rest that opens up with speed.
            float speed01 = Mathf.Clamp01(rb.velocity.magnitude / speedForFullFlow);
            flow.volume = Smooth(flow.volume, (0.25f + 0.75f * speed01) * flowVolume * Master, dt, 4f);
            flow.pitch = Mathf.Lerp(0.75f, 1.35f, speed01);

            heart.volume = heartbeatVolume * Master * (playing ? 1f : 0.4f);

            // Wall scrape while the pilot is holding us off the wall, or while a bad cell is in contact.
            bool onWall = pilot != null && pilot.IsHoldingWall && speed01 > 0.05f;
            scrapeLevel = Smooth(scrapeLevel, onWall ? Mathf.Lerp(0.5f, 1f, speed01) : 0f, dt, 8f);
            contactLevel = Smooth(contactLevel, 0f, dt, 2f);
            scrape.volume = Mathf.Max(scrapeLevel, contactLevel * 0.6f) * scrapeVolume * Master;
            scrape.pitch = Mathf.Lerp(0.8f, 1.2f, speed01);

            // Beam hum, pitched per tool; louder once it has locked on.
            bool beaming = beam != null && beam.IsActive && playing;
            float humTarget = beaming ? (beam.Target != null ? 1f : 0.45f) : 0f;
            hum.volume = Smooth(hum.volume, humTarget * beamVolume * Master, dt, 12f);
            if (beam != null)
            {
                hum.pitch = beam.CurrentTool switch
                {
                    SurgicalTool.Suture => 1.2f,
                    SurgicalTool.Stent => 0.8f,
                    SurgicalTool.Balloon => 0.6f,
                    _ => 1.5f
                };
            }
        }

        // A red blood cell slid along the hull.
        private void OnCellBrushed(Vector3 position, float strength)
        {
            AudioClip[] rubs = ProceduralAudio.Rubs;
            GameAudio.PlayAt(rubs[Random.Range(0, rubs.Length)], position,
                rubVolume * Mathf.Lerp(0.35f, 1f, strength), Random.Range(0.85f, 1.2f), 0.08f);
        }

        // Bad cells and lesions have real colliders, so bumps arrive as physics contacts.
        private void OnCollisionEnter(Collision collision)
        {
            float impact = Mathf.Clamp01(collision.relativeVelocity.magnitude / 20f);
            bool cell = collision.collider.GetComponentInParent<EnemyAgent>() != null;
            AudioClip[] rubs = ProceduralAudio.Rubs;
            GameAudio.PlayAt(rubs[Random.Range(0, rubs.Length)], collision.GetContact(0).point,
                rubVolume * Mathf.Lerp(0.5f, 1f, impact) * (cell ? 1.2f : 1f), cell ? 0.75f : 0.9f, 0.1f);
            contactLevel = Mathf.Max(contactLevel, 0.5f + impact * 0.5f);
        }

        private void OnCollisionStay(Collision collision)
        {
            // Pressed up against something: keep the grinding going.
            contactLevel = Mathf.Max(contactLevel, 0.6f);
        }

        private static float Master => GameAudio.MasterVolume;

        private static float Smooth(float current, float target, float dt, float rate)
        {
            return Mathf.Lerp(current, target, 1f - Mathf.Exp(-rate * dt));
        }

        private AudioSource MakeLoop(string name, AudioClip clip)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            AudioSource source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0f;
            source.dopplerLevel = 0f;
            source.Play();
            return source;
        }
    }
}
