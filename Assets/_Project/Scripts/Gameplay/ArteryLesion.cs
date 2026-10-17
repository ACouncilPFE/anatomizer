using System.Collections.Generic;
using AIGame.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace AIGame.Gameplay
{
    public enum LesionType
    {
        /// <summary>A rip in the wall that bleeds until it is sutured shut (suture beam, hold F).</summary>
        Tear,
        /// <summary>A fatty build-up narrowing the vessel; break it up with the laser (left-click).</summary>
        Plaque,
        /// <summary>A blood clot stuck to the wall; dissolve it with clot-buster medicine (right-click).</summary>
        Clot
    }

    /// <summary>
    /// Damage to the artery wall itself: the kind of problem that normally means open surgery.
    /// Each type only responds to the right tool; the wrong one flashes a hint and does nothing.
    /// Open tears bleed, adding to the patient's blood loss (GameManager.AddBloodLoss).
    ///
    /// Sits on the wall facing the centre of the vessel; ArteryLesionField places them.
    /// </summary>
    public class ArteryLesion : MonoBehaviour, IDamageable, IRepairable, ISuturable
    {
        private static readonly List<ArteryLesion> active = new List<ArteryLesion>();
        /// <summary>Every lesion still needing treatment.</summary>
        public static IReadOnlyList<ArteryLesion> Active => active;

        [SerializeField] private LesionType type = LesionType.Tear;
        [SerializeField] private float treatmentRequired = 60f; // laser damage, medicine, or suture-seconds x rate
        [SerializeField] private float bleedPerSecond = 0.0015f; // share of the patient's blood, per open tear
        [SerializeField] private float labelDistance = 60f;

        private float progress;
        private bool treated;
        private float treatedTime;
        private float lastWrongToolTime = -999f;

        private Transform body;       // plaque / clot mass, or the tear's dark gash
        private Transform rim;        // tear only: raw red edges
        private Transform stitches;   // tear only: appear as it is sutured
        private ParticleSystem bleed; // tear only
        private Light glow;
        private Vector3 bodyScale;
        private GUIStyle labelStyle;
        private Camera cam;

        public LesionType Type => type;
        public bool IsTreated => treated;
        public float Progress01 => Mathf.Clamp01(progress / Mathf.Max(treatmentRequired, 0.01f));
        public bool JustRejectedTool => Time.time - lastWrongToolTime < 0.8f;

        public string Title => type switch
        {
            LesionType.Tear => "ARTERY TEAR",
            LesionType.Plaque => "PLAQUE",
            _ => "BLOOD CLOT"
        };

        public string ToolHint => type switch
        {
            LesionType.Tear => "hold F to suture",
            LesionType.Plaque => "laser it (left-click)",
            _ => "clot-buster medicine (right-click)"
        };

        /// <summary>Place the lesion on the wall at depth z and angle (radians) around the vessel.</summary>
        public void Configure(LesionType newType, float z, float angle)
        {
            type = newType;
            Vector3 center = ArteryPath.CenterAt(z);
            Vector2 radii = ArteryPath.RadiiAt(z);
            Vector3 onWall = center + new Vector3(Mathf.Cos(angle) * radii.x, Mathf.Sin(angle) * radii.y, 0f) * 0.93f;
            transform.position = onWall;
            // Face the middle of the vessel, with "up" running along the artery.
            transform.rotation = Quaternion.LookRotation((center - onWall).normalized, ArteryPath.TangentAt(z));
            BuildVisual();
        }

        private void OnEnable()
        {
            active.Add(this);
        }

        private void OnDisable()
        {
            active.Remove(this);
        }

        // ─── Tools ────────────────────────────────────────────────────────────────

        public void ApplyDamage(float amount) => Treat(LesionType.Plaque, amount);
        public void Repair(float amount)      => Treat(LesionType.Clot, amount);
        public void Suture(float amount)      => Treat(LesionType.Tear, amount);

        private void Treat(LesionType toolFixes, float amount)
        {
            if (treated || amount <= 0f)
            {
                return;
            }

            if (toolFixes != type)
            {
                lastWrongToolTime = Time.time;
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

            // Nothing left to hit.
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

            GameEvents.RaiseLesionTreated();
        }

        private void Update()
        {
            if (type == LesionType.Tear && !treated)
            {
                if (GameManager.Instance != null)
                {
                    // Partly sutured tears bleed less.
                    GameManager.Instance.AddBloodLoss(bleedPerSecond * (1f - Progress01) * Time.deltaTime);
                }
                if (glow != null)
                {
                    glow.intensity = 1.2f + 0.5f * Mathf.Sin(Time.time * 5f);
                }
            }

            // Cleared plaque and clots melt away; the healed tear leaves a scar.
            if (treated && type != LesionType.Tear && body != null)
            {
                float t = Mathf.Clamp01((Time.time - treatedTime) / 1.2f);
                body.localScale = Vector3.Lerp(bodyScale * 0.3f, Vector3.zero, t);
                if (t >= 1f)
                {
                    body.gameObject.SetActive(false);
                }
            }
        }

        // ─── Look ─────────────────────────────────────────────────────────────────

        private void BuildVisual()
        {
            SphereCollider hitbox = gameObject.AddComponent<SphereCollider>();
            hitbox.center = new Vector3(0f, 0f, 0.5f);

            switch (type)
            {
                case LesionType.Tear:
                    hitbox.radius = 1.4f;
                    BuildTear();
                    break;
                case LesionType.Plaque:
                    hitbox.radius = 1.3f;
                    body = Mass("Plaque", LesionMeshes.Plaque, LesionMeshes.PlaqueMaterial, new Vector3(2.4f, 3.2f, 1.1f));
                    break;
                default:
                    hitbox.radius = 1.1f;
                    body = Mass("Clot", LesionMeshes.Clot, LesionMeshes.ClotMaterial, new Vector3(1.8f, 2.2f, 1.5f));
                    break;
            }
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
            float length = Random.Range(3.4f, 4.4f);
            rim = Part("TearRim", LesionMeshes.Gash(length * 1.1f, 1.5f, 9), LesionMeshes.TearRimMaterial, new Vector3(0f, 0f, 0.03f));
            body = Part("TearGash", LesionMeshes.Gash(length * 0.9f, 0.5f, 9), LesionMeshes.TearGashMaterial, new Vector3(0f, 0f, 0.06f));
            bodyScale = body.localScale;

            // Stitches across the seam, revealed one by one as the beam works.
            stitches = new GameObject("Stitches").transform;
            stitches.SetParent(transform, false);
            int count = 7;
            for (int i = 0; i < count; i++)
            {
                float y = Mathf.Lerp(-length * 0.4f, length * 0.4f, i / (float)(count - 1));
                Transform s = Part("Stitch", LesionMeshes.Quad, LesionMeshes.StitchMaterial, new Vector3(0f, y, 0.09f));
                s.SetParent(stitches, true);
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
            if (type == LesionType.Tear)
            {
                // The gash narrows as it is pulled shut and stitches appear along it.
                body.localScale = new Vector3(bodyScale.x * (1f - t), bodyScale.y, bodyScale.z);
                rim.localScale = new Vector3(Mathf.Lerp(1f, 0.25f, t), 1f, 1f);
                int shown = Mathf.FloorToInt(t * stitches.childCount + 0.001f);
                for (int i = 0; i < stitches.childCount; i++)
                {
                    stitches.GetChild(i).gameObject.SetActive(i < shown);
                }
                if (bleed != null)
                {
                    ParticleSystem.EmissionModule emission = bleed.emission;
                    emission.rateOverTime = 40f * (1f - t);
                }
                if (t >= 1f)
                {
                    rim.GetComponent<Renderer>().sharedMaterial = LesionMeshes.ScarMaterial;
                }
            }
            else if (body != null)
            {
                // Plaque breaks down / the clot dissolves.
                body.localScale = bodyScale * Mathf.Lerp(1f, 0.3f, t);
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

            if (cam == null)
            {
                cam = Camera.main;
                if (cam == null)
                {
                    return;
                }
            }

            Vector3 screen = cam.WorldToScreenPoint(transform.position + transform.forward * 1.5f);
            if (screen.z <= 0f || screen.z > labelDistance)
            {
                return;
            }

            labelStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

            string text;
            Color color;
            if (treated)
            {
                text = type == LesionType.Tear ? "TEAR SUTURED" : type == LesionType.Plaque ? "PLAQUE CLEARED" : "CLOT DISSOLVED";
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
                color = type == LesionType.Tear ? new Color(1f, 0.4f, 0.35f)
                      : type == LesionType.Plaque ? new Color(1f, 0.9f, 0.5f)
                      : new Color(1f, 0.55f, 0.6f);
            }

            float x = screen.x;
            float y = Screen.height - screen.y;
            Color previous = GUI.color;
            GUI.color = color;
            GUI.Label(new Rect(x - 160f, y - 26f, 320f, 20f), text, labelStyle);

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
