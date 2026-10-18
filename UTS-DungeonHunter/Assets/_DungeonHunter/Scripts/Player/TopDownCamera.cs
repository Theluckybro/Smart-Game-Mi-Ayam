using UnityEngine;

namespace DungeonHunter
{
    /// <summary>
    /// Kamera top-down yang mengikuti player dengan halus, plus camera shake berbasis "trauma":
    /// setiap hit menambah trauma (0..1), guncangan = trauma² × noise, lalu trauma meluruh.
    /// </summary>
    public class TopDownCamera : MonoBehaviour
    {
        private static TopDownCamera instance;

        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 16f, -9f);
        [SerializeField] private float smoothTime = 0.15f;

        [Header("Shake")]
        [SerializeField] private float maxShakeOffset = 0.6f;
        [SerializeField] private float maxShakeAngle = 2.5f;
        [SerializeField] private float traumaDecay = 1.6f;

        private Vector3 velocity;
        private Vector3 basePosition;
        private float trauma;
        private float seed;

        public void SetTarget(Transform newTarget) => target = newTarget;

        public static void Shake(float amount)
        {
            if (instance != null)
                instance.trauma = Mathf.Clamp01(instance.trauma + amount);
        }

        private void Awake()
        {
            instance = this;
            seed = Random.value * 100f;
            basePosition = transform.position;
        }

        private void LateUpdate()
        {
            if (target == null)
                return;

            Vector3 desired = target.position + offset;
            basePosition = Vector3.SmoothDamp(basePosition, desired, ref velocity, smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
            Quaternion baseRotation = Quaternion.LookRotation(-offset);

            trauma = Mathf.Max(0f, trauma - traumaDecay * Time.unscaledDeltaTime);
            float shake = trauma * trauma;
            float t = Time.unscaledTime * 25f;
            Vector3 jitter = new Vector3(
                Mathf.PerlinNoise(seed, t) * 2f - 1f,
                Mathf.PerlinNoise(seed + 1f, t) * 2f - 1f,
                Mathf.PerlinNoise(seed + 2f, t) * 2f - 1f) * (maxShakeOffset * shake);
            float roll = (Mathf.PerlinNoise(seed + 3f, t) * 2f - 1f) * maxShakeAngle * shake;

            transform.position = basePosition + jitter;
            transform.rotation = baseRotation * Quaternion.Euler(0f, 0f, roll);
        }
    }
}
