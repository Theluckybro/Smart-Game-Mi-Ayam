using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace DungeonHunter
{
    public enum GameState
    {
        Playing,
        Won,
        Lost
    }

    /// <summary>
    /// Mengatur gameplay loop:
    /// START → cari Key → buka pintu → ambil Artifact → WIN
    /// Player mati → LOSE
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [SerializeField] private Health playerHealth;

        public GameState State { get; private set; } = GameState.Playing;
        public bool IsPlaying => State == GameState.Playing;
        public bool HasKey { get; private set; }
        public bool DoorOpened { get; private set; }
        public Health PlayerHealth => playerHealth;
        public int EnemiesKilled { get; private set; }
        public float ElapsedTime { get; private set; }

        public string LastMessage { get; private set; } = "";
        public float LastMessageTime { get; private set; } = -100f;

        public string Objective
        {
            get
            {
                if (!HasKey) return "Cari Dungeon Key";
                if (!DoorOpened) return "Buka pintu ruang Artifact (utara)";
                return "Ambil Ancient Artifact";
            }
        }

        private void Awake()
        {
            Instance = this;
            Time.timeScale = 1f;
        }

        private void OnEnable()
        {
            if (playerHealth != null)
                playerHealth.Died += OnPlayerDied;
        }

        private void OnDisable()
        {
            if (playerHealth != null)
                playerHealth.Died -= OnPlayerDied;
        }

        private void Start()
        {
            ShowMessage("Jelajahi dungeon dan temukan Dungeon Key!");
        }

        private void Update()
        {
            if (IsPlaying)
                ElapsedTime += Time.deltaTime;

            Keyboard kb = Keyboard.current;
            if (kb != null && !IsPlaying && kb.rKey.wasPressedThisFrame)
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void ShowMessage(string message)
        {
            LastMessage = message;
            LastMessageTime = Time.time;
        }

        public void CollectKey()
        {
            HasKey = true;
            ShowMessage("Dungeon Key didapat! Buka pintu ruang Artifact.");
        }

        public void NotifyDoorOpened()
        {
            DoorOpened = true;
            ShowMessage("Pintu terbuka! Ambil Artifact-nya.");
        }

        public void NotifyEnemyKilled(EnemyAI enemy)
        {
            EnemiesKilled++;
        }

        public void CollectArtifact()
        {
            if (!IsPlaying)
                return;

            State = GameState.Won;
            ShowMessage("Artifact didapat!");
        }

        private void OnPlayerDied()
        {
            if (!IsPlaying)
                return;

            State = GameState.Lost;
            ShowMessage("Kamu tewas di dalam dungeon...");
        }
    }
}
