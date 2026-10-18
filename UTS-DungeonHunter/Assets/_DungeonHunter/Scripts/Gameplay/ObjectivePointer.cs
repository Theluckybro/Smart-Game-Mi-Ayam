using UnityEngine;
using UnityEngine.AI;

namespace DungeonHunter
{
    /// <summary>
    /// Panah penunjuk objective di lantai sekitar player.
    /// Arah panah TIDAK lurus ke target, tetapi ke tikungan pertama jalur NavMesh (NavMesh.CalculatePath),
    /// sehingga panah menuntun lewat pintu & koridor, bukan menembus dinding.
    /// Target mengikuti objective aktif: tuas Armory → Key → tuas Katakombe → pintu/Guardian → Artifact.
    /// </summary>
    public class ObjectivePointer : MonoBehaviour
    {
        private static readonly int TintId = Shader.PropertyToID("_TintColor");

        [SerializeField] private float radius = 1.7f;
        [SerializeField] private float hideDistance = 4f;
        [SerializeField] private float repathInterval = 0.25f;
        [SerializeField] private Color color = new Color(1f, 0.8f, 0.35f);

        private Transform arrow;
        private MeshRenderer arrowRenderer;
        private MaterialPropertyBlock block;
        private NavMeshPath path;
        private Vector3 direction = Vector3.forward;
        private Vector3 desiredDirection = Vector3.forward;
        private float nextRepath;
        private float alpha;
        private bool visible;

        private Transform armoryLever, keyTarget, catacombLever, doorTarget, guardianTarget, artifactTarget;

        private void Start()
        {
            path = new NavMeshPath();
            block = new MaterialPropertyBlock();
            keyTarget = FindFirstObjectByType<KeyPickup>()?.transform;
            foreach (Lever lever in FindObjectsByType<Lever>(FindObjectsSortMode.None))
            {
                if (lever.Index == 0) armoryLever = lever.transform;
                else catacombLever = lever.transform;
            }
            doorTarget = FindFirstObjectByType<LockedDoor>()?.transform;
            guardianTarget = FindFirstObjectByType<GuardianEnemy>()?.transform;
            artifactTarget = FindFirstObjectByType<ArtifactPickup>()?.transform;
            CreateArrow();
        }

        private void OnDestroy()
        {
            if (arrow != null)
                Destroy(arrow.gameObject);
        }

        private void CreateArrow()
        {
            if (VFXManager.Instance == null || VFXManager.Instance.Solid == null)
                return;

            var go = new GameObject("ObjectivePointer");
            arrow = go.transform;
            var mesh = new Mesh { name = "Chevron" };
            // Chevron datar menghadap +Z
            mesh.vertices = new[]
            {
                new Vector3(0f, 0f, 0.45f), new Vector3(-0.42f, 0f, -0.2f), new Vector3(0f, 0f, 0.05f),
                new Vector3(0.42f, 0f, -0.2f), new Vector3(-0.42f, 0f, -0.42f), new Vector3(0.42f, 0f, -0.42f),
                new Vector3(0f, 0f, -0.17f)
            };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2, 1, 2, 6, 1, 6, 4, 2, 3, 6, 3, 5, 6 };
            mesh.uv = new Vector2[7];
            var colors = new Color32[7];
            for (int i = 0; i < colors.Length; i++)
                colors[i] = new Color32(255, 255, 255, 255);
            mesh.colors32 = colors;
            mesh.RecalculateBounds();

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            arrowRenderer = go.AddComponent<MeshRenderer>();
            arrowRenderer.sharedMaterial = VFXManager.Instance.Solid;
            arrowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            arrowRenderer.receiveShadows = false;
        }

        private Transform CurrentTarget(out float hideAt)
        {
            hideAt = hideDistance;
            GameManager gm = GameManager.Instance;
            if (gm == null)
                return null;
            switch (gm.ObjectiveStep)
            {
                case 0: return armoryLever;
                case 1: return keyTarget != null && keyTarget.gameObject.activeInHierarchy ? keyTarget : null;
                case 2: return catacombLever;
                case 3:
                    if (!gm.DoorOpened)
                        return doorTarget;
                    hideAt = 12f; // saat duel boss panah tidak diperlukan
                    return guardianTarget;
                case 4: return artifactTarget;
                default: return null;
            }
        }

        private void LateUpdate()
        {
            if (arrow == null)
                return;

            GameManager gm = GameManager.Instance;
            Transform target = CurrentTarget(out float hideAt);
            visible = target != null && gm != null && gm.IsPlaying;

            if (visible)
            {
                Vector3 toTarget = target.position - transform.position;
                toTarget.y = 0f;
                if (toTarget.magnitude < hideAt)
                    visible = false;
                else if (Time.time >= nextRepath)
                {
                    nextRepath = Time.time + repathInterval;
                    desiredDirection = PathDirection(target.position, toTarget);
                }
            }

            float dt = Time.unscaledDeltaTime;
            alpha = Mathf.MoveTowards(alpha, visible ? 1f : 0f, dt * 3f);
            arrowRenderer.enabled = alpha > 0.01f;
            if (!arrowRenderer.enabled)
                return;

            direction = Vector3.Slerp(direction, desiredDirection, 1f - Mathf.Exp(-10f * dt)).normalized;
            float pulse = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 5f);
            float slide = 0.15f * Mathf.Sin(Time.unscaledTime * 5f);
            arrow.position = transform.position + direction * (radius + slide) + Vector3.up * 0.08f;
            arrow.rotation = Quaternion.LookRotation(direction);

            Color c = color * (alpha * pulse);
            c.a = alpha * pulse;
            block.SetColor(TintId, c);
            arrowRenderer.SetPropertyBlock(block);
        }

        /// <summary>Arah ke tikungan pertama jalur NavMesh; fallback ke garis lurus kalau path gagal.</summary>
        private Vector3 PathDirection(Vector3 targetPos, Vector3 straight)
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit from, 2f, NavMesh.AllAreas)
                && NavMesh.SamplePosition(targetPos, out NavMeshHit to, 3f, NavMesh.AllAreas)
                && NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, path)
                && path.corners.Length >= 2)
            {
                foreach (Vector3 corner in path.corners)
                {
                    Vector3 d = corner - transform.position;
                    d.y = 0f;
                    if (d.magnitude > 1.2f)
                        return d.normalized;
                }
            }
            return straight.normalized;
        }
    }
}
