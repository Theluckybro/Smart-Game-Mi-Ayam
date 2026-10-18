using UnityEngine;

namespace DungeonHunter
{
    /// <summary>
    /// Busur tebasan (mesh prosedural, additive). Muncul di depan penyerang, menyapu dari kiri ke kanan,
    /// sedikit membesar, lalu memudar. Ujung busur paling terang agar terasa cepat.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class SlashArc : MonoBehaviour
    {
        private const int Segments = 24;
        private const float Duration = 0.26f;

        private Mesh mesh;
        private Color color;
        private float arc;
        private float radius;
        private float born;

        public static void Spawn(Transform owner, Material material, Color color, float radius, float arcDegrees, float height)
        {
            var go = new GameObject("SlashArc");
            go.transform.position = owner.position + Vector3.up * height;
            go.transform.rotation = Quaternion.LookRotation(Flat(owner.forward)) * Quaternion.Euler(-8f, 0f, 0f);
            var slash = go.AddComponent<SlashArc>();
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            slash.color = color;
            slash.radius = radius;
            slash.arc = arcDegrees;
            slash.born = Time.time;
            slash.mesh = new Mesh { name = "SlashArc" };
            slash.GetComponent<MeshFilter>().sharedMesh = slash.mesh;
            slash.Rebuild(0f);
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 0.001f ? v.normalized : Vector3.forward;
        }

        private void Update()
        {
            float t = (Time.time - born) / Duration;
            if (t >= 1f)
            {
                Destroy(mesh);
                Destroy(gameObject);
                return;
            }
            Rebuild(t);
        }

        private void Rebuild(float t)
        {
            float sweep = Mathf.Clamp01(t * 2.2f);        // 0..1 sudut yang sudah tersapu
            float fade = t < 0.45f ? 1f : Mathf.InverseLerp(1f, 0.45f, t);
            float scale = Mathf.Lerp(0.85f, 1.1f, t);
            float outer = radius * scale;
            float inner = outer * 0.3f;

            var verts = new Vector3[(Segments + 1) * 2];
            var cols = new Color[verts.Length];
            var tris = new int[Segments * 6];
            float half = arc * 0.5f;

            for (int i = 0; i <= Segments; i++)
            {
                float u = i / (float)Segments;
                float ang = Mathf.Lerp(-half, half, u) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang));
                verts[i * 2] = dir * inner;
                verts[i * 2 + 1] = dir * outer;

                // Hanya bagian yang sudah tersapu yang terlihat; ujung depan paling terang
                float lead = sweep <= 0f ? 0f : Mathf.Clamp01(1f - (sweep - u) * 2.5f);
                float visible = u <= sweep ? 1f : 0f;
                float a = visible * Mathf.Lerp(0.55f, 1f, lead) * fade;
                cols[i * 2] = new Color(color.r, color.g, color.b, a * 0.15f);
                cols[i * 2 + 1] = new Color(Mathf.Lerp(color.r, 1f, lead * 0.6f), Mathf.Lerp(color.g, 1f, lead * 0.6f),
                    Mathf.Lerp(color.b, 1f, lead * 0.6f), a);
            }

            for (int i = 0; i < Segments; i++)
            {
                int v = i * 2;
                int k = i * 6;
                tris[k] = v; tris[k + 1] = v + 1; tris[k + 2] = v + 3;
                tris[k + 3] = v; tris[k + 4] = v + 3; tris[k + 5] = v + 2;
            }

            mesh.Clear();
            mesh.vertices = verts;
            mesh.colors = cols;
            // Semua UV di tengah tekstur soft-dot (alpha penuh); bentuk & gradasi diatur lewat vertex color
            var uvs = new Vector2[verts.Length];
            for (int i = 0; i < uvs.Length; i++)
                uvs[i] = new Vector2(0.5f, 0.5f);
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
        }
    }
}
