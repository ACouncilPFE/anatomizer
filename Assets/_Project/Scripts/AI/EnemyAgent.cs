using AIGame.Core;
using AIGame.Gameplay;
using UnityEngine;

namespace AIGame.AI
{
    public enum EnemyAgentState
    {
        Patrol,
        Investigate,
        Pursue,
        Attack,
        Retreat,
        Dead
    }

    [RequireComponent(typeof(EnemyPerception))]
    public class EnemyAgent : MonoBehaviour, IDamageable
    {
        [Header("Identity")]
        [SerializeField] private EnemyArchetype archetype = EnemyArchetype.MacrophageSwarm;

        [Header("Combat")]
        [SerializeField] private float maxHealth = 60f;
        [SerializeField] private float contactDamage = 2f;
        [SerializeField] private float attackCooldownSeconds = 2.5f;
        [SerializeField, Range(0.1f, 1f)] private float attackFacingDotThreshold = 0.65f;
        [SerializeField, Range(0.05f, 0.95f)] private float retreatHealthThreshold = 0.2f;

        [Header("Movement")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float rotationSpeed = 220f;
        [SerializeField] private float patrolRadius = 14f;

        private EnemyPerception perception;
        private Transform shipTarget;
        private ShipSystems shipSystems;
        private IDamageable shipDamageable;

        private EnemyAgentState currentState;
        private Vector3 patrolAnchor;
        private Vector3 patrolTarget;
        private float currentHealth;
        private float nextAttackTime;

        public EnemyAgentState CurrentState => currentState;
        public EnemyArchetype Archetype => archetype;

        private void Awake()
        {
            perception = GetComponent<EnemyPerception>();
            currentHealth = maxHealth;
            patrolAnchor = transform.position;
            patrolTarget = patrolAnchor;
            currentState = EnemyAgentState.Patrol;
        }

        private void Start()
        {
            SetArchetypeColor();
            AcquireShipReferences();
            SelectNewPatrolTarget();
            ApplyArchetypeTuning();
        }

        private void SetArchetypeColor()
        {
            Color color = archetype switch
            {
                EnemyArchetype.MacrophageSwarm => new Color(0.98f, 0.98f, 1f),
                EnemyArchetype.AntibodyNet => new Color(0.9f, 0.95f, 1f),
                EnemyArchetype.BioelectricDisruptor => new Color(1f, 0.96f, 0.98f),
                _ => Color.white
            };

            Renderer renderer = GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                Material mat = renderer.material;
                mat.color = color;
                mat.SetFloat("_Glossiness", 0.55f);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color * 0.25f);
            }
        }

        private void Update()
        {
            TickAgent();
        }

        public void TickAgent()
        {
            if (currentState == EnemyAgentState.Dead)
            {
                return;
            }

            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.InMission)
            {
                return;
            }

            if (shipTarget == null)
            {
                AcquireShipReferences();
            }

            float detectability = CalculateShipDetectabilityMultiplier();
            perception.Tick(shipTarget, detectability);

