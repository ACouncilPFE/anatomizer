using AIGame.AI;
using AIGame.Core;
using UnityEngine;

namespace AIGame.Gameplay
{
    /// <summary>
    /// The co-pilot crew member: picks out bad cells ahead of the ship and shoots them, so you
    /// can concentrate on flying.
    ///
    ///   Destroy mode  fires damage rounds that kill the cell.
    ///   Repair mode   fires repair rounds that heal the cell back into a harmless one.
    /// Toggle with R (or A on the right VR controller).
    ///
    /// The co-pilot only engages cells in front of the ship, within range and with a clear line of
    /// sight (the artery wall blocks shots). It fires from the ship's WeaponSystem fire point using
    /// the same projectile prefab, so it needs no extra scene setup. Your own trigger still fires
    /// the main gun.
    /// </summary>
    public class ShipCoPilot : MonoBehaviour
    {
        public enum Mode
        {
            Destroy,
            Repair
        }

        [SerializeField] private Mode mode = Mode.Destroy;

        [Header("Targeting")]
        [SerializeField] private float range = 40f;
        [SerializeField, Range(-1f, 1f)] private float minAimDot = 0.3f; // ~70° cone ahead of the nose
        [SerializeField] private LayerMask lineOfSightMask = ~0;

        [Header("Firing")]
        [SerializeField] private float fireRate     = 4f;
        [SerializeField] private float damagePerShot = 10f;
        [SerializeField] private float repairPerShot = 15f;
        [SerializeField] private Projectile projectilePrefab; // falls back to the WeaponSystem's
        [SerializeField] private Transform firePoint;         // falls back to the WeaponSystem's

        private float nextFireTime;
        private bool toggleHeld;
        private EnemyAgent target;
        private int destroyedCount;
        private int repairedCount;

        public Mode CurrentMode => mode;
        public EnemyAgent CurrentTarget => target;
        public int DestroyedCount => destroyedCount;
        public int RepairedCount => repairedCount;

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

        private void OnEnable()
        {
            GameEvents.OnCellDestroyed += HandleCellDestroyed;
            GameEvents.OnCellRepaired  += HandleCellRepaired;
        }

        private void OnDisable()
        {
            GameEvents.OnCellDestroyed -= HandleCellDestroyed;
            GameEvents.OnCellRepaired  -= HandleCellRepaired;
        }

        private void HandleCellDestroyed() => destroyedCount++;
        private void HandleCellRepaired()  => repairedCount++;

        public void SetMode(Mode newMode)
        {
            mode = newMode;
        }

        private void Update()
        {
            bool toggle = ShipInput.Read().ToggleCoPilot;
            if (toggle && !toggleHeld)
            {
                SetMode(mode == Mode.Destroy ? Mode.Repair : Mode.Destroy);
            }
            toggleHeld = toggle;

            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.InMission)
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
            if (mode == Mode.Repair)
            {
                shot.Configure(ProjectileEffect.Repair, repairPerShot);
            }
            else
            {
                shot.Configure(ProjectileEffect.Damage, damagePerShot);
            }

            nextFireTime = Time.time + 1f / Mathf.Max(fireRate, 0.01f);
        }
    }
}
