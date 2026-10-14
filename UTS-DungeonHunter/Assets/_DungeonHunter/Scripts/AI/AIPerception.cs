using System.Collections.Generic;
using UnityEngine;

namespace DungeonHunter
{
    /// <summary>
    /// Perception enemy:
    /// - Vision  : Distance + Field of View + Line of Sight (Raycast)
    /// - Close awareness : player yang sangat dekat selalu terasa walau di belakang
    /// - Hearing : menerima noise dari NoiseSystem
    /// Dievaluasi oleh EnemyAI lewat Tick() agar urutan update konsisten.
    /// </summary>
    public class AIPerception : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;

        [Header("Vision")]
        [SerializeField] private float viewRadius = 10f;
        [SerializeField, Range(10f, 360f)] private float viewAngle = 110f;
        [SerializeField] private float eyeHeight = 1.2f;
        [SerializeField] private float targetHeight = 1.0f;
        [SerializeField] private float closeAwarenessRadius = 1.6f;

        [Header("Hearing")]
        [SerializeField] private bool canHear = true;
        [SerializeField] private float hearingMultiplier = 1f;

        public Transform Target => target;
        public float ViewRadius => viewRadius;
        public float ViewAngle => viewAngle;
        public float EyeHeight => eyeHeight;

        // ---- Hasil perception (dibaca oleh decision & debug) ----
        public bool CanSeeTarget { get; private set; }
        public bool InViewRadius { get; private set; }
        public bool InFieldOfView { get; private set; }
        public bool LineOfSightBlocked { get; private set; }
        public float DistanceToTarget { get; private set; } = Mathf.Infinity;
        public Vector3 LastRayEnd { get; private set; }

        public bool HasPendingNoise { get; private set; }
        public Vector3 PendingNoisePosition { get; private set; }

        private readonly RaycastHit[] rayHits = new RaycastHit[16];
        private static readonly IComparer<RaycastHit> HitDistanceComparer =
            Comparer<RaycastHit>.Create((a, b) => a.distance.CompareTo(b.distance));

        public void SetTarget(Transform newTarget) => target = newTarget;

        public void Configure(float radius, float angle)
        {
            viewRadius = radius;
            viewAngle = angle;
        }

        private void OnEnable()
        {
            NoiseSystem.NoiseEmitted += OnNoise;
        }

        private void OnDisable()
        {
            NoiseSystem.NoiseEmitted -= OnNoise;
        }

        public void Tick()
        {
            CanSeeTarget = EvaluateVision();
        }

        private bool EvaluateVision()
        {
            InViewRadius = InFieldOfView = LineOfSightBlocked = false;

            if (target == null)
            {
                DistanceToTarget = Mathf.Infinity;
                return false;
            }

            Vector3 origin = transform.position + Vector3.up * eyeHeight;
            Vector3 targetPoint = target.position + Vector3.up * targetHeight;
            Vector3 toTarget = targetPoint - origin;

            Vector3 flat = toTarget;
            flat.y = 0f;
            DistanceToTarget = flat.magnitude;
            LastRayEnd = targetPoint;

            // 1. Distance
            InViewRadius = DistanceToTarget <= viewRadius;
            if (!InViewRadius)
                return false;

            // 2. Field of View (kecuali sangat dekat)
            float angle = Vector3.Angle(transform.forward, flat);
            InFieldOfView = angle <= viewAngle * 0.5f || DistanceToTarget <= closeAwarenessRadius;
            if (!InFieldOfView)
                return false;

            // 3. Line of Sight (enemy lain tidak menghalangi pandangan)
            int count = Physics.RaycastNonAlloc(origin, toTarget.normalized, rayHits, toTarget.magnitude,
                ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(rayHits, 0, count, HitDistanceComparer);

            for (int i = 0; i < count; i++)
            {
                Transform hitTransform = rayHits[i].transform;
                if (hitTransform == target || hitTransform.IsChildOf(target))
                    return true;
                if (hitTransform.IsChildOf(transform) || rayHits[i].collider.GetComponentInParent<EnemyAI>() != null)
                    continue;

                LineOfSightBlocked = true;
                LastRayEnd = rayHits[i].point;
                return false;
            }

            return true;
        }

        private void OnNoise(Vector3 position, float radius)
        {
            if (!canHear || !isActiveAndEnabled)
                return;

            float heardRadius = radius * hearingMultiplier;
            if ((position - transform.position).sqrMagnitude > heardRadius * heardRadius)
                return;

            HasPendingNoise = true;
            PendingNoisePosition = position;
        }

        public bool ConsumeNoise(out Vector3 position)
        {
            position = PendingNoisePosition;
            if (!HasPendingNoise)
                return false;

            HasPendingNoise = false;
            return true;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, viewRadius);

            Vector3 left = Quaternion.Euler(0f, -viewAngle * 0.5f, 0f) * transform.forward;
            Vector3 right = Quaternion.Euler(0f, viewAngle * 0.5f, 0f) * transform.forward;
            Gizmos.DrawRay(transform.position, left * viewRadius);
            Gizmos.DrawRay(transform.position, right * viewRadius);

            if (target != null)
            {
                Gizmos.color = CanSeeTarget ? Color.green : Color.red;
                Gizmos.DrawLine(transform.position + Vector3.up * eyeHeight, LastRayEnd);
            }
        }
    }
}
