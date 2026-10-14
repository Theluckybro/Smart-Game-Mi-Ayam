using UnityEngine;

namespace DungeonHunter
{
    /// <summary>
    /// Debug visual runtime (terlihat di Game view, bukan hanya Scene view):
    /// - Field of View (kuning = tidak melihat, merah = melihat player)
    /// - Path NavMesh (cyan)
    /// - Last Known Position (magenta)
    /// - Ring jarak: attack range (melee) / maintain distance (ranged)
    /// </summary>
    [RequireComponent(typeof(EnemyAI))]
    public class EnemyDebugVisual : MonoBehaviour
    {
        private const int ArcSegments = 24;
        private const int RingSegments = 40;
        private const float LineY = 0.08f;

        private static Material lineMaterial;

        private EnemyAI enemy;
        private LineRenderer fovLine;
        private LineRenderer pathLine;
        private LineRenderer memoryLine;
        private LineRenderer ringA;
        private LineRenderer ringB;
        private Transform root;

        private void Awake()
        {
            enemy = GetComponent<EnemyAI>();

            if (lineMaterial == null)
                lineMaterial = new Material(Shader.Find("Sprites/Default"));

            root = new GameObject("AI Debug Lines").transform;
            fovLine = CreateLine("FOV", 0.05f);
            pathLine = CreateLine("Path", 0.07f);
            memoryLine = CreateLine("Last Known Position", 0.05f);
            ringA = CreateLine("Range A", 0.035f);
            ringB = CreateLine("Range B", 0.035f);
        }

        private void OnDisable()
        {
            if (root != null)
                root.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (root != null)
                Destroy(root.gameObject);
        }

        private LineRenderer CreateLine(string lineName, float width)
        {
            var go = new GameObject(lineName);
            go.transform.SetParent(root, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = lineMaterial;
            lr.useWorldSpace = true;
            lr.widthMultiplier = width;
            lr.numCapVertices = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.positionCount = 0;
            return lr;
        }

        private void LateUpdate()
        {
            bool show = AIDebugManager.DebugEnabled && enemy.State != EnemyState.Dead && enemy.isActiveAndEnabled;
            root.gameObject.SetActive(show);
            if (!show)
                return;

            DrawFov();
            DrawPath();
            DrawMemory();
            DrawRings();
        }

        private void DrawFov()
        {
            AIPerception p = enemy.Perception;
            Color c = p.CanSeeTarget ? new Color(1f, 0.15f, 0.15f, 0.9f) : new Color(1f, 0.9f, 0.2f, 0.6f);
            SetColor(fovLine, c);

            fovLine.positionCount = ArcSegments + 3;
            Vector3 origin = Flat(transform.position);
            fovLine.SetPosition(0, origin);

            float half = p.ViewAngle * 0.5f;
            for (int i = 0; i <= ArcSegments; i++)
            {
                float a = Mathf.Lerp(-half, half, i / (float)ArcSegments);
                Vector3 dir = Quaternion.Euler(0f, a, 0f) * transform.forward;
                fovLine.SetPosition(i + 1, origin + dir * p.ViewRadius);
            }
            fovLine.SetPosition(ArcSegments + 2, origin);
        }

        private void DrawPath()
        {
            var agent = enemy.Agent;
            if (agent == null || !agent.enabled || !agent.hasPath)
            {
                pathLine.positionCount = 0;
                return;
            }

            Vector3[] corners = agent.path.corners;
            pathLine.positionCount = corners.Length;
            for (int i = 0; i < corners.Length; i++)
                pathLine.SetPosition(i, Flat(corners[i]) + Vector3.up * 0.02f);
            SetColor(pathLine, new Color(0.2f, 0.9f, 1f, 0.9f));
        }

        private void DrawMemory()
        {
            AIMemory m = enemy.Memory;
            if (!m.HasLastKnownPosition)
            {
                memoryLine.positionCount = 0;
                return;
            }

            Vector3 lkp = Flat(m.LastKnownPosition);
            const float s = 0.45f;
            memoryLine.positionCount = 7;
            memoryLine.SetPosition(0, Flat(transform.position));
            memoryLine.SetPosition(1, lkp);
            memoryLine.SetPosition(2, lkp + new Vector3(s, 0f, 0f));
            memoryLine.SetPosition(3, lkp + new Vector3(0f, 0f, s));
            memoryLine.SetPosition(4, lkp + new Vector3(-s, 0f, 0f));
            memoryLine.SetPosition(5, lkp + new Vector3(0f, 0f, -s));
            memoryLine.SetPosition(6, lkp + new Vector3(s, 0f, 0f));
            SetColor(memoryLine, new Color(1f, 0.2f, 1f, 0.9f));
        }

        private void DrawRings()
        {
            switch (enemy)
            {
                case MeleeEnemy melee:
                    DrawRing(ringA, transform.position, melee.AttackRange, new Color(1f, 0.3f, 0.3f, 0.8f));
                    ringB.positionCount = 0;
                    break;
                case RangedEnemy ranged:
                    DrawRing(ringA, transform.position, ranged.PreferredMin, new Color(1f, 0.5f, 0.1f, 0.8f));
                    DrawRing(ringB, transform.position, ranged.PreferredMax, new Color(1f, 0.75f, 0.2f, 0.6f));
                    break;
                default:
                    ringA.positionCount = 0;
                    ringB.positionCount = 0;
                    break;
            }
        }

        private static void DrawRing(LineRenderer lr, Vector3 center, float radius, Color color)
        {
            lr.positionCount = RingSegments + 1;
            Vector3 c = Flat(center);
            for (int i = 0; i <= RingSegments; i++)
            {
                float a = i / (float)RingSegments * Mathf.PI * 2f;
                lr.SetPosition(i, c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius);
            }
            SetColor(lr, color);
        }

        private static void SetColor(LineRenderer lr, Color c)
        {
            lr.startColor = c;
            lr.endColor = c;
        }

        private static Vector3 Flat(Vector3 p) => new Vector3(p.x, LineY, p.z);
    }
}
