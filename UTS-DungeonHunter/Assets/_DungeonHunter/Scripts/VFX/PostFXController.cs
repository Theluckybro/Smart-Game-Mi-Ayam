using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DungeonHunter
{
    /// <summary>
    /// Post-processing dinamis (URP Volume) yang bereaksi terhadap gameplay:
    /// - Player kena hit   → chromatic aberration + lens distortion sesaat
    /// - HP kritis         → warna memudar (desaturasi) + vignette lebih pekat
    /// - Slow-motion       → aberasi + desaturasi (momen dramatis)
    /// - Guardian Enrage   → bloom sedikit lebih kuat selama duel Phase 2
    /// - Pause             → latar blur (depth of field) + abu-abu
    /// - Menang            → lebih terang & bloom kuat;  Kalah → perlahan hitam-putih
    /// Profile di-clone saat runtime sehingga asset aslinya tidak berubah.
    /// </summary>
    [RequireComponent(typeof(Volume))]
    public class PostFXController : MonoBehaviour
    {
        private Bloom bloom;
        private ColorAdjustments color;
        private ChromaticAberration chromatic;
        private LensDistortion lens;
        private Vignette vignette;
        private DepthOfField dof;

        private float baseBloom, baseSaturation, baseExposure, baseVignette;
        private float hurt, pauseBlend, endBlend, enrageBlend;
        private float lowHp, slowBlend;

        private Health playerHp;
        private GuardianEnemy guardian;

        private void Start()
        {
            VolumeProfile profile = GetComponent<Volume>().profile; // instance (clone)
            profile.TryGet(out bloom);
            profile.TryGet(out color);
            profile.TryGet(out chromatic);
            profile.TryGet(out lens);
            profile.TryGet(out vignette);
            profile.TryGet(out dof);

            if (bloom != null) baseBloom = bloom.intensity.value;
            if (color != null)
            {
                baseSaturation = color.saturation.value;
                baseExposure = color.postExposure.value;
            }
            if (vignette != null) baseVignette = vignette.intensity.value;

            playerHp = GameManager.Instance != null ? GameManager.Instance.PlayerHealth : null;
            if (playerHp != null)
                playerHp.Damaged += (amount, source) => hurt = Mathf.Min(1f, hurt + 0.5f + amount / 60f);
            guardian = FindFirstObjectByType<GuardianEnemy>();
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            GameManager gm = GameManager.Instance;
            bool paused = gm != null && gm.State == GameState.Paused;
            bool over = gm != null && gm.IsGameOver;
            bool won = gm != null && gm.State == GameState.Won;

            hurt = Mathf.MoveTowards(hurt, 0f, dt * 2.2f);
            float hp = playerHp != null ? playerHp.Normalized : 1f;
            lowHp = Smooth(lowHp, over ? 0f : Mathf.Clamp01((0.35f - hp) / 0.35f), dt, 3f);
            slowBlend = Smooth(slowBlend, 1f - VFXManager.SlowMotionScale, dt, 6f);
            pauseBlend = Smooth(pauseBlend, paused ? 1f : 0f, dt, 8f);
            endBlend = Smooth(endBlend, over ? 1f : 0f, dt, 0.8f);
            bool enraged = guardian != null && guardian.IsEnraged && guardian.State != EnemyState.Dead;
            enrageBlend = Smooth(enrageBlend, enraged ? 1f : 0f, dt, 2f);

            if (chromatic != null)
                chromatic.intensity.value = Mathf.Clamp01(hurt * 0.9f + slowBlend * 0.7f + enrageBlend * 0.08f);
            if (lens != null)
                lens.intensity.value = -0.22f * hurt * hurt;

            if (color != null)
            {
                float sat = baseSaturation - 45f * lowHp - 35f * slowBlend - 70f * pauseBlend;
                float exposure = baseExposure;
                if (over)
                {
                    sat = won ? sat + 10f * endBlend : Mathf.Lerp(sat, -100f, endBlend);
                    exposure += won ? 0.3f * endBlend : -0.35f * endBlend;
                }
                color.saturation.value = Mathf.Clamp(sat, -100f, 100f);
                color.postExposure.value = exposure;
            }

            if (vignette != null)
                vignette.intensity.value = Mathf.Clamp01(baseVignette + 0.15f * lowHp + 0.12f * hurt + 0.12f * pauseBlend + 0.1f * endBlend);

            if (bloom != null)
                bloom.intensity.value = baseBloom * (1f + 0.35f * enrageBlend + (won ? 0.6f * endBlend : 0f));

            if (dof != null)
            {
                dof.active = pauseBlend > 0.01f;
                dof.gaussianMaxRadius.value = Mathf.Lerp(0.5f, 1.5f, pauseBlend);
            }
        }

        private static float Smooth(float current, float target, float dt, float rate) =>
            Mathf.Lerp(current, target, 1f - Mathf.Exp(-rate * dt));
    }
}
