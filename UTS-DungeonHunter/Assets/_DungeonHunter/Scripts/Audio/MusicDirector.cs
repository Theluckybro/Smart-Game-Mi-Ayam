using UnityEngine;

namespace DungeonHunter
{
    public enum MusicMode { Menu, Explore, Tension, Combat, Boss, BossPhase2, BossPhase3, Ending }

    /// <summary>
    /// MUSIK ADAPTIF — musik membaca kondisi AI.
    ///
    /// Musik terdiri dari 4 stem dengan tempo & akor yang sama, diputar serentak (PlayScheduled) lalu di-crossfade:
    ///   explore  : drone + pad + lonceng (selalu ada)
    ///   combat   : taiko + bass ostinato + tremolo
    ///   boss     : drum berat + bass distorsi + brass + choir
    ///   boss2    : lapisan tambahan saat Guardian Phase 2
    ///
    /// Mode ditentukan dari state FSM / Behavior Tree musuh:
    ///   Chase / Attack / Reposition   → COMBAT  (bertahan 4 detik setelah AI terakhir berhenti mengejar)
    ///   Investigate / Search / Flee    → TENSION (lapisan combat pelan)
    ///   Guardian terlibat              → BOSS, Phase 2 (Enrage) → BOSS + lapisan boss2
    ///   tidak ada AI waspada           → EXPLORE
    /// Saat pause musik diredam (low-pass), saat slow-motion juga ikut teredam.
    /// Menang / kalah → stem memudar, diganti stinger victory / defeat.
    /// </summary>
    public class MusicDirector : MonoBehaviour
    {
        public static MusicDirector Instance { get; private set; }

        [SerializeField] private float combatHoldTime = 4f;

        public MusicMode Mode { get; private set; } = MusicMode.Explore;
        public int ChasingEnemies { get; private set; }
        public int SuspiciousEnemies { get; private set; }

        private AudioSource explore, combat, boss, boss2, stinger;
        private AudioLowPassFilter[] filters;
        private float lastCombatTime = -100f;
        private float endingTime = -1f;
        private GuardianEnemy guardian;
        private Transform player;

        private float vExplore, vCombat, vBoss, vBoss2;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            explore = CreateStem("Music_Explore", "Music/music_explore", true);
            combat = CreateStem("Music_Combat", "Music/music_combat", true);
            boss = CreateStem("Music_Boss", "Music/music_boss", true);
            boss2 = CreateStem("Music_Boss2", "Music/music_boss2", true);
            stinger = CreateStem("Music_Stinger", null, false);
            filters = GetComponentsInChildren<AudioLowPassFilter>();

            // Semua stem mulai di waktu DSP yang sama → selalu sinkron ketukannya
            double start = AudioSettings.dspTime + 0.2;
            foreach (AudioSource s in new[] { explore, combat, boss, boss2 })
                if (s.clip != null)
                    s.PlayScheduled(start);

            guardian = FindFirstObjectByType<GuardianEnemy>();
            GameObject p = GameObject.FindWithTag("Player");
            if (p != null)
                player = p.transform;
            if (GameManager.Instance != null)
                GameManager.Instance.StateChanged += OnStateChanged;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            if (GameManager.Instance != null)
                GameManager.Instance.StateChanged -= OnStateChanged;
        }

        private AudioSource CreateStem(string name, string clipPath, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = loop;
            src.spatialBlend = 0f;
            src.volume = 0f;
            src.priority = 0;
            src.ignoreListenerPause = true; // musik tetap jalan saat pause (diredam)
            if (clipPath != null)
                src.clip = Resources.Load<AudioClip>(clipPath);
            go.AddComponent<AudioLowPassFilter>().cutoffFrequency = 22000f;
            return src;
        }

        private void OnStateChanged(GameState state)
        {
            if (state != GameState.Won && state != GameState.Lost)
                return;

            bool won = state == GameState.Won;
            endingTime = Time.unscaledTime;
            stinger.clip = Resources.Load<AudioClip>(won ? "Music/stinger_victory" : "Music/stinger_defeat");
            stinger.volume = 0.9f * AudioManager.MusicVolume;
            stinger.PlayDelayed(0.25f);
            AudioManager.Play2D(won ? Sfx.JingleVictory : Sfx.JingleDefeat, 1f, 1f, 0.35f);
        }

