using System.Collections.Generic;
using UnityEngine;

namespace DungeonHunter
{
    /// <summary>
    /// Pemutar efek suara (SFX).
    /// - Pool AudioSource: tidak ada Instantiate/Destroy saat game berjalan
    /// - Variasi klip + pitch acak agar suara berulang tidak terdengar monoton
    /// - Suara 3D: AudioListener mengikuti player (bukan kamera yang tinggi di atas),
    ///   sehingga musuh di kiri terdengar di kiri dan makin jauh makin pelan
    /// - Saat slow-motion pitch SFX ikut turun; saat pause semua SFX gameplay berhenti
    ///   (suara UI tetap jalan)
    /// - Volume Musik & Efek disimpan di PlayerPrefs (diatur dari menu Pause)
    /// - Ambient: tetesan air acak di sekitar player
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        private const int PoolSize = 32;
        private const string MusicKey = "vol_music";
        private const string SfxKey = "vol_sfx";

        public static float MusicVolume { get; private set; } = 0.8f;
        public static float SfxVolume { get; private set; } = 1f;

        private class Voice
        {
            public AudioSource Source;
            public float BasePitch;
            public float BaseVolume;
            public Transform Follow;
            public float StopAt = -1f;
            public float FadeDuration;
            public bool IsUI;
        }

