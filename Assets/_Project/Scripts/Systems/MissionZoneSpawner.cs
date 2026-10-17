using AIGame.AI;
using AIGame.Core;
using AIGame.Gameplay;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIGame.Systems
{
    public enum MissionZone
    {
        Artery,
        Cardiac,
        Neural
    }

    [Serializable]
    public struct ZoneArchetypeWeight
    {
        public EnemyArchetype archetype;
        [Min(0f)] public float weight;
    }

    [Serializable]
    public class ZoneSpawnBinding
    {
        public MissionZoneSpawnProfile profile;
        public Transform[] spawnPoints;
    }

    public class MissionZoneSpawner : MonoBehaviour
    {
        [SerializeField] private MissionZone currentZone = MissionZone.Artery;
        [SerializeField] private List<ZoneSpawnBinding> zoneBindings = new List<ZoneSpawnBinding>();

        [Header("Artery spawning")]
        [Tooltip("Spawn cells inside the artery ahead of the ship instead of at the fixed spawn points.")]
        [SerializeField] private bool spawnAlongArtery = true;
        [SerializeField] private Vector2 spawnDistanceAhead = new Vector2(25f, 70f);
        [SerializeField, Range(0f, 1f)] private float maxSpawnRadialFraction = 0.55f;

        private Transform ship;

        private readonly List<EnemyAgent> aliveEnemies = new List<EnemyAgent>();
        private ZoneSpawnBinding activeBinding;
        private float nextSpawnTime;

        public MissionZone CurrentZone => currentZone;
        public int AliveEnemyCount => aliveEnemies.Count;
        public int MaxAliveInZone => activeBinding != null && activeBinding.profile != null ? activeBinding.profile.maxAlive : 0;
        public float TimeUntilNextSpawn => Mathf.Max(0f, nextSpawnTime - Time.time);

        private void Start()
        {
            ResolveActiveProfile();
            nextSpawnTime = Time.time;
        }

        private void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.InMission)
            {
                return;
            }

            if (activeBinding == null || activeBinding.profile == null || activeBinding.profile.enemyPrefab == null)
            {
                return;
            }

            bool hasSpawnPoints = activeBinding.spawnPoints != null && activeBinding.spawnPoints.Length > 0;
            if (!spawnAlongArtery && !hasSpawnPoints)
            {
                return;
            }

            CleanupDestroyedEnemies();

            if (aliveEnemies.Count >= activeBinding.profile.maxAlive || Time.time < nextSpawnTime)
            {
                return;
            }

            SpawnEnemy();
            nextSpawnTime = Time.time + activeBinding.profile.spawnIntervalSeconds;
        }

        public void SetCurrentZone(MissionZone zone)
        {
            currentZone = zone;
            ResolveActiveProfile();
            nextSpawnTime = Time.time;
        }

        public bool TryGetZoneBinding(MissionZone zone, out ZoneSpawnBinding binding)
        {
            binding = zoneBindings.Find(item => item != null && item.profile != null && item.profile.zone == zone);
            return binding != null;
        }

        private void ResolveActiveProfile()
        {
            activeBinding = zoneBindings.Find(item => item != null && item.profile != null && item.profile.zone == currentZone);
        }

        private void SpawnEnemy()
        {
            Vector3 position;
            Quaternion rotation;
            if (spawnAlongArtery)
            {
                position = RandomPointAheadInArtery();
                rotation = Quaternion.LookRotation(Vector3.back); // facing the oncoming ship
            }
            else
            {
                Transform spawnPoint = activeBinding.spawnPoints[UnityEngine.Random.Range(0, activeBinding.spawnPoints.Length)];
                position = spawnPoint.position;
                rotation = spawnPoint.rotation;
            }

            EnemyAgent enemy = Instantiate(activeBinding.profile.enemyPrefab, position, rotation);

            EnemyArchetype archetype = RollArchetype(activeBinding.profile.archetypeWeights);
            enemy.ConfigureArchetype(archetype);
            enemy.ConfigureCondition(UnityEngine.Random.value < activeBinding.profile.curableChance
                ? CellCondition.Infected
                : CellCondition.Malignant);

            aliveEnemies.Add(enemy);
        }

        private Vector3 RandomPointAheadInArtery()
        {
            if (ship == null)
            {
                ShipController controller = FindObjectOfType<ShipController>();
                ship = controller != null ? controller.transform : null;
            }

            float shipZ = ship != null ? ship.position.z : 0f;
            float z = Mathf.Clamp(shipZ + UnityEngine.Random.Range(spawnDistanceAhead.x, spawnDistanceAhead.y),
                ArteryPath.MinTravelZ + 2f, ArteryPath.MaxTravelZ - 2f);

            Vector3 center = ArteryPath.CenterAt(z);
            Vector2 radii = ArteryPath.RadiiAt(z);
            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            float radial = Mathf.Sqrt(UnityEngine.Random.value) * maxSpawnRadialFraction;
            return center + new Vector3(Mathf.Cos(angle) * radii.x * radial, Mathf.Sin(angle) * radii.y * radial, 0f);
        }

        private EnemyArchetype RollArchetype(List<ZoneArchetypeWeight> weights)
        {
            if (weights == null || weights.Count == 0)
            {
                return EnemyArchetype.MacrophageSwarm;
            }

            float total = 0f;
            for (int i = 0; i < weights.Count; i++)
            {
                total += Mathf.Max(0f, weights[i].weight);
            }

            if (total <= 0f)
            {
                return weights[0].archetype;
            }

            float roll = UnityEngine.Random.Range(0f, total);
            float cumulative = 0f;

            for (int i = 0; i < weights.Count; i++)
            {
                cumulative += Mathf.Max(0f, weights[i].weight);
                if (roll <= cumulative)
                {
                    return weights[i].archetype;
                }
            }

            return weights[weights.Count - 1].archetype;
        }

        private void CleanupDestroyedEnemies()
        {
            aliveEnemies.RemoveAll(enemy => enemy == null);
        }
    }
}
