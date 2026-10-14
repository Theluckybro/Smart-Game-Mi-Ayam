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
        Attack,      // menyerang (melee / menembak)
        Reposition,  // Maintain Distance (ranged)
        Dead
    }

    /// <summary>
    /// Base class enemy berbasis Finite State Machine.
    ///
    /// Pipeline AI per frame:
    ///   Perception (vision + hearing) → Memory → Decision (FSM) → Navigation (NavMesh) + Steering (Separation)
    ///
    /// Subclass (Melee / Ranged) meng-override state tertentu.
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

        [Header("Steering - Separation")]
        [SerializeField] protected float separationRadius = 1.6f;
        [SerializeField] protected float separationStrength = 2.5f;

        // ---- Komponen ----
        protected NavMeshAgent agent;
        protected AIPerception perception;
        protected AIMemory memory;
        protected Health health;
        protected HitFlash hitFlash;
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

        // ---- Data untuk AI Debug Mode ----
        public string DisplayName => displayName;
        public EnemyState State { get; private set; } = EnemyState.Patrol;
        public string Reason { get; protected set; } = "";
        public Vector3 CurrentDestination { get; private set; }
        public bool HasDestination { get; private set; }
        public string DestinationLabel { get; private set; } = "-";
        public Vector3 SeparationForce { get; private set; }
        public NavMeshAgent Agent => agent;
        public AIPerception Perception => perception;
        public AIMemory Memory => memory;
        public Health Health => health;
        public Transform Target => target;
        public Vector3 HomePosition => homePosition;
        public virtual string ExtraDebugInfo => "";

        public void Setup(string name, Transform player, List<Transform> patrolPoints)
        {
            displayName = name;
            waypoints = patrolPoints ?? new List<Transform>();
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

            if (target == null && perception.Target != null)
                SetTarget(perception.Target);
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
            EnterState(EnemyState.Patrol);
        }

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

            // 3. DECISION — jalankan state FSM saat ini
            stateTimer += Time.deltaTime;
            UpdateState(State);

            // 4. STEERING — separation agar enemy tidak bertumpuk
            ApplySeparation();
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

            if (State == EnemyState.Patrol || State == EnemyState.Search || State == EnemyState.Investigate)
            {
                ChangeState(EnemyState.Investigate, "Diserang → cek arah serangan");
            }
        }

        // =====================================================
        // FSM CORE
        // =====================================================

        public void ChangeState(EnemyState newState, string reason)
        {
            Reason = reason;
            if (State == newState)
                return;

            ExitState(State);
            State = newState;
            stateTimer = 0f;
            EnterState(newState);
        }

        protected virtual void EnterState(EnemyState state)
        {
            agent.updateRotation = true;

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
                case EnemyState.Dead:
                    OnDeath();
                    break;
            }
        }

        protected virtual void ExitState(EnemyState state)
        {
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
                // Sentry: berjaga di posisi awal sambil melihat sekeliling
                if (Vector3.Distance(transform.position, homePosition) > waypointTolerance)
                {
                    MoveTo(homePosition, "Pos jaga");
                }
                else
                {
                    StopMoving();
                    DestinationLabel = "Pos jaga (look around)";
                    float yaw = Mathf.Sin(Time.time * 0.6f) * 70f;
                    transform.rotation = Quaternion.RotateTowards(transform.rotation,
                        homeRotation * Quaternion.Euler(0f, yaw, 0f), 90f * Time.deltaTime);
                }
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
        }

        protected virtual void UpdateAttack()
        {
        }

        protected virtual void UpdateReposition()
        {
        }

        // ---------------- DEAD ----------------

        private void OnDeath()
        {
            Reason = "Mati";
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
            if (!agent.isOnNavMesh)
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
            if (agent.isOnNavMesh)
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

        protected float DistanceToTarget =>
            target != null ? Vector3.Distance(transform.position, target.position) : Mathf.Infinity;

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
            if (push.sqrMagnitude > 0.0001f && agent.isOnNavMesh)
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
        }
    }
}
