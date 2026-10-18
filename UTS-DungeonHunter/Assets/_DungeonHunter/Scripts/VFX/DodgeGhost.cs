using UnityEngine;

namespace DungeonHunter
{
    /// <summary>
    /// Bayangan (afterimage) saat dodge: pose karakter saat ini di-"bake" menjadi mesh statis
    /// berwarna terang yang memudar dalam waktu singkat.
    /// </summary>
    public class DodgeGhost : MonoBehaviour
    {
        private static readonly int TintId = Shader.PropertyToID("_TintColor");

        private const float Life = 0.28f;

        private MeshRenderer[] renderers;
        private Mesh[] bakedMeshes;
        private MaterialPropertyBlock block;
        private Color color;
        private float age;

        public static void Spawn(GameObject source, Color color)
        {
            if (VFXManager.Instance == null || VFXManager.Instance.Solid == null)
                return;

            var root = new GameObject("DodgeGhost");
            var ghost = root.AddComponent<DodgeGhost>();
            ghost.color = color;
            ghost.block = new MaterialPropertyBlock();

            SkinnedMeshRenderer[] skinned = source.GetComponentsInChildren<SkinnedMeshRenderer>();
            ghost.renderers = new MeshRenderer[skinned.Length];
            ghost.bakedMeshes = new Mesh[skinned.Length];
            for (int i = 0; i < skinned.Length; i++)
            {
                SkinnedMeshRenderer smr = skinned[i];
                var mesh = new Mesh();
                smr.BakeMesh(mesh, true);
                // shader partikel mengalikan warna vertex → isi putih
                var colors = new Color32[mesh.vertexCount];
                for (int c = 0; c < colors.Length; c++)
                    colors[c] = new Color32(255, 255, 255, 255);
                mesh.colors32 = colors;

                var part = new GameObject("Part");
                part.transform.SetParent(root.transform, false);
                part.transform.SetPositionAndRotation(smr.transform.position, smr.transform.rotation);
                part.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = part.AddComponent<MeshRenderer>();
                mr.sharedMaterial = VFXManager.Instance.Solid;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                ghost.renderers[i] = mr;
                ghost.bakedMeshes[i] = mesh;
            }
            ghost.Apply(1f);
        }

        private void Update()
        {
            age += Time.deltaTime;
            float t = age / Life;
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }
            Apply(1f - t);
        }

        private void Apply(float strength)
        {
            Color c = color * strength;
            c.a = color.a * strength;
            block.SetColor(TintId, c);
            foreach (MeshRenderer r in renderers)
                r.SetPropertyBlock(block);
        }

        private void OnDestroy()
        {
            if (bakedMeshes == null)
                return;
            foreach (Mesh m in bakedMeshes)
                if (m != null)
                    Destroy(m);
        }
    }
}
