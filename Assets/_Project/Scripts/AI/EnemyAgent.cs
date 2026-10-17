using AIGame.Core;
using AIGame.Gameplay;
using System.Collections.Generic;
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
        Dead,
        Repaired
    }

    [RequireComponent(typeof(EnemyPerception))]
    public class EnemyAgent : MonoBehaviour, IDamageable, IRepairable
    {
        // Every live agent, so the co-pilot can pick targets without searching the scene.
        private static readonly List<EnemyAgent> active = new List<EnemyAgent>();
        public static IReadOnlyList<EnemyAgent> Active => active;

        [Header("Identity")]
        [SerializeField] private EnemyArchetype archetype = EnemyArchetype.MacrophageSwarm;
        [Tooltip("Infected cells can be cured with medicine; malignant ones must be destroyed.")]
        [SerializeField] private CellCondition condition = CellCondition.Infected;

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

        [Header("Repair")]
        [SerializeField] private float repairRequired = 45f;        // medicine needed to cure an infected cell
        [SerializeField] private float repairedLingerSeconds = 3f;  // cured cell stays visible, then leaves

        [Header("Artery")]
        [SerializeField, Range(0.3f, 1f)] private float maxWallFraction = 0.8f; // cells stay inside the vessel
        [SerializeField] private float wallClearance = 1f;

        private EnemyPerception perception;
        private Transform shipTarget;
        private ShipSystems shipSystems;
        private IDamageable shipDamageable;

        private EnemyAgentState currentState;
        private Vector3 patrolAnchor;
        private Vector3 patrolTarget;
        private float currentHealth;
        private float nextAttackTime;
        private float repairProgress;
        private float lastResistedTime = -999f;
        private DiseasedCellVisual visual;

        public EnemyAgentState CurrentState => currentState;
        public EnemyArchetype Archetype => archetype;
        /// <summary>False once the cell is destroyed or repaired; nothing should shoot it then.</summary>
        public bool IsHostile => currentState != EnemyAgentState.Dead && currentState != EnemyAgentState.Repaired;
        public float RepairProgress01 => repairRequired <= 0f ? 1f : Mathf.Clamp01(repairProgress / repairRequired);
        public float Health01 => maxHealth <= 0f ? 0f : Mathf.Clamp01(currentHealth / maxHealth);
        public CellCondition Condition => condition;
        /// <summary>Infected cells can be cured; destroying one wastes a cell that could have been saved.</summary>
        public bool IsCurable => condition == CellCondition.Infected;
        /// <summary>True for a moment after medicine hit a malignant cell and did nothing.</summary>
        public bool JustResistedMedicine => Time.time - lastResistedTime < 0.6f;

        private void OnEnable()
        {
            active.Add(this);
        }

        private void OnDisable()
        {
            active.Remove(this);
        }

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
            visual = GetComponent<DiseasedCellVisual>();
            if (visual == null)
            {
                visual = gameObject.AddComponent<DiseasedCellVisual>();
            }
            visual.Apply(condition);

            AcquireShipReferences();
            SelectNewPatrolTarget();
            ApplyArchetypeTuning();
        }

        private void Update()
        {
            TickAgent();
        }

        public void TickAgent()
        {
            if (!IsHostile)
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
            if (amount <= 0f || !IsHostile)
            {
                return;
            }

            currentHealth = Mathf.Max(0f, currentHealth - amount);
            if (currentHealth <= 0f)
            {
                currentState = EnemyAgentState.Dead;
                GameEvents.RaiseCellDestroyed(IsCurable);
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Medicine rounds cure an infected cell instead of killing it. Once cured it stops
        /// attacking, turns back into a healthy red cell, and leaves after a few seconds.
        /// Malignant cells shrug medicine off.
        /// </summary>
        public void Repair(float amount)
        {
            if (amount <= 0f || !IsHostile)
            {
                return;
            }

            if (!IsCurable)
            {
                lastResistedTime = Time.time;
                return;
            }

            repairProgress += amount;
            if (repairProgress < repairRequired)
            {
                return;
            }

            currentState = EnemyAgentState.Repaired;
            if (visual != null)
            {
                visual.ShowHealed();
            }

            GameEvents.RaiseCellRepaired();
            Destroy(gameObject, repairedLingerSeconds);
        }

        public void ConfigureArchetype(EnemyArchetype newArchetype)
        {
            archetype = newArchetype;
        }

        /// <summary>Call right after spawning (before Start) to choose infected or malignant.</summary>
        public void ConfigureCondition(CellCondition newCondition)
        {
            condition = newCondition;
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
            Vector3 next = transform.position + direction * moveSpeed * Time.deltaTime;
            KeepInsideArtery(ref next);
            transform.position = next;
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
            KeepInsideArtery(ref patrolTarget);
        }

        private void KeepInsideArtery(ref Vector3 point)
        {
            ArteryPath.ClampInside(ref point, maxWallFraction, wallClearance, ArteryPath.MinTravelZ, ArteryPath.MaxTravelZ, out _);
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
