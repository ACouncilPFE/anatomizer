using AIGame.Core;
using UnityEngine;

namespace AIGame.Gameplay
{
    /// <summary>
    /// The ship's suture beam: hold F (or Y on the left VR controller) to stitch an artery tear
    /// shut. The beam locks onto the nearest open tear roughly ahead of the nose, so you only need
    /// to point the ship at it, not hit it exactly.
    /// </summary>
    public class SutureBeam : MonoBehaviour
    {
        [SerializeField] private float range = 22f;
        [SerializeField, Range(0f, 90f)] private float lockAngle = 40f;
        [SerializeField] private float suturePerSecond = 20f; // a tear needs 60, so ~3 s of beam

        private Transform firePoint;
        private LineRenderer line;
        private ArteryLesion target;

        public bool IsActive { get; private set; }
        public ArteryLesion Target => target;

        private void Awake()
        {
            WeaponSystem weapon = GetComponent<WeaponSystem>();
            firePoint = weapon != null && weapon.FirePoint != null ? weapon.FirePoint : transform;

            line = new GameObject("Suture Beam").AddComponent<LineRenderer>();
            line.transform.SetParent(transform, false);
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.startWidth = 0.08f;
            line.endWidth = 0.03f;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sharedMaterial = new Material(Shader.Find("Sprites/Default")) { name = "SutureBeam" };
            line.enabled = false;
        }

        private void Update()
        {
            bool held = ShipInput.Read().Suture
                && (GameManager.Instance == null || GameManager.Instance.CurrentState == GameState.InMission);
            IsActive = held;
            target = held ? FindTear() : null;

            if (!held)
            {
                line.enabled = false;
                return;
            }

            Vector3 start = firePoint.position;
            Vector3 end = target != null ? target.transform.position : start + transform.forward * 6f;
            line.enabled = true;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
            float flicker = 0.75f + 0.25f * Mathf.Sin(Time.time * 40f);
            Color color = target != null ? new Color(0.5f, 0.9f, 1f, flicker) : new Color(0.5f, 0.9f, 1f, 0.25f);
            line.startColor = color;
            line.endColor = color;

            if (target != null)
            {
                target.Suture(suturePerSecond * Time.deltaTime);
            }
        }

        private ArteryLesion FindTear()
        {
            ArteryLesion best = null;
            float bestDistance = range;
            float minDot = Mathf.Cos(lockAngle * Mathf.Deg2Rad);
            foreach (ArteryLesion lesion in ArteryLesion.Active)
            {
                if (lesion.Type != LesionType.Tear)
                {
                    continue;
                }

                Vector3 to = lesion.transform.position - firePoint.position;
                float distance = to.magnitude;
                if (distance > bestDistance || distance < 0.01f || Vector3.Dot(transform.forward, to / distance) < minDot)
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
