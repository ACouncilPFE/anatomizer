using UnityEngine;

namespace AIGame.AI
{
    [RequireComponent(typeof(EnemyAgent))]
    public class EnemyDebugOverlay : MonoBehaviour
    {
        [SerializeField] private bool showOverlay = true;
        [SerializeField] private bool showGizmos = true;
        [SerializeField] private Vector3 labelOffset = new Vector3(0f, 1.5f, 0f);
        [SerializeField] private float maxLabelDistance = 55f;
        [SerializeField] private bool showDebugState;

        private GUIStyle labelStyle;

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

        // Above each bad cell: what it is, what to shoot it with, and a progress bar
        // (medicine needed for infected cells, health left for malignant ones).
        private void OnGUI()
        {
            // Follow whichever camera is main right now (chase camera or the cockpit pilot view).
            mainCamera = Camera.main;
            if (!showOverlay || agent == null || mainCamera == null || !agent.IsHostile)
            {
                return;
            }

            Vector3 worldPoint = transform.position + labelOffset;
            Vector3 screenPoint = mainCamera.WorldToScreenPoint(worldPoint);
            if (screenPoint.z <= 0f || screenPoint.z > maxLabelDistance)
            {
                return;
            }

            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            }

            bool curable = agent.IsCurable;
            string title = curable ? "INFECTED - medicine (right-click)" : "MALIGNANT - destroy (left-click)";
            if (!curable && agent.JustResistedMedicine)
            {
                title = "RESISTANT - medicine has no effect!";
            }

            Color color = curable ? new Color(0.65f, 1f, 0.3f) : new Color(1f, 0.35f, 0.55f);
            float fill = curable ? agent.RepairProgress01 : agent.Health01;

            float x = screenPoint.x;
            float y = Screen.height - screenPoint.y;
            Color previous = GUI.color;

            GUI.color = color;
            GUI.Label(new Rect(x - 150f, y - 26f, 300f, 20f), title, labelStyle);

            const float barWidth = 90f;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(x - barWidth * 0.5f - 1f, y - 5f, barWidth + 2f, 8f), Texture2D.whiteTexture);
            GUI.color = color;
            GUI.DrawTexture(new Rect(x - barWidth * 0.5f, y - 4f, barWidth * fill, 6f), Texture2D.whiteTexture);

            if (showDebugState)
            {
                GUI.color = Color.white;
                string targetLine = perception != null && perception.TrackedTarget != null ? "Locked" : "None";
                GUI.Label(new Rect(x - 120f, y + 4f, 240f, 20f), agent.CurrentState + " / target " + targetLine, labelStyle);
            }

            GUI.color = previous;
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
