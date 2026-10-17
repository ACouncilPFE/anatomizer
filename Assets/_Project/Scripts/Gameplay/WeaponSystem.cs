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

        public Projectile ProjectilePrefab => projectilePrefab;
        public Transform FirePoint => firePoint;

        private void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.InMission)
            {
                return;
            }

            if (Time.time < nextFireTime)
            {
                return;
            }

            // Left click / right trigger destroys; right click / B fires medicine.
            ShipInput.State input = ShipInput.Read();
            if (input.Fire)
            {
                Fire(ProjectileEffect.Damage);
            }
            else if (input.FireMedicine)
            {
                Fire(ProjectileEffect.Repair);
            }
        }

        private void Fire(ProjectileEffect effect)
        {
            if (projectilePrefab == null || firePoint == null)
            {
                return;
            }

            Projectile shot = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);
            shot.Configure(effect);
            nextFireTime = Time.time + 1f / fireRate;
        }
    }
}
