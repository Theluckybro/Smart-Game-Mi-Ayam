using DungeonHunter.BT;
using UnityEngine;

namespace DungeonHunter
{
    /// <summary>
    /// Guardian Behavior — boss penjaga Artifact, keputusan memakai BEHAVIOR TREE.
    ///
    /// Root (Selector)
    ///  ├─ Phase 2      : [HP ≤ 50%]                    → Enrage!
    ///  ├─ Leash        : [Player keluar arena]         → Kembali ke pos + regen
    ///  ├─ Ground Slam  : [Jarak ≤ 2.6 m & siap]        → Slam (windup → area damage)
    ///  ├─ Charge       : [Phase 2] [Jarak 4.5–10 m]    → Terjang (windup → dash)
    ///  ├─ Kejar        : [Player / LKP di dalam arena] → Kejar
    ///  └─ Jaga pos     (fallback)
    ///
    /// Slam, Charge, dan Enrage bersifat "committed": sekali mulai, diselesaikan dulu
    /// sebelum cabang lain boleh mengambil alih.
    /// </summary>
    public class GuardianEnemy : EnemyAI
    {
        [Header("Guardian Arena")]
        [SerializeField] private float leashRadius = 10f;
        [SerializeField] private float returnRegenPerSecond = 15f;

        [Header("Ground Slam")]
        [SerializeField] private float slamTriggerRange = 2.6f;
        [SerializeField] private float slamRadius = 3f;
        [SerializeField] private float slamDamage = 25f;
        [SerializeField] private float slamWindup = 0.8f;
        [SerializeField] private float slamCooldown = 2.2f;
        [SerializeField] private Color slamColor = new Color(1f, 0.2f, 0.2f);

        [Header("Phase 2 - Enrage")]
        [SerializeField, Range(0f, 1f)] private float enrageThreshold = 0.5f;
        [SerializeField] private float enrageDuration = 1.2f;
        [SerializeField] private float enrageSpeedMultiplier = 1.4f;
        [SerializeField] private Color enrageColor = new Color(0.8f, 0.1f, 1f);

        [Header("Charge (Phase 2)")]
        [SerializeField] private float chargeMinDistance = 4.5f;
        [SerializeField] private float chargeMaxDistance = 10f;
        [SerializeField] private float chargeWindup = 0.6f;
        [SerializeField] private float chargeSpeed = 13f;
        [SerializeField] private float chargeDuration = 0.8f;
        [SerializeField] private float chargeDamage = 20f;
        [SerializeField] private float chargeCooldown = 5f;
        [SerializeField] private float chargeHitRadius = 1.6f;
        [SerializeField] private Color chargeColor = new Color(1f, 0.5f, 0f);

        private BehaviorTree tree;
        private bool engaged;
        private bool enraged;
        private bool enraging;
        private bool slamming;
        private bool charging;
        private bool chargeDashing;
        private bool chargeHit;
        private float actionTimer;
        private float nextSlamTime;
        private float nextChargeTime;
        private Vector3 chargeDirection;
        private float nextDustTime;
        private ParticleSystem enrageAura;

        private static readonly Color SlamColor = new Color(1f, 0.35f, 0.15f);
        private static readonly Color EnrageColor = new Color(0.75f, 0.35f, 1f);
        private static readonly Color ChargeLineColor = new Color(1f, 0.55f, 0.1f);

        public float LeashRadius => leashRadius;
        public float SlamRadius => slamRadius;
        public bool IsWindingUp => slamming;
        public bool IsCharging => charging;
        public bool IsEnraged => enraged;
        public Vector3 ChargeDirection => chargeDirection;
        public string BTPath => tree != null ? tree.Context.ActivePath : "-";

        public override string ExtraDebugInfo =>
            $"BT : {BTPath} [{tree?.Context.LastStatus}]\n" +
            $"Phase : {(enraged ? "2 (ENRAGED)" : "1")}\n" +
            $"Leash : {Vector3.Distance(target != null ? target.position : homePosition, homePosition):0.0}/{leashRadius:0}m";

        protected override EnemyState InitialState => EnemyState.Guard;

        protected override void Awake()
        {
            base.Awake();
            canFlee = false;
            BuildTree();
        }

        private void BuildTree()
        {
            tree = new BehaviorTree(
                new BTSelector("Root",
                    new BTSequence("Phase 2",
                        new BTCondition("HP ≤ 50%", () => enraging || (!Busy && !enraged && health.Normalized <= enrageThreshold)),
                        new BTAction("Enrage!", DoEnrage)),
                    new BTSequence("Leash",
                        new BTCondition("Player keluar arena", () => !Busy && engaged && !TargetInsideArena),
                        new BTAction("Kembali ke pos + regen", DoReturn)),
                    new BTSequence("Ground Slam",
                        new BTCondition("Jarak ≤ 2.6 m & siap",
                            () => slamming || (!Busy && Aware && DistanceToTarget <= slamTriggerRange && Time.time >= nextSlamTime)),
                        new BTAction("Slam", DoSlam)),
                    new BTSequence("Charge",
                        new BTCondition("Phase 2", () => charging || enraged),
                        new BTCondition("Jarak 4.5–10 m & siap", () => charging || CanCharge()),
                        new BTAction("Terjang", DoCharge)),
                    new BTSequence("Kejar",
                        new BTCondition("Player / LKP di arena", () => Aware),
                        new BTAction("Kejar", DoChase)),
                    new BTAction("Jaga pos", DoGuard)));
        }

