using AIGame.Core;
using UnityEngine;

namespace AIGame.Gameplay
{
    public class ShipHealth : MonoBehaviour, IDamageable
    {
        [SerializeField] private float maxHealth = 200f;
        [SerializeField] private float currentHealth = 100f;

        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;

        private void Start()
        {
            currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
            GameEvents.RaiseShipHealthChanged(currentHealth, maxHealth);
        }

        public void ApplyDamage(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            currentHealth = Mathf.Max(0f, currentHealth - amount);
            GameEvents.RaiseShipHealthChanged(currentHealth, maxHealth);

            if (currentHealth <= 0f)
            {
                GameManager.Instance?.FailMission();
            }
        }

        public void Repair(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            GameEvents.RaiseShipHealthChanged(currentHealth, maxHealth);
        }
    }
}
