using UnityEngine;
using UnityEngine.AI;

namespace DungeonHunter
{
    /// <summary>
    /// Gerbang besi (DYNAMIC OBSTACLE).
    /// Bisa mulai tertutup lalu dibuka tuas (Open), dan/atau terbanting menutup saat Key diambil (Close).
    /// Saat tertutup NavMeshObstacle (carve) aktif → NavMesh berubah → semua NavMeshAgent otomatis
    /// menghitung ulang jalur (repath). Saat dibuka, carve dimatikan dan jalur baru langsung tersedia.
    /// </summary>
    public class Portcullis : MonoBehaviour
    {
        [SerializeField] private Transform gatePanel;
        [SerializeField] private float openHeight = -2.6f;
        [SerializeField] private float moveSpeed = 3f;
        [SerializeField] private bool startClosed;
        [Tooltip("Gerbang Treasury: terbanting menutup saat Dungeon Key diambil")]
        [SerializeField] private bool closeOnKey = true;

        private NavMeshObstacle obstacle;
        private Collider gateCollider;
        private Vector3 closedLocalPos;
        private Vector3 openLocalPos;
        private bool moving;
        private bool targetClosed;

        public bool IsClosed { get; private set; }

        private void Awake()
        {
            obstacle = gatePanel.GetComponentInChildren<NavMeshObstacle>(true);
            gateCollider = gatePanel.GetComponentInChildren<Collider>(true);
            closedLocalPos = gatePanel.localPosition;
            openLocalPos = closedLocalPos + Vector3.up * openHeight;

            IsClosed = startClosed;
            targetClosed = startClosed;
            gatePanel.localPosition = startClosed ? closedLocalPos : openLocalPos;
            obstacle.enabled = startClosed;
            gateCollider.enabled = startClosed;
        }

        private void Start()
        {
            if (closeOnKey && GameManager.Instance != null)
                GameManager.Instance.KeyCollected += Close;
        }

        private void OnDestroy()
        {
            if (closeOnKey && GameManager.Instance != null)
                GameManager.Instance.KeyCollected -= Close;
        }

        /// <summary>Buka gerbang (dipanggil tuas), opsional dengan jeda.</summary>
        public void Open(float delay = 0f)
        {
            if (!targetClosed || openPending)
                return;
            if (delay > 0f)
            {
                openPending = true;
                StartCoroutine(OpenAfter(delay));
                return;
            }
            targetClosed = false;
            moving = true;
            NoiseSystem.Emit(transform.position, 10f);
            Burst(false);
            AudioManager.Play(Sfx.Chains, transform.position);
            AudioManager.Play(Sfx.DoorCreak, transform.position, 0.8f, 1.1f, 0.2f);
        }

        private bool openPending;

        private System.Collections.IEnumerator OpenAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
            openPending = false;
            Open();
        }

        /// <summary>Banting gerbang menutup.</summary>
        public void Close()
        {
            if (targetClosed)
                return;
            targetClosed = true;
            moving = true;
            // collider & carve langsung aktif agar tidak ada yang lolos saat gerbang turun
            gateCollider.enabled = true;
            obstacle.enabled = true;
            NoiseSystem.Emit(transform.position, 14f);
            Burst(true);
            VFXManager.Shake(0.4f);
            AudioManager.Play(Sfx.Chains, transform.position);
            AudioManager.Play(Sfx.Chains, transform.position, 0.7f, 0.85f, 0.25f);
            if (GameManager.Instance != null)
                GameManager.Instance.ShowMessage("Gerbang Treasury terbanting! Kabur lewat Katakombe di selatan.");
        }

        private void Burst(bool sparks)
        {
            // sebarkan efek sepanjang panel (panel bisa melintang utara-selatan atau timur-barat)
            Vector3 size = gateCollider.transform.lossyScale;
            Vector3 along = size.z > size.x ? Vector3.forward : Vector3.right;
            for (int i = -1; i <= 1; i++)
            {
                Vector3 p = transform.position + along * (i * 1.4f) + Vector3.up * 0.2f;
                VFXManager.Smoke(p, new Color(0.45f, 0.42f, 0.4f, 0.85f), 6, 1.2f, 2f);
                if (sparks)
                    VFXManager.Sparks(p, Vector3.up, new Color(1f, 0.75f, 0.4f), 5, 5f, 0.8f);
            }
        }

        private void Update()
        {
            if (!moving)
                return;

            Vector3 target = targetClosed ? closedLocalPos : openLocalPos;
            gatePanel.localPosition = Vector3.MoveTowards(gatePanel.localPosition, target, moveSpeed * Time.deltaTime);
            if ((gatePanel.localPosition - target).sqrMagnitude > 0.0001f)
                return;

            moving = false;
            IsClosed = targetClosed;
            if (IsClosed)
            {
                AudioManager.Play(Sfx.GateSlam, transform.position);
                AudioManager.Play(Sfx.SlamRocks, transform.position, 0.6f, 1.2f);
            }
            else
            {
                // terbuka penuh → jalur dibuka untuk NavMesh & player
                gateCollider.enabled = false;
                obstacle.enabled = false;
            }
        }
    }
}
