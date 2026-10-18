using System.Collections.Generic;
using DungeonHunter.BT;
using UnityEngine;
using UnityEngine.AI;

namespace DungeonHunter
{
    /// <summary>
    /// Guardian Behavior — boss penjaga Artifact, keputusan memakai BEHAVIOR TREE.
    ///
    /// Tiga phase, skill bertambah setiap phase:
    ///   Phase 1 (HP > 66%)  WARDEN      : Kejar, Ground Slam, Summon (jika player menjaga jarak)
    ///   Phase 2 (HP ≤ 66%)  NECROMANCER : + gelombang minion, Bone Eruption (duri tulang di posisi & prediksi player)
    ///   Phase 3 (HP ≤ 33%)  ENRAGED     : + lebih cepat, Triple Slam, Charge, Bone Eruption 5 titik
    ///
    /// Root (Selector)
    ///  ├─ Phase 2       : [HP ≤ 66%]                                 → Bangkitkan minion (gelombang pertama)
    ///  ├─ Phase 3       : [HP ≤ 33%]                                 → Enrage!
    ///  ├─ Leash         : [Player keluar arena]                      → Kembali ke pos + regen
    ///  ├─ Summon        : [Minion &lt; batas phase] [player jauh / Phase ≥ 2] [siap] → Bangkitkan minion
    ///  ├─ Ground Slam   : [Jarak ≤ 2.6 m & siap]                     → Slam (Phase 3: Triple Slam)
    ///  ├─ Charge        : [Phase 3] [Jarak 4.5–10 m & siap]          → Terjang
    ///  ├─ Bone Eruption : [Phase ≥ 2] [Jarak 5–16 m & terlihat & siap] → Duri tulang
    ///  ├─ Kejar         : [Player / LKP di dalam arena]              → Kejar
    ///  └─ Jaga pos      (fallback)
    ///
    /// Semua skill bersifat "committed": sekali mulai, diselesaikan dulu sebelum cabang lain mengambil alih.
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

        [Header("Summon Minion (skill semua phase)")]
        [SerializeField] private EnemyAI minionPrefab;
        [SerializeField] private float summonWindup = 1.3f;
        [SerializeField] private float summonRadius = 4f;
        [Tooltip("Player menjaga jarak lebih dari ini ...")]
        [SerializeField] private float kiteDistance = 8f;
        [Tooltip("... selama sekian detik → Guardian memanggil minion")]
        [SerializeField] private float kiteTime = 1.5f;
        [SerializeField] private int[] maxMinionsPerPhase = { 2, 3, 3 };
        [SerializeField] private float[] summonCooldownPerPhase = { 14f, 10f, 12f };

        [Header("Phase 2 - Necromancer")]
        [SerializeField, Range(0f, 1f)] private float summonThreshold = 0.66f;
        [SerializeField] private int firstWaveSize = 3;

        [Header("Bone Eruption (Phase 2+)")]
        [SerializeField] private float eruptMinRange = 5f;
        [SerializeField] private float eruptMaxRange = 16f;
        [SerializeField] private float eruptRadius = 1.5f;
        [SerializeField] private float eruptDamage = 18f;
        [SerializeField] private float eruptWindup = 0.9f;
        [SerializeField] private float eruptCooldown = 6f;

        [Header("Phase 3 - Enrage")]
        [SerializeField, Range(0f, 1f)] private float enrageThreshold = 0.33f;
        [SerializeField] private float enrageDuration = 1.2f;
        [SerializeField] private float enrageSpeedMultiplier = 1.4f;
        [SerializeField] private Color enrageColor = new Color(0.8f, 0.1f, 1f);

        [Header("Charge (Phase 3)")]
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
        private bool necromancer;
        private bool summoning;
        private bool phaseWave;   // summon yang sedang berjalan = gelombang transisi Phase 2
        private bool erupting;
        private bool slamming;
        private bool charging;
        private bool chargeDashing;
        private bool chargeHit;
        private float actionTimer;
        private float nextSlamTime;
        private float nextChargeTime;
        private float nextSummonTime;
        private float nextEruptTime;
        private float kiteTimer;
        private int slamsLeft;
        private float currentSlamRadius;
        private Vector3 chargeDirection;
        private float nextDustTime;
        private ParticleSystem enrageAura;
        private PlayerController playerController;

        private readonly List<EnemyAI> minions = new List<EnemyAI>();
        private readonly List<Vector3> summonPoints = new List<Vector3>();
        private readonly List<Vector3> eruptPoints = new List<Vector3>();

