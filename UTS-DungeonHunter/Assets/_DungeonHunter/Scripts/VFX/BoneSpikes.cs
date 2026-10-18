using UnityEngine;

namespace DungeonHunter
{
    /// <summary>
    /// Duri tulang spektral yang menyembul dari tanah (skill Bone Eruption Guardian):
    /// beberapa pecahan runcing naik cepat, bertahan sebentar, lalu tenggelam dan hilang.
    /// </summary>
    public class BoneSpikes : MonoBehaviour
    {
        private static readonly int TintId = Shader.PropertyToID("_TintColor");
        private static Mesh shardMesh;

        private const float RiseTime = 0.1f, HoldTime = 0.35f, SinkTime = 0.3f;

        private Transform[] shards;
        private Vector3[] fullScale;
        private MeshRenderer[] renderers;
        private MaterialPropertyBlock block;
        private Color color;
        private float age;

        public static void Spawn(Vector3 position, float radius, Color color)
        {
            if (VFXManager.Instance == null || VFXManager.Instance.Solid == null)
                return;

            var go = new GameObject("BoneSpikes");
            go.transform.position = position;
            var fx = go.AddComponent<BoneSpikes>();
            fx.color = color;
            fx.block = new MaterialPropertyBlock();

            int count = 7;
            fx.shards = new Transform[count];
            fx.fullScale = new Vector3[count];
            fx.renderers = new MeshRenderer[count];
            for (int i = 0; i < count; i++)
            {
                var s = new GameObject("Shard");
                s.transform.SetParent(go.transform, false);
                Vector2 off = i == 0 ? Vector2.zero : Random.insideUnitCircle * radius * 0.8f;
                s.transform.localPosition = new Vector3(off.x, 0f, off.y);
                s.transform.localRotation = Quaternion.Euler(Random.Range(-18f, 18f), Random.Range(0f, 360f), Random.Range(-18f, 18f));
                float h = i == 0 ? 2.2f : Random.Range(1f, 1.8f);
                float w = Random.Range(0.25f, 0.4f);
                fx.fullScale[i] = new Vector3(w, h, w);
                s.transform.localScale = new Vector3(w, 0.01f, w);
                s.AddComponent<MeshFilter>().sharedMesh = ShardMesh;
                var mr = s.AddComponent<MeshRenderer>();
                mr.sharedMaterial = VFXManager.Instance.Solid;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                fx.shards[i] = s.transform;
                fx.renderers[i] = mr;
            }
            fx.Apply(1f);
        }

        /// <summary>Piramida segi empat (alas di y=0, ujung di y=1), warna vertex putih.</summary>
        private static Mesh ShardMesh
        {
            get
            {
                if (shardMesh != null)
                    return shardMesh;
                shardMesh = new Mesh { name = "BoneShard" };
                Vector3 top = Vector3.up;
                Vector3[] b = { new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(-0.5f, 0f, 0.5f) };
                var verts = new System.Collections.Generic.List<Vector3>();
                var tris = new System.Collections.Generic.List<int>();
                for (int i = 0; i < 4; i++)
                {
                    int start = verts.Count;
                    verts.Add(b[i]);
                    verts.Add(top);
                    verts.Add(b[(i + 1) % 4]);
                    tris.Add(start); tris.Add(start + 1); tris.Add(start + 2);
                }
                shardMesh.SetVertices(verts);
                shardMesh.SetTriangles(tris, 0);
                var colors = new Color32[verts.Count];
                for (int i = 0; i < colors.Length; i++)
                    colors[i] = new Color32(255, 255, 255, 255);
                shardMesh.colors32 = colors;
                shardMesh.uv = new Vector2[verts.Count];
                shardMesh.RecalculateNormals();
                shardMesh.RecalculateBounds();
                return shardMesh;
            }
        }

        private void Update()
        {
            age += Time.deltaTime;
            float grow;
            if (age < RiseTime) grow = age / RiseTime;
            else if (age < RiseTime + HoldTime) grow = 1f;
            else grow = 1f - (age - RiseTime - HoldTime) / SinkTime;

            if (grow <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            float e = 1f - (1f - grow) * (1f - grow);
            for (int i = 0; i < shards.Length; i++)
                shards[i].localScale = new Vector3(fullScale[i].x, Mathf.Max(0.01f, fullScale[i].y * e), fullScale[i].z);
            Apply(age < RiseTime + HoldTime ? 1f : grow);
        }

        private void Apply(float strength)
        {
            Color c = color * (0.55f * strength);
            c.a = 0.8f * strength;
            block.SetColor(TintId, c);
            foreach (MeshRenderer r in renderers)
                r.SetPropertyBlock(block);
        }
    }
}
