using UnityEngine;

namespace AIGame.AI
{
    [RequireComponent(typeof(EnemyAgent))]
    public class EnemyDebugOverlay : MonoBehaviour
    {
        [SerializeField] private bool showOverlay = true;
        [SerializeField] private bool showGizmos = true;
        [SerializeField] private Vector3 labelOffset = new Vector3(0f, 1.5f, 0f);

        private EnemyAgent agent;
        private EnemyPerception perception;
        private Camera mainCamera;

        private void Awake()
        {
            agent = GetComponent<EnemyAgent>();
            perception = GetComponent<EnemyPerception>();
            mainCamera = Camera.main;
        }

        private void LateUpdate()
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }
        }

        private void OnGUI()
        {
            if (!showOverlay || agent == null || mainCamera == null)
            {
                return;
            }

            Vector3 worldPoint = transform.position + labelOffset;
            Vector3 screenPoint = mainCamera.WorldToScreenPoint(worldPoint);
            if (screenPoint.z <= 0f)
            {
                return;
            }

            float y = Screen.height - screenPoint.y;
            Rect rect = new Rect(screenPoint.x - 120f, y - 20f, 240f, 44f);
            string stateLine = "State: " + agent.CurrentState;
            string targetLine = perception != null && perception.TrackedTarget != null ? "Target: Locked" : "Target: None";
            GUI.Label(rect, stateLine + "\n" + targetLine);
        }

        private void OnDrawGizmosSelected()
        {
            if (!showGizmos)
            {
                return;
            }

            EnemyPerception localPerception = perception != null ? perception : GetComponent<EnemyPerception>();
            if (localPerception == null)
            {
                return;
            }

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, localPerception.DetectionRadius);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, localPerception.AttackRange);
        }
    }
}
