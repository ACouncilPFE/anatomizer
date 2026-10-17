using AIGame.Systems;
using UnityEngine;

namespace AIGame.UI
{
    public class MissionZoneSpawnerHUD : MonoBehaviour
    {
        [SerializeField] private MissionZoneSpawner spawner;
        [SerializeField] private bool visible = true;
        [SerializeField] private Vector2 anchor = new Vector2(12f, 12f);

        private GUIStyle style;

        private void Awake()
        {
            if (spawner == null)
            {
                spawner = FindObjectOfType<MissionZoneSpawner>();
            }

            style = new GUIStyle
            {
                fontSize = 14,
                normal = new GUIStyleState { textColor = Color.cyan }
            };
        }

        private void OnGUI()
        {
            if (!visible || spawner == null)
            {
                return;
            }

            Rect rect = new Rect(anchor.x, anchor.y, 340f, 90f);
            string text = "Zone: " + spawner.CurrentZone
                + "\nAlive: " + spawner.AliveEnemyCount + " / " + spawner.MaxAliveInZone
                + "\nNext Spawn In: " + spawner.TimeUntilNextSpawn.ToString("0.0") + "s";

            GUI.Label(rect, text, style);
        }
    }
}
