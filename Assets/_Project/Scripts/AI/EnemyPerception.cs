using UnityEngine;

namespace AIGame.AI
{
    public class EnemyPerception : MonoBehaviour
    {
        [Header("Ranges")]
        [SerializeField] private float detectionRadius = 24f;
        [SerializeField] private float attackRange = 6f;

        [Header("Detection")]
        [SerializeField, Range(0f, 2f)] private float baseDetectionPerSecond = 0.55f;
        [SerializeField] private float memorySeconds = 2.2f;
        [SerializeField] private LayerMask lineOfSightMask = ~0;

        private Transform trackedTarget;
        private Vector3 lastKnownPosition;
        private float lastSeenTime = -999f;

        public Transform TrackedTarget => trackedTarget;
        public Vector3 LastKnownPosition => lastKnownPosition;
        public bool HasRecentContact => trackedTarget != null || Time.time - lastSeenTime <= memorySeconds;
        public float DetectionRadius => detectionRadius;
        public float AttackRange => attackRange;

        public void Tick(Transform candidateTarget, float detectabilityMultiplier)
        {
            if (candidateTarget == null)
            {
                trackedTarget = null;
                return;
            }

            if (CanSeeTarget(candidateTarget))
            {
                float detectionChance = Mathf.Clamp01(baseDetectionPerSecond * Mathf.Max(0f, detectabilityMultiplier) * Time.deltaTime);
                if (Random.value <= detectionChance)
                {
                    trackedTarget = candidateTarget;
                    lastKnownPosition = candidateTarget.position;
                    lastSeenTime = Time.time;
                }
            }
            else if (trackedTarget == candidateTarget)
            {
                trackedTarget = null;
            }

            if (trackedTarget == null && Time.time - lastSeenTime > memorySeconds)
            {
                lastKnownPosition = transform.position;
            }
        }

        public bool IsWithinAttackRange(Vector3 worldPosition)
        {
            return Vector3.Distance(transform.position, worldPosition) <= attackRange;
        }

        public void ForceTrackTarget(Transform target)
        {
            trackedTarget = target;
            if (target == null)
            {
                return;
            }

            lastKnownPosition = target.position;
            lastSeenTime = Time.time;
        }

        private bool CanSeeTarget(Transform target)
        {
            Vector3 toTarget = target.position - transform.position;
            float distance = toTarget.magnitude;
            if (distance > detectionRadius)
            {
                return false;
            }

            if (distance <= Mathf.Epsilon)
            {
                return true;
            }

            Vector3 direction = toTarget / distance;
            if (Physics.Raycast(transform.position, direction, out RaycastHit hit, distance, lineOfSightMask, QueryTriggerInteraction.Ignore))
            {
                return hit.transform == target || hit.transform.IsChildOf(target);
            }

            return true;
        }
    }
}
