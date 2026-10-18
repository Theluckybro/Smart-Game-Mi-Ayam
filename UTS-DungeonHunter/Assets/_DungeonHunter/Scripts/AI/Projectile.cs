using UnityEngine;

namespace DungeonHunter
{
    /// <summary>
    /// Proyektil ranged enemy. Bergerak lurus dengan sweep raycast tiap frame,
    /// menembus sesama enemy, berhenti di dinding, memberi damage ke player.
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        [SerializeField] private float radius = 0.15f;
        [SerializeField] private float lifetime = 3f;
        [SerializeField] private Color fxColor = new Color(0.75f, 0.4f, 1f);
        [SerializeField] private bool magic = true;

        private float speed;
        private float damage;
        private Transform owner;
        private float dieTime;
        private readonly RaycastHit[] hits = new RaycastHit[8];

        public void Launch(float projectileSpeed, float projectileDamage, Transform shooter)
        {
            speed = projectileSpeed;
            damage = projectileDamage;
            owner = shooter;
            dieTime = Time.time + lifetime;
        }

        private void Update()
        {
            if (Time.time >= dieTime)
            {
                VFXManager.Glow(transform.position, fxColor, 0.8f, 0.15f);
                Destroy(gameObject);
                return;
            }

            float step = speed * Time.deltaTime;
            int count = Physics.SphereCastNonAlloc(transform.position, radius, transform.forward, hits, step,
                ~0, QueryTriggerInteraction.Ignore);

            float nearest = float.MaxValue;
            Collider nearestCol = null;
            for (int i = 0; i < count; i++)
            {
                Collider c = hits[i].collider;
                if (c.GetComponentInParent<EnemyAI>() != null || (owner != null && c.transform.IsChildOf(owner)))
                    continue;
                if (hits[i].distance < nearest)
                {
                    nearest = hits[i].distance;
                    nearestCol = c;
                }
            }

            if (nearestCol != null)
            {
                Health h = nearestCol.GetComponentInParent<Health>();
                if (h != null)
                    h.TakeDamage(damage, owner != null ? owner.position : transform.position);
                Vector3 hitPoint = transform.position + transform.forward * nearest;
                VFXManager.Impact(hitPoint, -transform.forward, fxColor, h != null ? 1f : 0.7f);
                AudioManager.Play(magic ? Sfx.MageImpact : Sfx.BoltImpact, hitPoint, h != null ? 1f : 0.7f);
                Destroy(gameObject);
                return;
            }

            transform.position += transform.forward * step;
        }
    }
}
