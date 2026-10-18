using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DungeonHunter.UI
{
    /// <summary>Efek tombol: membesar & teks memutih saat hover, "punch" kecil saat diklik. Ada suara hover & klik. Memakai unscaled time (jalan saat pause).</summary>
    public class UIButtonFX : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
    {
        private Text label;
        private bool hover;
        private float punch;
        private Color baseColor;

        public void Init(Text text)
        {
            label = text;
            baseColor = text.color;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            hover = true;
            AudioManager.Play2D(Sfx.UIHover);
        }

        public void OnPointerExit(PointerEventData eventData) => hover = false;

        public void OnPointerDown(PointerEventData eventData)
        {
            punch = 1f;
            AudioManager.Play2D(Sfx.UIClick);
        }

        private void OnDisable()
        {
            hover = false;
            punch = 0f;
            transform.localScale = Vector3.one;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            punch = Mathf.MoveTowards(punch, 0f, dt * 6f);
            float target = (hover ? 1.06f : 1f) - punch * 0.06f;
            float s = Mathf.Lerp(transform.localScale.x, target, 1f - Mathf.Exp(-18f * dt));
            transform.localScale = new Vector3(s, s, 1f);
            if (label != null)
                label.color = Color.Lerp(label.color, hover ? Color.white : baseColor, 1f - Mathf.Exp(-14f * dt));
        }
    }
}
