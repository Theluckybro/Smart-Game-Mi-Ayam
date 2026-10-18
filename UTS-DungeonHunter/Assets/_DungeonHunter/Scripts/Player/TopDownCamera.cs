using UnityEngine;

namespace DungeonHunter
{
    /// <summary>
    /// Kamera top-down yang mengikuti player dengan halus, plus camera shake berbasis "trauma":
    /// setiap hit menambah trauma (0..1), guncangan = trauma² × noise, lalu trauma meluruh.
    /// Momen sinematik: Focus() menggeser kamera ke titik penting (Guardian muncul, Enrage, Guardian mati)
    /// sambil zoom in; saat game berakhir kamera zoom ke player dan berputar perlahan.
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

        // Sinematik
        private Vector3 focusPoint;
        private float focusUntil = -1f;
        private float focusZoom = 1f, focusWeight;
        private bool focusLetterbox;
        private float zoom = 1f, weight, orbit;

        /// <summary>True saat momen sinematik (UI menampilkan letterbox).</summary>
        public static bool CinematicActive =>
            instance != null && ((Time.unscaledTime < instance.focusUntil && instance.focusLetterbox)
                                 || (GameManager.Instance != null && GameManager.Instance.IsGameOver));

        /// <summary>
        /// Fokus sinematik sementara.
        /// zoom &lt; 1 = mendekat, weight = seberapa jauh pusat kamera bergeser dari player ke titik (0..1).
        /// </summary>
        public static void Focus(Vector3 point, float duration, float zoomFactor = 0.7f, float shift = 0.6f, bool letterbox = true)
        {
            if (instance == null)
                return;
            instance.focusPoint = point;
            instance.focusUntil = Time.unscaledTime + duration;
            instance.focusZoom = zoomFactor;
            instance.focusWeight = shift;
            instance.focusLetterbox = letterbox;
        }

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

            // ---- Sinematik: zoom, geser fokus, orbit saat game berakhir ----
            float udt = Time.unscaledDeltaTime;
            bool ending = GameManager.Instance != null && GameManager.Instance.IsGameOver;
            float wantZoom = 1f, wantWeight = 0f;
            if (ending)
            {
                wantZoom = 0.5f;
                orbit += udt * 9f;
            }
            else if (Time.unscaledTime < focusUntil)
            {
                wantZoom = focusZoom;
                wantWeight = focusWeight;
            }
            zoom = Mathf.Lerp(zoom, wantZoom, 1f - Mathf.Exp(-2.5f * udt));
            weight = Mathf.Lerp(weight, wantWeight, 1f - Mathf.Exp(-3f * udt));

            Vector3 center = Vector3.Lerp(target.position, focusPoint, weight);
            Vector3 currentOffset = Quaternion.Euler(0f, orbit, 0f) * offset * zoom;
            Vector3 desired = center + currentOffset;
            Quaternion baseRotation = Quaternion.LookRotation(-currentOffset);

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