        private static readonly Color SlamColor = new Color(1f, 0.35f, 0.15f);
        private static readonly Color EnrageColor = new Color(0.75f, 0.35f, 1f);
        private static readonly Color NecroColor = new Color(0.35f, 1f, 0.6f);
        private static readonly Color BoneColor = new Color(0.85f, 1f, 0.8f);
        private static readonly Color ChargeLineColor = new Color(1f, 0.55f, 0.1f);

        public float LeashRadius => leashRadius;
        public float SlamRadius => currentSlamRadius > 0f ? currentSlamRadius : slamRadius;
        public bool IsWindingUp => slamming;
        public bool IsCharging => charging;
        public bool IsEnraged => enraged;
        public bool IsSummoning => summoning;

        /// <summary>1 = Warden, 2 = Necromancer, 3 = Enraged.</summary>
        public int Phase => enraged || enraging ? 3 : necromancer || (summoning && phaseWave) ? 2 : 1;
        public int AliveMinions => minions.FindAll(m => m != null && m.State != EnemyState.Dead).Count;
        public int MaxMinions => maxMinionsPerPhase[Mathf.Clamp(Phase - 1, 0, maxMinionsPerPhase.Length - 1)];
        public float SummonThreshold => summonThreshold;
        public float EnrageThreshold => enrageThreshold;
        public Vector3 ChargeDirection => chargeDirection;
        public string BTPath => tree != null ? tree.Context.ActivePath : "-";

        public override string ExtraDebugInfo =>
            $"BT : {BTPath} [{tree?.Context.LastStatus}]\n" +
            $"Phase : {PhaseName}   Minion : {AliveMinions}/{MaxMinions}\n" +
            $"Kiting : {kiteTimer:0.0}/{kiteTime:0.0}s   CD summon {Mathf.Max(0f, nextSummonTime - Time.time):0}s  erupt {Mathf.Max(0f, nextEruptTime - Time.time):0}s\n" +
            $"Leash : {Vector3.Distance(target != null ? target.position : homePosition, homePosition):0.0}/{leashRadius:0}m";

        public string PhaseName => Phase == 3 ? "3 ENRAGED" : Phase == 2 ? "2 NECROMANCER" : "1 WARDEN";

        protected override EnemyState InitialState => EnemyState.Guard;

        protected override void Awake()
        {
            base.Awake();
            canFlee = false;
            BuildTree();
        }

        protected override void Start()
        {
            base.Start();
            if (target != null)
                playerController = target.GetComponent<PlayerController>();
        }

        private void BuildTree()
        {
            tree = new BehaviorTree(
                new BTSelector("Root",
                    new BTSequence("Phase 2",
                        new BTCondition("HP ≤ 66%", () => (summoning && phaseWave) || (!Busy && !necromancer && health.Normalized <= summonThreshold)),
                        new BTAction("Bangkitkan minion", DoSummon)),
                    new BTSequence("Phase 3",
                        new BTCondition("HP ≤ 33%", () => enraging || (!Busy && necromancer && !enraged && health.Normalized <= enrageThreshold)),
                        new BTAction("Enrage!", DoEnrage)),
                    new BTSequence("Leash",
                        new BTCondition("Player keluar arena", () => !Busy && engaged && !TargetInsideArena),
                        new BTAction("Kembali ke pos + regen", DoReturn)),
                    new BTSequence("Summon",
                        new BTCondition("Minion < batas phase", () => (summoning && !phaseWave) || AliveMinions < MaxMinions),
                        new BTCondition("Player menjaga jarak / Phase ≥ 2", () => (summoning && !phaseWave) || CanSummon()),
                        new BTAction("Bangkitkan minion", DoSummon)),
                    new BTSequence("Ground Slam",
                        new BTCondition("Jarak ≤ 2.6 m & siap",
                            () => slamming || (!Busy && Aware && DistanceToTarget <= slamTriggerRange && Time.time >= nextSlamTime)),
                        new BTAction("Slam", DoSlam)),
                    new BTSequence("Charge",
                        new BTCondition("Phase 3", () => charging || enraged),
                        new BTCondition("Jarak 4.5–10 m & siap", () => charging || CanCharge()),
                        new BTAction("Terjang", DoCharge)),
                    new BTSequence("Bone Eruption",
                        new BTCondition("Phase ≥ 2", () => erupting || necromancer),
                        new BTCondition("Jarak 5–16 m & terlihat & siap", () => erupting || CanErupt()),
                        new BTAction("Duri tulang", DoErupt)),
                    new BTSequence("Kejar",
                        new BTCondition("Player / LKP di arena", () => Aware),
                        new BTAction("Kejar", DoChase)),
                    new BTAction("Jaga pos", DoGuard)));
        }

