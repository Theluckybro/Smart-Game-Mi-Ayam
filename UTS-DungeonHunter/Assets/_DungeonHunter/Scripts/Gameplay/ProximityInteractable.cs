using UnityEngine;

namespace DungeonHunter
{
    /// <summary>Base untuk objek yang aktif saat player mendekat (cek jarak, tanpa physics trigger).</summary>
    public abstract class ProximityInteractable : MonoBehaviour
    {
        [SerializeField] protected float interactRadius = 1.5f;
        [SerializeField] private float bobAmplitude = 0.15f;
        [SerializeField] private float spinSpeed = 90f;
        [SerializeField] private Transform visual;

        protected Transform player;
        private Vector3 visualBasePos;

        protected virtual void Start()
        {
            if (GameManager.Instance != null && GameManager.Instance.PlayerHealth != null)
                player = GameManager.Instance.PlayerHealth.transform;
            if (visual != null)
                visualBasePos = visual.localPosition;
        }

        protected virtual void Update()
        {
            if (visual != null)
            {
                visual.localPosition = visualBasePos + Vector3.up * Mathf.Sin(Time.time * 2f) * bobAmplitude;
                visual.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
            }

            if (player == null || GameManager.Instance == null || !GameManager.Instance.IsPlaying)
                return;

            Vector3 diff = player.position - transform.position;
            diff.y = 0f;
            if (diff.magnitude <= interactRadius)
                OnPlayerNear();
        }

        protected abstract void OnPlayerNear();
    }
}
