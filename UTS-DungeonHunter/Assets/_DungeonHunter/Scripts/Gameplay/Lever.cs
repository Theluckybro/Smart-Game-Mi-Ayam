using UnityEngine;

namespace DungeonHunter
{
    /// <summary>
    /// Tuas yang membuka gerbang besi saat player mendekat.
    /// - Tuas Armory (index 0)    → membuka gerbang Treasury
    /// - Tuas Katakombe (index 1) → membuka jalan pintas ke Entrance
    /// Menarik tuas berisik (noise radius besar) sehingga musuh di sekitar datang memeriksa (Investigate).
    /// Kamera menyorot gerbang sesaat agar player tahu apa yang terbuka.
    /// </summary>
    public class Lever : ProximityInteractable
    {
        [SerializeField] private Portcullis gate;
        [SerializeField] private int leverIndex;
        [SerializeField] private Transform handle;
        [SerializeField] private Light lamp;
        [SerializeField] private float noiseRadius = 14f;

        private static readonly Color IdleColor = new Color(1f, 0.75f, 0.25f);
        private static readonly Color PulledColor = new Color(0.35f, 1f, 0.45f);

        private bool pulled;
        private float pullT;

        public int Index => leverIndex;
        public bool Pulled => pulled;

        protected override void Start()
        {
            base.Start();
            if (lamp != null)
                lamp.color = IdleColor;
        }

        protected override void Update()
        {
            base.Update();

            if (pulled && pullT < 1f)
            {
                pullT = Mathf.Min(1f, pullT + Time.deltaTime * 4f);
                float e = 1f - (1f - pullT) * (1f - pullT);
                if (handle != null)
                    handle.localRotation = Quaternion.Euler(Mathf.Lerp(-40f, 40f, e), 0f, 0f);
            }

            if (lamp != null)
                lamp.intensity = pulled ? 1.6f : 2f + 0.8f * Mathf.Sin(Time.time * 4f);
        }

        protected override void OnPlayerNear()
        {
            if (pulled)
                return;
            pulled = true;

            if (lamp != null)
                lamp.color = PulledColor;
            var sparkles = GetComponent<AmbientSparkles>();
            if (sparkles != null)
                sparkles.enabled = false;

            Vector3 top = transform.position + Vector3.up * 1.2f;
            VFXManager.Sparks(top, Vector3.up, IdleColor, 14, 5f, 1f);
            VFXManager.Glow(top, PulledColor, 2.5f, 0.25f);
            VFXManager.Shake(0.15f);
            AudioManager.Play(Sfx.DoorUnlock, transform.position, 1.2f, 0.8f);
            AudioManager.Play(Sfx.MeleeWindup, transform.position, 1f, 1.2f);
            NoiseSystem.Emit(transform.position, noiseRadius);

            if (gate != null)
            {
                // fokus penuh ke gerbang (bukan titik tengah player–gerbang); gerbang mulai turun
                // setelah kamera sampai, supaya player melihat gerbang mana yang terbuka
                TopDownCamera.Focus(gate.transform.position, 2.6f, 0.85f, 1f);
                gate.Open(0.9f);
            }
            if (GameManager.Instance != null)
                GameManager.Instance.NotifyLeverPulled(leverIndex);
        }
    }
}
