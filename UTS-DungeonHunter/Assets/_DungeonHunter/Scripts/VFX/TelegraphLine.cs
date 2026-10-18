using UnityEngine;

namespace DungeonHunter
{
    /// <summary>
    /// Garis peringatan di lantai (jalur Charge Guardian). Berdenyut makin cepat menjelang serangan.
    /// </summary>
    public class TelegraphLine : MonoBehaviour
    {
        private LineRenderer line;
        private Color color;
        private float born;
        private float duration;

        public static void Spawn(Material material, Vector3 start, Vector3 dir, float length, float width, float duration, Color color)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f)
                return;
            dir.Normalize();

            var go = new GameObject("TelegraphLine");
            var t = go.AddComponent<TelegraphLine>();
            t.line = go.AddComponent<LineRenderer>();
            t.line.sharedMaterial = material;
            t.line.useWorldSpace = true;
            t.line.alignment = LineAlignment.TransformZ;
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            t.line.positionCount = 2;
            Vector3 y = Vector3.up * 0.07f;
            t.line.SetPosition(0, new Vector3(start.x, start.y, start.z) + y);
            t.line.SetPosition(1, new Vector3(start.x, start.y, start.z) + dir * length + y);
            t.line.widthCurve = new AnimationCurve(new Keyframe(0f, width), new Keyframe(0.85f, width), new Keyframe(1f, width * 0.2f));
            t.line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            t.color = color;
            t.born = Time.time;
            t.duration = duration;
        }

        private void Update()
        {
            float t = (Time.time - born) / duration;
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Lerp(10f, 40f, t));
            float a = Mathf.Lerp(0.55f, 1f, t) * Mathf.Lerp(0.7f, 1f, pulse);
            var c = new Color(color.r, color.g, color.b, a);
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
                new[] { new GradientAlphaKey(a, 0f), new GradientAlphaKey(a, 0.8f), new GradientAlphaKey(0f, 1f) });
            line.colorGradient = g;
        }
    }
}
