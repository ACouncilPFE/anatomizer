using UnityEngine;

namespace AIGame.Core
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Mission")]
        [SerializeField] private float missionDurationSeconds = 1800f;
        [SerializeField] private bool autoStartMission = true;

        [Header("Progress")]
        [SerializeField] private int requiredObjectives = 5;

        [Header("Cell Treatment")]
        [Tooltip("Correct treatments to win: cure an infected cell, or destroy a malignant one.")]
        [SerializeField] private int requiredTreatments = 12;
        [Tooltip("Curable cells you may destroy before the mission fails.")]
        [SerializeField] private int maxHealthyCellsLost = 5;

        private float remainingTime;
        private int completedObjectives;
        private int cellsTreated;
        private int healthyCellsLost;
        private float bloodLoss;

        public GameState CurrentState { get; private set; } = GameState.Briefing;
        public float RemainingTime => remainingTime;
        public int RequiredObjectives => requiredObjectives;
        public int CompletedObjectives => completedObjectives;
        public int CellsTreated => cellsTreated;
        public int RequiredTreatments => requiredTreatments;
        public int HealthyCellsLost => healthyCellsLost;
        public int MaxHealthyCellsLost => maxHealthyCellsLost;
        /// <summary>The patient's blood loss from open tears, 0..1. At 1 the mission fails.</summary>
        public float BloodLoss01 => bloodLoss;

        private void OnEnable()
        {
            GameEvents.OnCellRepaired += HandleCellCured;
            GameEvents.OnCellDestroyed += HandleCellDestroyed;
            GameEvents.OnLesionTreated += RegisterCorrectTreatment;
        }

        private void OnDisable()
        {
            GameEvents.OnCellRepaired -= HandleCellCured;
            GameEvents.OnCellDestroyed -= HandleCellDestroyed;
            GameEvents.OnLesionTreated -= RegisterCorrectTreatment;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            SetState(GameState.Briefing);

            if (autoStartMission)
            {
                StartMission();
            }
        }

        private void Update()
        {
            if (CurrentState != GameState.InMission)
            {
                return;
            }

            remainingTime -= Time.deltaTime;
            GameEvents.RaiseMissionTimerUpdated(remainingTime);

            if (remainingTime <= 0f)
            {
                FailMission();
            }
        }

        public void StartMission()
        {
            remainingTime = missionDurationSeconds;
            completedObjectives = 0;
            cellsTreated = 0;
            healthyCellsLost = 0;
            bloodLoss = 0f;
            GameEvents.RaiseObjectiveProgressChanged(completedObjectives, requiredObjectives);
            SetState(GameState.InMission);
            Time.timeScale = 1f;
        }

        public void SetPaused(bool paused)
        {
            if (CurrentState != GameState.InMission && !paused)
            {
                return;
            }

            if (paused)
            {
                SetState(GameState.Paused);
                Time.timeScale = 0f;
            }
            else
            {
                SetState(GameState.InMission);
                Time.timeScale = 1f;
            }
        }

        public void RegisterObjectiveComplete()
        {
            if (CurrentState != GameState.InMission)
            {
                return;
            }

            completedObjectives++;
            GameEvents.RaiseObjectiveProgressChanged(completedObjectives, requiredObjectives);

            if (completedObjectives >= requiredObjectives)
            {
                CompleteMission();
            }
        }

        // Curing an infected cell is always the right call.
        private void HandleCellCured()
        {
            RegisterCorrectTreatment();
        }

        // Destroying a malignant cell is right; destroying a curable one wastes healthy tissue.
        private void HandleCellDestroyed(bool wasCurable)
        {
            if (CurrentState != GameState.InMission)
            {
                return;
            }

            if (!wasCurable)
            {
                RegisterCorrectTreatment();
                return;
            }

            healthyCellsLost++;
            if (healthyCellsLost >= maxHealthyCellsLost)
            {
                FailMission();
            }
        }

        /// <summary>Open tears call this every frame while they bleed.</summary>
        public void AddBloodLoss(float amount)
        {
            if (CurrentState != GameState.InMission || amount <= 0f)
            {
                return;
            }

            bloodLoss = Mathf.Clamp01(bloodLoss + amount);
            if (bloodLoss >= 1f)
            {
                FailMission();
            }
        }

        private void RegisterCorrectTreatment()
        {
            if (CurrentState != GameState.InMission)
            {
                return;
            }

            cellsTreated++;
            if (cellsTreated >= requiredTreatments)
            {
                CompleteMission();
            }
        }

        public void CompleteMission()
        {
            if (CurrentState == GameState.Success || CurrentState == GameState.Failure)
            {
                return;
            }

            SetState(GameState.Success);
            GameEvents.RaiseMissionEnded(true);
            Time.timeScale = 0f;
        }

        public void FailMission()
        {
            if (CurrentState == GameState.Success || CurrentState == GameState.Failure)
            {
                return;
            }

            SetState(GameState.Failure);
            GameEvents.RaiseMissionEnded(false);
            Time.timeScale = 0f;
        }

        private void SetState(GameState newState)
        {
            CurrentState = newState;
            GameEvents.RaiseGameStateChanged(CurrentState);
        }
    }
}
