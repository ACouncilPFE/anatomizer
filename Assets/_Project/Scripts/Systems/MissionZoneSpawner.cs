using AIGame.AI;
using AIGame.Core;
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

            if (activeBinding == null || activeBinding.profile == null || activeBinding.profile.enemyPrefab == null || activeBinding.spawnPoints == null || activeBinding.spawnPoints.Length == 0)
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
            Transform spawnPoint = activeBinding.spawnPoints[UnityEngine.Random.Range(0, activeBinding.spawnPoints.Length)];
            EnemyAgent enemy = Instantiate(activeBinding.profile.enemyPrefab, spawnPoint.position, spawnPoint.rotation);

            EnemyArchetype archetype = RollArchetype(activeBinding.profile.archetypeWeights);
            enemy.ConfigureArchetype(archetype);

            aliveEnemies.Add(enemy);
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
