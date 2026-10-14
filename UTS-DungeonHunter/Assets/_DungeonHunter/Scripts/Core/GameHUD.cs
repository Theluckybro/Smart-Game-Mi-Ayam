using UnityEngine;

namespace DungeonHunter
{
    /// <summary>
    /// HUD sederhana (IMGUI) untuk tahap progress:
    /// HP player, objective, status key, pesan, HP bar enemy, layar WIN / LOSE.
    /// TODO (progress berikutnya): ganti ke uGUI / UI Toolkit dengan aset UI final.
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        [SerializeField] private float messageDuration = 3.5f;

        private GameManager gm;
        private Camera cam;
        private Texture2D white;
        private GUIStyle label;
        private GUIStyle bigLabel;
        private GUIStyle centerLabel;
        private float damageFlash;

        private void Start()
        {
            gm = GameManager.Instance;
            cam = Camera.main;
            white = Texture2D.whiteTexture;

            if (gm != null && gm.PlayerHealth != null)
                gm.PlayerHealth.Damaged += (amount, source) => damageFlash = 0.35f;
        }

        private void Update()
        {
            damageFlash = Mathf.Max(0f, damageFlash - Time.deltaTime);
        }

        private void EnsureStyles()
        {
            if (label != null)
                return;

            label = new GUIStyle(GUI.skin.label) { fontSize = 16, richText = true };
            label.normal.textColor = Color.white;
            centerLabel = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, fontSize = 18 };
            bigLabel = new GUIStyle(centerLabel) { fontSize = 48, fontStyle = FontStyle.Bold };
        }

        private void OnGUI()
        {
            if (gm == null)
                return;

            EnsureStyles();

            if (damageFlash > 0f)
                DrawRect(new Rect(0, 0, Screen.width, Screen.height), new Color(0.8f, 0f, 0f, damageFlash * 0.6f));

            DrawEnemyBars();

            Health hp = gm.PlayerHealth;
            DrawRect(new Rect(16, 16, 260, 100), new Color(0f, 0f, 0f, 0.55f));
            GUI.Label(new Rect(26, 20, 240, 24), "<b>HP</b>", label);
            DrawBar(new Rect(60, 26, 200, 16), hp != null ? hp.Normalized : 0f, new Color(0.85f, 0.15f, 0.15f));
            GUI.Label(new Rect(26, 46, 240, 24), $"Objective: <b>{gm.Objective}</b>", label);
            GUI.Label(new Rect(26, 68, 240, 24),
                $"Key: {(gm.HasKey ? "<color=#ffd700>✔ Dimiliki</color>" : "<color=#aaa>Belum</color>")}   Kill: {gm.EnemiesKilled}",
                label);
            GUI.Label(new Rect(26, 90, 240, 24), $"Waktu: {gm.ElapsedTime:0}s", label);

            GUI.Label(new Rect(Screen.width - 330, 16, 320, 24),
                "<size=13>WASD gerak · Shift sprint · Klik/Space serang · F1 AI Debug</size>", label);

            if (Time.time - gm.LastMessageTime < messageDuration && gm.IsPlaying)
            {
                DrawRect(new Rect(Screen.width * 0.5f - 300, 70, 600, 36), new Color(0f, 0f, 0f, 0.55f));
                GUI.Label(new Rect(Screen.width * 0.5f - 300, 70, 600, 36), gm.LastMessage, centerLabel);
            }

            if (!gm.IsPlaying)
                DrawEndScreen();
        }

        private void DrawEnemyBars()
        {
            if (cam == null || AIDebugManager.DebugEnabled)
                return;

            foreach (EnemyAI e in EnemyAI.All)
            {
                if (e == null || e.State == EnemyState.Dead || e.Health.Normalized >= 1f)
                    continue;

                Vector3 s = cam.WorldToScreenPoint(e.transform.position + Vector3.up * 2.2f);
                if (s.z < 0f)
                    continue;

                DrawBar(new Rect(s.x - 25, Screen.height - s.y, 50, 6), e.Health.Normalized, new Color(0.9f, 0.3f, 0.2f));
            }
        }

        private void DrawEndScreen()
        {
            bool won = gm.State == GameState.Won;
            DrawRect(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.65f));

            float cy = Screen.height * 0.5f;
            GUI.Label(new Rect(0, cy - 80, Screen.width, 60),
                won ? "<color=#ffd700>VICTORY</color>" : "<color=#ff4444>DEFEAT</color>", bigLabel);
            GUI.Label(new Rect(0, cy - 10, Screen.width, 30), gm.LastMessage, centerLabel);
            GUI.Label(new Rect(0, cy + 20, Screen.width, 30),
                $"Waktu {gm.ElapsedTime:0}s · Enemy dikalahkan {gm.EnemiesKilled}", centerLabel);
            GUI.Label(new Rect(0, cy + 60, Screen.width, 30), "Tekan <b>R</b> untuk main lagi", centerLabel);
        }

        private void DrawBar(Rect r, float t, Color fill)
        {
            DrawRect(r, new Color(0.15f, 0.15f, 0.15f, 0.9f));
            DrawRect(new Rect(r.x, r.y, r.width * Mathf.Clamp01(t), r.height), fill);
        }

        private void DrawRect(Rect r, Color c)
        {
            Color prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, white);
            GUI.color = prev;
        }
    }
}
