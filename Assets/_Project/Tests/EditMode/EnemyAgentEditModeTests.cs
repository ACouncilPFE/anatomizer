using AIGame.AI;
using AIGame.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace AIGame.Tests.EditMode
{
    public class EnemyAgentEditModeTests
    {
        [Test]
        public void EnemyAgent_EntersAttackState_WhenTargetInRange()
        {
            GameObject ship = new GameObject("Ship");
            ship.transform.position = Vector3.zero;
            ShipHealth shipHealth = ship.AddComponent<ShipHealth>();
            ShipSystems shipSystems = ship.AddComponent<ShipSystems>();
            shipSystems.SetPowerAllocation(0.5f, 0.5f, 1f, 0.5f);

            GameObject enemyObject = new GameObject("Enemy");
            enemyObject.transform.position = new Vector3(0f, 0f, 2f);
            EnemyPerception perception = enemyObject.AddComponent<EnemyPerception>();
            EnemyAgent enemy = enemyObject.AddComponent<EnemyAgent>();

            perception.ForceTrackTarget(ship.transform);
            enemy.TickAgent();

            Assert.AreEqual(EnemyAgentState.Attack, enemy.CurrentState);

            Object.DestroyImmediate(enemyObject);
            Object.DestroyImmediate(ship);
            _ = shipHealth;
        }

        [Test]
        public void EnemyAgent_EntersRetreatState_WhenHealthLowAndTargetTracked()
        {
            GameObject ship = new GameObject("Ship");
            ship.transform.position = Vector3.zero;
            ship.AddComponent<ShipHealth>();
            ship.AddComponent<ShipSystems>();

            GameObject enemyObject = new GameObject("Enemy");
            enemyObject.transform.position = new Vector3(0f, 0f, 3f);
            EnemyPerception perception = enemyObject.AddComponent<EnemyPerception>();
            EnemyAgent enemy = enemyObject.AddComponent<EnemyAgent>();

            perception.ForceTrackTarget(ship.transform);
            enemy.ApplyDamage(49f);
            enemy.TickAgent();

            Assert.AreEqual(EnemyAgentState.Retreat, enemy.CurrentState);

            Object.DestroyImmediate(enemyObject);
            Object.DestroyImmediate(ship);
        }

        [Test]
        public void EnemyAgent_DetectabilityMultiplier_DropsWithSensorDisruption()
        {
            GameObject ship = new GameObject("Ship");
            ship.transform.position = Vector3.zero;
            ship.AddComponent<ShipHealth>();
            ShipSystems shipSystems = ship.AddComponent<ShipSystems>();
            shipSystems.SetPowerAllocation(0.5f, 0.5f, 1f, 0.5f);

            GameObject enemyObject = new GameObject("Enemy");
            enemyObject.transform.position = new Vector3(0f, 0f, 8f);
            enemyObject.AddComponent<EnemyPerception>();
            EnemyAgent enemy = enemyObject.AddComponent<EnemyAgent>();

            enemy.TickAgent();
            float fullSensors = enemy.GetDetectabilityMultiplierForDebug();

            shipSystems.ApplySensorDisruption(0.8f, 3f);
            enemy.TickAgent();
            float disruptedSensors = enemy.GetDetectabilityMultiplierForDebug();

            Assert.Greater(fullSensors, disruptedSensors);
            Assert.AreEqual(1f, fullSensors, 0.01f);

            Object.DestroyImmediate(enemyObject);
            Object.DestroyImmediate(ship);
        }
    }
}
