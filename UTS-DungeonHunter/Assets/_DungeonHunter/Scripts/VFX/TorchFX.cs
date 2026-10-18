using UnityEngine;

namespace DungeonHunter
{
    /// <summary>Obor hidup: cahaya berkedip (Perlin noise), api bergoyang, dan bara api yang naik.</summary>
    public class TorchFX : MonoBehaviour
    {
        [SerializeField] private Light torchLight;
        [SerializeField] private Transform flame;
        [SerializeField] private Color emberColor = new Color(1f, 0.55f, 0.15f);

        private float baseIntensity;
        private Vector3 baseFlameScale;
        private float seed;
        private AudioSource crackle;

        private void Start()
        {
            seed = Random.value * 100f;
            if (torchLight != null)
                baseIntensity = torchLight.intensity;
            if (flame != null)
                baseFlameScale = flame.localScale;

            if (VFXManager.Instance != null && flame != null)
                CreateEmbers();
            CreateCrackleAudio();
        }

        /// <summary>Suara api berderak (loop prosedural), 3D dengan jangkauan pendek.</summary>
        private void CreateCrackleAudio()
        {
            AudioClip clip = SoundLibrary.Load("Ambient/torch_loop");
            if (clip == null)
                return;
            var src = crackle = gameObject.AddComponent<AudioSource>();
            src.clip = clip;
            src.loop = true;
            src.spatialBlend = 1f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = 1f;
            src.maxDistance = 9f;
            src.volume = 0.5f * AudioManager.SfxVolume;
            src.pitch = Random.Range(0.85f, 1.15f);
            src.dopplerLevel = 0f;
            src.priority = 200;
            src.time = Random.Range(0f, clip.length * 0.9f);
            src.Play();
        }

        private void CreateEmbers()
        {
            var go = new GameObject("Embers");
            go.transform.SetParent(flame.parent, false);
            go.transform.localPosition = flame.localPosition;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
            main.startColor = emberColor;
            main.gravityModifier = -0.35f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 7f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.12f;
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.4f;
            noise.frequency = 1.5f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = VFXManager.Instance.Additive;
            ps.Play();
        }

        private void Update()
        {
            float n = Mathf.PerlinNoise(seed, Time.time * 6f);
            float n2 = Mathf.PerlinNoise(seed + 10f, Time.time * 13f);
            float flicker = 0.75f + 0.35f * n + 0.15f * n2;
            if (crackle != null)
                crackle.volume = 0.5f * AudioManager.SfxVolume;

            if (torchLight != null)
                torchLight.intensity = baseIntensity * flicker;
            if (flame != null)
                flame.localScale = baseFlameScale * (0.85f + 0.3f * n2) + Vector3.up * 0.08f * n;
        }
    }
}