        // =====================================================
        // KONDISI
        // =====================================================

        private bool Busy => slamming || charging || enraging || summoning || erupting;

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

        /// <summary>
        /// Summon dipakai jika player "kiting" (menjaga jarak > 8 m selama 1.5 s) — minion menutup jarak
        /// yang tidak bisa dikejar Guardian. Mulai Phase 2, summon juga dipakai setiap jumlah minion berkurang.
        /// </summary>
        private bool CanSummon()
        {
            if (Busy || !Aware || minionPrefab == null || Time.time < nextSummonTime)
                return false;
            bool kiting = kiteTimer >= kiteTime;
            return kiting || Phase >= 2;
        }

        private bool CanCharge()
        {
            if (Busy || !perception.CanSeeTarget || !TargetInsideArena || Time.time < nextChargeTime)
                return false;
            float d = DistanceToTarget;
            return d >= chargeMinDistance && d <= chargeMaxDistance;
        }

        private bool CanErupt()
        {
            if (Busy || !perception.CanSeeTarget || !TargetInsideArena || Time.time < nextEruptTime)
                return false;
            float d = DistanceToTarget;
            return d >= eruptMinRange && d <= eruptMaxRange;
        }

        // =====================================================
        // FSM HOOK → BEHAVIOR TREE
        // =====================================================

