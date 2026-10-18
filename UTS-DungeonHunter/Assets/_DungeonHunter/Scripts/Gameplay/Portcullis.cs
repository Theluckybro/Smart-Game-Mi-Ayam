using UnityEngine;
using UnityEngine.AI;

namespace DungeonHunter
{
    /// <summary>
    /// Gerbang besi (DYNAMIC OBSTACLE).
    /// Awalnya terbuka. Saat Dungeon Key diambil, gerbang turun menutup jalur hall ↔ ruang timur.
    /// NavMeshObstacle (carve) aktif → NavMesh berubah → semua NavMeshAgent otomatis
    /// menghitung ulang jalur (repath) lewat koridor selatan.
    /// </summary>
    public class Portcullis : MonoBehaviour
    {
        [SerializeField] private Transform gatePanel;
        [SerializeField] private float openHeight = -2.6f;
        [SerializeField] private float closeSpeed = 4f;

        private NavMeshObstacle obstacle;
        private Collider gateCollider;
        private Vector3 closedLocalPos;
        private bool closing;

        public bool IsClosed { get; private set; }

        private void Awake()
        {
            obstacle = gatePanel.GetComponentInChildren<NavMeshObstacle>(true);
            gateCollider = gatePanel.GetComponentInChildren<Collider>(true);
            closedLocalPos = gatePanel.localPosition;

            gatePanel.localPosition = closedLocalPos + Vector3.up * openHeight;
            obstacle.enabled = false;
            gateCollider.enabled = false;
        }

        private void Start()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.KeyCollected += Close;
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.KeyCollected -= Close;
        }

        public void Close()
        {
            if (closing || IsClosed)
                return;

            closing = true;
            gateCollider.enabled = true;
            obstacle.enabled = true;
            NoiseSystem.Emit(transform.position, 14f);
            for (int i = -1; i <= 1; i++)
            {
                Vector3 p = transform.position + Vector3.forward * (i * 1.4f) + Vector3.up * 0.2f;
                VFXManager.Smoke(p, new Color(0.45f, 0.42f, 0.4f, 0.85f), 6, 1.2f, 2f);
                VFXManager.Sparks(p, Vector3.up, new Color(1f, 0.75f, 0.4f), 5, 5f, 0.8f);
            }
            VFXManager.Shake(0.4f);
            AudioManager.Play(Sfx.Chains, transform.position);
            AudioManager.Play(Sfx.Chains, transform.position, 0.7f, 0.85f, 0.25f);
            if (GameManager.Instance != null)
                GameManager.Instance.ShowMessage("Gerbang besi tertutup! Keluar lewat koridor selatan.");
        }

        private void Update()
        {
            if (!closing)
                return;

            gatePanel.localPosition = Vector3.MoveTowards(gatePanel.localPosition, closedLocalPos, closeSpeed * Time.deltaTime);
            if ((gatePanel.localPosition - closedLocalPos).sqrMagnitude < 0.0001f)
            {
                closing = false;
                IsClosed = true;
                AudioManager.Play(Sfx.GateSlam, transform.position);
                AudioManager.Play(Sfx.SlamRocks, transform.position, 0.6f, 1.2f);
            }
        }
    }
}
