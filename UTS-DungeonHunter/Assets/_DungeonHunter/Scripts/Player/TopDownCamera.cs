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

        [Header("Main Menu (kamera berputar mengelilingi dungeon)")]
        [SerializeField] private Vector3 menuCenter = new Vector3(31f, 0f, 24f);
        [SerializeField] private float menuRadius = 24f;
        [SerializeField] private float menuHeight = 26f;
        [SerializeField] private float menuOrbitSpeed = 6f;

        [Header("Shake")]
        [SerializeField] private float maxShakeOffset = 0.6f;
        [SerializeField] private float maxShakeAngle = 2.5f;
        [SerializeField] private float traumaDecay = 1.6f;

        private Vector3 velocity;
        private Vector3 basePosition;
        private Quaternion currentRotation = Quaternion.identity;
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
            currentRotation = transform.rotation;
        }

        private void LateUpdate()
        {
            if (target == null)
                return;

            Vector3 desired = target.position + offset;
            Quaternion baseRotation = Quaternion.LookRotation(-offset);

            if (GameManager.Instance != null && GameManager.Instance.State == GameState.Menu)
            {
                float angle = Time.unscaledTime * menuOrbitSpeed * Mathf.Deg2Rad;
                desired = menuCenter + new Vector3(Mathf.Sin(angle) * menuRadius, menuHeight, -Mathf.Cos(angle) * menuRadius);
                basePosition = desired;
                currentRotation = Quaternion.LookRotation(menuCenter - basePosition);
                transform.position = basePosition;
                transform.rotation = currentRotation;
                return;
            }

            // Transisi halus dari kamera menu ke kamera gameplay
            float smooth = Quaternion.Angle(currentRotation, baseRotation) > 1f ? 0.6f : smoothTime;
            basePosition = Vector3.SmoothDamp(basePosition, desired, ref velocity, smooth, Mathf.Infinity, Time.unscaledDeltaTime);
            currentRotation = Quaternion.Slerp(currentRotation, baseRotation, 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime));
            baseRotation = currentRotation;

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