        protected override void UpdateState(EnemyState state)
        {
            // hitung berapa lama player menjaga jarak (dipakai kondisi Summon)
            bool far = Aware && DistanceToTarget > kiteDistance;
            kiteTimer = far ? kiteTimer + Time.deltaTime : Mathf.Max(0f, kiteTimer - Time.deltaTime * 2f);
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

        protected override void OnEnable()
        {
            base.OnEnable();
            health.Died += OnGuardianDied;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            health.Died -= OnGuardianDied;
        }

        // =====================================================
        // PHASE 3: ENRAGE
        // =====================================================

        private BTStatus DoEnrage()
        {
            engaged = true;
            if (!enraging)
            {
                enraging = true;
                actionTimer = enrageDuration;
                StopMoving();
                ChangeState(EnemyState.Enrage, $"HP {health.Normalized:P0} ≤ 33% → Phase 3");
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
                AudioManager.Play(Sfx.EnrageBoom, transform.position);
                TopDownCamera.Focus(transform.position, 1.6f, 0.7f, 0.6f);
                AudioManager.Play(Sfx.Gong, transform.position, 1f, 0.9f, 0.1f);
                AudioManager.Play(Sfx.SlamWindup, transform.position, 1.2f, 0.85f);
                if (enrageAura == null)
                    enrageAura = VFXManager.Aura(transform, EnrageColor, 1.1f, 3f);
                if (GameManager.Instance != null)
                    GameManager.Instance.ShowMessage("Guardian mengamuk! (Phase 3: Triple Slam + Charge)");
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
            eruptCooldown *= 0.75f;
            eruptWindup *= 0.8f;
            return BTStatus.Success;
        }

        // =====================================================
        // SUMMON MINION
        // =====================================================

        /// <summary>
        /// Summon: Guardian berhenti, mengangkat tangan (taunt), lingkaran hijau muncul di titik-titik NavMesh
        /// di sekitar arena (telegraph), lalu minion bangkit dari tanah dan langsung mengejar player.
        /// Gelombang pertama Phase 2 selalu 3 minion; selanjutnya mengisi sampai batas phase.
        /// </summary>
        private BTStatus DoSummon()
        {
            engaged = true;
            if (!summoning)
            {
                summoning = true;
                bool firstWave = !necromancer && health.Normalized <= summonThreshold;
                phaseWave = firstWave;
                bool kiting = kiteTimer >= kiteTime;
                actionTimer = summonWindup;
                StopMoving();
                ChangeState(EnemyState.Summon, firstWave
                    ? $"HP {health.Normalized:P0} ≤ 66% → Phase 2: bangkitkan minion"
                    : kiting ? $"Player menjaga jarak {DistanceToTarget:0.0} m → panggil minion"
                             : $"Minion {AliveMinions}/{MaxMinions} → panggil bala bantuan");
                if (characterAnimator != null)
                    characterAnimator.Taunt();
                if (hitFlash != null)
                    hitFlash.Flash(NecroColor, summonWindup);

                // gelombang Phase 2 tetap terasa besar (min 2), tapi total minion tidak menumpuk berlebihan
                int count = firstWave ? Mathf.Max(2, firstWaveSize - AliveMinions) : Mathf.Max(1, MaxMinions - AliveMinions);
                PickSummonPoints(count);
                foreach (Vector3 p in summonPoints)
                    VFXManager.Telegraph(p, 1.1f, summonWindup, NecroColor);
                VFXManager.Ring(transform.position, NecroColor, 6f, 0.6f);
                VFXManager.Glow(transform.position + Vector3.up * 2f, NecroColor, 4f, 0.4f);
                AudioManager.Play(Sfx.SlamWindup, transform.position, 1.1f, 0.7f);
                AudioManager.Play(Sfx.Gong, transform.position, 0.6f, 1.3f);
                NoiseSystem.Emit(transform.position, 12f);

                if (firstWave)
                {
                    VFXManager.FloatingLabel(transform.position + Vector3.up * 4f, "RISE, MY MINIONS!", NecroColor, 32f);
                    TopDownCamera.Focus(transform.position, 1.6f, 0.75f, 0.6f);
                    if (GameManager.Instance != null)
                        GameManager.Instance.ShowMessage("Guardian membangkitkan minion! (Phase 2: Necromancer + Bone Eruption)");
                }
                else
                {
                    VFXManager.FloatingLabel(transform.position + Vector3.up * 4f, "ARISE!", NecroColor, 26f);
                }
            }

            if (target != null)
                FaceTowards(target.position);

            actionTimer -= Time.deltaTime;
            if (actionTimer > 0f)
                return BTStatus.Running;

            foreach (Vector3 p in summonPoints)
                SpawnMinion(p);

            summoning = false;
            if (health.Normalized <= summonThreshold)
                necromancer = true;
            kiteTimer = 0f;
            nextSummonTime = Time.time + summonCooldownPerPhase[Mathf.Clamp(Phase - 1, 0, summonCooldownPerPhase.Length - 1)];
            return BTStatus.Success;
        }

        /// <summary>
        /// Titik spawn di NavMesh dalam arena. Saat player kiting, minion muncul di antara Guardian dan player
        /// (memotong jalan player); selain itu melingkar di sekitar Guardian.
        /// </summary>
        private void PickSummonPoints(int count)
        {
            summonPoints.Clear();
            Vector3 center = transform.position;
            float radius = summonRadius;
            if (target != null && kiteTimer >= kiteTime)
            {
                center = Vector3.Lerp(transform.position, target.position, 0.6f);
                radius = 2.5f;
            }

            float offset = Random.value * 360f;
            for (int i = 0; i < count * 3 && summonPoints.Count < count; i++)
            {
                float angle = (offset + i * 360f / count) * Mathf.Deg2Rad;
                Vector3 candidate = center + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * radius;
                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 1.5f, NavMesh.AllAreas) && InsideArena(hit.position))
                    summonPoints.Add(hit.position);
            }
        }

        private void SpawnMinion(Vector3 position)
        {
            Vector3 look = target != null ? target.position - position : -transform.forward;
            look.y = 0f;
            EnemyAI minion = Instantiate(minionPrefab, position, Quaternion.LookRotation(look.sqrMagnitude > 0.01f ? look : Vector3.forward));
            minion.gameObject.SetActive(true);
            minion.Setup("Skeleton Minion", target, new List<Transform>(), new List<Transform>());
            minion.AggroOnSpawn();
            if (minion.GetComponent<EnemyDebugVisual>() == null)
                minion.gameObject.AddComponent<EnemyDebugVisual>();
            minions.Add(minion);

            VFXManager.Ring(position, NecroColor, 2.2f, 0.4f);
            VFXManager.Smoke(position + Vector3.up * 0.2f, new Color(0.3f, 0.45f, 0.35f, 0.8f), 10, 1f, 2f);
            VFXManager.Debris(position + Vector3.up * 0.3f, new Color(0.9f, 0.86f, 0.75f), 8, 4f, 0.12f);
            VFXManager.Sparks(position + Vector3.up * 0.5f, Vector3.up, NecroColor, 12, 5f, 0.8f);
            AudioManager.Play(Sfx.BoneBreak, position, 0.9f, 0.8f);
            AudioManager.PlayBurst(Sfx.SwordHitBone, position, 3, 0.06f, 0.7f);
        }

