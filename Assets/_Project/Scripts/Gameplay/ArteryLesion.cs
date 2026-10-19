using System.Collections.Generic;
using AIGame.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace AIGame.Gameplay
{
    public enum LesionType
    {
        /// <summary>A rip in the wall that bleeds until sutured shut.</summary>
        Tear,
        /// <summary>A fatty build-up on the wall; broken up by the laser.</summary>
        Plaque,
        /// <summary>A blood clot stuck to the wall; dissolved by clot-buster medicine.</summary>
        Clot,
        /// <summary>A weak, bulging patch of wall that grows until it bursts; reinforce it with a stent.</summary>
        Aneurysm,
        /// <summary>A ring of plaque narrowing the whole vessel; opened with a balloon (angioplasty).</summary>
        Blockage,
        /// <summary>A metal splinter lodged in the wall; pulled out with the grabber.</summary>
        ForeignObject,
        /// <summary>Heart: a torn valve leaflet that lets blood leak backwards; sutured shut.</summary>
        LeakyValve,
        /// <summary>Heart: a scar in the muscle wall that makes the rhythm irregular; lasered away (ablation).</summary>
        ScarTissue,
        /// <summary>Gut: a raw, bleeding sore in the stomach lining; sutured shut.</summary>
        Ulcer,
        /// <summary>Gut: a mushroom-shaped growth on the intestine wall; lasered off.</summary>
        Polyp
    }

    /// <summary>Everything the ship can treat with: two guns and the four surgical-beam tools.</summary>
    public enum SurgicalTool
    {
        Laser,
        Medicine,
        Suture,
        Stent,
        Balloon,
        Grabber
    }

    /// <summary>
    /// Damage to the artery itself: the kind of problem that normally means open surgery.
    /// Each type responds only to the right tool; the wrong one flashes a hint and does nothing.
    ///   Tear           suture beam   - bleeds into the patient until closed
    ///   Plaque         laser
    ///   Clot           medicine
    ///   Aneurysm       stent beam    - grows, and bursts into a tear if left too long
    ///   Blockage       balloon beam  - narrows the vessel; fly into it and inflate
    ///   ForeignObject  grabber beam
    ///   LeakyValve     suture beam   - heart: strains the heart until closed
    ///   ScarTissue     laser         - heart: makes the heartbeat irregular until ablated
    ///   Ulcer          suture beam   - gut: bleeds like a tear
    ///   Polyp          laser         - gut
    ///
    /// ArteryLesionField places them. Wall lesions sit on the wall facing the vessel's centre;
    /// a blockage sits on the centreline, since it rings the whole vessel.
    /// </summary>
    public class ArteryLesion : MonoBehaviour, IDamageable, IRepairable
    {
        private static readonly List<ArteryLesion> active = new List<ArteryLesion>();
        /// <summary>Every lesion still needing treatment.</summary>
        public static IReadOnlyList<ArteryLesion> Active => active;

        [SerializeField] private LesionType type = LesionType.Tear;
        [SerializeField] private float treatmentRequired = 60f; // laser damage, medicine, or beam-seconds x beam rate
        [SerializeField] private float bleedPerSecond = 0.0015f; // share of the patient's blood, per open tear
        [SerializeField] private float aneurysmBurstSeconds = 150f;
        [SerializeField] private float burstBloodLoss = 0.1f;
        [SerializeField] private float labelDistance = 60f;
        [SerializeField] private float leakStrainPerSecond = 0.002f; // heart strain from a torn valve

        private const float BlockageHalfLength = 3f;
        private const float BlockageDepth = 0.6f;

        private float progress;
        private bool treated;
        private float treatedTime;
        private float lastWrongToolTime = -999f;
        private float age;
        private Passage passage;
        private float s;

        private Transform body;       // main mass, or the tear's dark gash
        private Transform rim;        // tear: raw red edges
        private Transform steps;      // tear stitches / stent hoops, revealed as treatment progresses
        private Transform balloon;    // blockage: inflating balloon
        private ParticleSystem bleed;
        private Light glow;
        private Vector3 bodyScale;
        private Vector3 bodyRestPosition;
        private Mesh blockageMesh;
        private MeshCollider blockageCollider;
        private float lastColliderDepth;
        private GUIStyle labelStyle;
        private Camera cam;

        public LesionType Type => type;
        public bool IsTreated => treated;
        public float Progress01 => Mathf.Clamp01(progress / Mathf.Max(treatmentRequired, 0.01f));
        public bool JustRejectedTool => Time.time - lastWrongToolTime < 0.8f;
        /// <summary>Seconds until an untreated aneurysm bursts.</summary>
        public float SecondsUntilBurst => Mathf.Max(0f, aneurysmBurstSeconds - age);

        public SurgicalTool RequiredTool => RequiredToolFor(type);

        public static SurgicalTool RequiredToolFor(LesionType lesion) => lesion switch
        {
            LesionType.Tear => SurgicalTool.Suture,
            LesionType.Plaque => SurgicalTool.Laser,
            LesionType.Clot => SurgicalTool.Medicine,
            LesionType.Aneurysm => SurgicalTool.Stent,
            LesionType.Blockage => SurgicalTool.Balloon,
            LesionType.LeakyValve => SurgicalTool.Suture,
            LesionType.ScarTissue => SurgicalTool.Laser,
            LesionType.Ulcer => SurgicalTool.Suture,
            LesionType.Polyp => SurgicalTool.Laser,
            _ => SurgicalTool.Grabber
        };

        public string Title => type switch
        {
            LesionType.Tear => "ARTERY TEAR",
            LesionType.Plaque => "PLAQUE",
            LesionType.Clot => "BLOOD CLOT",
            LesionType.Aneurysm => "ANEURYSM",
            LesionType.Blockage => "BLOCKAGE",
            LesionType.LeakyValve => "TORN VALVE LEAFLET",
            LesionType.ScarTissue => "SCAR TISSUE (irregular heartbeat)",
            LesionType.Ulcer => "BLEEDING ULCER",
            LesionType.Polyp => "POLYP",
            _ => "FOREIGN OBJECT"
        };

        public string ToolHint => ToolHintFor(RequiredTool);

        public static string ToolHintFor(SurgicalTool tool) => tool switch
        {
            SurgicalTool.Laser => "laser it (left-click)",
            SurgicalTool.Medicine => "clot-buster medicine (right-click)",
            SurgicalTool.Suture => "press 1 for Suture, then hold F",
            SurgicalTool.Stent => "press 2 for Stent, then hold F",
            SurgicalTool.Balloon => "press 3 for Balloon, fly inside it, then hold F",
            _ => "press 4 for Grabber, then hold F"
        };

        /// <summary>Where tools should aim.</summary>
        public Vector3 AimPoint => type == LesionType.Blockage ? transform.position : transform.position + transform.forward * 0.6f;

        /// <summary>The passage this lesion is in, and how far along it.</summary>
        public Passage Passage => passage;
        public float S => s;

        /// <summary>Place the lesion on a passage at s, at angle (radians) around it.</summary>
        public void Configure(LesionType newType, Passage onPassage, float atS, float angle)
        {
            type = newType;
            passage = onPassage;
            s = atS;
            passage.Frame(s, out Vector3 center, out Vector3 tangent, out _, out Vector3 up);

            if (type == LesionType.Blockage)
            {
                transform.position = center;
                transform.rotation = Quaternion.LookRotation(tangent, up);
            }
            else
            {
                Vector3 onWall = passage.PointAt(s, angle, 0.93f);
                transform.position = onWall;
                // Face the middle of the vessel, with "up" running along it.
                transform.rotation = Quaternion.LookRotation((center - onWall).normalized, tangent);
            }

            BuildVisual();
        }

        private void OnEnable()
        {
            if (!treated)
            {
                active.Add(this);
            }
        }

        private void OnDisable()
        {
            active.Remove(this);
        }

        // ─── Tools ────────────────────────────────────────────────────────────────

        public void ApplyDamage(float amount) => Treat(SurgicalTool.Laser, amount);
        public void Repair(float amount)      => Treat(SurgicalTool.Medicine, amount);

        /// <summary>Apply a tool. Anything but the right tool is rejected with a hint.</summary>
        public void Treat(SurgicalTool tool, float amount)
        {
            if (treated || amount <= 0f)
            {
                return;
            }

            if (tool != RequiredTool)
            {
                lastWrongToolTime = Time.time;
                GameAudio.PlayAt(ProceduralAudio.WrongTool, transform.position, 0.6f, 1f, 0.5f);
                return;
            }

            progress += amount;
            ShowProgress(Progress01);
            if (progress >= treatmentRequired)
            {
                Complete();
            }
        }

        private void Complete()
        {
            treated = true;
            treatedTime = Time.time;
            active.Remove(this);

            foreach (Collider c in GetComponentsInChildren<Collider>())
            {
                c.enabled = false;
            }
            if (bleed != null)
            {
                bleed.Stop();
            }
            if (glow != null)
            {
                glow.enabled = false;
            }

            if (type == LesionType.ScarTissue)
            {
                HeartBeat.Instance?.RemoveArrhythmiaSource();
            }

            GameEvents.RaiseLesionTreated();
            GameAudio.PlayAt(ProceduralAudio.LesionFixed, transform.position, 0.85f);
        }

        private void Update()
        {
            bool inMission = GameManager.Instance == null || GameManager.Instance.CurrentState == GameState.InMission;
            float dt = Time.deltaTime;

            if (!treated && inMission)
            {
                age += dt;
                switch (type)
                {
                    case LesionType.Tear:
                    case LesionType.Ulcer:
                        GameManager.Instance?.AddBloodLoss(bleedPerSecond * (1f - Progress01) * dt);
                        if (glow != null)
                        {
                            glow.intensity = 1.2f + 0.5f * Mathf.Sin(Time.time * 5f);
                        }
                        break;

                    case LesionType.Aneurysm:
                        if (age >= aneurysmBurstSeconds)
                        {
                            Burst();
                        }
                        else
                        {
                            // Swells as it weakens and throbs with the heartbeat; a stent stops both.
                            float swell = Mathf.Lerp(0.8f, 1.5f, age / aneurysmBurstSeconds) * Mathf.Lerp(1f, 0.55f, Progress01);
                            float beat = 1f + 0.07f * (1f - Progress01) * Mathf.Pow(Mathf.Abs(Mathf.Sin(Time.time * Mathf.PI * 1.2f)), 6f);
                            body.localScale = bodyScale * swell * beat;
                        }
                        break;

                    case LesionType.LeakyValve:
                        // Blood leaking back through the valve makes the heart work harder.
                        GameManager.Instance?.AddStrain(leakStrainPerSecond * (1f - Progress01) * dt);
                        if (glow != null)
                        {
                            glow.intensity = 0.8f + 0.4f * Mathf.Sin(Time.time * 6f);
                        }
                        break;

                    case LesionType.ScarTissue:
                        // Stray electrical sparks crackle across the scar.
                        if (glow != null)
                        {
                            glow.intensity = Random.value < 0.08f ? Random.Range(1.5f, 3f) : Mathf.MoveTowards(glow.intensity, 0f, dt * 12f);
                        }
                        break;

                    case LesionType.ForeignObject:
                        // Being worked loose: slides out of the wall and rattles while the grabber pulls.
                        if (progress > 0f)
                        {
                            body.localPosition = bodyRestPosition + Vector3.forward * (Progress01 * 1.6f) + Random.insideUnitSphere * 0.03f;
                        }
                        break;
                }
            }

            if (treated)
            {
                float t = Mathf.Clamp01((Time.time - treatedTime) / 1.2f);
                switch (type)
                {
                    case LesionType.Plaque:
                    case LesionType.Clot:
                    case LesionType.ScarTissue:
                    case LesionType.Polyp:
                    case LesionType.ForeignObject:
                        if (body != null && body.gameObject.activeSelf)
                        {
                            body.localScale = Vector3.Lerp(bodyScale * (type == LesionType.ForeignObject ? 1f : 0.3f), Vector3.zero, t);
                            if (t >= 1f)
                            {
                                body.gameObject.SetActive(false);
                            }
                        }
                        break;
                    case LesionType.Blockage:
                        if (balloon != null && balloon.gameObject.activeSelf)
                        {
                            balloon.localScale *= 1f - Mathf.Clamp01(dt * 3f);
                            if (t >= 1f)
                            {
                                balloon.gameObject.SetActive(false);
                            }
                        }
                        break;
                }
            }
        }

        // An aneurysm left too long ruptures into a bleeding tear.
        private void Burst()
        {
            GameManager.Instance?.AddBloodLoss(burstBloodLoss);
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
            body = rim = steps = null;
            type = LesionType.Tear;
            progress = 0f;
            age = 0f;
            BuildTear();
        }

        // ─── Look ─────────────────────────────────────────────────────────────────

        private void BuildVisual()
        {
            switch (type)
            {
                case LesionType.Tear:
                    BuildTear();
                    break;
                case LesionType.Plaque:
                    Hitbox(1.3f);
                    body = Mass("Plaque", LesionMeshes.Plaque, LesionMeshes.PlaqueMaterial, new Vector3(2.4f, 3.2f, 1.1f));
                    break;
                case LesionType.Clot:
                    Hitbox(1.1f);
                    body = Mass("Clot", LesionMeshes.Clot, LesionMeshes.ClotMaterial, new Vector3(1.8f, 2.2f, 1.5f));
                    break;
                case LesionType.Aneurysm:
                    BuildAneurysm();
                    break;
                case LesionType.Blockage:
                    BuildBlockage();
                    break;
                case LesionType.LeakyValve:
                    BuildTear();
                    // A torn leaflet doesn't bleed out of the body: the blood leaks backwards, so no spray.
                    if (bleed != null)
                    {
                        Destroy(bleed.gameObject);
                        bleed = null;
                    }
                    glow.color = new Color(1f, 0.5f, 0.6f);
                    break;
                case LesionType.Ulcer:
                    BuildTear();
                    rim.localScale = new Vector3(1.6f, 0.75f, 1f); // a round crater rather than a slit
                    body.localScale = new Vector3(1.5f, 0.7f, 1f);
                    bodyScale = body.localScale;
                    break;
                case LesionType.Polyp:
                    BuildPolyp();
                    break;
                case LesionType.ScarTissue:
                    Hitbox(1.3f);
                    body = Mass("Scar", LesionMeshes.Plaque, LesionMeshes.ScarTissueMaterial, new Vector3(2.8f, 2.2f, 0.7f));
                    glow = new GameObject("Sparks").AddComponent<Light>();
                    glow.transform.SetParent(transform, false);
                    glow.transform.localPosition = new Vector3(0f, 0f, 0.8f);
                    glow.type = LightType.Point;
                    glow.color = new Color(0.7f, 0.9f, 1f);
                    glow.range = 4f;
                    glow.intensity = 0f;
                    glow.shadows = LightShadows.None;
                    HeartBeat.Instance?.AddArrhythmiaSource();
                    break;
                default:
                    BuildForeignObject();
                    break;
            }
        }

        private void Hitbox(float radius)
        {
            SphereCollider hitbox = GetComponent<SphereCollider>();
            if (hitbox == null)
            {
                hitbox = gameObject.AddComponent<SphereCollider>();
            }
            hitbox.center = new Vector3(0f, 0f, 0.5f);
            hitbox.radius = radius;
            hitbox.enabled = true;
        }

        private Transform Mass(string name, Mesh mesh, Material material, Vector3 scale)
        {
            Transform t = Part(name, mesh, material, Vector3.zero);
            t.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-25f, 25f));
            t.localScale = scale * Random.Range(0.85f, 1.15f);
            bodyScale = t.localScale;
            return t;
        }

        private void BuildTear()
        {
            Hitbox(1.4f);
            float length = Random.Range(3.4f, 4.4f);
            rim = Part("TearRim", LesionMeshes.Gash(length * 1.1f, 1.5f, 9), LesionMeshes.TearRimMaterial, new Vector3(0f, 0f, 0.03f));
            body = Part("TearGash", LesionMeshes.Gash(length * 0.9f, 0.5f, 9), LesionMeshes.TearGashMaterial, new Vector3(0f, 0f, 0.06f));
            bodyScale = body.localScale;

            // Stitches across the seam, revealed one by one as the beam works.
            steps = new GameObject("Stitches").transform;
            steps.SetParent(transform, false);
            const int count = 7;
            for (int i = 0; i < count; i++)
            {
                float y = Mathf.Lerp(-length * 0.4f, length * 0.4f, i / (float)(count - 1));
                Transform s = Part("Stitch", LesionMeshes.Quad, LesionMeshes.StitchMaterial, new Vector3(0f, y, 0.09f));
                s.SetParent(steps, true);
                s.localScale = new Vector3(0.7f, 0.06f, 1f);
                s.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-12f, 12f));
                s.gameObject.SetActive(false);
            }

            bleed = BuildBleed();

            GameObject lightObj = new GameObject("TearGlow");
            lightObj.transform.SetParent(transform, false);
            lightObj.transform.localPosition = new Vector3(0f, 0f, 0.8f);
            glow = lightObj.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.15f, 0.12f);
            glow.range = 5f;
            glow.shadows = LightShadows.None;
        }

        private void BuildAneurysm()
        {
            Hitbox(1.6f);
            body = Mass("Aneurysm", LesionMeshes.Aneurysm, LesionMeshes.AneurysmMaterial, new Vector3(3f, 3.4f, 1.8f));

            // The stent: hoops lining the vessel across the bulge, added as the beam works.
            // Built in world axes, so this holder sits at the origin unrotated.
            steps = new GameObject("Stent").transform;
            steps.SetParent(transform, false);
            steps.position = Vector3.zero;
            steps.rotation = Quaternion.identity;
            foreach (Mesh hoop in LesionMeshes.StentHoops(Vector3.zero, passage, s, 2.2f, 6))
            {
                GameObject h = new GameObject("StentHoop");
                h.transform.SetParent(steps, false);
                h.AddComponent<MeshFilter>().sharedMesh = hoop;
                MeshRenderer r = h.AddComponent<MeshRenderer>();
                r.sharedMaterial = LesionMeshes.StentMaterial;
                r.shadowCastingMode = ShadowCastingMode.Off;
                h.SetActive(false);
            }
        }

        private void BuildBlockage()
        {
            blockageMesh = LesionMeshes.StenosisBand(null, transform.position, passage, s, BlockageHalfLength, BlockageDepth);
            GameObject band = new GameObject("Blockage");
            band.transform.SetParent(transform, false);
            band.transform.rotation = Quaternion.identity; // mesh is in world axes around our position
            band.AddComponent<MeshFilter>().sharedMesh = blockageMesh;
            MeshRenderer r = band.AddComponent<MeshRenderer>();
            r.sharedMaterial = LesionMeshes.PlaqueMaterial;
            r.shadowCastingMode = ShadowCastingMode.Off;
            blockageCollider = band.AddComponent<MeshCollider>();
            blockageCollider.sharedMesh = blockageMesh;
            lastColliderDepth = BlockageDepth;
            body = band.transform;

            balloon = Part("Balloon", LesionMeshes.Sphere, LesionMeshes.BalloonMaterial, Vector3.zero);
            balloon.localScale = Vector3.zero;
        }

        // A polyp: a fleshy cap on a short stalk, growing out of the wall.
        private void BuildPolyp()
        {
            Hitbox(1.2f);
            body = new GameObject("Polyp").transform;
            body.SetParent(transform, false);
            Transform stalk = Part("Stalk", LesionMeshes.Sphere, LesionMeshes.PolypMaterial, new Vector3(0f, 0f, 0.5f));
            stalk.SetParent(body, false);
            stalk.localScale = new Vector3(0.45f, 0.45f, 1.2f);
            Transform cap = Part("Cap", LesionMeshes.Plaque, LesionMeshes.PolypMaterial, new Vector3(0f, 0f, 1.15f));
            cap.SetParent(body, false);
            cap.localScale = new Vector3(1.5f, 1.4f, 1.1f);
            body.localScale = Vector3.one * Random.Range(0.9f, 1.2f);
            bodyScale = body.localScale;
        }

        private void BuildForeignObject()
        {
            Hitbox(1f);
            body = Part("Shard", LesionMeshes.Shard, LesionMeshes.MetalMaterial, new Vector3(0f, 0f, 0.3f));
            // Stuck in at an angle, about half of it sticking out of the wall.
            body.localRotation = Quaternion.Euler(Random.Range(-40f, -25f), Random.Range(-30f, 30f), 0f);
            body.localScale = new Vector3(0.35f, 0.3f, 2.6f);
            bodyScale = body.localScale;
            bodyRestPosition = body.localPosition;
        }

        private ParticleSystem BuildBleed()
        {
            GameObject go = new GameObject("Bleeding");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, 0.1f);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.85f, 0.05f, 0.08f, 1f), new Color(0.6f, 0.02f, 0.05f, 0.9f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 200;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 40f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(0.3f, 2.4f, 0.05f);

            // Blood is carried off downstream.
            ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            // All three axes must use the same curve mode, or Unity logs an error every frame.
            velocity.x = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);
            velocity.y = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);
            velocity.z = new ParticleSystem.MinMaxCurve(2.5f, 4f);

            ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
            color.enabled = true;
            Gradient fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            color.color = fade;

            ParticleSystemRenderer psr = go.GetComponent<ParticleSystemRenderer>();
            psr.sharedMaterial = LesionMeshes.BloodParticleMaterial;
            psr.shadowCastingMode = ShadowCastingMode.Off;

            ps.Play();
            return ps;
        }

        private void ShowProgress(float t)
        {
            switch (type)
            {
                case LesionType.Tear:
                case LesionType.LeakyValve:
                case LesionType.Ulcer:
                    // The gash narrows as it is pulled shut and stitches appear along it.
                    body.localScale = new Vector3(bodyScale.x * (1f - t), bodyScale.y, bodyScale.z);
                    rim.localScale = new Vector3(Mathf.Lerp(1f, 0.25f, t), 1f, 1f);
                    RevealSteps(t);
                    if (bleed != null)
                    {
                        ParticleSystem.EmissionModule emission = bleed.emission;
                        emission.rateOverTime = 40f * (1f - t);
                    }
                    if (t >= 1f)
                    {
                        rim.GetComponent<Renderer>().sharedMaterial = LesionMeshes.ScarMaterial;
                    }
                    break;

                case LesionType.Aneurysm:
                    RevealSteps(t);
                    break;

                case LesionType.Blockage:
                    // The balloon inflates and presses the plaque back into the wall.
                    float depth = BlockageDepth * (1f - t);
                    LesionMeshes.StenosisBand(blockageMesh, transform.position, passage, s, BlockageHalfLength, depth);
                    if (lastColliderDepth - depth > 0.08f)
                    {
                        blockageCollider.sharedMesh = null;
                        blockageCollider.sharedMesh = blockageMesh;
                        lastColliderDepth = depth;
                    }
                    Vector2 radii = passage.RadiiAt(s);
                    float open = Mathf.Lerp(1f - BlockageDepth, 0.95f, t);
                    balloon.localScale = new Vector3(radii.x * 2f * open, radii.y * 2f * open, BlockageHalfLength * 1.6f) * Mathf.Clamp01(t * 4f);
                    break;

                case LesionType.Plaque:
                case LesionType.Clot:
                case LesionType.ScarTissue:
                case LesionType.Polyp:
                    // Plaque breaks down / the clot dissolves / the scar or polyp is burned away.
                    body.localScale = bodyScale * Mathf.Lerp(1f, 0.3f, t);
                    break;
            }
        }

        private void RevealSteps(float t)
        {
            if (steps == null)
            {
                return;
            }

            int shown = Mathf.FloorToInt(t * steps.childCount + 0.001f);
            for (int i = 0; i < steps.childCount; i++)
            {
                steps.GetChild(i).gameObject.SetActive(i < shown);
            }
        }

        private Transform Part(string name, Mesh mesh, Material material, Vector3 localPosition)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPosition;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return go.transform;
        }

        // ─── Label ────────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            if (treated && Time.time - treatedTime > 2.5f)
            {
                return;
            }

            // Follow whichever camera is main right now (chase camera or the cockpit pilot view).
            cam = Camera.main;
            if (cam == null)
            {
                return;
            }

            Vector3 anchor = type == LesionType.Blockage && passage != null
                ? transform.position + transform.up * passage.RadiiAt(s).y * 0.5f
                : transform.position + transform.forward * 1.5f;
            Vector3 screen = cam.WorldToScreenPoint(anchor);
            if (screen.z <= 0f || screen.z > labelDistance)
            {
                return;
            }

            labelStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

            string text;
            Color color;
            if (treated)
            {
                text = type switch
                {
                    LesionType.Tear => "TEAR SUTURED",
                    LesionType.Plaque => "PLAQUE CLEARED",
                    LesionType.Clot => "CLOT DISSOLVED",
                    LesionType.Aneurysm => "ANEURYSM STENTED",
                    LesionType.Blockage => "ARTERY REOPENED",
                    LesionType.LeakyValve => "VALVE REPAIRED",
                    LesionType.ScarTissue => "SCAR ABLATED - rhythm steady",
                    LesionType.Ulcer => "ULCER CLOSED",
                    LesionType.Polyp => "POLYP REMOVED",
                    _ => "OBJECT REMOVED"
                };
                color = new Color(0.5f, 1f, 0.6f);
            }
            else if (JustRejectedTool)
            {
                text = "WRONG TOOL - " + ToolHint;
                color = Color.yellow;
            }
            else
            {
                text = Title + " - " + ToolHint;
                if (type == LesionType.Aneurysm)
                {
                    text += $"  (bursts in {SecondsUntilBurst:F0}s)";
                }
                color = type switch
                {
                    LesionType.Tear => new Color(1f, 0.4f, 0.35f),
                    LesionType.Plaque => new Color(1f, 0.9f, 0.5f),
                    LesionType.Clot => new Color(1f, 0.55f, 0.6f),
                    LesionType.Aneurysm => new Color(1f, 0.5f, 0.85f),
                    LesionType.Blockage => new Color(1f, 0.8f, 0.4f),
                    LesionType.LeakyValve => new Color(1f, 0.6f, 0.75f),
                    LesionType.ScarTissue => new Color(0.75f, 0.9f, 1f),
                    LesionType.Ulcer => new Color(1f, 0.45f, 0.35f),
                    LesionType.Polyp => new Color(1f, 0.7f, 0.75f),
                    _ => new Color(0.8f, 0.85f, 0.95f)
                };
            }

            float x = screen.x;
            float y = Screen.height - screen.y;
            Color previous = GUI.color;
            GUI.color = color;
            GUI.Label(new Rect(x - 200f, y - 26f, 400f, 20f), text, labelStyle);

            if (!treated)
            {
                const float barWidth = 90f;
                GUI.color = new Color(0f, 0f, 0f, 0.6f);
                GUI.DrawTexture(new Rect(x - barWidth * 0.5f - 1f, y - 5f, barWidth + 2f, 8f), Texture2D.whiteTexture);
                GUI.color = color;
                GUI.DrawTexture(new Rect(x - barWidth * 0.5f, y - 4f, barWidth * Progress01, 6f), Texture2D.whiteTexture);
            }

            GUI.color = previous;
        }
    }
}