        // =====================================================
        // KONDISI
        // =====================================================

        private bool Busy => slamming || charging || enraging;

        private bool InsideArena(Vector3 position)
        {
            Vector3 d = position - homePosition;
            d.y = 0f;
            return d.magnitude <= leashRadius;
        }

        private bool TargetInsideArena => target != null && InsideArena(target.position);

        private bool Aware =>
            TargetInsideArena &&
            (perception.CanSeeTarget ||
             (memory.IsFresh && memory.HasLastKnownPosition && InsideArena(memory.LastKnownPosition)));

        private bool CanCharge()
        {
            if (Busy || !perception.CanSeeTarget || !TargetInsideArena || Time.time < nextChargeTime)
                return false;
            float d = DistanceToTarget;
            return d >= chargeMinDistance && d <= chargeMaxDistance;
        }

        // =====================================================
        // FSM HOOK → BEHAVIOR TREE
        // =====================================================

        protected override void UpdateState(EnemyState state)
        {
            tree.Tick();
        }

        protected override void OnDamagedReaction()
        {
            // Memory sudah diisi posisi penyerang oleh base class.
            // Behavior Tree akan bereaksi lewat kondisi "Player / LKP di arena".
        }

        protected override void EnterState(EnemyState state)
        {
            base.EnterState(state);
            switch (state)
            {
                case EnemyState.Guard:
                    agent.speed = patrolSpeed;
                    break;
                case EnemyState.Return:
                    agent.speed = patrolSpeed * 1.5f;
                    break;
            }
        }

        // =====================================================
        // AKSI
        // =====================================================

        private BTStatus DoEnrage()
        {
            engaged = true;
            if (!enraging)
            {
                enraging = true;
                actionTimer = enrageDuration;
                StopMoving();
                ChangeState(EnemyState.Enrage, $"HP {health.Normalized:P0} ≤ 50% → Phase 2");
                if (characterAnimator != null)
                    characterAnimator.Taunt();
                if (hitFlash != null)
                    hitFlash.Flash(enrageColor, enrageDuration);
                NoiseSystem.Emit(transform.position, 15f);
                VFXManager.Ring(transform.position, EnrageColor, 9f, 0.7f);
                VFXManager.Ring(transform.position, Color.white, 4f, 0.35f);
                VFXManager.Smoke(transform.position + Vector3.up * 0.2f, new Color(0.45f, 0.3f, 0.55f, 0.8f), 14, 1.3f, 4f);
                VFXManager.Glow(transform.position + Vector3.up * 1.6f, EnrageColor, 6f, 0.4f);
                VFXManager.Sparks(transform.position + Vector3.up * 1.5f, Vector3.up, EnrageColor, 30, 8f, 1.3f);
                VFXManager.Shake(0.55f);
                VFXManager.FloatingLabel(transform.position + Vector3.up * 4f, "ENRAGED!", EnrageColor, 36f);
                if (enrageAura == null)
                    enrageAura = VFXManager.Aura(transform, EnrageColor, 1.1f, 3f);
                if (GameManager.Instance != null)
                    GameManager.Instance.ShowMessage("Guardian mengamuk! (Phase 2: lebih cepat + Charge)");
            }

            if (target != null)
                FaceTowards(target.position);

            actionTimer -= Time.deltaTime;
            if (actionTimer > 0f)
                return BTStatus.Running;

            enraging = false;
            enraged = true;
            chaseSpeed *= enrageSpeedMultiplier;
            slamCooldown *= 0.7f;
            return BTStatus.Success;
        }

        private BTStatus DoReturn()
        {
            ChangeState(EnemyState.Return, "Player keluar arena → kembali ke pos & regen");
            health.Heal(returnRegenPerSecond * Time.deltaTime);
            if (health.Normalized < 1f)
                HealEffect();
            MoveTo(homePosition, "Pos Guardian");

            if (!agent.pathPending && agent.remainingDistance <= waypointTolerance)
            {
                engaged = false;
                memory.Forget();
                return BTStatus.Success;
            }
            return BTStatus.Running;
        }

        private BTStatus DoSlam()
        {
            engaged = true;
            if (!slamming)
            {
                slamming = true;
                actionTimer = slamWindup;
                StopMoving();
                ChangeState(EnemyState.Attack, "Player dalam jangkauan → Ground Slam");
                PlayAttackAnimation();
                VFXManager.Telegraph(transform.position, slamRadius, slamWindup, SlamColor);
                if (hitFlash != null)
                    hitFlash.Flash(slamColor, slamWindup);
            }

            actionTimer -= Time.deltaTime;
            if (actionTimer > 0f)
                return BTStatus.Running;

            slamming = false;
            nextSlamTime = Time.time + slamCooldown;
            Slam();
            return BTStatus.Success;
        }

