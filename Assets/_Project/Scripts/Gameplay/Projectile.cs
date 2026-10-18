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

        /// <summary>
        /// Builds an inactive projectile to clone from, matching the scene builder's prefab:
        /// a small glowing sphere with a trigger collider on a kinematic body.
        /// </summary>
        public static Projectile CreateRuntimeTemplate()
        {
            GameObject root = new GameObject("Projectile (runtime template)");
            root.SetActive(false);
            Object.DontDestroyOnLoad(root);

            SphereCollider col = root.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.3f;
            Rigidbody rb = root.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = true;

            GameObject mesh = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            mesh.name = "Mesh";
            Object.Destroy(mesh.GetComponent<Collider>());
            mesh.transform.SetParent(root.transform, false);
            mesh.transform.localScale = Vector3.one * 0.3f;

            return root.AddComponent<Projectile>();
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

            bool hitSomething;
            if (effect == ProjectileEffect.Repair)
            {
                IRepairable target = other.GetComponentInParent<IRepairable>();
                target?.Repair(repairAmount);
                hitSomething = target != null;
            }
            else
            {
                IDamageable target = other.GetComponentInParent<IDamageable>();
                target?.ApplyDamage(damage);
                hitSomething = target != null;
            }

            GameAudio.PlayAt(hitSomething ? ProceduralAudio.HitSquish : ProceduralAudio.HitWall, transform.position,
                hitSomething ? 0.7f : 0.4f, Random.Range(0.85f, 1.15f), 0.03f);
            Despawn();
        }

        private void Despawn()
        {
            CancelInvoke();
            Destroy(gameObject);
        }
    }
}
