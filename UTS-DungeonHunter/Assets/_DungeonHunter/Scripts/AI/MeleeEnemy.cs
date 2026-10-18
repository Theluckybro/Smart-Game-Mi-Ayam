using UnityEngine;

namespace DungeonHunter
{
    /// <summary>
    /// Melee AI (Skeleton Warrior):
    /// PATROL → CHASE → ATTACK (windup + hit) → ... → FLEE saat HP rendah
    /// Kehilangan player → INVESTIGATE (Last Known Position) → SEARCH → PATROL
    /// </summary>
    public class MeleeEnemy : EnemyAI
    {
        [Header("Melee Attack")]
        [SerializeField] private float attackRange = 1.8f;
        [SerializeField] private float attackDamage = 12f;
        [SerializeField] private float attackCooldown = 1.2f;
        [SerializeField] private float attackWindup = 0.35f;
        [SerializeField] private Color windupColor = new Color(1f, 0.85f, 0.3f);

        private float nextAttackTime;
        private float windupTimer;
        private bool isWindingUp;

        public float AttackRange => attackRange;
        public override string ExtraDebugInfo => isWindingUp ? "Attack : WINDUP" : "";

        protected override void UpdateChase()
        {
            base.UpdateChase();
            if (State != EnemyState.Chase)
                return;

            if (DistanceToTarget <= attackRange)
                ChangeState(EnemyState.Attack, "Player dalam jangkauan serang");
        }

        protected override void EnterState(EnemyState state)
        {
            base.EnterState(state);
            if (state == EnemyState.Attack)
            {
                StopMoving();
                isWindingUp = false;
            }
        }

        protected override void ExitState(EnemyState state)
        {
            base.ExitState(state);
            if (state == EnemyState.Attack)
                isWindingUp = false;
        }

        protected override void UpdateAttack()
        {
            if (target == null)
                return;

            FaceTowards(target.position);

            if (isWindingUp)
            {
                windupTimer -= Time.deltaTime;
                if (windupTimer <= 0f)
                {
                    isWindingUp = false;
                    nextAttackTime = Time.time + attackCooldown;

                    Vector3 dir = target.position - transform.position;
                    dir.y = 0f;
                    bool inRange = dir.magnitude <= attackRange + 0.4f;
                    bool inFront = Vector3.Angle(transform.forward, dir) <= 60f;

                    VFXManager.Slash(transform, new Color(1f, 0.35f, 0.2f), attackRange + 0.3f, 110f, 1f);
                    AudioManager.Play(Sfx.MeleeSwing, transform.position);
                    AudioManager.Play(Sfx.SwordWhoosh, transform.position, 0.8f, 0.75f);
                    if (inRange && inFront && targetHealth != null)
                        targetHealth.TakeDamage(attackDamage, transform.position);
                }
                return;
            }

            if (!perception.CanSeeTarget || DistanceToTarget > attackRange + 0.5f)
            {
                ChangeState(EnemyState.Chase, "Player keluar jangkauan serang");
                return;
            }

            Reason = "Menyerang player";
            if (Time.time >= nextAttackTime)
            {
                isWindingUp = true;
                windupTimer = attackWindup;
                PlayAttackAnimation();
                VFXManager.Telegraph(transform.position + transform.forward * (attackRange * 0.55f), attackRange * 0.6f,
                    attackWindup, new Color(1f, 0.25f, 0.15f));
                hitFlash?.Flash(windupColor, attackWindup);
                AudioManager.Play(Sfx.MeleeWindup, transform.position);
            }
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
    }
}
