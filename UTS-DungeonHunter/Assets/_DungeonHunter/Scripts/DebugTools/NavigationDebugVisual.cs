using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;

namespace DungeonHunter
{
    /// <summary>
    /// Debug visual navigasi level (aktif bersama AI Debug Mode / F1):
    /// - Zona Trap (area cost tinggi)     : kotak merah bersilang
    /// - NavMeshLink (lompat turun)       : panah putih
    /// - Retreat Point (tujuan Flee)      : lingkaran hijau
    /// - Gerbang besi (dynamic obstacle)  : abu-abu (terbuka) / merah (tertutup)
    /// </summary>
    public class NavigationDebugVisual : MonoBehaviour
    {
        private const float LineY = 0.1f;

        private static Material lineMaterial;
        private readonly List<LineRenderer> lines = new List<LineRenderer>();
        private readonly List<Portcullis> gates = new List<Portcullis>();
        private readonly List<LineRenderer> gateLines = new List<LineRenderer>();
        private Transform root;

        private void Start()
        {
            if (lineMaterial == null)
                lineMaterial = new Material(Shader.Find("Sprites/Default"));
            root = new GameObject("Navigation Debug Lines").transform;

            foreach (SpikeTrap trap in FindObjectsByType<SpikeTrap>(FindObjectsSortMode.None))
                DrawTrap(trap);

            foreach (NavMeshLink link in FindObjectsByType<NavMeshLink>(FindObjectsSortMode.None))
                DrawLink(link);

            foreach (Transform t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (t.name.StartsWith("RetreatPoint_"))
                    DrawCircle(t.position, 0.7f, new Color(0.2f, 1f, 0.4f, 0.9f));

            foreach (Portcullis gate in FindObjectsByType<Portcullis>(FindObjectsSortMode.None))
            {
                gates.Add(gate);
                gateLines.Add(DrawGate(gate));
            }

            root.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (root != null)
                Destroy(root.gameObject);
        }

        private void LateUpdate()
        {
            if (root == null)
                return;

            root.gameObject.SetActive(AIDebugManager.DebugEnabled);

            for (int i = 0; i < gates.Count; i++)
            {
                Color c = gates[i].IsClosed ? new Color(1f, 0.25f, 0.25f, 1f) : new Color(0.7f, 0.7f, 0.7f, 0.8f);
                gateLines[i].startColor = c;
                gateLines[i].endColor = c;
            }
        }

        private void DrawTrap(SpikeTrap trap)
        {
            Vector3 c = trap.transform.position + Vector3.up * LineY;
            Vector3 hx = trap.transform.right * trap.Size.x * 0.5f;
            Vector3 hz = trap.transform.forward * trap.Size.z * 0.5f;
            Color red = new Color(1f, 0.15f, 0.15f, 0.9f);

            CreateLine(red, 0.06f, true, c - hx - hz, c + hx - hz, c + hx + hz, c - hx + hz);
            CreateLine(red, 0.03f, false, c - hx - hz, c + hx + hz);
            CreateLine(red, 0.03f, false, c + hx - hz, c - hx + hz);
        }

        private void DrawLink(NavMeshLink link)
        {
            Vector3 a = link.transform.TransformPoint(link.startPoint) + Vector3.up * LineY;
            Vector3 b = link.transform.TransformPoint(link.endPoint) + Vector3.up * LineY;
            Vector3 mid = (a + b) * 0.5f + Vector3.up * 1f;
            Color white = new Color(1f, 1f, 1f, 0.95f);

            var arc = new List<Vector3>();
            for (int i = 0; i <= 12; i++)
            {
                float t = i / 12f;
                arc.Add(Vector3.Lerp(Vector3.Lerp(a, mid, t), Vector3.Lerp(mid, b, t), t));
            }
            CreateLine(white, 0.08f, false, arc.ToArray());

            Vector3 dir = (b - arc[10]).normalized;
            Vector3 side = Vector3.Cross(dir, Vector3.up).normalized * 0.4f;
            CreateLine(white, 0.08f, false, b - dir * 0.6f + side, b, b - dir * 0.6f - side);

            Vector3 widthDir = link.transform.right * link.width * 0.5f;
            CreateLine(white, 0.04f, false, a - widthDir, a + widthDir);
            CreateLine(white, 0.04f, false, b - widthDir, b + widthDir);
        }

        private void DrawCircle(Vector3 center, float radius, Color color)
        {
            var pts = new Vector3[25];
            for (int i = 0; i < pts.Length; i++)
            {
                float a = i / 24f * Mathf.PI * 2f;
                pts[i] = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius + Vector3.up * LineY;
            }
            CreateLine(color, 0.06f, false, pts);
        }

        private LineRenderer DrawGate(Portcullis gate)
        {
            Renderer rend = gate.GetComponentInChildren<Renderer>(true);
            Bounds b = rend != null ? rend.bounds : new Bounds(gate.transform.position, Vector3.one);
            float y = gate.transform.position.y + LineY;
            Vector3 p0 = new Vector3(b.min.x, y, b.min.z);
            Vector3 p1 = new Vector3(b.max.x, y, b.min.z);
            Vector3 p2 = new Vector3(b.max.x, y, b.max.z);
            Vector3 p3 = new Vector3(b.min.x, y, b.max.z);
            return CreateLine(Color.gray, 0.12f, true, p0, p1, p2, p3);
        }

        private LineRenderer CreateLine(Color color, float width, bool loop, params Vector3[] points)
        {
            var go = new GameObject("NavDebugLine");
            go.transform.SetParent(root, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = lineMaterial;
            lr.useWorldSpace = true;
            lr.loop = loop;
            lr.widthMultiplier = width;
            lr.numCapVertices = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.startColor = color;
            lr.endColor = color;
            lr.positionCount = points.Length;
            lr.SetPositions(points);
            lines.Add(lr);
            return lr;
        }
    }
}