        private void Update()
        {
            UpdateMode();

            // target volume per stem untuk tiap mode
            float tExplore = 1f, tCombat = 0f, tBoss = 0f, tBoss2 = 0f;
            switch (Mode)
            {
                case MusicMode.Menu: tExplore = 0.75f; break;
                case MusicMode.Explore: break;
                case MusicMode.Tension: tExplore = 0.85f; tCombat = 0.4f; break;
                case MusicMode.Combat: tExplore = 0.55f; tCombat = 1f; break;
                case MusicMode.Boss: tExplore = 0.35f; tBoss = 1f; break;
                case MusicMode.BossPhase2: tExplore = 0.3f; tBoss = 1f; tCombat = 0.7f; break;
                case MusicMode.BossPhase3: tExplore = 0.3f; tBoss = 1f; tBoss2 = 1f; break;
                case MusicMode.Ending:
                    // setelah stinger selesai, ambience explore kembali pelan di layar akhir
                    tExplore = Time.unscaledTime - endingTime > 8f ? 0.4f : 0f;
                    break;
            }

            float dt = Time.unscaledDeltaTime;
            vExplore = Fade(vExplore, tExplore, dt, Mode == MusicMode.Ending ? 0.8f : 0.35f, 0.35f);
            vCombat = Fade(vCombat, tCombat, dt, 1.2f, 0.3f);   // masuk cepat, keluar pelan
            vBoss = Fade(vBoss, tBoss, dt, 0.8f, 0.5f);
            vBoss2 = Fade(vBoss2, tBoss2, dt, 1f, 0.5f);

            float master = AudioManager.MusicVolume;
            explore.volume = vExplore * 0.9f * master;
            combat.volume = vCombat * 0.85f * master;
            boss.volume = vBoss * 0.9f * master;
            boss2.volume = vBoss2 * 0.8f * master;
            if (stinger.isPlaying)
                stinger.volume = 0.9f * master;

            // Redam musik saat pause / slow-motion
            GameManager gm = GameManager.Instance;
            float cutoff = 22000f;
            if (gm != null && gm.State == GameState.Paused)
                cutoff = 650f;
            else if (VFXManager.SlowMotionScale < 1f)
                cutoff = 1400f;
            foreach (AudioLowPassFilter f in filters)
                f.cutoffFrequency = Mathf.Lerp(f.cutoffFrequency, cutoff, 1f - Mathf.Exp(-8f * dt));
        }

        private static float Fade(float current, float target, float dt, float upSpeed, float downSpeed) =>
            Mathf.MoveTowards(current, target, dt * (target > current ? upSpeed : downSpeed));

        /// <summary>Tentukan mode musik dari state AI.</summary>
        private void UpdateMode()
        {
            GameManager gm = GameManager.Instance;
            if (gm != null && gm.State == GameState.Menu)
            {
                Mode = MusicMode.Menu;
                return;
            }
            if (gm != null && gm.IsGameOver)
            {
                Mode = MusicMode.Ending;
                return;
            }
            if (gm != null && gm.State == GameState.Paused)
                return; // pertahankan mode terakhir

            int chasing = 0, suspicious = 0;
            foreach (EnemyAI e in EnemyAI.All)
            {
                if (e == null || e == guardian)
                    continue;
                switch (e.State)
                {
                    case EnemyState.Chase:
                    case EnemyState.Attack:
                    case EnemyState.Reposition:
                        chasing++;
                        break;
                    case EnemyState.Investigate:
                    case EnemyState.Search:
                    case EnemyState.Flee:
                        suspicious++;
                        break;
                }
            }
            ChasingEnemies = chasing;
            SuspiciousEnemies = suspicious;
            if (chasing > 0)
                lastCombatTime = Time.time;

            if (GuardianEngaged())
                Mode = guardian.Phase == 3 ? MusicMode.BossPhase3 : guardian.Phase == 2 ? MusicMode.BossPhase2 : MusicMode.Boss;
            else if (chasing > 0 || Time.time - lastCombatTime < combatHoldTime)
                Mode = MusicMode.Combat;
            else if (suspicious > 0)
                Mode = MusicMode.Tension;
            else
                Mode = MusicMode.Explore;
        }

        /// <summary>Guardian dianggap bertarung jika pintu terbuka dan ia tidak sedang berjaga tenang (atau player dekat arena).</summary>
        private bool GuardianEngaged()
        {
            GameManager gm = GameManager.Instance;
            if (guardian == null || guardian.State == EnemyState.Dead || gm == null || !gm.DoorOpened)
                return false;
            if (guardian.State != EnemyState.Guard && guardian.State != EnemyState.Return)
                return true;
            return player != null && Vector3.Distance(player.position, guardian.HomePosition) < guardian.LeashRadius + 2f;
        }

        /// <summary>Teks ringkas untuk AI Debug Mode.</summary>
        public string DebugLabel
        {
            get
            {
                switch (Mode)
                {
                    case MusicMode.Combat:
                        return ChasingEnemies > 0 ? $"COMBAT ({ChasingEnemies} AI mengejar)" : "COMBAT (mereda...)";
                    case MusicMode.Tension: return $"TENSION ({SuspiciousEnemies} AI curiga)";
                    case MusicMode.Boss: return "BOSS (Guardian bertarung)";
                    case MusicMode.BossPhase2: return "BOSS PHASE 2 (Necromancer)";
                    case MusicMode.BossPhase3: return "BOSS PHASE 3 (Enrage)";
                    default: return Mode.ToString().ToUpper();
                }
            }
        }
    }
}
