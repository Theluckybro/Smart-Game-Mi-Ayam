using UnityEngine;
using UnityEngine.UI;

namespace DungeonHunter.UI
{
    /// <summary>
    /// Toolkit UI: palet warna tema "dark fantasy", sprite prosedural (panel, tombol, bar, vignette, glow),
    /// dan helper pembuat elemen uGUI. Semua sprite dibuat sekali lalu di-cache.
    /// </summary>
    public static class UIKit
    {
        // ---------------- Palet ----------------
        public static readonly Color Gold = new Color(1f, 0.82f, 0.42f);
        public static readonly Color GoldDim = new Color(0.72f, 0.58f, 0.32f);
        public static readonly Color TextLight = new Color(0.93f, 0.9f, 0.84f);
        public static readonly Color TextDim = new Color(0.6f, 0.57f, 0.52f);
        public static readonly Color PanelFill = new Color(0.07f, 0.06f, 0.1f, 0.88f);
        public static readonly Color HpRed = new Color(0.86f, 0.18f, 0.2f);
        public static readonly Color Chip = new Color(1f, 0.9f, 0.7f);
        public static readonly Color BossPurple = new Color(0.62f, 0.28f, 0.9f);
        public static readonly Color Victory = new Color(1f, 0.84f, 0.35f);
        public static readonly Color Defeat = new Color(0.95f, 0.25f, 0.22f);

        private static Font font;
        private static Sprite panel, button, bar, circle, dot, vignette, glow, fadeH;

        public static Font Font => font != null ? font : font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // ---------------- Sprite prosedural ----------------

        /// <summary>Panel gelap bersudut membulat dengan bingkai emas tipis (9-slice).</summary>
        public static Sprite PanelSprite => panel != null ? panel : panel = RoundedSprite(48, 12f, 2f, PanelFill, GoldDim);

        /// <summary>Tombol: isi merah tua gelap + bingkai emas (9-slice).</summary>
        public static Sprite ButtonSprite => button != null ? button : button = RoundedSprite(48, 10f, 2f,
            new Color(0.2f, 0.08f, 0.09f, 0.95f), Gold);

        /// <summary>Bar putih membulat (diwarnai lewat Image.color).</summary>
        public static Sprite BarSprite => bar != null ? bar : bar = RoundedSprite(32, 7f, 0f, Color.white, Color.white);

        public static Sprite CircleSprite => circle != null ? circle : circle = RadialSprite(64, r =>
            Mathf.Clamp01(1f - Mathf.Abs(r - 0.82f) / 0.12f));

        public static Sprite DotSprite => dot != null ? dot : dot = RadialSprite(64, r => Mathf.Clamp01((1f - r) * 12f));

        /// <summary>Vignette: transparan di tengah, pekat di tepi layar.</summary>
        public static Sprite VignetteSprite => vignette != null ? vignette : vignette = RadialSprite(256, r =>
            Mathf.Pow(Mathf.Clamp01((r - 0.45f) / 0.65f), 1.6f));

        public static Sprite GlowSprite => glow != null ? glow : glow = RadialSprite(128, r => Mathf.Pow(Mathf.Clamp01(1f - r), 2f));

        /// <summary>Gradasi horizontal: pekat di tengah, memudar ke kiri-kanan (latar toast).</summary>
        public static Sprite FadeSprite => fadeH != null ? fadeH : fadeH = MakeSprite(128, 8, (x, y) =>
        {
            float a = 1f - Mathf.Abs(x * 2f - 1f);
            return new Color(1f, 1f, 1f, Mathf.Clamp01(a * 1.6f));
        }, Vector4.zero);

        private static Sprite RoundedSprite(int size, float radius, float border, Color fill, Color edge)
        {
            return MakeSprite(size, size, (u, v) =>
            {
                float px = u * size, py = v * size;
                float dx = Mathf.Max(radius - px, px - (size - radius), 0f);
                float dy = Mathf.Max(radius - py, py - (size - radius), 0f);
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float inside = Mathf.Clamp01(radius - dist + 0.5f);
                if (inside <= 0f)
                    return Color.clear;

                // jarak ke tepi bentuk (untuk bingkai)
                float edgeDist = Mathf.Min(Mathf.Min(px, size - px), Mathf.Min(py, size - py));
                if (dx > 0f && dy > 0f)
                    edgeDist = radius - dist;
                Color c = border > 0f && edgeDist < border ? edge : fill;
                c.a *= inside;
                return c;
            }, new Vector4(radius + 2f, radius + 2f, radius + 2f, radius + 2f));
        }

        private static Sprite RadialSprite(int size, System.Func<float, float> alpha)
        {
            return MakeSprite(size, size, (u, v) =>
            {
                float x = u * 2f - 1f, y = v * 2f - 1f;
                float r = Mathf.Sqrt(x * x + y * y);
                return new Color(1f, 1f, 1f, alpha(r));
            }, Vector4.zero);
        }

        private static Sprite MakeSprite(int w, int h, System.Func<float, float, Color> pixel, Vector4 border)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    tex.SetPixel(x, y, pixel((x + 0.5f) / w, (y + 0.5f) / h));
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }

        // ---------------- Elemen ----------------

