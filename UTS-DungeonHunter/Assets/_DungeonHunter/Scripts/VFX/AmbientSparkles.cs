using UnityEngine;

namespace DungeonHunter
{
    /// <summary>Kilau partikel di sekitar objek penting (Key, Artifact) agar mudah terlihat dari jauh.</summary>
    public class AmbientSparkles : MonoBehaviour
    {
        [SerializeField] private Color color = new Color(1f, 0.85f, 0.3f);
        [SerializeField] private float radius = 0.7f;
        [SerializeField] private float height = 1f;
        [SerializeField] private float rate = 10f;

        private ParticleSystem ps;

        private void Start()
        {
            if (VFXManager.Instance == null)
                return;

            var go = new GameObject("Sparkles");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * height;
            ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
            main.startColor = color;
            main.gravityModifier = -0.15f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = rate;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.3f), new Keyframe(0.4f, 1f), new Keyframe(1f, 0f)));
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = VFXManager.Instance.Additive;
            ps.Play();
        }
    }
}
