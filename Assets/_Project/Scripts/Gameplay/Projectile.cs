using AIGame.Core;
using UnityEngine;

namespace AIGame.Gameplay
{
    public enum ProjectileEffect
    {
        Damage,
        Repair
    }

    public class Projectile : MonoBehaviour
    {
        [SerializeField] private ProjectileEffect effect = ProjectileEffect.Damage;
        [SerializeField] private float speed = 65f;
        [SerializeField] private float damage = 10f;
        [SerializeField] private float repairAmount = 15f;
        [SerializeField] private float lifetime = 4f;

        private static readonly Color RepairColor = new Color(0.3f, 1f, 0.55f);
        private static readonly Color DamageColor = new Color(1f, 0.45f, 0.15f);

        public ProjectileEffect Effect => effect;

        /// <summary>
        /// Set what this round does on hit, keeping the prefab's damage / repair amount.
        /// Medicine rounds glow green, destroy rounds orange.
        /// </summary>
        public void Configure(ProjectileEffect newEffect)
        {
            effect = newEffect;
            Color color = effect == ProjectileEffect.Repair ? RepairColor : DamageColor;
            Renderer renderer = GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                Material mat = renderer.material;
                mat.color = color;
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color * 0.9f);
            }
        }

        /// <summary>As Configure(effect), but with a custom damage or repair amount.</summary>
        public void Configure(ProjectileEffect newEffect, float amount)
        {
            if (newEffect == ProjectileEffect.Repair)
            {
                repairAmount = amount;
            }
            else
            {
                damage = amount;
            }
            Configure(newEffect);
        }

        private void OnEnable()
        {
            Invoke(nameof(Despawn), lifetime);
        }

        private void Update()
        {
            transform.position += transform.forward * speed * Time.deltaTime;
        }

        private void OnTriggerEnter(Collider other)
        {
            // Pass through the ship that fired and through other rounds (the co-pilot and the
            // main gun share a fire point).
            if (other.GetComponentInParent<ShipController>() != null || other.GetComponentInParent<Projectile>() != null)
            {
                return;
            }

            if (effect == ProjectileEffect.Repair)
            {
                other.GetComponentInParent<IRepairable>()?.Repair(repairAmount);
            }
            else
            {
                other.GetComponentInParent<IDamageable>()?.ApplyDamage(damage);
            }

            Despawn();
        }

        private void Despawn()
        {
            CancelInvoke();
            Destroy(gameObject);
        }
    }
}