        public static RectTransform NewRect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(string name, Transform parent, float inset = 0f)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        public static Image AddImage(RectTransform rt, Sprite sprite, Color color, bool sliced = false)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.type = sliced ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            img.raycastTarget = false;
            return img;
        }

        public static Text AddText(RectTransform rt, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleLeft,
            FontStyle style = FontStyle.Normal, bool shadow = true)
        {
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            if (shadow)
            {
                var s = rt.gameObject.AddComponent<Shadow>();
                s.effectColor = new Color(0f, 0f, 0f, 0.8f);
                s.effectDistance = new Vector2(2f, -2f);
            }
            return t;
        }

        public static Text Label(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size,
            string text, int fontSize, Color color, TextAnchor align = TextAnchor.MiddleLeft, FontStyle style = FontStyle.Normal)
        {
            return AddText(NewRect(name, parent, anchor, pivot, pos, size), text, fontSize, color, align, style);
        }

        /// <summary>Tombol bertema dengan efek hover (membesar + bersinar) dan klik.</summary>
        public static Button MakeButton(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, string label,
            UnityEngine.Events.UnityAction onClick, int fontSize = 26)
        {
            RectTransform rt = NewRect(name, parent, anchor, new Vector2(0.5f, 0.5f), pos, size);
            Image img = AddImage(rt, ButtonSprite, Color.white, true);
            img.raycastTarget = true;

            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var colors = b.colors;
            colors.normalColor = new Color(0.85f, 0.85f, 0.85f);
            colors.highlightedColor = Color.white;
            colors.selectedColor = new Color(0.85f, 0.85f, 0.85f);
            colors.pressedColor = new Color(0.7f, 0.65f, 0.6f);
            colors.colorMultiplier = 1.4f;
            colors.fadeDuration = 0.08f;
            b.colors = colors;
            b.onClick.AddListener(onClick);

            Text t = AddText(Stretch("Label", rt), label, fontSize, Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            rt.gameObject.AddComponent<UIButtonFX>().Init(t);
            return b;
        }

        /// <summary>Bar dengan latar, lapisan "chip" (sisa damage) dan isi. Nilai diatur lewat anchorMax.x.</summary>
        public static (RectTransform fill, RectTransform chip) MakeBar(Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos,
            Vector2 size, Color fillColor)
        {
            RectTransform bg = NewRect("Bar", parent, anchor, pivot, pos, size);
            AddImage(bg, BarSprite, new Color(0f, 0f, 0f, 0.65f), true);

            RectTransform chip = Stretch("Chip", bg, 2f);
            chip.anchorMax = new Vector2(1f, 1f);
            AddImage(chip, BarSprite, Chip, true);

            RectTransform fill = Stretch("Fill", bg, 2f);
            AddImage(fill, BarSprite, fillColor, true);

            RectTransform shine = Stretch("Shine", fill);
            shine.anchorMin = new Vector2(0f, 0.55f);
            AddImage(shine, BarSprite, new Color(1f, 1f, 1f, 0.18f), true);
            return (fill, chip);
        }

        /// <summary>Slider bertema: label di kiri, bar emas + knob di tengah, persentase di kanan.</summary>
        public static Slider MakeSlider(string name, Transform parent, Vector2 anchor, Vector2 pos, string label, float value,
            UnityEngine.Events.UnityAction<float> onChange)
        {
            RectTransform row = NewRect(name, parent, anchor, new Vector2(0.5f, 0.5f), pos, new Vector2(460f, 44f));
            Label("Label", row, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(150f, 40f), label, 22, TextLight);
            Text percent = Label("Value", row, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(64f, 40f),
                "", 22, Gold, TextAnchor.MiddleRight, FontStyle.Bold);

            RectTransform track = NewRect("Track", row, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(160f, 0f), new Vector2(220f, 14f));
            Image bg = AddImage(track, BarSprite, new Color(0f, 0f, 0f, 0.65f), true);
            bg.raycastTarget = true;

            RectTransform fillArea = Stretch("FillArea", track, 2f);
            RectTransform fill = Stretch("Fill", fillArea);
            AddImage(fill, BarSprite, Gold, true);

            RectTransform handleArea = Stretch("HandleArea", track);
            handleArea.offsetMin = new Vector2(10f, 0f);
            handleArea.offsetMax = new Vector2(-10f, 0f);
            RectTransform handle = NewRect("Handle", handleArea, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(28f, 28f));
            Image knob = AddImage(handle, DotSprite, Color.white);
            knob.raycastTarget = true;

            var slider = track.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = knob;
            slider.direction = Slider.Direction.LeftToRight;
            var colors = slider.colors;
            colors.normalColor = new Color(1f, 0.9f, 0.7f);
            colors.highlightedColor = Color.white;
            colors.pressedColor = Gold;
            slider.colors = colors;
            slider.SetValueWithoutNotify(value);
            percent.text = Mathf.RoundToInt(value * 100f) + "%";
            slider.onValueChanged.AddListener(v => percent.text = Mathf.RoundToInt(v * 100f) + "%");
            slider.onValueChanged.AddListener(onChange);
            return slider;
        }

        public static void SetBar(RectTransform bar, float value)
        {
            bar.anchorMax = new Vector2(Mathf.Clamp01(value), 1f);
        }
    }
}
