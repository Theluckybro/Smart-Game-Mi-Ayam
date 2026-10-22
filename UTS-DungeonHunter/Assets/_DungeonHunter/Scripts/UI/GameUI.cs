using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace DungeonHunter.UI
{
    /// <summary>
    /// Seluruh UI game (uGUI), dibangun lewat kode saat runtime:
    /// - Main Menu (+ Cara Bermain), HUD (HP, Key, objective, toast, hint), Boss Bar Guardian,
    ///   bar HP enemy, vignette damage, Pause, layar Victory / Defeat.
    /// Semua animasi memakai unscaled time agar tetap jalan saat pause / slow-motion.
    /// </summary>
    public class GameUI : MonoBehaviour
    {
        private const float ToastDuration = 3.2f;
        private const float EndScreenDelay = 1.2f;

        private GameManager gm;
        private Health playerHp;
        private GuardianEnemy guardian;

        private RectTransform root;
        private readonly Dictionary<CanvasGroup, float> groupTargets = new Dictionary<CanvasGroup, float>();
        private CanvasGroup hudGroup, menuGroup, howToGroup, pauseGroup, endGroup, bossGroup, toastGroup;

        // HUD
        private RectTransform hpFill, hpChip;
        private Text hpText, keyText, statsText;
        private Image keyGlow, keyFrame;
        private readonly List<Text> objectiveTexts = new List<Text>();
        private readonly List<Image> objectiveBullets = new List<Image>();
        private RectTransform toastRt;
        private Text toastText;
        private float toastShownAt = -100f;
        private float lastSeenMessageTime = -100f;
        private Image vignette;
        private float damageFlash;
        private float healFlash;
        private PlayerController playerController;
        private RectTransform dodgeFill;
        private Text dodgeLabel;
        private float hpChipValue = 1f, hpChipDelay;

        // Boss
        private RectTransform bossFill, bossChip;
        private Image bossFillImage;
        private Text bossPhase;
        private float bossChipValue = 1f, bossChipDelay;

        // Bar HP enemy
        private RectTransform enemyBarRoot;
        private readonly List<(RectTransform root, RectTransform fill)> enemyBars = new List<(RectTransform, RectTransform)>();

        // Menu
        private RectTransform titleRt;
        private Image titleGlow;

        // Pause
        private Text debugButtonText;

        // End
        private RectTransform endTitleRt;
        private Text endTitle, endSubtitle, endStats;
        private float endShowAt = -1f;
        private bool endHitPlayed;

        // =====================================================
        // SETUP
        // =====================================================

        private void Awake()
        {
            gameObject.AddComponent<ScreenFader>();
            BuildCanvas();
            BuildHud();
            BuildBossBar();
            BuildMenu();
            BuildHowTo();
            BuildPause();
            BuildEnd();
            BuildLetterbox();
            EnsureEventSystem();
        }

        // ---- Letterbox (bar hitam sinematik) ----
        private RectTransform letterTop, letterBottom;
        private float letterAmount;

        private void BuildLetterbox()
        {
            letterTop = UIKit.NewRect("LetterboxTop", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(4000f, 90f));
            letterBottom = UIKit.NewRect("LetterboxBottom", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(4000f, 90f));
            UIKit.AddImage(letterTop, null, Color.black);
            UIKit.AddImage(letterBottom, null, Color.black);
            letterTop.SetSiblingIndex(1);
            letterBottom.SetSiblingIndex(1);
        }

        private void UpdateLetterbox(float dt)
        {
            letterAmount = Mathf.MoveTowards(letterAmount, TopDownCamera.CinematicActive ? 1f : 0f, dt * 2.5f);
            float e = letterAmount * letterAmount * (3f - 2f * letterAmount);
            letterTop.anchoredPosition = new Vector2(0f, -90f * e);
            letterBottom.anchoredPosition = new Vector2(0f, 90f * e);
        }

        private void Start()
        {
            gm = GameManager.Instance;
            playerHp = gm != null ? gm.PlayerHealth : null;
            guardian = FindFirstObjectByType<GuardianEnemy>();

            if (playerHp != null)
            {
                playerHp.Damaged += (amount, source) =>
                {
                    damageFlash = Mathf.Min(1f, damageFlash + 0.55f);
                    hpChipDelay = 0.45f;
                };
                playerHp.Healed += amount => healFlash = 1f;
                playerController = playerHp.GetComponent<PlayerController>();
            }
            if (guardian != null)
                guardian.Health.Damaged += (amount, source) => bossChipDelay = 0.45f;

            if (gm != null)
            {
                gm.StateChanged += OnStateChanged;
                OnStateChanged(gm.State);
            }
            foreach (CanvasGroup g in new List<CanvasGroup>(groupTargets.Keys))
                g.alpha = groupTargets[g];
        }

        private void OnDestroy()
        {
            if (gm != null)
                gm.StateChanged -= OnStateChanged;
        }

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null)
                return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }

        private void BuildCanvas()
        {
            var canvasGo = new GameObject("UI Canvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            root = (RectTransform)canvasGo.transform;
        }

        private CanvasGroup Group(string name, Transform parent)
        {
            RectTransform rt = UIKit.Stretch(name, parent);
            var g = rt.gameObject.AddComponent<CanvasGroup>();
            groupTargets[g] = 0f;
            g.alpha = 0f;
            return g;
        }

        private void Show(CanvasGroup g, bool visible) => groupTargets[g] = visible ? 1f : 0f;

        // =====================================================
        // HUD
        // =====================================================

        private void BuildHud()
        {
            vignette = UIKit.AddImage(UIKit.Stretch("DamageVignette", root), UIKit.VignetteSprite, new Color(0.8f, 0f, 0.05f, 0f));

            enemyBarRoot = UIKit.Stretch("EnemyBars", root);
            hudGroup = Group("HUD", root);
            Transform hud = hudGroup.transform;

            // ---- Panel player ----
            RectTransform player = UIKit.NewRect("PlayerPanel", hud, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -28f), new Vector2(470f, 112f));
            UIKit.AddImage(player, UIKit.PanelSprite, Color.white, true);
            UIKit.Label("Name", player, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -12f), new Vector2(240f, 28f),
                "KNIGHT", 20, UIKit.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
            (hpFill, hpChip) = UIKit.MakeBar(player, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -46f), new Vector2(340f, 28f), UIKit.HpRed);
            hpText = UIKit.Label("HP", player, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -78f), new Vector2(340f, 24f),
                "100 / 100", 18, UIKit.TextLight);

            // Indikator dodge: bar kecil yang terisi saat dodge siap
            dodgeLabel = UIKit.Label("DodgeLabel", player, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(180f, -78f), new Vector2(80f, 24f),
                "DODGE", 16, UIKit.TextDim, TextAnchor.MiddleRight, FontStyle.Bold);
            RectTransform dodgeChip;
            (dodgeFill, dodgeChip) = UIKit.MakeBar(player, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(268f, -84f), new Vector2(92f, 12f),
                new Color(0.45f, 0.8f, 1f));
            dodgeChip.gameObject.SetActive(false);

            keyFrame = UIKit.AddImage(UIKit.NewRect("KeySlot", player, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-18f, 0f), new Vector2(78f, 78f)),
                UIKit.PanelSprite, new Color(0.6f, 0.6f, 0.6f), true);
            keyGlow = UIKit.AddImage(UIKit.Stretch("Glow", keyFrame.transform, -18f), UIKit.GlowSprite, new Color(1f, 0.8f, 0.3f, 0f));
            keyText = UIKit.AddText(UIKit.Stretch("Text", keyFrame.transform), "KEY", 18, UIKit.TextDim, TextAnchor.MiddleCenter, FontStyle.Bold);

            // ---- Panel objective ----
            RectTransform obj = UIKit.NewRect("ObjectivePanel", hud, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -152f), new Vector2(470f, 96f + GameManager.ObjectiveTexts.Length * 36f));
            UIKit.AddImage(obj, UIKit.PanelSprite, Color.white, true);
            UIKit.Label("Title", obj, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -12f), new Vector2(300f, 28f),
                "OBJECTIVE", 20, UIKit.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIKit.AddImage(UIKit.NewRect("Divider", obj, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -46f), new Vector2(430f, 2f)),
                null, new Color(UIKit.GoldDim.r, UIKit.GoldDim.g, UIKit.GoldDim.b, 0.6f));
            for (int i = 0; i < GameManager.ObjectiveTexts.Length; i++)
            {
                float y = -62f - i * 36f;
                objectiveBullets.Add(UIKit.AddImage(UIKit.NewRect("Bullet" + i, obj, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f),
                    new Vector2(32f, y - 13f), new Vector2(22f, 22f)), UIKit.CircleSprite, UIKit.TextDim));
                objectiveTexts.Add(UIKit.Label("Step" + i, obj, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(56f, y), new Vector2(390f, 26f),
                    GameManager.ObjectiveTexts[i], 20, UIKit.TextDim));
            }
            statsText = UIKit.Label("Stats", obj, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(20f, 12f), new Vector2(430f, 24f),
                "", 17, UIKit.TextDim);

            // ---- Toast pesan ----
            toastGroup = Group("Toast", hud);
            toastRt = UIKit.NewRect("ToastBox", toastGroup.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(1000f, 60f));
            UIKit.AddImage(toastRt, UIKit.FadeSprite, new Color(0f, 0f, 0f, 0.78f));
            UIKit.AddImage(UIKit.NewRect("Line", toastRt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(700f, 2f)),
                UIKit.FadeSprite, UIKit.Gold);
            toastText = UIKit.AddText(UIKit.Stretch("Text", toastRt), "", 26, UIKit.TextLight, TextAnchor.MiddleCenter);

            // ---- Hint kontrol ----
            Text hint = UIKit.Label("Hint", hud, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(1400f, 30f),
                "WASD  Gerak     Shift  Sprint     Klik  Serang     Space / Klik Kanan  Dodge     F1  AI Debug     Esc  Pause", 18,
                new Color(UIKit.TextLight.r, UIKit.TextLight.g, UIKit.TextLight.b, 0.7f), TextAnchor.MiddleCenter);
            hint.fontStyle = FontStyle.Bold;
        }

        private void BuildBossBar()
        {
            bossGroup = Group("BossBar", hudGroup.transform);
            RectTransform box = UIKit.NewRect("Box", bossGroup.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(820f, 88f));
            UIKit.AddImage(box, UIKit.PanelSprite, Color.white, true);
            UIKit.Label("Name", box, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(22f, -10f), new Vector2(420f, 32f),
                "CRYPT GUARDIAN", 26, UIKit.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
            bossPhase = UIKit.Label("Phase", box, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-22f, -12f), new Vector2(360f, 28f),
                "PHASE 1", 18, UIKit.TextLight, TextAnchor.MiddleRight, FontStyle.Bold);
            (bossFill, bossChip) = UIKit.MakeBar(box, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(776f, 24f), UIKit.BossPurple);
            // garis batas phase (66% & 33%)
            foreach (float mark in new[] { 0.66f, 0.33f })
            {
                RectTransform tick = UIKit.NewRect("PhaseMark", bossFill.parent, new Vector2(mark, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(3f, 30f));
                UIKit.AddImage(tick, null, new Color(1f, 0.9f, 0.7f, 0.85f));
            }
            bossFillImage = bossFill.GetComponent<Image>();
        }

        // =====================================================
        // MENU
        // =====================================================

        private void BuildMenu()
        {
            menuGroup = Group("MainMenu", root);
            Transform m = menuGroup.transform;
            UIKit.AddImage(UIKit.Stretch("Backdrop", m), null, new Color(0.02f, 0.01f, 0.04f, 0.5f));
            UIKit.AddImage(UIKit.Stretch("Vignette", m), UIKit.VignetteSprite, new Color(0f, 0f, 0f, 0.85f));

            titleGlow = UIKit.AddImage(UIKit.NewRect("TitleGlow", m, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -270f), new Vector2(1500f, 520f)),
                UIKit.GlowSprite, new Color(1f, 0.6f, 0.2f, 0.22f));

            Text title = UIKit.Label("Title", m, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -270f), new Vector2(1500f, 160f),
                "CRYPTBORNE", 136, UIKit.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            var outline = title.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.25f, 0.1f, 0.02f, 1f);
            outline.effectDistance = new Vector2(3f, -3f);
            titleRt = (RectTransform)title.transform;

            UIKit.MakeButton("Play", m, new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(400f, 70f), "MULAI", () => gm?.StartGame(), 30);
            UIKit.MakeButton("HowTo", m, new Vector2(0.5f, 0.5f), new Vector2(0f, -126f), new Vector2(400f, 70f), "CARA BERMAIN", () => OpenHowTo(true), 30);
            UIKit.MakeButton("Quit", m, new Vector2(0.5f, 0.5f), new Vector2(0f, -212f), new Vector2(400f, 70f), "KELUAR", () => gm?.Quit(), 30);

            UIKit.Label("Credit", m, Vector2.zero, Vector2.zero, new Vector2(28f, 20f), new Vector2(900f, 26f),
                "Aset 3D & animasi: KayKit oleh Kay Lousberg (CC0)", 17, UIKit.TextDim);
        }

        private void BuildHowTo()
        {
            howToGroup = Group("HowTo", root);
            Transform h = howToGroup.transform;
            UIKit.AddImage(UIKit.Stretch("Backdrop", h), null, new Color(0f, 0f, 0f, 0.7f));
            RectTransform panel = UIKit.NewRect("Panel", h, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1260f, 720f));
            UIKit.AddImage(panel, UIKit.PanelSprite, Color.white, true);
            UIKit.Label("Title", panel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(800f, 56f),
                "CARA BERMAIN", 44, UIKit.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);

            Column(panel, -400f, "KONTROL",
                "WASD  ·  Gerak\nMouse  ·  Arah hadap\n\nKlik kiri / J\nSerang (pedang)\n\nSpace / Klik kanan\nDodge (kebal sesaat)\n\nShift\nSprint (berisik!)\n\nF1  ·  AI Debug Mode\nEsc  ·  Pause");
            Column(panel, 0f, "OBJECTIVE",
                "1.  Tarik tuas di Armory (barat)\n2.  Ambil Key di Treasury (timur)\n3.  Gerbang menutup! Kabur lewat\n     Katakombe, buka jalan pintas\n4.  Kalahkan Crypt Guardian (utara)\n5.  Ambil Ancient Artifact\n\nIkuti panah emas di lantai\nPotion merah = pulihkan HP\nHP habis = kalah");
            Column(panel, 400f, "TIPS MELAWAN AI",
                "Ikon  !  = musuh melihatmu\nIkon  ?  = musuh curiga\n\nSprint & serangan terdengar\nmusuh di sekitar\n\nLingkaran merah = serangan\nakan datang, DODGE!\n\nArcher selalu menjaga jarak\n\nMusuh menghindari jebakan duri\n\nGuardian tidak keluar arena,\nmengamuk saat HP 50%");

            UIKit.MakeButton("Back", panel, new Vector2(0.5f, 0f), new Vector2(0f, 56f), new Vector2(320f, 62f), "KEMBALI", () => OpenHowTo(false), 26);
        }

        private static void Column(RectTransform panel, float x, string header, string body)
        {
            UIKit.Label(header, panel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(x, -110f), new Vector2(360f, 34f),
                header, 24, UIKit.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Label(header + "Body", panel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(x, -160f), new Vector2(360f, 420f),
                body, 20, UIKit.TextLight, TextAnchor.UpperCenter);
        }

        private void OpenHowTo(bool open)
        {
            Show(howToGroup, open);
            Show(menuGroup, !open && gm != null && gm.State == GameState.Menu);
        }

        // =====================================================
        // PAUSE & END
        // =====================================================

        private void BuildPause()
        {
            pauseGroup = Group("Pause", root);
            Transform p = pauseGroup.transform;
            UIKit.AddImage(UIKit.Stretch("Backdrop", p), null, new Color(0f, 0f, 0f, 0.6f));
            RectTransform panel = UIKit.NewRect("Panel", p, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560f, 700f));
            UIKit.AddImage(panel, UIKit.PanelSprite, Color.white, true);
            UIKit.Label("Title", panel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(500f, 70f),
                "PAUSED", 60, UIKit.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);

            UIKit.MakeButton("Resume", panel, new Vector2(0.5f, 0.5f), new Vector2(0f, 160f), new Vector2(380f, 64f), "LANJUTKAN", () => gm?.Resume());
            UIKit.MakeButton("Restart", panel, new Vector2(0.5f, 0.5f), new Vector2(0f, 82f), new Vector2(380f, 64f), "ULANGI", () => gm?.Restart(true));
            Button debug = UIKit.MakeButton("Debug", panel, new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), new Vector2(380f, 64f), "AI DEBUG: OFF",
                () => AIDebugManager.SetEnabled(!AIDebugManager.DebugEnabled));
            debugButtonText = debug.GetComponentInChildren<Text>();
            UIKit.MakeButton("Menu", panel, new Vector2(0.5f, 0.5f), new Vector2(0f, -74f), new Vector2(380f, 64f), "MENU UTAMA", () => gm?.Restart(false));

            // ---- Volume ----
            UIKit.AddImage(UIKit.NewRect("Divider", panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -140f), new Vector2(440f, 2f)),
                UIKit.FadeSprite, UIKit.GoldDim);
            UIKit.MakeSlider("MusicVolume", panel, new Vector2(0.5f, 0.5f), new Vector2(0f, -190f), "MUSIK",
                AudioManager.MusicVolume, v => { AudioManager.SetMusicVolume(v); VolumeTick(v); });
            UIKit.MakeSlider("SfxVolume", panel, new Vector2(0.5f, 0.5f), new Vector2(0f, -250f), "EFEK SUARA",
                AudioManager.SfxVolume, v => { AudioManager.SetSfxVolume(v); VolumeTick(v); });
        }

        private float lastTickValue = -1f;

        /// <summary>Bunyi "tik" setiap slider bergeser 10% sebagai umpan balik volume.</summary>
        private void VolumeTick(float value)
        {
            if (Mathf.Abs(value - lastTickValue) < 0.1f)
                return;
            lastTickValue = value;
            AudioManager.Play2D(Sfx.UIHover, 1.5f, 0.8f + value * 0.5f);
        }

        private void BuildEnd()
        {
            endGroup = Group("EndScreen", root);
            Transform e = endGroup.transform;
            UIKit.AddImage(UIKit.Stretch("Backdrop", e), null, new Color(0f, 0f, 0f, 0.62f));
            UIKit.AddImage(UIKit.Stretch("Vignette", e), UIKit.VignetteSprite, new Color(0f, 0f, 0f, 0.9f));
            RectTransform panel = UIKit.NewRect("Panel", e, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(720f, 560f));
            UIKit.AddImage(panel, UIKit.PanelSprite, Color.white, true);

            endTitle = UIKit.Label("Title", e, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 290f), new Vector2(1200f, 130f),
                "VICTORY", 104, UIKit.Victory, TextAnchor.MiddleCenter, FontStyle.Bold);
            var outline = endTitle.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.15f, 0.05f, 0f, 1f);
            outline.effectDistance = new Vector2(3f, -3f);
            endTitleRt = (RectTransform)endTitle.transform;

            endSubtitle = UIKit.Label("Subtitle", panel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(660f, 36f),
                "", 26, UIKit.TextLight, TextAnchor.MiddleCenter);
            UIKit.AddImage(UIKit.NewRect("Divider", panel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -96f), new Vector2(560f, 2f)),
                UIKit.FadeSprite, UIKit.GoldDim);
            endStats = UIKit.Label("Stats", panel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(560f, 220f),
                "", 26, UIKit.TextLight, TextAnchor.UpperCenter);
            endStats.lineSpacing = 1.25f;

            UIKit.MakeButton("Again", panel, new Vector2(0.5f, 0f), new Vector2(-170f, 64f), new Vector2(300f, 66f), "MAIN LAGI", () => gm?.Restart(true));
            UIKit.MakeButton("Menu", panel, new Vector2(0.5f, 0f), new Vector2(170f, 64f), new Vector2(300f, 66f), "MENU UTAMA", () => gm?.Restart(false));
        }

        private void OnStateChanged(GameState state)
        {
            Show(menuGroup, state == GameState.Menu);
            Show(howToGroup, false);
            Show(hudGroup, state == GameState.Playing || state == GameState.Paused);
            Show(pauseGroup, state == GameState.Paused);
            Show(endGroup, false);

            if (state == GameState.Won || state == GameState.Lost)
            {
                bool won = state == GameState.Won;
                endShowAt = Time.unscaledTime + EndScreenDelay;
                endHitPlayed = false;
                endTitle.text = won ? "VICTORY" : "DEFEAT";
                endTitle.color = won ? UIKit.Victory : UIKit.Defeat;
                endSubtitle.text = won ? "Ancient Artifact berhasil diamankan!" : "Kamu tewas di dalam dungeon...";
                int done = Mathf.Min(gm.ObjectiveStep, GameManager.ObjectiveTexts.Length);
                int total = Mathf.FloorToInt(gm.ElapsedTime);
                endStats.text =
                    $"Waktu   <b>{total / 60:00}:{total % 60:00}</b>\n" +
                    $"Musuh dikalahkan   <b>{gm.EnemiesKilled}</b>\n" +
                    $"Damage diterima   <b>{Mathf.RoundToInt(gm.DamageTaken)}</b>\n" +
                    $"Objective   <b>{done} / {GameManager.ObjectiveTexts.Length}</b>";
                endStats.supportRichText = true;
            }
        }

        // =====================================================
        // UPDATE
        // =====================================================

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            foreach (var kv in new List<KeyValuePair<CanvasGroup, float>>(groupTargets))
            {
                CanvasGroup g = kv.Key;
                g.alpha = Mathf.MoveTowards(g.alpha, kv.Value, dt * 4f);
                bool on = kv.Value > 0.5f;
                g.interactable = on;
                g.blocksRaycasts = on;
            }

            if (gm == null)
                return;

            UpdateHud(dt);
            UpdateBoss(dt);
            UpdateEnemyBars();
            UpdateMenu(dt);
            UpdateEnd(dt);
            UpdateLetterbox(dt);

            if (debugButtonText != null)
                debugButtonText.text = AIDebugManager.DebugEnabled ? "AI DEBUG: ON" : "AI DEBUG: OFF";
        }

        private void UpdateHud(float dt)
        {
            if (playerHp != null)
            {
                float hp = playerHp.Normalized;
                UIKit.SetBar(hpFill, hp);
                hpChipDelay -= dt;
                if (hp > hpChipValue)
                    hpChipValue = hp;
                else if (hpChipDelay <= 0f)
                    hpChipValue = Mathf.MoveTowards(hpChipValue, hp, dt * 0.8f);
                UIKit.SetBar(hpChip, hpChipValue);
                hpText.text = $"{Mathf.CeilToInt(playerHp.CurrentHealth)} / {Mathf.RoundToInt(playerHp.MaxHealth)}";

                // Vignette: kilatan saat kena damage + denyut saat HP kritis
                damageFlash = Mathf.MoveTowards(damageFlash, 0f, dt * 1.8f);
                // HP < 30%: vignette merah berdenyut (detak jantung), makin pekat saat HP makin rendah
                float lowPulse = hp < 0.3f && gm.IsPlaying
                    ? (0.4f + 0.15f * Mathf.Sin(Time.unscaledTime * 6f)) * Mathf.Clamp01(0.5f + (0.3f - hp) / 0.3f)
                    : 0f;
                vignette.color = new Color(0.85f, 0f, 0.05f, Mathf.Clamp01(damageFlash * 0.95f + lowPulse));
                healFlash = Mathf.MoveTowards(healFlash, 0f, dt * 1.5f);
                Color hpColor = hp < 0.3f
                    ? Color.Lerp(UIKit.HpRed, Color.white, 0.25f + 0.25f * Mathf.Sin(Time.unscaledTime * 8f))
                    : UIKit.HpRed;
                hpFill.GetComponent<Image>().color = Color.Lerp(hpColor, new Color(0.45f, 1f, 0.55f), healFlash);
            }

            if (playerController != null)
            {
                float ready = playerController.DodgeReady01;
                UIKit.SetBar(dodgeFill, ready);
                dodgeLabel.color = ready >= 1f ? new Color(0.6f, 0.9f, 1f) : UIKit.TextDim;
            }

            bool hasKey = gm.HasKey;
            keyText.color = hasKey ? UIKit.Gold : UIKit.TextDim;
            keyFrame.color = hasKey ? Color.white : new Color(0.6f, 0.6f, 0.6f);
            keyGlow.color = new Color(1f, 0.8f, 0.3f, hasKey ? 0.35f + 0.15f * Mathf.Sin(Time.unscaledTime * 3f) : 0f);

            int step = gm.ObjectiveStep;
            for (int i = 0; i < objectiveTexts.Count; i++)
            {
                bool done = i < step;
                bool current = i == step;
                objectiveTexts[i].color = done ? new Color(0.55f, 0.75f, 0.5f) : current ? UIKit.Gold : UIKit.TextDim;
                objectiveTexts[i].fontStyle = current ? FontStyle.Bold : FontStyle.Normal;
                objectiveBullets[i].sprite = done ? UIKit.DotSprite : UIKit.CircleSprite;
                objectiveBullets[i].color = done ? new Color(0.55f, 0.85f, 0.5f) : current ? UIKit.Gold : UIKit.TextDim;
                float s = current ? 1f + 0.18f * Mathf.Sin(Time.unscaledTime * 5f) : 1f;
                objectiveBullets[i].rectTransform.localScale = new Vector3(s, s, 1f);
            }

            int t = Mathf.FloorToInt(gm.ElapsedTime);
            statsText.text = $"Waktu  {t / 60:00}:{t % 60:00}        Musuh dikalahkan  {gm.EnemiesKilled}";

            // Toast: muncul saat ada pesan baru, meluncur turun lalu memudar
            if (gm.LastMessageTime > lastSeenMessageTime)
            {
                lastSeenMessageTime = gm.LastMessageTime;
                toastShownAt = Time.unscaledTime;
                toastText.text = gm.LastMessage;
                if (!gm.IsGameOver)
                    AudioManager.Play2D(Sfx.UIToast);
            }
            float age = Time.unscaledTime - toastShownAt;
            bool visible = age < ToastDuration && !gm.IsGameOver;
            groupTargets[toastGroup] = visible ? 1f : 0f;
            float slide = Mathf.Clamp01(age / 0.25f);
            toastRt.anchoredPosition = new Vector2(0f, Mathf.Lerp(10f, -36f, 1f - (1f - slide) * (1f - slide)));
        }

        private void UpdateBoss(float dt)
        {
            bool show = guardian != null && guardian.State != EnemyState.Dead && gm.DoorOpened
                        && (gm.State == GameState.Playing || gm.State == GameState.Paused)
                        && playerHp != null
                        && (guardian.State != EnemyState.Guard
                            || Vector3.Distance(playerHp.transform.position, guardian.HomePosition) < guardian.LeashRadius + 2f);
            if (guardian != null && guardian.State == EnemyState.Dead && gm.State == GameState.Playing)
                show = false;
            groupTargets[bossGroup] = show ? 1f : 0f;
            if (guardian == null)
                return;

            float hp = guardian.Health.Normalized;
            UIKit.SetBar(bossFill, hp);
            bossChipDelay -= dt;
            if (hp > bossChipValue)
                bossChipValue = hp;
            else if (bossChipDelay <= 0f)
                bossChipValue = Mathf.MoveTowards(bossChipValue, hp, dt * 0.6f);
            UIKit.SetBar(bossChip, bossChipValue);

            if (guardian.Phase == 3)
            {
                bossPhase.text = "PHASE 3  ·  ENRAGED" + MinionSuffix();
                bossPhase.color = Color.Lerp(new Color(1f, 0.4f, 0.9f), Color.white, 0.3f + 0.3f * Mathf.Sin(Time.unscaledTime * 6f));
                bossFillImage.color = new Color(0.85f, 0.25f, 0.75f);
            }
            else if (guardian.Phase == 2)
            {
                bossPhase.text = "PHASE 2  ·  NECROMANCER" + MinionSuffix();
                bossPhase.color = new Color(0.45f, 1f, 0.65f);
                bossFillImage.color = new Color(0.35f, 0.75f, 0.55f);
            }
            else
            {
                bossPhase.text = "PHASE 1  ·  WARDEN" + MinionSuffix();
                bossPhase.color = UIKit.TextLight;
                bossFillImage.color = UIKit.BossPurple;
            }
        }

        private string MinionSuffix() => guardian.AliveMinions > 0 ? $"   ·  MINION ×{guardian.AliveMinions}" : "";

        private void UpdateEnemyBars()
        {
            Camera cam = Camera.main;
            int used = 0;
            bool allowed = gm.State == GameState.Playing || gm.State == GameState.Paused;

            if (cam != null && allowed && !AIDebugManager.DebugEnabled)
            {
                foreach (EnemyAI e in EnemyAI.All)
                {
                    if (e == null || e is GuardianEnemy || e.State == EnemyState.Dead || e.Health.Normalized >= 0.999f)
                        continue;

                    Vector3 screen = cam.WorldToScreenPoint(e.transform.position + Vector3.up * 2.4f);
                    if (screen.z < 0f)
                        continue;

                    if (used >= enemyBars.Count)
                        enemyBars.Add(CreateEnemyBar());
                    var bar = enemyBars[used++];
                    bar.root.gameObject.SetActive(true);
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(enemyBarRoot, screen, null, out Vector2 local);
                    bar.root.anchoredPosition = local;
                    UIKit.SetBar(bar.fill, e.Health.Normalized);
                }
            }

            for (int i = used; i < enemyBars.Count; i++)
                enemyBars[i].root.gameObject.SetActive(false);
        }

        private (RectTransform, RectTransform) CreateEnemyBar()
        {
            RectTransform rt = UIKit.NewRect("EnemyBar", enemyBarRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(84f, 12f));
            UIKit.AddImage(rt, UIKit.BarSprite, new Color(0f, 0f, 0f, 0.7f), true);
            RectTransform fill = UIKit.Stretch("Fill", rt, 2f);
            UIKit.AddImage(fill, UIKit.BarSprite, new Color(0.95f, 0.3f, 0.2f), true);
            return (rt, fill);
        }

        private void UpdateMenu(float dt)
        {
            if (menuGroup.alpha <= 0f)
                return;
            float s = 1f + 0.015f * Mathf.Sin(Time.unscaledTime * 1.6f);
            titleRt.localScale = new Vector3(s, s, 1f);
            titleGlow.color = new Color(1f, 0.6f, 0.2f, 0.18f + 0.08f * Mathf.Sin(Time.unscaledTime * 1.6f));
        }

        private void UpdateEnd(float dt)
        {
            if (endShowAt < 0f || Time.unscaledTime < endShowAt)
                return;

            groupTargets[endGroup] = 1f;
            groupTargets[hudGroup] = 0f;
            if (!endHitPlayed)
            {
                endHitPlayed = true;
                AudioManager.Play2D(Sfx.UIEndHit);
            }
            float t = (Time.unscaledTime - endShowAt) / 0.4f;
            float pop = t >= 1f ? 1f : 1f + 0.8f * (1f - t) * (1f - t) - 0.08f * Mathf.Sin(t * Mathf.PI);
            float idle = t >= 1f ? 0.02f * Mathf.Sin(Time.unscaledTime * 2f) : 0f;
            endTitleRt.localScale = Vector3.one * (pop + idle);
        }
    }
}
