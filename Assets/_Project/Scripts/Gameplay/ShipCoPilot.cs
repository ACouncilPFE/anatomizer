using AIGame.AI;
using AIGame.Core;
using UnityEngine;

namespace AIGame.Gameplay
{
    /// <summary>
    /// The co-pilot crew member: picks out bad cells ahead of the ship and treats them the right
    /// way, so you can concentrate on flying.
    ///
    ///   Off   (default) the co-pilot holds fire; all shooting is yours.
    ///   Treat medicine rounds for infected cells, destroy rounds for malignant ones.
    /// R (or A on the right VR controller) switches between them.
    ///
    /// The co-pilot only engages cells in front of the ship, within range and with a clear line of
    /// sight (the artery wall blocks shots). It fires from the ship's WeaponSystem fire point using
    /// the same projectile prefab, so it needs no extra scene setup.
    /// </summary>
    public class ShipCoPilot : MonoBehaviour
    {
        public enum Mode
        {
            Off,
            Treat
        }

        [SerializeField] private Mode mode = Mode.Off;

        [Header("Targeting")]
        [SerializeField] private float range = 40f;
        [SerializeField, Range(-1f, 1f)] private float minAimDot = 0.3f; // ~70° cone ahead of the nose
        [SerializeField] private LayerMask lineOfSightMask = ~0;

        [Header("Firing")]
        [SerializeField] private float fireRate = 4f;
        [SerializeField] private Projectile projectilePrefab; // falls back to the WeaponSystem's
        [SerializeField] private Transform firePoint;         // falls back to the WeaponSystem's

        private float nextFireTime;
        private bool toggleHeld;
        private EnemyAgent target;

        public Mode CurrentMode => mode;
        public EnemyAgent CurrentTarget => target;

        private void Awake()
        {
            WeaponSystem weapon = GetComponent<WeaponSystem>();
            if (weapon != null)
            {
                if (projectilePrefab == null) projectilePrefab = weapon.ProjectilePrefab;
                if (firePoint == null)        firePoint        = weapon.FirePoint;
            }
            if (firePoint == null)
            {
                firePoint = transform;
            }
        }

        public void SetMode(Mode newMode)
        {
            mode = newMode;
        }

        private void Update()
        {
            bool toggle = ShipInput.Read().ToggleCoPilot;
            if (toggle && !toggleHeld)
            {
                SetMode(mode == Mode.Off ? Mode.Treat : Mode.Off);
            }
            toggleHeld = toggle;

            if (mode == Mode.Off || (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.InMission))
            {
                target = null;
                return;
            }

            target = IsValidTarget(target) ? target : PickTarget();
            if (target != null && Time.time >= nextFireTime)
            {
                FireAt(target);
            }
        }

        private EnemyAgent PickTarget()
        {
            EnemyAgent best = null;
            float bestDistance = float.MaxValue;

            foreach (EnemyAgent enemy in EnemyAgent.Active)
            {
                if (!IsValidTarget(enemy))
                {
                    continue;
                }

                float distance = Vector3.Distance(firePoint.position, enemy.transform.position);
                if (distance < bestDistance)
                {
                    best = enemy;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private bool IsValidTarget(EnemyAgent enemy)
        {
            if (enemy == null || !enemy.IsHostile)
            {
                return false;
            }

            Vector3 origin = firePoint.position;
            Vector3 toEnemy = enemy.transform.position - origin;
            float distance = toEnemy.magnitude;
            if (distance > range || distance < 0.01f)
            {
                return false;
            }

            Vector3 direction = toEnemy / distance;
            if (Vector3.Dot(transform.forward, direction) < minAimDot)
            {
                return false;
            }

            if (Physics.Raycast(origin, direction, out RaycastHit hit, distance, lineOfSightMask, QueryTriggerInteraction.Ignore))
            {
                return hit.collider.GetComponentInParent<EnemyAgent>() == enemy;
            }

            return true;
        }

        private void FireAt(EnemyAgent enemy)
        {
            if (projectilePrefab == null)
            {
                return;
            }

            Vector3 aim = enemy.transform.position - firePoint.position;
            Projectile shot = Instantiate(projectilePrefab, firePoint.position, Quaternion.LookRotation(aim.normalized));
            shot.Configure(enemy.IsCurable ? ProjectileEffect.Repair : ProjectileEffect.Damage);
            shot.gameObject.SetActive(true); // the runtime template is kept inactive

            nextFireTime = Time.time + 1f / Mathf.Max(fireRate, 0.01f);
        }
    }
}
