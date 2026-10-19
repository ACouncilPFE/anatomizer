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

        private bool shieldBoost;
        private bool toggleHeld;

        /// <summary>H: route power from propulsion to shields (less acid damage, lower speed).</summary>
        public bool ShieldBoost => shieldBoost;
        /// <summary>How much damage from the environment gets through the shields (1 = all of it).</summary>
        public float ShieldLeak => Mathf.Lerp(1f, 0.15f, shieldsPower);

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
            bool toggle = ShipInput.Read().ToggleShieldBoost;
            if (toggle && !toggleHeld)
            {
                SetShieldBoost(!shieldBoost);
            }
            toggleHeld = toggle;

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

        public void SetShieldBoost(bool on)
        {
            shieldBoost = on;
            if (on)
            {
                SetPowerAllocation(0.25f, 1f, sensorsPower, surgicalArrayPower);
            }
            else
            {
                SetPowerAllocation(0.5f, 0.5f, sensorsPower, surgicalArrayPower);
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