        /// <summary>Guardian kalah → semua minion ikut runtuh.</summary>
        private void OnGuardianDied()
        {
            foreach (EnemyAI m in minions)
                if (m != null && m.State != EnemyState.Dead)
                    m.Health.TakeDamage(9999f, transform.position);
        }

        // =====================================================
        // BONE ERUPTION (Phase 2+)
        // =====================================================

        /// <summary>
        /// Guardian menghantam tanah dari jauh: lingkaran muncul di posisi player SEKARANG dan posisi PREDIKSI
        /// (posisi + kecepatan × waktu windup), lalu duri tulang menyembul. Player yang terus bergerak lurus
        /// akan kena; harus berbelok atau dodge. Phase 3: 5 titik dan windup lebih cepat.
        /// </summary>
        private BTStatus DoErupt()
        {
            engaged = true;
            if (!erupting)
            {
                erupting = true;
                actionTimer = eruptWindup;
                StopMoving();
                ChangeState(EnemyState.Attack, $"Player menjaga jarak {DistanceToTarget:0.0} m → Bone Eruption");
                PlayAttackAnimation();
                if (hitFlash != null)
                    hitFlash.Flash(NecroColor, eruptWindup);

                PickEruptPoints();
                foreach (Vector3 p in eruptPoints)
                    VFXManager.Telegraph(p, eruptRadius, eruptWindup, BoneColor);
                VFXManager.Glow(transform.position + Vector3.up * 1.5f, NecroColor, 3f, 0.3f);
                AudioManager.Play(Sfx.SlamWindup, transform.position, 0.9f, 1.2f);
                AudioManager.Play(Sfx.MageCharge, target.position, 1.5f, 0.6f);
            }

            if (target != null)
                FaceTowards(target.position);

            actionTimer -= Time.deltaTime;
            if (actionTimer > 0f)
                return BTStatus.Running;

            bool hit = false;
            foreach (Vector3 p in eruptPoints)
            {
                BoneSpikes.Spawn(p, eruptRadius, BoneColor);
                VFXManager.Debris(p + Vector3.up * 0.3f, new Color(0.9f, 0.86f, 0.75f), 8, 6f, 0.12f);
                VFXManager.Smoke(p + Vector3.up * 0.1f, new Color(0.45f, 0.4f, 0.36f, 0.7f), 5, 0.9f, 1.5f);
                VFXManager.Ring(p, BoneColor, eruptRadius * 1.3f, 0.3f);
                AudioManager.Play(Sfx.SpikeStrike, p, 1.4f, 0.8f);
                AudioManager.Play(Sfx.BoneBreak, p, 0.7f, 0.7f);

                Vector3 d = target.position - p;
                d.y = 0f;
                if (!hit && targetHealth != null && d.magnitude <= eruptRadius)
                {
                    hit = true;
                    targetHealth.TakeDamage(eruptDamage, p);
                }
            }
            AudioManager.Play(Sfx.SlamRocks, eruptPoints.Count > 0 ? eruptPoints[0] : transform.position, 0.6f);
            VFXManager.Shake(0.25f);

            erupting = false;
            nextEruptTime = Time.time + eruptCooldown;
            return BTStatus.Success;
        }

        private void PickEruptPoints()
        {
            eruptPoints.Clear();
            Vector3 now = target.position;
            Vector3 velocity = playerController != null ? playerController.Velocity : Vector3.zero;
            velocity.y = 0f;
            Vector3 predicted = now + Vector3.ClampMagnitude(velocity * eruptWindup, 5f);

            AddEruptPoint(now);
            AddEruptPoint(predicted);
            // titik ketiga: di antara keduanya / di samping arah gerak
            Vector3 side = velocity.sqrMagnitude > 0.1f ? Vector3.Cross(Vector3.up, velocity.normalized) : Random.insideUnitSphere;
            side.y = 0f;
            AddEruptPoint(now + side.normalized * eruptRadius * 1.8f);
            if (Phase >= 3)
            {
                AddEruptPoint(now - side.normalized * eruptRadius * 1.8f);
                AddEruptPoint(predicted + velocity.normalized * eruptRadius * 1.6f);
            }
        }