        private void Slam()
        {
            NoiseSystem.Emit(transform.position, 6f);

            Vector3 p = transform.position;
            VFXManager.Ring(p, SlamColor, slamRadius * 1.35f, 0.45f);
            VFXManager.Ring(p, new Color(1f, 0.85f, 0.6f), slamRadius * 0.8f, 0.3f);
            VFXManager.Glow(p + Vector3.up * 0.5f, SlamColor, 5f, 0.18f);
            VFXManager.Smoke(p + Vector3.up * 0.2f, new Color(0.45f, 0.4f, 0.36f, 0.85f), 18, 1.4f, 5f);
            VFXManager.Debris(p + Vector3.up * 0.3f, new Color(0.4f, 0.38f, 0.42f), 14, 6f, 0.18f);
            VFXManager.Sparks(p + Vector3.up * 0.3f, Vector3.up, new Color(1f, 0.7f, 0.3f), 20, 7f, 1.4f);
            VFXManager.Shake(target != null && Vector3.Distance(target.position, p) < 9f ? 0.6f : 0.3f);
            if (targetHealth == null)
                return;

            Vector3 diff = target.position - transform.position;
            diff.y = 0f;
            if (diff.magnitude <= slamRadius)
                targetHealth.TakeDamage(slamDamage, transform.position);
        }

        private BTStatus DoCharge()
        {
            engaged = true;
            if (!charging)
            {
                charging = true;
                chargeDashing = false;
                chargeHit = false;
                actionTimer = chargeWindup;
                StopMoving();
                ChangeState(EnemyState.Charge, $"Phase 2, jarak {DistanceToTarget:0.0}m → Charge");
                Vector3 toPlayer = target != null ? target.position - transform.position : transform.forward;
                VFXManager.LineTelegraph(transform.position, toPlayer, chargeSpeed * chargeDuration + 1f, 1.8f, chargeWindup, ChargeLineColor);
                if (characterAnimator != null)
                    characterAnimator.Attack2();
                if (hitFlash != null)
                    hitFlash.Flash(chargeColor, chargeWindup);
            }

            if (!chargeDashing)
            {
                if (target != null)
                    FaceTowards(target.position);
                Vector3 dir = target != null ? target.position - transform.position : transform.forward;
                dir.y = 0f;
                chargeDirection = dir.sqrMagnitude > 0.01f ? dir.normalized : transform.forward;

                actionTimer -= Time.deltaTime;
                if (actionTimer > 0f)
                    return BTStatus.Running;

                chargeDashing = true;
                actionTimer = chargeDuration;
                Reason = "Menerjang!";
            }

            if (agent.isOnNavMesh)
                agent.Move(chargeDirection * chargeSpeed * Time.deltaTime);

            if (Time.time >= nextDustTime)
            {
                nextDustTime = Time.time + 0.05f;
                VFXManager.Smoke(transform.position + Vector3.up * 0.15f, new Color(0.5f, 0.45f, 0.4f, 0.75f), 2, 1f, 1.2f);
                VFXManager.Sparks(transform.position + Vector3.up * 0.3f, -chargeDirection, ChargeLineColor, 2, 4f, 0.5f);
            }

            if (!chargeHit && targetHealth != null)
            {
                Vector3 diff = target.position - transform.position;
                diff.y = 0f;
                if (diff.magnitude <= chargeHitRadius)
                {
                    chargeHit = true;
                    targetHealth.TakeDamage(chargeDamage, transform.position);
                    VFXManager.HitStop(0.08f);
                    VFXManager.Shake(0.5f);
                }
            }

            actionTimer -= Time.deltaTime;
            if (actionTimer > 0f)
                return BTStatus.Running;

            charging = false;
            chargeDashing = false;
            nextChargeTime = Time.time + chargeCooldown;
            return BTStatus.Success;
        }

        private BTStatus DoChase()
        {
            engaged = true;

            if (perception.CanSeeTarget)
            {
                ChangeState(EnemyState.Chase, "Player terlihat di dalam arena");
                MoveTo(target.position, "Player");
                return BTStatus.Running;
            }

            ChangeState(EnemyState.Chase, "Menuju Last Known Position di arena");
            MoveTo(memory.LastKnownPosition, "Last Known Position");
            if (!agent.pathPending && agent.remainingDistance <= 1f)
            {
                memory.Forget();
                return BTStatus.Success;
            }
            return BTStatus.Running;
        }

        private BTStatus DoGuard()
        {
            engaged = false;
            ChangeState(EnemyState.Guard, "Tidak ada ancaman di arena");
            UpdateSentry("Pos Guardian");
            return BTStatus.Running;
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            Vector3 center = Application.isPlaying ? homePosition : transform.position;
            Gizmos.color = new Color(0.7f, 0.2f, 1f);
            Gizmos.DrawWireSphere(center, leashRadius);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, slamRadius);
        }
    }
}
