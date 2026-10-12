using System;
using UnityEngine;
using UnityEngine.UI;

namespace MojiBattle
{
    /// <summary>Printed arcade UI, in a centered 1920 × 1080 safe area.</summary>
    public static class RetroUi
    {
        public static readonly Color Paper = new Color32(242, 235, 219, 255);
        public static readonly Color Ink = new Color32(32, 33, 31, 255);
        public static readonly Color Red = new Color32(186, 60, 45, 255);
        public static readonly Color Blue = new Color32(51, 76, 120, 255);
        public static readonly Color Muted = new Color32(114, 109, 97, 255);
        public static readonly Color Rule = new Color32(161, 153, 133, 255);
        public static readonly Color Wash = new Color32(222, 215, 195, 255);
        static Font mono;
        static Font DisplayFont(string value)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return Font;
#else
            foreach (char c in value) if (c >= 0x2e80) return Font;
            if (mono == null) mono = UnityEngine.Font.CreateDynamicFontFromOSFont(new[] { "Consolas", "Courier New", "Arial" }, 64);
            return mono;
#endif
        }
        public static Font Font => GlyphFontLibrary.Default?.Get(FontStyleId.Gothic)?.font ?? UiKit.Font;
        public static Color Team(int side) => side == 0 ? Red : Blue;

        public static RectTransform Screen(string name, out Canvas canvas, int order = 0)
        {
            canvas = UguiKit.MakeCanvas(name, order);
            canvas.GetComponent<CanvasScaler>().screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var backing = UguiKit.Fill(canvas.transform, "PaperBackground").gameObject.AddComponent<Image>();
            backing.color = Paper; backing.raycastTarget = false;
            canvas.gameObject.AddComponent<RetroMotionSettings>();
            var root = UguiKit.Box(canvas.transform, "SafeArea", 0, 0, 1920, 1080);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(.5f, .5f);
            root.anchoredPosition = Vector2.zero;
            PaperSurface(root, 0, 0, 1920, 1080);
            Frame(root, 24, 24, 1872, 1032, Ink, 3);
            Frame(root, 39, 39, 1842, 1002, Ink, 1);
            foreach (float x in new[] { 24f, 1872f })
                foreach (float y in new[] { 24f, 1032f })
                {
                    Block(root, "Corner", x - 8, y + 3, 16, 16, Ink);
                    Block(root, "Corner", x + 8, y - 13, 16, 16, Ink);
                    Block(root, "Corner", x + 8, y + 19, 16, 16, Ink);
                    Block(root, "Corner", x + 24, y + 3, 16, 16, Ink);
                }
            return root;
        }

