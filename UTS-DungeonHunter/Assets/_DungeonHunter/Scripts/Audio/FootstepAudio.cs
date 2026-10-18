using UnityEngine;

namespace DungeonHunter
{
    /// <summary>
    /// Suara langkah kaki berbasis jarak tempuh: satu langkah setiap <c>strideLength</c> meter.
    /// Makin cepat bergerak, langkah makin rapat dan sedikit lebih keras (sprint / Chase terdengar beda dari Patrol).
    /// </summary>
    public class FootstepAudio : MonoBehaviour
    {
        [SerializeField] private Sfx sfx = Sfx.SkeletonStep;
        [SerializeField] private float strideLength = 1.4f;
        [SerializeField] private float volume = 1f;
        [SerializeField] private float referenceSpeed = 4f;

        private Vector3 lastPosition;
        private float travelled;
        private Health health;

        private void Awake()
        {
            health = GetComponent<Health>();
            lastPosition = transform.position;
        }

        private void Update()
        {
            if (Time.deltaTime <= 0f)
                return;

            Vector3 delta = transform.position - lastPosition;
            delta.y = 0f;
            lastPosition = transform.position;
            float step = delta.magnitude;

            // diam, mati, atau teleport → reset (langkah pertama terdengar cepat saat mulai jalan)
            if ((health != null && health.IsDead) || step > 3f || step / Time.deltaTime < 0.4f)
            {
                travelled = strideLength * 0.7f;
                return;
            }

            travelled += step;
            if (travelled < strideLength)
                return;

            travelled = 0f;
            float speed = step / Time.deltaTime;
            AudioManager.Play(sfx, transform.position, volume * Mathf.Clamp(speed / referenceSpeed, 0.6f, 1.3f));
        }
    }
}
