using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MojiBattle
{
    /// <summary>
    /// カスタマイズ・タイトル・リザルト画面の uGUI 部品をコードで組み立てる（プレハブを持たない）。
    /// 白黒ベース・太い線・大きな文字（カスタマイズ仕様 18）。座標は 1920×1080 基準の左上原点ピクセル。
    /// </summary>
    public static class UguiKit
    {
        public static readonly Color Ink = FighterFactory.Ink;
        public static readonly Color Paper = FighterFactory.Paper;
        public static readonly Color Muted = new Color(0.55f, 0.55f, 0.55f, 1f);

        public static Canvas MakeCanvas(string name, int sortingOrder = 0)
        {
            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            canvas.pixelPerfect = false;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        public static RectTransform Box(Transform parent, string name, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        /// <summary>親いっぱいに広げる。</summary>
        public static RectTransform Fill(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static Image Panel(Transform parent, string name, float x, float y, float w, float h, Color color, float outline = 0f)
        {
            var img = Box(parent, name, x, y, w, h).gameObject.AddComponent<Image>();
            img.color = color;
            if (outline > 0f) AddOutline(img.gameObject, outline);
            return img;
        }

        public static void AddOutline(GameObject go, float width, Color? color = null)
        {
            var o = go.AddComponent<Outline>();
            o.effectColor = color ?? Ink;
            o.effectDistance = new Vector2(width, -width);
            o.useGraphicAlpha = false;
        }

        public static Text Label(Transform parent, string name, string text, float x, float y, float w, float h, int size,
            Color? color = null, TextAnchor anchor = TextAnchor.MiddleLeft, bool bold = true)
        {
            var t = Box(parent, name, x, y, w, h).gameObject.AddComponent<Text>();
            t.font = UiKit.Font;
            t.text = text;
            t.fontSize = size;
            t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.color = color ?? Ink;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>選択状態を持つボタン（選択中は黒地に白文字、未選択は白地に黒の太線）。</summary>
        public sealed class ChoiceButton
        {
            public Button button;
            public Image image;
            public Text label;
            public Color accent = Ink;

            public void SetSelected(bool on)
            {
                image.color = on ? accent : Color.white;
                label.color = on ? Color.white : (button.interactable ? Ink : Muted);
            }
        }

        public static ChoiceButton Choice(Transform parent, string name, string label, float x, float y, float w, float h, int fontSize, Action onClick)
        {
            var img = Panel(parent, name, x, y, w, h, Color.white, 3f);
            var btn = img.gameObject.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.disabledColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            btn.colors = colors;
            btn.targetGraphic = img;
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            var text = Label(img.transform, "Label", label, 0f, 0f, w, h, fontSize, Ink, TextAnchor.MiddleCenter);
            var rt = text.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            return new ChoiceButton { button = btn, image = img, label = text };
        }

        public static Slider SliderBar(Transform parent, string name, float x, float y, float w, float h, Action<float> onChange)
        {
            var root = Box(parent, name, x, y, w, h);
            var slider = root.gameObject.AddComponent<Slider>();
            var track = Panel(root, "Track", 0f, h * 0.5f - 6f, w, 12f, Ink);
            track.raycastTarget = true;
            var fillArea = Box(root, "FillArea", 0f, h * 0.5f - 6f, w, 12f);
            var fill = Panel(fillArea, "Fill", 0f, 0f, w, 12f, Ink);
            var fr = fill.rectTransform;
            fr.anchorMin = new Vector2(0f, 0f); fr.anchorMax = new Vector2(0f, 1f); fr.pivot = new Vector2(0f, 0.5f); fr.sizeDelta = Vector2.zero;
            var handleArea = Box(root, "HandleArea", 0f, 0f, w, h);
            var handle = Panel(handleArea, "Handle", 0f, 0f, h * 0.7f, h * 0.7f, Color.white, 4f);
            var hr = handle.rectTransform;
            hr.anchorMin = new Vector2(0f, 0.5f); hr.anchorMax = new Vector2(0f, 0.5f); hr.pivot = new Vector2(0.5f, 0.5f);
            // Slider はハンドルを縦方向に引き伸ばすアンカーにするため、高さは親との差分で指定する
            hr.sizeDelta = new Vector2(h * 0.45f, -h * 0.2f);
            hr.anchoredPosition = Vector2.zero;
            slider.fillRect = fr;
            slider.handleRect = hr;
            slider.targetGraphic = handle;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            if (onChange != null) slider.onValueChanged.AddListener(v => onChange(v));
            return slider;
        }

        public static InputField InputBox(Transform parent, string name, float x, float y, float w, float h, int fontSize, Action<string> onEnd)
        {
            var img = Panel(parent, name, x, y, w, h, Color.white, 4f);
            var field = img.gameObject.AddComponent<InputField>();
            var text = Label(img.transform, "Text", "", 0f, 0f, w, h, fontSize, Ink, TextAnchor.MiddleCenter);
            text.supportRichText = false;
            var placeholder = Label(img.transform, "Placeholder", "文字", 0f, 0f, w, h, fontSize / 2, Muted, TextAnchor.MiddleCenter, false);
            foreach (var rt in new[] { text.rectTransform, placeholder.rectTransform })
            {
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            }
            field.textComponent = text;
            field.placeholder = placeholder;
            field.targetGraphic = img;
            field.characterLimit = 2; // サロゲートペア 1 文字まで
            field.lineType = InputField.LineType.SingleLine;
            if (onEnd != null) field.onEndEdit.AddListener(s => onEnd(s));
            return field;
        }
    }
}
