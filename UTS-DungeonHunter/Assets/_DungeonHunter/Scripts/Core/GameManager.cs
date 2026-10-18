using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace DungeonHunter
{
    public enum GameState
    {
        Menu,
        Playing,
        Paused,
        Won,
        Lost
    }

    /// <summary>
    /// Mengatur gameplay loop:
    /// MENU → START → cari Key → buka pintu → kalahkan Guardian → ambil Artifact → WIN
    /// Player mati → LOSE.  Esc → PAUSE.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        /// <summary>Dipakai tombol "Play Again" agar scene yang di-load ulang langsung masuk gameplay.</summary>
        private static bool skipMenuOnNextLoad;

        [SerializeField] private Health playerHealth;
        [SerializeField] private bool startInMenu = true;

        public GameState State { get; private set; } = GameState.Playing;
        public bool IsPlaying => State == GameState.Playing;
        public bool IsGameOver => State == GameState.Won || State == GameState.Lost;
        public bool HasKey { get; private set; }
        public bool DoorOpened { get; private set; }
        public bool GuardianDefeated { get; private set; }
        public Health PlayerHealth => playerHealth;
        public int EnemiesKilled { get; private set; }
        public float ElapsedTime { get; private set; }
        public float DamageTaken { get; private set; }

        public string LastMessage { get; private set; } = "";
        public float LastMessageTime { get; private set; } = -100f;

        /// <summary>Dipanggil saat Key diambil (dipakai gerbang besi / dynamic obstacle).</summary>
        public event Action KeyCollected;

        /// <summary>Dipanggil setiap kali state game berubah (dipakai UI).</summary>
        public event Action<GameState> StateChanged;

        /// <summary>Indeks langkah objective saat ini (0 = Key, 1 = pintu, 2 = Guardian, 3 = Artifact, 4 = selesai).</summary>
        public int ObjectiveStep
        {
            get
            {
                if (State == GameState.Won) return 4;
                if (!HasKey) return 0;
                if (!DoorOpened) return 1;
                if (!GuardianDefeated) return 2;
                return 3;
            }
        }

        public static readonly string[] ObjectiveTexts =
        {
            "Cari Dungeon Key",
            "Buka pintu ruang Guardian",
            "Kalahkan Crypt Guardian",
            "Ambil Ancient Artifact"
        };

        public string Objective => ObjectiveStep < ObjectiveTexts.Length ? ObjectiveTexts[ObjectiveStep] : "Selesai!";

        private void Awake()
        {
            Instance = this;
            Time.timeScale = 1f;
            State = startInMenu && !skipMenuOnNextLoad ? GameState.Menu : GameState.Playing;
            skipMenuOnNextLoad = false;
        }

        private void OnEnable()
        {
            if (playerHealth != null)
            {
                playerHealth.Died += OnPlayerDied;
                playerHealth.Damaged += OnPlayerDamaged;
            }
        }

        private void OnDisable()
        {
            if (playerHealth != null)
            {
                playerHealth.Died -= OnPlayerDied;
                playerHealth.Damaged -= OnPlayerDamaged;
            }
        }

        private void Start()
        {
            if (State == GameState.Playing)
                ShowMessage("Jelajahi dungeon dan temukan Dungeon Key!");
        }

        private void Update()
        {
            if (IsPlaying)
                ElapsedTime += Time.deltaTime;

            Keyboard kb = Keyboard.current;
            if (kb == null)
                return;

            if (IsGameOver && kb.rKey.wasPressedThisFrame)
                Restart(true);

            if (kb.escapeKey.wasPressedThisFrame)
            {
                if (State == GameState.Playing)
                    Pause();
                else if (State == GameState.Paused)
                    Resume();
            }
        }

        // =====================================================
        // ALUR GAME (dipanggil UI)
        // =====================================================

        public void StartGame()
        {
            if (State != GameState.Menu)
                return;
            SetState(GameState.Playing);
            ShowMessage("Jelajahi dungeon dan temukan Dungeon Key!");
        }

        public void Pause()
        {
            if (State == GameState.Playing)
                SetState(GameState.Paused);
        }

        public void Resume()
        {
            if (State == GameState.Paused)
                SetState(GameState.Playing);
        }

        /// <summary>Muat ulang scene. skipMenu = true → langsung main lagi; false → kembali ke main menu.</summary>
        public void Restart(bool skipMenu)
        {
            skipMenuOnNextLoad = skipMenu;
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void SetState(GameState state)
        {
            if (State == state)
                return;
            State = state;
            StateChanged?.Invoke(state);
        }

        // =====================================================
        // OBJECTIVE
        // =====================================================

        public void ShowMessage(string message)
        {
            LastMessage = message;
            LastMessageTime = Time.unscaledTime;
        }

        public void CollectKey()
        {
            if (HasKey)
                return;
            HasKey = true;
            ShowMessage("Dungeon Key didapat! Buka pintu ruang Guardian.");
            KeyCollected?.Invoke();
        }

        public void NotifyDoorOpened()
        {
            DoorOpened = true;
            ShowMessage("Pintu terbuka... Guardian menunggu.");
        }

        public void NotifyEnemyKilled(EnemyAI enemy)
        {
            EnemiesKilled++;

            if (enemy is GuardianEnemy)
            {
                GuardianDefeated = true;
                ShowMessage("Guardian dikalahkan! Segel Artifact terbuka.");
            }
        }

        public void CollectArtifact()
        {
            if (!IsPlaying)
                return;

            ShowMessage("Artifact didapat!");
            SetState(GameState.Won);
        }

        private void OnPlayerDamaged(float amount, Vector3 source)
        {
            DamageTaken += amount;
        }

        private void OnPlayerDied()
        {
            if (!IsPlaying)
                return;

            ShowMessage("Kamu tewas di dalam dungeon...");
            SetState(GameState.Lost);
        }
    }
}