            ResolveState();
            TickState();
        }

        public void ApplyDamage(float amount)
        {
            if (amount <= 0f || currentState == EnemyAgentState.Dead)
            {
                return;
            }

            currentHealth = Mathf.Max(0f, currentHealth - amount);
            if (currentHealth <= 0f)
            {
                currentState = EnemyAgentState.Dead;
                Destroy(gameObject);
            }
        }

        public void ConfigureArchetype(EnemyArchetype newArchetype)
        {
            archetype = newArchetype;
        }

        public float GetDetectabilityMultiplierForDebug()
        {
            return CalculateShipDetectabilityMultiplier();
        }

        private void ResolveState()
        {
            float healthRatio = maxHealth <= 0f ? 0f : currentHealth / maxHealth;
            bool hasTrackedTarget = perception.TrackedTarget != null;

            if (healthRatio <= retreatHealthThreshold && hasTrackedTarget)
            {
                currentState = EnemyAgentState.Retreat;
                return;
            }

            if (hasTrackedTarget)
            {
                currentState = perception.IsWithinAttackRange(perception.TrackedTarget.position)
                    ? EnemyAgentState.Attack
                    : EnemyAgentState.Pursue;
                return;
            }

            currentState = perception.HasRecentContact ? EnemyAgentState.Investigate : EnemyAgentState.Patrol;
        }

        private void TickState()
        {
            switch (currentState)
            {
                case EnemyAgentState.Patrol:
                    MoveTowards(ref patrolTarget, true);
                    break;
                case EnemyAgentState.Investigate:
                    MoveTowards(perception.LastKnownPosition);
                    break;
                case EnemyAgentState.Pursue:
                    if (perception.TrackedTarget != null)
                    {
                        MoveTowards(perception.TrackedTarget.position);
                    }
                    break;
                case EnemyAgentState.Attack:
                    if (perception.TrackedTarget != null)
                    {
                        FaceTowards(perception.TrackedTarget.position);
                        TryAttackShip();
                    }
                    break;
                case EnemyAgentState.Retreat:
                    if (perception.TrackedTarget != null)
                    {
                        Vector3 away = transform.position + (transform.position - perception.TrackedTarget.position).normalized * 8f;
                        MoveTowards(away);
                    }
                    break;
            }
        }

        private void TryAttackShip()
        {
            if (shipDamageable == null || Time.time < nextAttackTime)
            {
                return;
            }

            if (shipTarget == null)
            {
                return;
            }

            Vector3 toShip = (shipTarget.position - transform.position).normalized;
            float facing = Vector3.Dot(transform.forward, toShip);
            if (facing < attackFacingDotThreshold)
            {
                return;
            }

            shipDamageable.ApplyDamage(contactDamage);

            if (archetype == EnemyArchetype.AntibodyNet && shipSystems != null)
            {
                shipSystems.ApplySensorDisruption(0.3f, 2.5f);
            }

            if (archetype == EnemyArchetype.BioelectricDisruptor && shipSystems != null)
            {
                shipSystems.ApplySensorDisruption(0.45f, 1.2f);
            }

            nextAttackTime = Time.time + attackCooldownSeconds;
        }

        private void MoveTowards(Vector3 destination)
        {
            Vector3 delta = destination - transform.position;
            if (delta.sqrMagnitude < 0.04f)
            {
                return;
            }

            Vector3 direction = delta.normalized;
            transform.position += direction * moveSpeed * Time.deltaTime;
            FaceTowards(destination);
        }

        private void MoveTowards(ref Vector3 destination, bool repickWhenReached)
        {
            MoveTowards(destination);
            if (!repickWhenReached)
            {
                return;
            }

            if (Vector3.Distance(transform.position, destination) <= 0.7f)
            {
                SelectNewPatrolTarget();
            }
        }

        private void FaceTowards(Vector3 worldPoint)
        {
            Vector3 forward = worldPoint - transform.position;
            if (forward.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Quaternion desiredRotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, desiredRotation, rotationSpeed * Time.deltaTime);
        }

        private void SelectNewPatrolTarget()
        {
            Vector3 randomOffset = Random.insideUnitSphere * patrolRadius;
            patrolTarget = patrolAnchor + randomOffset;
        }

        private void AcquireShipReferences()
        {
            ShipHealth health = FindObjectOfType<ShipHealth>();
            if (health == null)
            {
                return;
            }

            shipTarget = health.transform;
            shipDamageable = health;
            shipSystems = health.GetComponent<ShipSystems>();
            if (shipSystems == null)
            {
                shipSystems = health.GetComponentInParent<ShipSystems>();
            }
        }

        private float CalculateShipDetectabilityMultiplier()
        {
            if (shipSystems == null)
            {
                return 1f;
            }

            return Mathf.Lerp(0.3f, 1f, shipSystems.SensorsPower);
        }

        private void ApplyArchetypeTuning()
        {
            switch (archetype)
            {
                case EnemyArchetype.MacrophageSwarm:
                    moveSpeed *= 1.2f;
                    attackCooldownSeconds *= 0.75f;
                    break;
                case EnemyArchetype.AntibodyNet:
                    moveSpeed *= 0.8f;
                    contactDamage *= 1.15f;
                    break;
                case EnemyArchetype.BioelectricDisruptor:
                    attackCooldownSeconds *= 1.1f;
                    contactDamage *= 0.9f;
                    break;
            }
        }
    }
}