        private readonly List<Voice> voices = new List<Voice>();
        private readonly Dictionary<Sfx, float> lastPlayTime = new Dictionary<Sfx, float>();
        private int nextVoice;
        private Transform listener;
        private Transform player;
        private Camera cam;
        private float nextDripTime;
        private GameState lastState;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            MusicVolume = PlayerPrefs.GetFloat(MusicKey, 0.8f);
            SfxVolume = PlayerPrefs.GetFloat(SfxKey, 1f);

            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject($"Voice_{i}");
                go.transform.SetParent(transform, false);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.rolloffMode = AudioRolloffMode.Linear;
                src.minDistance = 4f;
                src.dopplerLevel = 0f;
                src.spread = 40f;
                voices.Add(new Voice { Source = src });
            }

            SetupListener();
            nextDripTime = Time.unscaledTime + 4f;
        }

        private void Start()
        {
            if (GameManager.Instance != null)
            {
                lastState = GameManager.Instance.State;
                GameManager.Instance.StateChanged += OnStateChanged;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            if (GameManager.Instance != null)
                GameManager.Instance.StateChanged -= OnStateChanged;
            AudioListener.pause = false;
        }

        /// <summary>Listener tunggal yang diposisikan di player; listener lain (mis. di kamera) dimatikan.</summary>
        private void SetupListener()
        {
            foreach (AudioListener l in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                l.enabled = false;
            var go = new GameObject("AudioListener");
            go.transform.SetParent(transform, false);
            go.AddComponent<AudioListener>();
            listener = go.transform;
        }

        private void OnStateChanged(GameState state)
        {
            AudioListener.pause = state == GameState.Paused;
            if (state == GameState.Paused)
                Play2D(Sfx.UIPauseOpen);
            else if (state == GameState.Playing && lastState == GameState.Paused)
                Play2D(Sfx.UIPauseClose);
            else if (state == GameState.Playing && lastState == GameState.Menu)
                Play2D(Sfx.UIStart);
            lastState = state;
        }

        // =====================================================
        // API
        // =====================================================

        /// <summary>Putar efek suara di posisi dunia (3D).</summary>
        public static AudioSource Play(Sfx sfx, Vector3 position, float volume = 1f, float pitch = 1f, float delay = 0f)
        {
            return Instance != null ? Instance.PlayInternal(sfx, position, volume, pitch, delay, null) : null;
        }

        /// <summary>Putar efek suara tanpa posisi (UI, jingle).</summary>
        public static AudioSource Play2D(Sfx sfx, float volume = 1f, float pitch = 1f, float delay = 0f)
        {
            if (Instance == null)
                return null;
            Vector3 pos = Instance.listener != null ? Instance.listener.position : Vector3.zero;
            return Instance.PlayInternal(sfx, pos, volume, pitch, delay, null);
        }

        /// <summary>Suara yang menempel pada objek bergerak dan berhenti (fade) setelah durasi tertentu.</summary>
        public static void PlayAttached(Sfx sfx, Transform follow, float duration, float volume = 1f)
        {
            if (Instance == null || follow == null)
                return;
            AudioSource src = Instance.PlayInternal(sfx, follow.position, volume, 1f, 0f, follow);
            if (src == null)
                return;
            Voice v = Instance.voices.Find(x => x.Source == src);
            v.StopAt = Time.time + duration;
            v.FadeDuration = 0.25f;
        }

        /// <summary>Beberapa suara beruntun (mis. tulang berjatuhan saat skeleton hancur).</summary>
        public static void PlayBurst(Sfx sfx, Vector3 position, int count, float interval, float volume = 1f)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 jitter = new Vector3(Random.Range(-0.6f, 0.6f), 0f, Random.Range(-0.6f, 0.6f));
                float vol = volume * (1f - i * 0.5f / Mathf.Max(1, count));
                Instance?.PlayInternal(sfx, position + jitter, vol, Random.Range(0.9f, 1.15f), i * interval + Random.Range(0f, 0.03f), null, true);
            }
        }

        public static void SetMusicVolume(float value)
        {
            MusicVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(MusicKey, MusicVolume);
        }

        public static void SetSfxVolume(float value)
        {
            SfxVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(SfxKey, SfxVolume);
        }

        // =====================================================
        // INTERNAL
        // =====================================================

        private AudioSource PlayInternal(Sfx sfx, Vector3 position, float volume, float pitch, float delay, Transform follow,
            bool ignoreCooldown = false)
        {
            SfxDef def = SoundLibrary.Get(sfx);
            float now = Time.unscaledTime;
            if (!ignoreCooldown && lastPlayTime.TryGetValue(sfx, out float last) && now - last < def.Cooldown)
                return null;
            lastPlayTime[sfx] = now;

            AudioClip clip = SoundLibrary.PickClip(sfx);
            if (clip == null)
                return null;

            Voice v = NextVoice();
            AudioSource src = v.Source;
            src.Stop();
            src.clip = clip;
            src.transform.position = position;
            src.spatialBlend = def.Spatial;
            src.maxDistance = def.MaxDistance;
            src.ignoreListenerPause = def.IsUI;
            src.priority = def.IsUI ? 32 : 128;

            v.BaseVolume = def.Volume * volume;
            v.BasePitch = Random.Range(def.PitchMin, def.PitchMax) * pitch;
            v.Follow = follow;
            v.StopAt = -1f;
            v.IsUI = def.IsUI;
            src.volume = v.BaseVolume * SfxVolume;
            src.pitch = v.BasePitch * (def.IsUI ? 1f : TimePitch());

            if (delay > 0f)
                src.PlayDelayed(delay);
            else
                src.Play();
            return src;
        }

        /// <summary>Ambil voice yang sedang diam; kalau semua sibuk, pakai voice tertua (round-robin).</summary>
        private Voice NextVoice()
        {
            for (int i = 0; i < voices.Count; i++)
            {
                int index = (nextVoice + i) % voices.Count;
                if (!voices[index].Source.isPlaying)
                {
                    nextVoice = (index + 1) % voices.Count;
                    return voices[index];
                }
            }
            Voice v = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Count;
            return v;
        }

        /// <summary>Pitch SFX saat slow-motion (hit-stop yang sangat singkat diabaikan).</summary>
        private static float TimePitch()
        {
            float slow = VFXManager.SlowMotionScale;
            return slow < 1f ? Mathf.Lerp(0.72f, 1f, slow) : 1f;
        }

        private void LateUpdate()
        {
            UpdateListener();

            float timePitch = TimePitch();
            foreach (Voice v in voices)
            {
                if (!v.Source.isPlaying)
                    continue;
                if (v.Follow != null)
                    v.Source.transform.position = v.Follow.position;
                if (!v.IsUI)
                    v.Source.pitch = v.BasePitch * timePitch;

                float vol = v.BaseVolume * SfxVolume;
                if (v.StopAt > 0f)
                {
                    float left = v.StopAt - Time.time;
                    if (left <= 0f)
                    {
                        v.Source.Stop();
                        v.StopAt = -1f;
                        continue;
                    }
                    vol *= Mathf.Clamp01(left / v.FadeDuration);
                }
                v.Source.volume = vol;
            }

            UpdateAmbient();
        }

        private void UpdateListener()
        {
            if (listener == null)
                return;
            if (player == null)
            {
                GameObject p = GameObject.FindWithTag("Player");
                if (p != null)
                    player = p.transform;
            }
            if (cam == null)
                cam = Camera.main;

            if (player != null)
                listener.position = player.position + Vector3.up * 1.6f;
            else if (cam != null)
                listener.position = cam.transform.position;

            // Arah "depan" listener = arah pandang kamera di bidang datar → kiri layar = telinga kiri
            if (cam != null)
                listener.rotation = Quaternion.Euler(0f, cam.transform.eulerAngles.y, 0f);
        }

        /// <summary>Ambience: tetesan air acak di sekitar player.</summary>
        private void UpdateAmbient()
        {
            GameManager gm = GameManager.Instance;
            if (gm != null && gm.State != GameState.Playing && gm.State != GameState.Menu)
                return;
            if (Time.unscaledTime < nextDripTime || listener == null)
                return;

            nextDripTime = Time.unscaledTime + Random.Range(3.5f, 9f);
            Vector2 dir = Random.insideUnitCircle.normalized * Random.Range(5f, 14f);
            Play(Sfx.Drip, listener.position + new Vector3(dir.x, 1.5f, dir.y));
        }
    }
}
