using AIGame.Core;
using UnityEngine;

namespace AIGame.Gameplay
{
    public class WeaponSystem : MonoBehaviour
    {
        [SerializeField] private Projectile projectilePrefab;
        [SerializeField] private Transform firePoint;
        [SerializeField] private float fireRate = 8f;

        private float nextFireTime;

        private void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.InMission)
            {
                return;
            }

            if (Input.GetMouseButton(0) && Time.time >= nextFireTime)
            {
                Fire();
            }
        }

        private void Fire()
        {
            if (projectilePrefab == null || firePoint == null)
            {
                return;
            }

            Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);
            nextFireTime = Time.time + 1f / fireRate;
        }
    }
}
