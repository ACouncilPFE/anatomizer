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

        public ProjectileEffect Effect => effect;

        /// <summary>Set what this round does on hit. Repair rounds glow green.</summary>
        public void Configure(ProjectileEffect newEffect, float amount)
        {
            effect = newEffect;
            if (effect == ProjectileEffect.Repair)
            {
                repairAmount = amount;
                Renderer renderer = GetComponentInChildren<Renderer>();
                if (renderer != null)
                {
                    Material mat = renderer.material;
                    mat.color = RepairColor;
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", RepairColor * 0.8f);
                }
            }
            else
            {
                damage = amount;
            }
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
