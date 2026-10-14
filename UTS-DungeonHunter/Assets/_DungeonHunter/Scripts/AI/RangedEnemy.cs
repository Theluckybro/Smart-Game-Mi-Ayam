using UnityEngine;
using UnityEngine.AI;

namespace DungeonHunter
{
    /// <summary>
    /// Ranged AI (Skeleton Archer) dengan tactical behavior MAINTAIN DISTANCE:
    ///   jarak > preferredMax  → CHASE (mendekat)
    ///   preferredMin..Max     → ATTACK (berhenti, membidik, menembak)
    ///   jarak &lt; preferredMin  → REPOSITION (mundur sambil tetap menghadap player)
    /// </summary>
    public class RangedEnemy : EnemyAI
    {
        [Header("Maintain Distance")]
        [SerializeField] private float preferredMinDistance = 5f;
        [SerializeField] private float preferredMaxDistance = 9f;
        [SerializeField] private float retreatStep = 4f;

        [Header("Shooting")]
        [SerializeField] private Projectile projectilePrefab;
        [SerializeField] private Transform firePoint;
        [SerializeField] private float projectileDamage = 10f;
        [SerializeField] private float projectileSpeed = 14f;
        [SerializeField] private float fireCooldown = 1.6f;
        [SerializeField] private float aimTime = 0.45f;
        [SerializeField] private Color aimColor = new Color(1f, 0.6f, 0.1f);

        private float nextFireTime;
        private float aimTimer;
        private bool isAiming;
        private float nextRepositionEval;
        private bool cornered;
        private float corneredUntil;
        private NavMeshPath scratchPath;

        private static readonly float[] RetreatAngles = { 0f, 35f, -35f, 70f, -70f, 105f, -105f };
        private static readonly float[] RetreatSteps = { 1f, 0.6f };

        public float PreferredMin => preferredMinDistance;
        public float PreferredMax => preferredMaxDistance;
        public override string ExtraDebugInfo =>
            $"Range : {preferredMinDistance:0}-{preferredMaxDistance:0}m" + (isAiming ? "  [AIMING]" : "");

        public void SetProjectile(Projectile prefab, Transform muzzle)
        {
            projectilePrefab = prefab;
            firePoint = muzzle;
        }

        protected override void Awake()
        {
            base.Awake();
            scratchPath = new NavMeshPath();
        }

        protected override void UpdateChase()
        {
            base.UpdateChase();
            if (State != EnemyState.Chase)
                return;

            float d = DistanceToTarget;
            if (d <= preferredMaxDistance)
                ChangeState(EnemyState.Attack, "Masuk jarak tembak ideal");
            else
                Reason = $"Terlalu jauh ({d:0.0}m) → mendekat";
        }

        protected override void EnterState(EnemyState state)
        {
            base.EnterState(state);

            switch (state)
            {
                case EnemyState.Attack:
                    StopMoving();
                    isAiming = false;
                    break;
                case EnemyState.Reposition:
                    agent.speed = chaseSpeed;
                    agent.updateRotation = false; // mundur sambil menghadap player
                    nextRepositionEval = 0f;
                    cornered = false;
                    break;
            }
        }

        protected override void ExitState(EnemyState state)
        {
            if (state == EnemyState.Reposition)
                agent.updateRotation = true;
            if (state == EnemyState.Attack)
                isAiming = false;
        }

        protected override void UpdateAttack()
        {
            if (target == null)
                return;

            FaceTowards(target.position);

            if (isAiming)
            {
                aimTimer -= Time.deltaTime;
                if (aimTimer <= 0f)
                {
                    isAiming = false;
                    Fire();
                }
                return;
            }

            float d = DistanceToTarget;

            if (!perception.CanSeeTarget)
            {
                ChangeState(EnemyState.Investigate, "Kehilangan Line of Sight");
                return;
            }

            if (cornered && Time.time >= corneredUntil)
                cornered = false;

            if (d < preferredMinDistance && !cornered)
            {
                ChangeState(EnemyState.Reposition, $"Player terlalu dekat ({d:0.0}m) → jaga jarak");
                return;
            }

            if (d > preferredMaxDistance + 1f)
            {
                ChangeState(EnemyState.Chase, "Player menjauh");
                return;
            }

            if (d >= preferredMinDistance)
                cornered = false;

            Reason = cornered ? "Terpojok → tetap menembak" : $"Jarak ideal ({d:0.0}m) → menembak";

            if (Time.time >= nextFireTime)
            {
                isAiming = true;
                aimTimer = aimTime;
                if (hitFlash != null)
                    hitFlash.Flash(aimColor, aimTime);
            }
        }

        protected override void UpdateReposition()
        {
            if (target == null)
                return;

            FaceTowards(target.position);
            float d = DistanceToTarget;

            if (d >= preferredMinDistance + 1.5f)
            {
                ChangeState(EnemyState.Attack, "Jarak aman tercapai");
                return;
            }

            if (Time.time < nextRepositionEval)
                return;

            nextRepositionEval = Time.time + 0.5f;

            if (TryFindRetreatPosition(out Vector3 retreatPos))
            {
                MoveTo(retreatPos, "Posisi mundur (maintain distance)");
                Reason = $"Player terlalu dekat ({d:0.0}m) → mundur";
            }
            else
            {
                ChangeState(EnemyState.Attack, "Tidak ada ruang mundur");
                cornered = true;
                corneredUntil = Time.time + 2f;
            }
        }

        /// <summary>
        /// Evaluasi beberapa kandidat arah mundur (lurus, ±35°, ±70°, ±105°)
        /// dengan dua panjang langkah, lalu pilih titik NavMesh yang paling jauh dari player.
        /// Sudut ±105° membuat archer bisa menyamping menyusuri dinding saat di pojok.
        /// </summary>
        private bool TryFindRetreatPosition(out Vector3 best)
        {
            best = transform.position;
            Vector3 away = transform.position - target.position;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f)
                away = -transform.forward;
            away.Normalize();

            float bestDist = DistanceToTarget + 0.5f;
            bool found = false;

            foreach (float step in RetreatSteps)
            foreach (float a in RetreatAngles)
            {
                Vector3 candidate = transform.position + Quaternion.Euler(0f, a, 0f) * away * (retreatStep * step);
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
                    continue;
                if (!agent.CalculatePath(hit.position, scratchPath) || scratchPath.status != NavMeshPathStatus.PathComplete)
                    continue;

                float dist = Vector3.Distance(hit.position, target.position);
                if (dist > bestDist)
                {
                    bestDist = dist;
                    best = hit.position;
                    found = true;
                }
            }

            return found;
        }

        private void Fire()
        {
            nextFireTime = Time.time + fireCooldown;
            if (projectilePrefab == null || target == null)
                return;

            Vector3 origin = firePoint != null ? firePoint.position : transform.position + Vector3.up * 1.2f;
            Vector3 aimPoint = target.position + Vector3.up * 1.0f;
            Projectile p = Instantiate(projectilePrefab, origin, Quaternion.LookRotation(aimPoint - origin));
            p.gameObject.SetActive(true);
            p.Launch(projectileSpeed, projectileDamage, transform);
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            Gizmos.color = new Color(1f, 0.5f, 0f);
            Gizmos.DrawWireSphere(transform.position, preferredMinDistance);
            Gizmos.DrawWireSphere(transform.position, preferredMaxDistance);
        }
    }
}