        public static Image Block(Transform root, string name, float x, float y, float w, float h, Color color)
        {
            if (name.Contains("Shadow") && color.a > .5f) color.a = .15f;
            var image = UguiKit.Panel(root, name, x, y, w, h, color);
            image.raycastTarget = false; return image;
        }
        public static RectTransform Group(Transform root, string name, float x, float y, float w, float h) => UguiKit.Box(root, name, x, y, w, h);
        public static Text Label(Transform root, string name, string value, float x, float y, float w, float h, int size,
            Color? color = null, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var text = UguiKit.Label(root, name, value, x, y, w, h, size, color ?? Ink, align, false);
            text.font = DisplayFont(value);
            text.fontStyle = text.font == Font ? FontStyle.Normal : FontStyle.Bold;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            if (size >= 54) PaperUi.Ink(text, Mathf.Clamp(size * .025f, 1.5f, 7));
            return text;
        }
        public static void Frame(Transform root, float x, float y, float w, float h, Color color, float weight = 2)
        {
            PaperUi.Frame(root, x, y, w, h, weight);
            Block(root, "RuleTop", x, y, w, weight, color);
            Block(root, "RuleBottom", x, y + h - weight, w, weight, color);
            Block(root, "RuleLeft", x, y, weight, h, color);
            Block(root, "RuleRight", x + w - weight, y, weight, h, color);
        }
        public static void Line(Transform root, Vector2 a, Vector2 b, Color color, float width = 3)
        {
            var d = b - a;
            var image = Block(root, "InkLine", a.x, a.y, d.magnitude, width, color);
            image.rectTransform.pivot = new Vector2(0, .5f);
            image.rectTransform.localRotation = Quaternion.Euler(0, 0, -Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            image.gameObject.AddComponent<RetroStroke>();
        }
        public static void Header(Transform root, string step, Action back)
        {
            Button(root, "Back", "← 戻る", 76, 55, 252,  h: 60, size: 30, action: back, fill: Ink, shadow: false);
            Block(root, "HeaderRule", 356, 85, 310, 2, Ink);
            Label(root, "Step", step, 698, 52, 540, 64, 30, null, TextAnchor.MiddleCenter);
            Block(root, "HeaderRule", 1260, 85, 220, 2, Ink);
            Label(root, "Brand", "MOJI Fighter X", 1510, 58, 326, 54, 24, null, TextAnchor.MiddleRight);
            Block(root, "HeaderBottom", 76, 127, 1768, 2, Ink);
        }
        public static void Heading(Transform root, string title, int size = 82)
        {
            Block(root, "HeadingRule", 76, 216, 430, 2, Ink);
            Block(root, "HeadingRule", 76, 223, 430, 1, Ink);
            Label(root, "Title", title, 505, 141, 910, 125, size, null, TextAnchor.MiddleCenter);
            Block(root, "HeadingRule", 1414, 216, 430, 2, Ink);
            Block(root, "HeadingRule", 1414, 223, 430, 1, Ink);
        }
        public static UguiKit.ChoiceButton Button(Transform root, string name, string label, float x, float y, float w,
            float h, int size, Action action, Color? fill = null, bool shadow = true)
        {
            // Keep named layout anchors used by existing screens; the mesh owns the visible shadow.
            if (shadow && fill.HasValue) Block(root, name + "Shadow", x + 9, y + 10, w, h, Color.clear);
            var button = UguiKit.Choice(root, name, label, x, y, w, h, size, action);
            var outline = button.image.GetComponent<Outline>();
            outline.effectDistance = new Vector2(1, -1); outline.effectColor = Rule;
            button.label.font = DisplayFont(label); button.label.fontStyle = button.label.font == Font ? FontStyle.Normal : FontStyle.Bold;
            button.label.verticalOverflow = VerticalWrapMode.Truncate;
            button.label.resizeTextForBestFit = true;
            button.label.resizeTextMinSize = Mathf.Max(16, size - 6); button.label.resizeTextMaxSize = size;
            button.accent = fill ?? Ink;
            button.image.color = fill ?? Paper;
            button.label.color = fill.HasValue ? Paper : Ink;
            var colors = button.button.colors;
            colors.normalColor = Color.white; colors.highlightedColor = Color.white;
            colors.selectedColor = Color.white; colors.pressedColor = new Color(.87f, .87f, .87f);
            colors.disabledColor = new Color(.8f, .8f, .8f, .65f); colors.fadeDuration = .12f;
            button.button.colors = colors;
            var motion = button.image.GetComponent<RetroButtonMotion>() ?? button.image.gameObject.AddComponent<RetroButtonMotion>();
            motion.restBorder = Rule; motion.focusBorder = Ink;
            return button;
        }
        public static void Selected(UguiKit.ChoiceButton button, bool selected, Color accent)
        {
            bool changed = button.image.color != (selected ? accent : Paper);
            button.image.color = selected ? accent : Paper;
            button.label.color = selected ? Paper : button.button.interactable ? Ink : Muted;
            if (selected && changed) button.image.GetComponent<RetroButtonMotion>()?.Pulse();
        }
        public static void Enter(GameObject go, float delay = 0, float distance = 24)
        {
            var motion = go.GetComponent<RetroReveal>() ?? go.AddComponent<RetroReveal>();
            motion.Configure(delay, distance);
        }
        public static RawImage PaperSurface(Transform parent, float x, float y, float w, float h)
        {
            var raw = Group(parent, "PrintedPaper", x, y, w, h).gameObject.AddComponent<RawImage>();
            raw.texture = Resources.Load<Texture2D>("UI/PrintedPaper"); raw.raycastTarget = false;
            return raw;
        }
        public static void Fighter(Transform parent, float x, float y, float scale, string glyph, Color color, bool mirrored = false)
        {
            var root = Group(parent, "GlyphFighter", x, y, 360 * scale, 420 * scale);
            if (scale >= .5f) PaperUi.Plinth(root, 2 * scale, 405 * scale, 320 * scale, 14 * scale);
            var body = Group(root, "BodyArt", 0, 0, 360 * scale, 420 * scale);
            var head = body.gameObject.AddComponent<RetroFighterArt>();
            head.mirrored = mirrored; head.artScale = scale;
            var weapon = Label(root, "GlyphWeapon", glyph, (mirrored ? -55 : 65) * scale, -30 * scale, 350 * scale, 280 * scale,
                Mathf.RoundToInt(264 * scale), color, TextAnchor.MiddleCenter);
            weapon.font = GlyphFontLibrary.Default?.Get(FontStyleId.Brush)?.font ?? Font;
            weapon.rectTransform.localRotation = Quaternion.Euler(0, 0, mirrored ? 14 : -14);
            weapon.gameObject.AddComponent<RetroIdle>();
            weapon.transform.SetAsFirstSibling();
            Enter(root.gameObject, .12f, 28);
        }
        public static void Cup(Transform parent, float x, float y, float scale, Color? color = null)
        {
            var art = Group(parent, "Cup", x, y, 120 * scale, 130 * scale).gameObject.AddComponent<RetroFighterArt>();
            art.trophy = true; art.artScale = scale; art.color = color ?? Ink;
        }
    }
}
