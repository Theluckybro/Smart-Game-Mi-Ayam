using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace DungeonHunter
{
    public enum EnemyState
    {
        Patrol,      // keliling waypoint / berjaga
        Investigate, // menuju Last Known Position / sumber suara
        Search,      // mencari di sekitar Last Known Position
        Chase,       // mengejar player yang terlihat
        Attack,      // menyerang (melee / menembak / slam)
        Reposition,  // Maintain Distance (ranged)
        Flee,        // Retreat ke titik aman saat HP rendah
        Guard,       // Guardian berjaga di pos
        Return,      // Guardian kembali ke pos (leash)
        Summon,      // Guardian membangkitkan minion (Phase 2)
        Enrage,      // Guardian masuk Phase 3
        Charge,      // Guardian menerjang (Phase 3)
        Dead
    }

    /// <summary>
    /// Base class enemy berbasis Finite State Machine.
    ///
    /// Pipeline AI per frame:
    ///   Perception (vision + hearing) → Memory → Decision (FSM) → Navigation (NavMesh) + Steering (Separation)
    ///
    /// Subclass (Melee / Ranged) meng-override state tertentu.
    /// Guardian meng-override UpdateState() dan memakai Behavior Tree.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(AIPerception), typeof(AIMemory))]
    [RequireComponent(typeof(Health))]
    public abstract class EnemyAI : MonoBehaviour
    {
        public static readonly List<EnemyAI> All = new List<EnemyAI>();

        [Header("Identity")]
        [SerializeField] private string displayName = "Enemy";

        [Header("Movement")]
        [SerializeField] protected float patrolSpeed = 2f;
        [SerializeField] protected float chaseSpeed = 4f;
        [SerializeField] protected float turnSpeed = 540f;

        [Header("Patrol")]
        [SerializeField] protected List<Transform> waypoints = new List<Transform>();
        [SerializeField] protected float waypointTolerance = 0.6f;
        [SerializeField] protected float waypointWaitTime = 1.2f;

        [Header("Investigate / Search")]
        [SerializeField] protected float investigateTimeout = 10f;
        [SerializeField] protected float searchDuration = 5f;
        [SerializeField] protected float searchRadius = 3.5f;
        [SerializeField] protected float unreachableWaitTime = 2f;

        [Header("Navigation")]
        [Tooltip("Cost area 'Trap' khusus agent ini. Makin besar → makin menghindari jebakan.")]
        [SerializeField] protected float trapAreaCost = 5f;
        [SerializeField] protected float linkJumpHeight = 1.2f;
        [SerializeField] protected float linkJumpSpeed = 6f;

        [Header("Flee / Retreat")]
        [SerializeField] protected bool canFlee = true;
        [SerializeField, Range(0f, 1f)] protected float fleeHealthThreshold = 0.3f;
        [SerializeField, Range(0f, 1f)] protected float recoverHealthThreshold = 0.7f;
        [SerializeField] protected float fleeSpeed = 5f;
        [SerializeField] protected float regenPerSecond = 8f;
        [SerializeField] protected List<Transform> retreatPoints = new List<Transform>();

        [Header("Feedback")]
        [SerializeField, Range(0f, 1f)] protected float knockbackScale = 1f;
        [SerializeField] protected float alertHeight = 2.5f;

        [Header("Steering - Separation")]
        [SerializeField] protected float separationRadius = 1.6f;
        [SerializeField] protected float separationStrength = 2.5f;

        // ---- Komponen ----
        protected NavMeshAgent agent;
        protected AIPerception perception;
        protected AIMemory memory;
        protected Health health;
        protected HitFlash hitFlash;
        protected CharacterAnimator characterAnimator;
        protected Transform target;
        protected Health targetHealth;

        // ---- Runtime state ----
        protected float stateTimer;
        protected Vector3 homePosition;
        protected Quaternion homeRotation;
        private int waypointIndex;
        private float waypointWaitTimer;
        private float nextSearchPointTime;
        private bool pendingInvestigate;
        private bool hasFled;
        private Transform currentRetreatPoint;
        private float unreachableTimer;
        private NavMeshPath scratchPath;
        private NavMeshQueryFilter queryFilter;
        private Vector3 knockbackVelocity;
        private float nextHealFxTime;

        // ---- Data untuk AI Debug Mode ----
        public string DisplayName => displayName;
        public EnemyState State { get; private set; } = EnemyState.Patrol;
        public string Reason { get; protected set; } = "";
        public Vector3 CurrentDestination { get; private set; }
        public bool HasDestination { get; private set; }
        public string DestinationLabel { get; protected set; } = "-";
        public Vector3 SeparationForce { get; private set; }
        public bool IsTraversingLink { get; private set; }
        public int LinksTraversed { get; private set; }
        public Transform CurrentRetreatPoint => currentRetreatPoint;
        public string RetreatInfo { get; private set; } = "";
        public NavMeshAgent Agent => agent;
        public AIPerception Perception => perception;
        public AIMemory Memory => memory;
        public Health Health => health;
        public Transform Target => target;
        public Vector3 HomePosition => homePosition;
        public virtual string ExtraDebugInfo => "";

        public static int TrapArea { get; private set; } = 3;

        public void Setup(string name, Transform player, List<Transform> patrolPoints, List<Transform> retreats)
        {
            displayName = name;
            waypoints = patrolPoints ?? new List<Transform>();
            retreatPoints = retreats ?? new List<Transform>();
            SetTarget(player);
        }

        public void SetTarget(Transform player)
        {
            target = player;
            targetHealth = player != null ? player.GetComponent<Health>() : null;
            if (perception == null)
                perception = GetComponent<AIPerception>();
            perception.SetTarget(player);
        }

        protected virtual void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            perception = GetComponent<AIPerception>();
            memory = GetComponent<AIMemory>();
            health = GetComponent<Health>();
            hitFlash = GetComponent<HitFlash>();
            characterAnimator = GetComponent<CharacterAnimator>();
            scratchPath = new NavMeshPath();

            if (target == null && perception.Target != null)
                SetTarget(perception.Target);

            // ---- NAVIGATION: Area Cost & NavMeshLink ----
            int trap = NavMesh.GetAreaFromName("Trap");
            if (trap >= 0)
                TrapArea = trap;
            agent.SetAreaCost(TrapArea, trapAreaCost);
            agent.autoTraverseOffMeshLink = false;

            queryFilter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = NavMesh.AllAreas };
            queryFilter.SetAreaCost(TrapArea, trapAreaCost);
        }

        protected virtual void OnEnable()
        {
            All.Add(this);
            health.Damaged += OnDamaged;
        }

        protected virtual void OnDisable()
        {
            All.Remove(this);
            health.Damaged -= OnDamaged;
        }

        protected virtual void Start()
        {
            homePosition = transform.position;
            homeRotation = transform.rotation;

            // Minion hasil summon: langsung tahu posisi player dan mengejar
            EnemyState first = spawnAggro && target != null ? EnemyState.Chase : InitialState;
            if (first == EnemyState.Chase)
                memory.RememberSeen(target.position);
            EnterState(first);
            State = first;
        }

        private bool spawnAggro;

        /// <summary>Dipanggil sebelum Start (mis. oleh Guardian saat summon): enemy langsung masuk Chase.</summary>
        public void AggroOnSpawn() => spawnAggro = true;

        protected virtual EnemyState InitialState => EnemyState.Patrol;

        private void Update()
        {
            if (State == EnemyState.Dead)
                return;

            if (health.IsDead)
            {
                ChangeState(EnemyState.Dead, "HP habis");
                return;
            }

            if (GameManager.Instance != null && !GameManager.Instance.IsPlaying)
            {
                StopMoving();
                return;
            }

            // 1. PERCEPTION
            perception.Tick();

            // 2. MEMORY
            UpdateMemory();

            // NAVIGATION: lompat lewat NavMeshLink (dikerjakan sendiri agar terlihat sebagai lompatan)
            if (IsTraversingLink)
                return;
            if (agent.isOnOffMeshLink)
            {
                StartCoroutine(TraverseLinkRoutine());
                return;
            }

            // 3. DECISION — transisi global berprioritas tinggi
            if (ShouldFlee())
            {
                hasFled = true;
                ChangeState(EnemyState.Flee, $"HP rendah ({health.Normalized:P0}) → retreat");
            }

            // 4. Jalankan state saat ini (FSM / Behavior Tree)
            stateTimer += Time.deltaTime;
            UpdateState(State);

            // 5. STEERING — separation agar enemy tidak bertumpuk
            ApplySeparation();
            ApplyKnockbackMotion();
        }

        // =====================================================
        // MEMORY
        // =====================================================

        private void UpdateMemory()
        {
            if (perception.CanSeeTarget && target != null)
                memory.RememberSeen(target.position);

            if (perception.ConsumeNoise(out Vector3 noisePos) && !perception.CanSeeTarget)
            {
                memory.RememberHeard(noisePos);
                pendingInvestigate = true;
            }
        }

        private void OnDamaged(float amount, Vector3 source)
        {
            if (State == EnemyState.Dead)
                return;

            memory.RememberDamage(source);
            OnDamagedReaction();
        }

        protected virtual void OnDamagedReaction()
        {
            if (State == EnemyState.Patrol || State == EnemyState.Search || State == EnemyState.Investigate)
                ChangeState(EnemyState.Investigate, "Diserang → cek arah serangan");
        }

        // =====================================================
        // FSM CORE
        // =====================================================

        public void ChangeState(EnemyState newState, string reason)
        {
            Reason = reason;
            if (State == newState)
                return;

            EnemyState previous = State;
            ExitState(State);
            State = newState;
            stateTimer = 0f;
            EnterState(newState);
            ShowAlert(previous, newState);
        }

        protected virtual void EnterState(EnemyState state)
        {
            agent.updateRotation = true;
            unreachableTimer = 0f;

            switch (state)
            {
                case EnemyState.Patrol:
                    agent.speed = patrolSpeed;
                    GoToCurrentWaypoint();
                    break;
                case EnemyState.Investigate:
                    agent.speed = chaseSpeed * 0.8f;
                    pendingInvestigate = false;
                    break;
                case EnemyState.Search:
                    agent.speed = patrolSpeed * 1.2f;
                    nextSearchPointTime = 0f;
                    break;
                case EnemyState.Chase:
                    agent.speed = chaseSpeed;
                    break;
                case EnemyState.Flee:
                    agent.speed = fleeSpeed;
                    currentRetreatPoint = PickRetreatPoint();
                    break;
                case EnemyState.Dead:
                    OnDeath();
                    break;
            }
        }

        protected virtual void ExitState(EnemyState state)
        {
            if (state == EnemyState.Flee)
                currentRetreatPoint = null;
        }

        protected virtual void UpdateState(EnemyState state)
        {
            switch (state)
            {
                case EnemyState.Patrol: UpdatePatrol(); break;
                case EnemyState.Investigate: UpdateInvestigate(); break;
                case EnemyState.Search: UpdateSearch(); break;
                case EnemyState.Chase: UpdateChase(); break;
                case EnemyState.Attack: UpdateAttack(); break;
                case EnemyState.Reposition: UpdateReposition(); break;
                case EnemyState.Flee: UpdateFlee(); break;
            }
        }

        /// <summary>Transisi umum dari state "tenang" ke state waspada.</summary>
        protected bool CheckAlertTransitions()
        {
            if (perception.CanSeeTarget)
            {
                ChangeState(EnemyState.Chase, "Melihat player");
                return true;
            }

            if (pendingInvestigate && memory.HasLastKnownPosition)
            {
                ChangeState(EnemyState.Investigate, "Mendengar suara");
                return true;
            }

            return false;
        }

        protected bool ConsumePendingInvestigate()
        {
            bool value = pendingInvestigate;
            pendingInvestigate = false;
            return value;
        }

        // =====================================================
        // STATES (default)
        // =====================================================

        protected virtual void UpdatePatrol()
        {
            if (CheckAlertTransitions())
                return;

            Reason = "Tidak ada ancaman";

            if (waypoints == null || waypoints.Count == 0)
            {
                UpdateSentry("Pos jaga");
                return;
            }

            if (!agent.pathPending && agent.remainingDistance <= waypointTolerance)
            {
                waypointWaitTimer += Time.deltaTime;
                if (waypointWaitTimer >= waypointWaitTime)
                {
                    waypointWaitTimer = 0f;
                    waypointIndex = (waypointIndex + 1) % waypoints.Count;
                    GoToCurrentWaypoint();
                }
            }
        }

        /// <summary>Berjaga di posisi awal sambil melihat sekeliling.</summary>
        protected void UpdateSentry(string label)
        {
            if (Vector3.Distance(transform.position, homePosition) > waypointTolerance)
            {
                MoveTo(homePosition, label);
                return;
            }

            StopMoving();
            DestinationLabel = label + " (look around)";
            float yaw = Mathf.Sin(Time.time * 0.6f) * 70f;
            transform.rotation = Quaternion.RotateTowards(transform.rotation,
                homeRotation * Quaternion.Euler(0f, yaw, 0f), 90f * Time.deltaTime);
        }

        private void GoToCurrentWaypoint()
        {
            if (waypoints == null || waypoints.Count == 0)
                return;

            waypointIndex = Mathf.Clamp(waypointIndex, 0, waypoints.Count - 1);
            Transform wp = waypoints[waypointIndex];
            if (wp != null)
                MoveTo(wp.position, $"Waypoint {waypointIndex + 1}/{waypoints.Count}");
        }

        public void SetStartWaypoint(int index) => waypointIndex = index;

        protected virtual void UpdateInvestigate()
        {
            if (perception.CanSeeTarget)
            {
                ChangeState(EnemyState.Chase, "Player ditemukan kembali");
                return;
            }

            if (!memory.HasLastKnownPosition)
            {
                ChangeState(EnemyState.Search, "Memory kosong");
                return;
            }

            // suara baru selama investigasi → target diperbarui otomatis lewat memory
            pendingInvestigate = false;

            MoveTo(memory.LastKnownPosition, "Last Known Position");

            // NAVIGATION: Path Partial → LKP tidak bisa dicapai, tunggu di titik terdekat
            if (HandleUnreachable(memory.LastKnownPosition, "Last Known Position tidak bisa dicapai"))
            {
                if (unreachableTimer >= unreachableWaitTime)
                    ChangeState(EnemyState.Search, "Tidak ada jalur ke LKP → mencari di sekitar");
                return;
            }

            bool arrived = !agent.pathPending && agent.remainingDistance <= 1f;
            bool invalidPath = !agent.pathPending && agent.pathStatus == NavMeshPathStatus.PathInvalid;

            if (arrived || invalidPath || stateTimer > investigateTimeout)
                ChangeState(EnemyState.Search, "Sampai di Last Known Position, player tidak ada");
        }

        protected virtual void UpdateSearch()
        {
            if (CheckAlertTransitions())
                return;

            Vector3 center = memory.HasLastKnownPosition ? memory.LastKnownPosition : transform.position;

            if (Time.time >= nextSearchPointTime)
            {
                nextSearchPointTime = Time.time + 1.6f;
                Vector2 rnd = Random.insideUnitCircle * searchRadius;
                Vector3 candidate = center + new Vector3(rnd.x, 0f, rnd.y);
                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                    MoveTo(hit.position, "Titik pencarian");
            }

            if (stateTimer >= searchDuration)
            {
                memory.Forget();
                OnSearchFinished();
            }
        }

        protected virtual void OnSearchFinished()
        {
            ChangeState(EnemyState.Patrol, "Pencarian selesai, kembali patroli");
        }

        /// <summary>Default chase: kejar player, jika hilang → ke Last Known Position.</summary>
        protected virtual void UpdateChase()
        {
            if (!perception.CanSeeTarget)
            {
                ChangeState(EnemyState.Investigate, "Kehilangan Line of Sight");
                return;
            }

            Reason = "Player terlihat";
            MoveTo(target.position, "Player");
            HandleUnreachable(target.position, "Player terlihat tapi tidak ada jalur");
        }

        protected virtual void UpdateAttack()
        {
        }

        protected virtual void UpdateReposition()
        {
        }

        // ---------------- FLEE / RETREAT ----------------

        private bool ShouldFlee()
        {
            return canFlee && !hasFled
                           && State != EnemyState.Flee && State != EnemyState.Dead
                           && health.Normalized <= fleeHealthThreshold;
        }

        /// <summary>
        /// Tactical + Navigation: pilih retreat point berdasarkan PANJANG PATH NavMesh, bukan garis lurus.
        /// Titik dianggap aman jika enemy bisa sampai lebih dulu daripada player
        /// (path enemy lebih pendek), lalu dipilih yang paling jauh dari player.
        /// </summary>
        private Transform PickRetreatPoint()
        {
            Transform best = null;
            float bestScore = float.MinValue;
            RetreatInfo = "tidak ada retreat point";

            foreach (Transform p in retreatPoints)
            {
                if (p == null)
                    continue;

                float myLen = PathLength(transform.position, p.position);
                if (float.IsInfinity(myLen))
                    continue;

                float playerLen = target != null ? PathLength(target.position, p.position) : 99f;
                if (float.IsInfinity(playerLen))
                    playerLen = 99f;

                bool safe = playerLen > myLen + 2f;
                float score = (safe ? 100f : 0f) + playerLen - 0.5f * myLen;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = p;
                    RetreatInfo = $"{p.name}: enemy {myLen:0}m vs player {playerLen:0}m{(safe ? "" : " (tidak aman)")}";
                }
            }

            return best;
        }

        protected virtual void UpdateFlee()
        {
            Vector3 fleeTarget;
            if (currentRetreatPoint != null)
            {
                fleeTarget = currentRetreatPoint.position;
            }
            else
            {
                Vector3 away = target != null ? (transform.position - target.position).normalized : transform.forward;
                fleeTarget = transform.position + away * 6f;
            }

            Vector3 flatDiff = fleeTarget - transform.position;
            flatDiff.y = 0f;
            bool atSafePoint = flatDiff.magnitude <= 1.5f;

            if (!atSafePoint)
            {
                MoveTo(fleeTarget, currentRetreatPoint != null ? "Retreat Point" : "Menjauh dari player");
                Reason = $"HP {health.Normalized:P0} → kabur ke tempat aman";
            }
            else
            {
                StopMoving();
                DestinationLabel = "Di Retreat Point (regen)";
                health.Heal(regenPerSecond * Time.deltaTime);
                HealEffect();
                Reason = $"Memulihkan HP ({health.Normalized:P0})";
                if (target != null)
                    FaceTowards(target.position);
            }

            // Terpojok: player terlalu dekat → melawan balik
            if (perception.CanSeeTarget && perception.DistanceToTarget < 2.2f && stateTimer > 1.5f)
            {
                ChangeState(EnemyState.Chase, "Terpojok → melawan balik");
                return;
            }

            if (health.Normalized >= recoverHealthThreshold)
            {
                if (memory.IsFresh)
                    ChangeState(EnemyState.Investigate, "HP pulih → kembali memburu");
                else
                    ChangeState(EnemyState.Patrol, "HP pulih");
            }
        }

        // ---------------- DEAD ----------------

        private void OnDeath()
        {
            Reason = "Mati";
            StopAllCoroutines();
            IsTraversingLink = false;
            if (agent.isOnNavMesh)
                agent.isStopped = true;
            agent.enabled = false;

            foreach (Collider c in GetComponentsInChildren<Collider>())
                c.enabled = false;

            if (GameManager.Instance != null)
                GameManager.Instance.NotifyEnemyKilled(this);
            StartCoroutine(DeathRoutine());
        }

        private IEnumerator DeathRoutine()
        {
            // Model beranimasi: animasi Death dimainkan oleh CharacterAnimator
            if (characterAnimator != null && characterAnimator.Animator != null)
            {
                yield return new WaitForSeconds(3f);
                gameObject.SetActive(false);
                yield break;
            }

            Quaternion start = transform.rotation;
            Quaternion end = start * Quaternion.Euler(-90f, 0f, 0f);
            float t = 0f;
            while (t < 0.4f)
            {
                t += Time.deltaTime;
                transform.rotation = Quaternion.Slerp(start, end, t / 0.4f);
                yield return null;
            }

            yield return new WaitForSeconds(2.5f);
            gameObject.SetActive(false);
        }

        // =====================================================
        // NAVIGATION & STEERING HELPERS
        // =====================================================

        protected void MoveTo(Vector3 position, string label)
        {
            if (!agent.isOnNavMesh || IsTraversingLink)
                return;

            agent.isStopped = false;
            if (!HasDestination || (CurrentDestination - position).sqrMagnitude > 0.09f || !agent.hasPath)
                agent.SetDestination(position);

            CurrentDestination = position;
            HasDestination = true;
            DestinationLabel = label;
        }

        protected void StopMoving()
        {
            if (agent.isOnNavMesh && !IsTraversingLink)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
            HasDestination = false;
            DestinationLabel = "-";
        }

        protected void FaceTowards(Vector3 position)
        {
            Vector3 dir = position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f)
                return;

            transform.rotation = Quaternion.RotateTowards(transform.rotation,
                Quaternion.LookRotation(dir), turnSpeed * Time.deltaTime);
        }

        // =====================================================
        // FEEDBACK (VFX)
        // =====================================================

        /// <summary>
        /// Ikon di atas kepala saat AI berubah dari tenang ke waspada:
        ///   "!" = melihat player (Chase)   "?" = curiga / mendengar sesuatu (Investigate)
        /// </summary>
        private void ShowAlert(EnemyState from, EnemyState to)
        {
            bool calm = from == EnemyState.Patrol || from == EnemyState.Guard || from == EnemyState.Search
                        || from == EnemyState.Return;
            Vector3 head = transform.position + Vector3.up * alertHeight;
            if (to == EnemyState.Chase && (calm || from == EnemyState.Investigate))
            {
                VFXManager.Alert(transform, "!", new Color(1f, 0.85f, 0.2f), alertHeight);
                AudioManager.Play(Sfx.AlertSpotted, head);
            }
            else if (to == EnemyState.Investigate && calm)
            {
                VFXManager.Alert(transform, "?", new Color(0.6f, 0.9f, 1f), alertHeight);
                AudioManager.Play(Sfx.AlertSuspicious, head);
            }
            else if (to == EnemyState.Flee)
            {
                VFXManager.Alert(transform, "!!", new Color(0.4f, 1f, 0.5f), alertHeight);
                AudioManager.Play(Sfx.AlertFlee, head);
                AudioManager.Play(Sfx.SwordWhoosh, head, 0.6f, 0.7f);
            }
        }

        protected void HealEffect()
        {
            if (Time.time < nextHealFxTime)
                return;
            nextHealFxTime = Time.time + 0.3f;
            VFXManager.Heal(transform.position, 2);
        }

        /// <summary>Dorongan singkat saat terkena serangan (dikurangi oleh knockbackScale).</summary>
        public void ApplyKnockback(Vector3 velocity)
        {
            velocity.y = 0f;
            knockbackVelocity = velocity * knockbackScale;
        }

        private void ApplyKnockbackMotion()
        {
            if (knockbackVelocity.sqrMagnitude < 0.0001f || !agent.isOnNavMesh || agent.isOnOffMeshLink)
                return;
            agent.Move(knockbackVelocity * Time.deltaTime);
            knockbackVelocity = Vector3.MoveTowards(knockbackVelocity, Vector3.zero, 25f * Time.deltaTime);
        }

        protected void PlayAttackAnimation()
        {
            if (characterAnimator != null)
                characterAnimator.Attack();
        }

        protected float DistanceToTarget =>
            target != null ? Vector3.Distance(transform.position, target.position) : Mathf.Infinity;

        /// <summary>
        /// Path Partial: tujuan tidak bisa dicapai (mis. di balik gerbang tertutup).
        /// Agent tetap berjalan ke ujung path (titik terdekat), lalu menunggu sambil menghadap tujuan.
        /// </summary>
        protected bool HandleUnreachable(Vector3 goal, string reason)
        {
            if (agent.pathPending || agent.pathStatus != NavMeshPathStatus.PathPartial)
            {
                unreachableTimer = 0f;
                return false;
            }

            DestinationLabel = "Titik terdekat yang bisa dicapai";
            Reason = reason + " → tunggu di titik terdekat";
            if (agent.remainingDistance <= 1f)
            {
                unreachableTimer += Time.deltaTime;
                FaceTowards(goal);
            }
            return true;
        }

        /// <summary>Panjang path NavMesh (memakai area cost agent ini). Infinity jika tidak ada path lengkap.</summary>
        public float PathLength(Vector3 from, Vector3 to)
        {
            if (!NavMesh.SamplePosition(from, out NavMeshHit a, 2f, NavMesh.AllAreas) ||
                !NavMesh.SamplePosition(to, out NavMeshHit b, 2f, NavMesh.AllAreas))
                return Mathf.Infinity;

            if (!NavMesh.CalculatePath(a.position, b.position, queryFilter, scratchPath) ||
                scratchPath.status != NavMeshPathStatus.PathComplete)
                return Mathf.Infinity;

            return CornersLength(scratchPath.corners);
        }

        public static float CornersLength(Vector3[] corners)
        {
            float len = 0f;
            for (int i = 1; i < corners.Length; i++)
                len += Vector3.Distance(corners[i - 1], corners[i]);
            return len;
        }

        /// <summary>Nama area NavMesh di bawah enemy (Walkable / Trap / ...).</summary>
        public string CurrentAreaName
        {
            get
            {
                if (!agent.enabled || !NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 1f, NavMesh.AllAreas))
                    return "-";
                int index = 0;
                int mask = hit.mask;
                while (mask > 1)
                {
                    mask >>= 1;
                    index++;
                }
                return index == TrapArea ? "Trap" : index == 0 ? "Walkable" : $"Area {index}";
            }
        }

        /// <summary>NavMeshLink: lompatan parabola dari ujung link awal ke ujung link akhir.</summary>
        private IEnumerator TraverseLinkRoutine()
        {
            IsTraversingLink = true;
            HasDestination = false;
            OffMeshLinkData data = agent.currentOffMeshLinkData;
            Vector3 start = transform.position;
            Vector3 end = data.endPos + Vector3.up * agent.baseOffset;
            float duration = Mathf.Max(0.35f, Vector3.Distance(start, end) / linkJumpSpeed);

            Vector3 flat = end - start;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(flat);

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / duration;
                float clamped = Mathf.Clamp01(t);
                Vector3 pos = Vector3.Lerp(start, end, clamped);
                pos.y += linkJumpHeight * 4f * clamped * (1f - clamped);
                transform.position = pos;
                yield return null;
            }

            if (agent.isOnOffMeshLink)
                agent.CompleteOffMeshLink();
            LinksTraversed++;
            VFXManager.Smoke(transform.position + Vector3.up * 0.1f, new Color(0.5f, 0.46f, 0.42f, 0.7f), 8, 0.8f, 2f);
            VFXManager.Shake(0.08f);
            AudioManager.Play(Sfx.LinkLand, transform.position);
            IsTraversingLink = false;
        }

        /// <summary>Steering behavior: Separation antar enemy.</summary>
        private void ApplySeparation()
        {
            Vector3 push = Vector3.zero;
            foreach (EnemyAI other in All)
            {
                if (other == this || other.State == EnemyState.Dead)
                    continue;

                Vector3 diff = transform.position - other.transform.position;
                diff.y = 0f;
                float dist = diff.magnitude;
                if (dist > 0.001f && dist < separationRadius)
                    push += diff / dist * (1f - dist / separationRadius);
            }

            SeparationForce = push * separationStrength;
            if (push.sqrMagnitude > 0.0001f && agent.isOnNavMesh && !agent.isOnOffMeshLink)
                agent.Move(SeparationForce * Time.deltaTime);
        }

        protected virtual void OnDrawGizmosSelected()
        {
            if (memory != null && memory.HasLastKnownPosition)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(memory.LastKnownPosition, 0.5f);
                Gizmos.DrawLine(transform.position, memory.LastKnownPosition);
            }

            Gizmos.color = Color.blue;
            foreach (Transform wp in waypoints)
                if (wp != null)
                    Gizmos.DrawWireCube(wp.position, Vector3.one * 0.4f);

            Gizmos.color = Color.green;
            foreach (Transform rp in retreatPoints)
                if (rp != null)
                    Gizmos.DrawWireSphere(rp.position, 0.6f);
        }
    }
}
