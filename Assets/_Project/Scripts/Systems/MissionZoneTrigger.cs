using UnityEngine;

namespace AIGame.Systems
{
    [RequireComponent(typeof(Collider))]
    public class MissionZoneTrigger : MonoBehaviour
    {
        [SerializeField] private MissionZone zone = MissionZone.Artery;
        [SerializeField] private MissionZoneSpawner spawner;
        [SerializeField] private string shipTag = "Player";

        private void Reset()
        {
            Collider col = GetComponent<Collider>();
            col.isTrigger = true;
        }

        private void Awake()
        {
            if (spawner == null)
            {
                spawner = FindObjectOfType<MissionZoneSpawner>();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (spawner == null)
            {
                return;
            }

            bool isShip = string.IsNullOrWhiteSpace(shipTag)
                ? other.GetComponentInParent<AIGame.Gameplay.ShipHealth>() != null
                : other.CompareTag(shipTag) || other.GetComponentInParent<AIGame.Gameplay.ShipHealth>() != null;

            if (!isShip)
            {
                return;
            }

            spawner.SetCurrentZone(zone);
        }
    }
}
