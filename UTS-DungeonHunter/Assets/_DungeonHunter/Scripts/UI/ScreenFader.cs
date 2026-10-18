using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DungeonHunter.UI
{
    /// <summary>
    /// Transisi layar: fade dari hitam saat scene dimulai, fade ke hitam (plus suara ikut meredup)
    /// sebelum scene di-load ulang / keluar game. Memakai unscaled time agar tetap jalan saat pause.
    /// </summary>
    public class ScreenFader : MonoBehaviour
    {
        public static ScreenFader Instance { get; private set; }

        [SerializeField] private float fadeInTime = 0.8f;
        [SerializeField] private float fadeOutTime = 0.5f;

        private CanvasGroup group;
        private float target;
        private float speed;
        private Action onBlack;

        public bool IsBusy => onBlack != null;

        private void Awake()
        {
            Instance = this;

            var go = new GameObject("ScreenFader", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            go.AddComponent<GraphicRaycaster>();
            group = go.AddComponent<CanvasGroup>();

            RectTransform img = UIKit.Stretch("Black", go.transform);
            Image image = UIKit.AddImage(img, null, Color.black);
            image.raycastTarget = true; // blokir klik selama transisi

            // mulai dari hitam → fade masuk
            group.alpha = 1f;
            target = 0f;
            speed = 1f / fadeInTime;
            AudioListener.volume = 0f;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>Fade ke hitam, lalu jalankan aksi (mis. load scene).</summary>
        public static void FadeOutThen(Action action)
        {
            if (Instance == null)
            {
                action?.Invoke();
                return;
            }
            if (Instance.IsBusy)
                return;
            Instance.onBlack = action;
            Instance.target = 1f;
            Instance.speed = 1f / Instance.fadeOutTime;
        }

        public static void LoadScene(int buildIndex) => FadeOutThen(() => SceneManager.LoadScene(buildIndex));

        private void Update()
        {
            group.alpha = Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime * speed);
            group.blocksRaycasts = group.alpha > 0.05f;
            AudioListener.volume = 1f - group.alpha;

            if (onBlack != null && group.alpha >= 1f)
            {
                Action a = onBlack;
                onBlack = null;
                a();
            }
        }
    }
}
