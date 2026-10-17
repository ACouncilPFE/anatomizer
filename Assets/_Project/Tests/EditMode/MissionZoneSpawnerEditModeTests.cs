using AIGame.Systems;
using NUnit.Framework;
using UnityEngine;

namespace AIGame.Tests.EditMode
{
    public class MissionZoneSpawnerEditModeTests
    {
        [Test]
        public void MissionZoneSpawner_DefaultStats_AreSafeWhenNoBindings()
        {
            GameObject spawnerObject = new GameObject("Spawner");
            MissionZoneSpawner spawner = spawnerObject.AddComponent<MissionZoneSpawner>();

            Assert.AreEqual(MissionZone.Artery, spawner.CurrentZone);
            Assert.AreEqual(0, spawner.AliveEnemyCount);
            Assert.AreEqual(0, spawner.MaxAliveInZone);
            Assert.GreaterOrEqual(spawner.TimeUntilNextSpawn, 0f);

            Object.DestroyImmediate(spawnerObject);
        }

        [Test]
        public void MissionZoneTrigger_RequiresCollider()
        {
            GameObject triggerObject = new GameObject("ZoneTrigger");
            MissionZoneTrigger trigger = triggerObject.AddComponent<MissionZoneTrigger>();

            Collider collider = trigger.GetComponent<Collider>();
            Assert.IsNotNull(collider);

            Object.DestroyImmediate(triggerObject);
        }
    }
}
