using AIGame.AI;
using System.Collections.Generic;
using UnityEngine;

namespace AIGame.Systems
{
    [CreateAssetMenu(fileName = "ZoneSpawnProfile", menuName = "AIGame/Spawning/Zone Spawn Profile")]
    public class MissionZoneSpawnProfile : ScriptableObject
    {
        public MissionZone zone;
        [Min(1)] public int maxAlive = 6;
        [Min(0.1f)] public float spawnIntervalSeconds = 5f;
        public EnemyAgent enemyPrefab;
        public List<ZoneArchetypeWeight> archetypeWeights = new List<ZoneArchetypeWeight>();
    }
}
