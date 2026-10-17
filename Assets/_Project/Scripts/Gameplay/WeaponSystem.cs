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

        /// <summary>
        /// The round the main gun (and the co-pilot) fires. If the scene never had one assigned,
        /// a plain round is built at runtime so the guns still work.
        /// </summary>
        public Projectile ProjectilePrefab
        {
            get
            {
                if (projectilePrefab == null)
                {
                    projectilePrefab = Projectile.CreateRuntimeTemplate();
                }
                return projectilePrefab;
            }
        }

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
            if (firePoint == null)
            {
                return;
            }

            Projectile shot = Instantiate(ProjectilePrefab, firePoint.position, firePoint.rotation);
            shot.Configure(effect);
            shot.gameObject.SetActive(true); // the runtime template is kept inactive
            nextFireTime = Time.time + 1f / fireRate;
        }
    }
}