        private void AddEruptPoint(Vector3 p)
        {
            if (!NavMesh.SamplePosition(p, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                return;
            foreach (Vector3 existing in eruptPoints)
                if (Vector3.Distance(existing, hit.position) < eruptRadius)
                    return;
            eruptPoints.Add(hit.position);
        }

        // =====================================================
        // LEASH
        // =====================================================

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

        // =====================================================
        // GROUND SLAM / TRIPLE SLAM
        // =====================================================

        /// <summary>Phase 1–2: satu Slam. Phase 3: Triple Slam — tiga hentakan beruntun, makin lebar, sambil maju.</summary>
        private BTStatus DoSlam()
        {
            engaged = true;
            if (!slamming)
            {
                slamming = true;
                slamsLeft = Phase >= 3 ? 3 : 1;
                currentSlamRadius = slamRadius;
                BeginSlamWindup(slamWindup);
                ChangeState(EnemyState.Attack, slamsLeft > 1 ? "Phase 3 → Triple Slam" : "Player dalam jangkauan → Ground Slam");
            }

            // combo: sedikit maju mengejar player di sela hentakan
            if (slamsLeft < 3 && Phase >= 3 && target != null && agent.isOnNavMesh)
            {
                Vector3 to = target.position - transform.position;
                to.y = 0f;
                if (to.magnitude > 1.5f)
                    agent.Move(to.normalized * 2.5f * Time.deltaTime);
                FaceTowards(target.position);
            }

            actionTimer -= Time.deltaTime;
            if (actionTimer > 0f)
                return BTStatus.Running;

            Slam(currentSlamRadius);
            slamsLeft--;
            if (slamsLeft > 0)
            {
                currentSlamRadius *= 1.2f;
                BeginSlamWindup(slamWindup * 0.55f);
                return BTStatus.Running;
            }

            slamming = false;
            currentSlamRadius = slamRadius;
            nextSlamTime = Time.time + slamCooldown * (Phase >= 3 ? 1.6f : 1f);
            return BTStatus.Success;
        }

        private void BeginSlamWindup(float windup)
        {
            actionTimer = windup;
            StopMoving();
            PlayAttackAnimation();
            VFXManager.Telegraph(transform.position, currentSlamRadius, windup, SlamColor);
            AudioManager.Play(Sfx.SlamWindup, transform.position);
            if (hitFlash != null)
                hitFlash.Flash(slamColor, windup);
        }

        private void Slam(float radius)
        {
            NoiseSystem.Emit(transform.position, 6f);

            Vector3 p = transform.position;
            VFXManager.Ring(p, SlamColor, radius * 1.35f, 0.45f);
            VFXManager.Ring(p, new Color(1f, 0.85f, 0.6f), radius * 0.8f, 0.3f);
            VFXManager.Glow(p + Vector3.up * 0.5f, SlamColor, 5f, 0.18f);
            VFXManager.Smoke(p + Vector3.up * 0.2f, new Color(0.45f, 0.4f, 0.36f, 0.85f), 18, 1.4f, 5f);
            VFXManager.Debris(p + Vector3.up * 0.3f, new Color(0.4f, 0.38f, 0.42f), 14, 6f, 0.18f);
            VFXManager.Sparks(p + Vector3.up * 0.3f, Vector3.up, new Color(1f, 0.7f, 0.3f), 20, 7f, 1.4f);
            VFXManager.Shake(target != null && Vector3.Distance(target.position, p) < 9f ? 0.6f : 0.3f);
            AudioManager.Play(Sfx.SlamBoom, p);
            AudioManager.Play(Sfx.SlamRocks, p);
            if (targetHealth == null)
                return;

            Vector3 diff = target.position - transform.position;
            diff.y = 0f;
            if (diff.magnitude <= radius)
                targetHealth.TakeDamage(slamDamage, transform.position);
        }

        // =====================================================
        // CHARGE (Phase 3)
        // =====================================================

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
                ChangeState(EnemyState.Charge, $"Phase 3, jarak {DistanceToTarget:0.0}m → Charge");
                Vector3 toPlayer = target != null ? target.position - transform.position : transform.forward;
                VFXManager.LineTelegraph(transform.position, toPlayer, chargeSpeed * chargeDuration + 1f, 1.8f, chargeWindup, ChargeLineColor);
                AudioManager.Play(Sfx.ChargeWindup, transform.position);
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
                AudioManager.PlayAttached(Sfx.ChargeRumble, transform, chargeDuration + 0.2f);
                AudioManager.Play(Sfx.GuardianStep, transform.position, 1.3f);
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
                    AudioManager.Play(Sfx.ChargeHit, target.position);
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

        // =====================================================
        // KEJAR & JAGA
        // =====================================================

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
            Gizmos.color = new Color(0.4f, 1f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, kiteDistance);
        }
    }
}
