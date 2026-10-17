using AIGame.Core;
using UnityEngine;

namespace AIGame.Gameplay
{
    /// <summary>
    /// The ship's surgical beam: one emitter with four tools for damage to the artery itself.
    ///   1 Suture   close a tear
    ///   2 Stent    reinforce an aneurysm
    ///   3 Balloon  open a blockage (fly into the narrowing first)
    ///   4 Grabber  pull out a foreign object
    /// Pick a tool with keys 1-4 (or click the left VR thumbstick to cycle), then hold F (or Y on
    /// the left VR controller). The beam locks onto the nearest problem roughly ahead of the nose,
    /// so you only need to point the ship at it. Using the wrong tool does nothing but flash a hint.
    /// </summary>
    public class SurgicalBeam : MonoBehaviour
    {
        private static readonly SurgicalTool[] Tools = { SurgicalTool.Suture, SurgicalTool.Stent, SurgicalTool.Balloon, SurgicalTool.Grabber };

        [SerializeField] private float range = 22f;
        [SerializeField, Range(0f, 90f)] private float lockAngle = 40f;
        [SerializeField] private float ratePerSecond = 20f; // most problems need 60, so ~3 s of beam

        private Transform firePoint;
        private LineRenderer line;
        private ArteryLesion target;
        private int toolIndex;
        private bool cycleHeld;

        public bool IsActive { get; private set; }
        public ArteryLesion Target => target;
        public SurgicalTool CurrentTool => Tools[toolIndex];
        public int CurrentToolNumber => toolIndex + 1;

        private void Awake()
        {
            WeaponSystem weapon = GetComponent<WeaponSystem>();
            firePoint = weapon != null && weapon.FirePoint != null ? weapon.FirePoint : transform;

            line = new GameObject("Surgical Beam").AddComponent<LineRenderer>();
            line.transform.SetParent(transform, false);
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.startWidth = 0.08f;
            line.endWidth = 0.03f;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sharedMaterial = new Material(Shader.Find("Sprites/Default")) { name = "SurgicalBeam" };
            line.enabled = false;
        }

        public void SelectTool(SurgicalTool tool)
        {
            int index = System.Array.IndexOf(Tools, tool);
            if (index >= 0)
            {
                toolIndex = index;
            }
        }

        private void Update()
        {
            ShipInput.State input = ShipInput.Read();

            if (input.BeamToolSelect >= 1 && input.BeamToolSelect <= Tools.Length)
            {
                toolIndex = input.BeamToolSelect - 1;
            }
            if (input.CycleBeamTool && !cycleHeld)
            {
                toolIndex = (toolIndex + 1) % Tools.Length;
            }
            cycleHeld = input.CycleBeamTool;

            bool held = input.Beam && (GameManager.Instance == null || GameManager.Instance.CurrentState == GameState.InMission);
            IsActive = held;
            target = held ? FindTarget() : null;

            if (!held)
            {
                line.enabled = false;
                return;
            }

            Vector3 start = firePoint.position;
            Vector3 end = target != null ? target.AimPoint : start + transform.forward * 6f;
            line.enabled = true;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
            float flicker = 0.75f + 0.25f * Mathf.Sin(Time.time * 40f);
            Color color = ToolColor(CurrentTool);
            color.a = target != null ? flicker : 0.25f;
            line.startColor = color;
            line.endColor = color;

            if (target != null)
            {
                target.Treat(CurrentTool, ratePerSecond * Time.deltaTime);
            }
        }

        public static Color ToolColor(SurgicalTool tool) => tool switch
        {
            SurgicalTool.Suture => new Color(0.5f, 0.9f, 1f),
            SurgicalTool.Stent => new Color(0.85f, 0.85f, 0.95f),
            SurgicalTool.Balloon => new Color(0.55f, 0.7f, 1f),
            _ => new Color(1f, 0.85f, 0.4f)
        };

        public static string ToolName(SurgicalTool tool) => tool switch
        {
            SurgicalTool.Suture => "Suture",
            SurgicalTool.Stent => "Stent",
            SurgicalTool.Balloon => "Balloon",
            SurgicalTool.Grabber => "Grabber",
            SurgicalTool.Laser => "Laser",
            _ => "Medicine"
        };

        private ArteryLesion FindTarget()
        {
            ArteryLesion best = null;
            float bestDistance = range;
            float minDot = Mathf.Cos(lockAngle * Mathf.Deg2Rad);
            foreach (ArteryLesion lesion in ArteryLesion.Active)
            {
                // The guns handle plaque and clots; the beam only locks onto beam problems.
                SurgicalTool needed = lesion.RequiredTool;
                if (needed == SurgicalTool.Laser || needed == SurgicalTool.Medicine)
                {
                    continue;
                }

                Vector3 to = lesion.AimPoint - firePoint.position;
                float distance = to.magnitude;
                // A blockage surrounds the ship once you're inside it, so skip the angle check up close.
                bool inside = lesion.Type == LesionType.Blockage && distance < 4f;
                if (distance > bestDistance || (!inside && (distance < 0.01f || Vector3.Dot(transform.forward, to / distance) < minDot)))
                {
                    continue;
                }

                best = lesion;
                bestDistance = distance;
            }
            return best;
        }
    }
}
