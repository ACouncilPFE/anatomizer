using AIGame.Core;
using UnityEngine;

namespace AIGame.Gameplay
{
    public class Projectile : MonoBehaviour
    {
        [SerializeField] private float speed = 65f;
        [SerializeField] private float damage = 10f;
        [SerializeField] private float lifetime = 4f;

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
            var damageable = other.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                damageable.ApplyDamage(damage);
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
