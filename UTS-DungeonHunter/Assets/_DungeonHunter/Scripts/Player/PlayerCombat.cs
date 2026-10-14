using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DungeonHunter
{
    /// <summary>
    /// Serangan melee player (Klik Kiri / Space).
    /// Mengenai semua target dalam busur di depan player.
    /// Setiap ayunan pedang memancarkan noise.
    /// </summary>
    public class PlayerCombat : MonoBehaviour
    {
        [SerializeField] private float damage = 25f;
        [SerializeField] private float range = 2.3f;
        [SerializeField, Range(10f, 360f)] private float arcAngle = 120f;
        [SerializeField] private float cooldown = 0.45f;
        [SerializeField] private float attackNoiseRadius = 9f;

        [Header("Visual")]
        [SerializeField] private Transform swordPivot;
        [SerializeField] private float swingDuration = 0.15f;

        private Health health;
        private float nextAttackTime;
        private readonly HashSet<Health> hitBuffer = new HashSet<Health>();

        private void Awake()
        {
            health = GetComponent<Health>();
        }

        private void Update()
        {
            if (GameManager.Instance != null && !GameManager.Instance.IsPlaying)
                return;
            if (health != null && health.IsDead)
                return;

            bool pressed = (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                           || (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame);

            if (pressed && Time.time >= nextAttackTime)
            {
                nextAttackTime = Time.time + cooldown;
                Attack();
            }
        }

        private void Attack()
        {
            NoiseSystem.Emit(transform.position, attackNoiseRadius);

            if (swordPivot != null)
                StartCoroutine(SwingRoutine());

            hitBuffer.Clear();
            Collider[] hits = Physics.OverlapSphere(transform.position, range, ~0, QueryTriggerInteraction.Ignore);

            foreach (Collider hit in hits)
            {
                Health target = hit.GetComponentInParent<Health>();
                if (target == null || target == health || target.IsDead || hitBuffer.Contains(target))
                    continue;

                Vector3 dir = target.transform.position - transform.position;
                dir.y = 0f;
                if (Vector3.Angle(transform.forward, dir) > arcAngle * 0.5f)
                    continue;

                hitBuffer.Add(target);
                target.TakeDamage(damage, transform.position);
            }
        }

        private IEnumerator SwingRoutine()
        {
            float t = 0f;
            while (t < swingDuration)
            {
                t += Time.deltaTime;
                float yaw = Mathf.Lerp(-70f, 70f, t / swingDuration);
                swordPivot.localRotation = Quaternion.Euler(0f, yaw, 0f);
                yield return null;
            }
            swordPivot.localRotation = Quaternion.Euler(0f, -35f, 0f);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, range);
        }
    }
}
