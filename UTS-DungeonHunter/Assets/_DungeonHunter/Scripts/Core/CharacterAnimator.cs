using UnityEngine;

namespace DungeonHunter
{
    /// <summary>
    /// Jembatan antara logika (AI / player) dan Animator model KayKit.
    /// - Speed  : dihitung dari perpindahan posisi (berlaku untuk NavMeshAgent, CharacterController,
    ///            lompatan NavMeshLink, maupun Charge Guardian)
    /// - Hit / Dead : otomatis dari event Health
    /// - Attack / Attack2 / Taunt : dipanggil oleh AI / PlayerCombat
    /// </summary>
    public class CharacterAnimator : MonoBehaviour
    {
        private static readonly int SpeedId = Animator.StringToHash("Speed");
        private static readonly int AttackId = Animator.StringToHash("Attack");
        private static readonly int Attack2Id = Animator.StringToHash("Attack2");
        private static readonly int HitId = Animator.StringToHash("Hit");
        private static readonly int DeadId = Animator.StringToHash("Dead");
        private static readonly int TauntId = Animator.StringToHash("Taunt");

        [SerializeField] private Animator animator;
        [SerializeField] private bool reactToHits = true;
        [SerializeField] private float speedDampTime = 0.1f;

        private Health health;
        private Vector3 lastPosition;

        public Animator Animator => animator;

        private void Awake()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
            health = GetComponent<Health>();
            lastPosition = transform.position;
        }

        private void OnEnable()
        {
            if (health != null)
            {
                health.Damaged += OnDamaged;
                health.Died += OnDied;
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.Damaged -= OnDamaged;
                health.Died -= OnDied;
            }
        }

        private void Update()
        {
            if (animator == null || Time.deltaTime <= 0f)
                return;

            Vector3 delta = transform.position - lastPosition;
            delta.y = 0f;
            lastPosition = transform.position;
            animator.SetFloat(SpeedId, delta.magnitude / Time.deltaTime, speedDampTime, Time.deltaTime);
        }

        public void Attack() => SetTrigger(AttackId);
        public void Attack2() => SetTrigger(Attack2Id);
        public void Taunt() => SetTrigger(TauntId);

        private void OnDamaged(float amount, Vector3 source)
        {
            if (reactToHits && health != null && !health.IsDead)
                SetTrigger(HitId);
        }

        private void OnDied()
        {
            if (animator != null)
                animator.SetBool(DeadId, true);
        }

        private void SetTrigger(int id)
        {
            if (animator != null && (health == null || !health.IsDead))
                animator.SetTrigger(id);
        }
    }
}
