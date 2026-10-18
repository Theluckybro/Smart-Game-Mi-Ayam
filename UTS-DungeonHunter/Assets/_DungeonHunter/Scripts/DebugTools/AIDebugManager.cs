using UnityEngine;
using UnityEngine.InputSystem;

namespace DungeonHunter
{
    /// <summary>
    /// AI DEBUG MODE (F1).
    /// Menampilkan panel info di atas setiap enemy:
    /// State, alasan keputusan, Target, Can See, Distance, Destination, Path, Last Known Position, HP.
    /// Visual garis (FOV, path, LKP, range) digambar oleh EnemyDebugVisual.
    /// </summary>
    public class AIDebugManager : MonoBehaviour
    {
        public static bool DebugEnabled { get; private set; }

        public static void SetEnabled(bool value) => DebugEnabled = value;

        [SerializeField] private bool enabledOnStart;

        private GUIStyle panelStyle;
        private GUIStyle titleStyle;
        private Camera cam;

        private void Awake()
        {
            DebugEnabled = enabledOnStart;
        }

        private void Start()
        {
            cam = Camera.main;
            foreach (EnemyAI enemy in FindObjectsByType<EnemyAI>(FindObjectsSortMode.None))
            {
                if (enemy.GetComponent<EnemyDebugVisual>() == null)
                    enemy.gameObject.AddComponent<EnemyDebugVisual>();
            }

            if (GetComponent<NavigationDebugVisual>() == null)
                gameObject.AddComponent<NavigationDebugVisual>();
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
                DebugEnabled = !DebugEnabled;
        }

        private void EnsureStyles()
        {
            if (panelStyle != null)
                return;

            var bg = new Texture2D(1, 1);
            bg.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.72f));
            bg.Apply();

            panelStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 11,
                richText = true,
                padding = new RectOffset(6, 6, 4, 4)
            };
            panelStyle.normal.background = bg;
            panelStyle.normal.textColor = Color.white;

            titleStyle = new GUIStyle(panelStyle) { fontSize = 13, fontStyle = FontStyle.Bold };
        }

        private void OnGUI()
        {
            if (!DebugEnabled)
                return;

            EnsureStyles();
            if (cam == null)
                cam = Camera.main;

            GUI.Box(new Rect(10, Screen.height - 178, 360, 168),
                "<b>AI DEBUG MODE</b> (F1)\n" +
                "<color=#ffe633>■</color> FOV   <color=#ff3333>■</color> FOV melihat player\n" +
                "<color=#33e6ff>■</color> NavMesh Path   <color=#ff33ff>■</color> Last Known Position\n" +
                "<color=#ff4d4d>■</color> Attack range (melee)   <color=#ff8019>■</color> Maintain distance\n" +
                "<color=#b34dff>■</color> Leash Guardian   <color=#ff9900>■</color> Arah Charge\n" +
                "<color=#33ff66>■</color> Retreat Point / jalur Flee\n" +
                "<color=#ff2a2a>■</color> Area Trap (cost tinggi)   <color=#ffffff>■</color> NavMeshLink\n" +
                "<color=#aaaaaa>■</color> Gerbang besi (dynamic obstacle)", panelStyle);

            foreach (EnemyAI e in EnemyAI.All)
            {
                if (e == null || e.State == EnemyState.Dead || cam == null)
                    continue;

                Vector3 screen = cam.WorldToScreenPoint(e.transform.position + Vector3.up * 2.4f);
                if (screen.z < 0f)
                    continue;

                string text = BuildInfo(e);
                Vector2 size = panelStyle.CalcSize(new GUIContent(text));
                var rect = new Rect(screen.x - size.x * 0.5f, Screen.height - screen.y - size.y, size.x, size.y);
                GUI.Box(rect, text, panelStyle);
            }
        }

        private static string BuildInfo(EnemyAI e)
        {
            AIPerception p = e.Perception;
            AIMemory m = e.Memory;
            string see = p.CanSeeTarget ? "<color=#5f5>TRUE</color>" : "<color=#f66>FALSE</color>";
            string why = p.CanSeeTarget ? "" :
                !p.InViewRadius ? " (luar radius)" :
                !p.InFieldOfView ? " (luar FOV)" :
                p.LineOfSightBlocked ? " (terhalang)" : "";

            string path = "-";
            string nav = "-";
            if (e.Agent != null && e.Agent.enabled)
            {
                path = e.Agent.hasPath
                    ? $"ACTIVE ({e.Agent.pathStatus}, {EnemyAI.CornersLength(e.Agent.path.corners):0.0}m)"
                    : "NONE";
                nav = e.IsTraversingLink ? "<color=#ffffff><b>LOMPAT via NavMeshLink</b></color>" : $"area {e.CurrentAreaName}";
            }

            string lkp = m.HasLastKnownPosition
                ? $"({m.LastKnownPosition.x:0.0}, {m.LastKnownPosition.z:0.0}) via {m.LastSource}"
                : "-";

            string text =
                $"<b>{e.DisplayName}</b>  HP {e.Health.CurrentHealth:0}/{e.Health.MaxHealth:0}\n" +
                $"State : <b><color=#ffd24d>{e.State.ToString().ToUpper()}</color></b>\n" +
                $"Why : {e.Reason}\n" +
                $"Target : {(e.Target != null ? e.Target.name : "-")}\n" +
                $"Can See : {see}{why}\n" +
                $"Distance : {p.DistanceToTarget:0.0}\n" +
                $"Destination : {e.DestinationLabel}\n" +
                $"Path : {path}\n" +
                $"Nav : {nav}\n" +
                $"Last Known : {lkp}";

            if (e.State == EnemyState.Flee && !string.IsNullOrEmpty(e.RetreatInfo))
                text += $"\nRetreat : {e.RetreatInfo}";

            if (!string.IsNullOrEmpty(e.ExtraDebugInfo))
                text += "\n" + e.ExtraDebugInfo;

            return text;
        }
    }
}
