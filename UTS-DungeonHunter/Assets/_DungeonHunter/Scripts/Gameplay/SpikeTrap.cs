using System.Collections.Generic;
using UnityEngine;

namespace DungeonHunter
{
    /// <summary>
    /// Jebakan duri. Area lantainya ditandai sebagai NavMesh area "Trap" (cost tinggi)
    /// lewat NavMeshModifierVolume, sehingga enemy memilih jalan memutar.
    /// Duri naik-turun berkala dan melukai SIAPA PUN di atasnya (player maupun enemy).
    /// </summary>
    public class SpikeTrap : MonoBehaviour
    {
        [SerializeField] private Vector3 size = new Vector3(8f, 1f, 4f);
        [SerializeField] private Transform spikes;
        [SerializeField] private float damage = 15f;
        [SerializeField] private float idleTime = 1.6f;
        [SerializeField] private float warnTime = 0.5f;
        [SerializeField] private float activeTime = 0.8f;
        [SerializeField] private float spikeUpHeight = 0.5f;

        private enum Phase { Idle, Warn, Active }

        private Phase phase = Phase.Idle;
        private float timer;
        private Vector3 spikesDownPos;
        private readonly HashSet<Health> hitThisCycle = new HashSet<Health>();
        private readonly Collider[] overlap = new Collider[16];

        public Vector3 Size => size;
        public bool IsActive => phase == Phase.Active;

        private void Awake()
        {
            if (spikes != null)
                spikesDownPos = spikes.localPosition;
            timer = idleTime;
        }

        private void Update()
        {
            timer -= Time.deltaTime;
            if (timer <= 0f)
                Advance();

            if (spikes != null)
            {
                float y = phase == Phase.Active ? spikeUpHeight : phase == Phase.Warn ? spikeUpHeight * 0.25f : 0f;
                Vector3 targetPos = spikesDownPos + Vector3.up * y;
                spikes.localPosition = Vector3.MoveTowards(spikes.localPosition, targetPos, 6f * Time.deltaTime);
            }

            if (phase == Phase.Active)
                DamageOccupants();
        }

        private void Advance()
        {
            switch (phase)
            {
                case Phase.Idle:
                    phase = Phase.Warn;
                    timer = warnTime;
                    break;
                case Phase.Warn:
                    phase = Phase.Active;
                    timer = activeTime;
                    hitThisCycle.Clear();
                    break;
                default:
                    phase = Phase.Idle;
                    timer = idleTime;
                    break;
            }
        }

        private void DamageOccupants()
        {
            Vector3 center = transform.position + Vector3.up * 0.75f;
            int count = Physics.OverlapBoxNonAlloc(center, new Vector3(size.x, 1.5f, size.z) * 0.5f, overlap,
                transform.rotation, ~0, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                Health h = overlap[i].GetComponentInParent<Health>();
                if (h == null || h.IsDead || hitThisCycle.Contains(h))
                    continue;

                hitThisCycle.Add(h);
                h.TakeDamage(damage, transform.position);
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.2f, 0.1f, 0.5f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.up * 0.05f, new Vector3(size.x, 0.1f, size.z));
        }
    }
}
