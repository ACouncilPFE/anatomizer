using UnityEngine;

namespace AIGame.Gameplay
{
    public class ShipSystems : MonoBehaviour
    {
        [Header("Base Power Allocation")]
        [SerializeField, Range(0f, 1f)] private float propulsionPower = 0.5f;
        [SerializeField, Range(0f, 1f)] private float shieldsPower = 0.5f;
        [SerializeField, Range(0f, 1f)] private float sensorsPower = 0.5f;
        [SerializeField, Range(0f, 1f)] private float surgicalArrayPower = 0.5f;

        [Header("Temporary Disruption")]
        [SerializeField, Range(0f, 1f)] private float disruptedSensorsPower;
        [SerializeField] private float sensorsDisruptionTimer;

        public float PropulsionPower => propulsionPower;
        public float ShieldsPower => shieldsPower;
        public float SensorsPower => disruptedSensorsPower;
        public float SurgicalArrayPower => surgicalArrayPower;

        private void Awake()
        {
            disruptedSensorsPower = sensorsPower;
        }

        private void Update()
        {
            if (sensorsDisruptionTimer <= 0f)
            {
                disruptedSensorsPower = sensorsPower;
                return;
            }

            sensorsDisruptionTimer -= Time.deltaTime;
            if (sensorsDisruptionTimer <= 0f)
            {
                disruptedSensorsPower = sensorsPower;
            }
        }

        public void SetPowerAllocation(float propulsion, float shields, float sensors, float surgicalArray)
        {
            propulsionPower = Mathf.Clamp01(propulsion);
            shieldsPower = Mathf.Clamp01(shields);
            sensorsPower = Mathf.Clamp01(sensors);
            surgicalArrayPower = Mathf.Clamp01(surgicalArray);

            if (sensorsDisruptionTimer <= 0f)
            {
                disruptedSensorsPower = sensorsPower;
            }
        }

        public void ApplySensorDisruption(float reduction, float duration)
        {
            if (duration <= 0f || reduction <= 0f)
            {
                return;
            }

            sensorsDisruptionTimer = Mathf.Max(sensorsDisruptionTimer, duration);
            disruptedSensorsPower = Mathf.Clamp01(sensorsPower - reduction);
        }
    }
}
