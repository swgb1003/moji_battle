using System;
using UnityEngine;
using UnityEngine.UI;

namespace MojiBattle
{
    /// <summary>
    /// Use a real browser input for Japanese IME, retaining the game's own validation.
    /// The browser element is keyed by this GameObject's name, so each attached field needs a unique name.
    /// </summary>
    public sealed class WebCharacterInput : MonoBehaviour
    {
        InputField field;
        Func<bool> visible;
        Func<int> resetKey;
        string label, placeholder;
        int maxLength;
        float fontScale;
        bool printedCode;
        Action submit;
        string lastValue;
#if UNITY_WEBGL && !UNITY_EDITOR
        readonly Vector3[] corners = new Vector3[4];
        bool created, focused, previousCapture;
        int lastKey = int.MinValue;

        [System.Runtime.InteropServices.DllImport("__Internal")] static extern void MojiInputCreate(string target, string label, string placeholder, int maxLength, float fontScale, bool printedCode);
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern void MojiInputRect(string target, float x, float y, float width, float height);
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern void MojiInputValue(string target, string value, bool force);
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern void MojiInputShow(string target, bool visible);
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern void MojiInputDestroy(string target);
#endif

        /// <summary>武器の一文字の入力（カスタマイズ画面）。</summary>
        public static WebCharacterInput Attach(InputField input, CustomizeScreen owner) =>
            Attach(input, () => owner.TextInputVisible, () => owner.Customizer.Side, "武器の文字", "文字", 0, .8f);

        /// <param name="isVisible">false の間は隠す（重なる画面を開いている時など）。</param>
        /// <param name="key">変わったら入力中の値を捨てて field の値に合わせる（左右の陣営の切り替えなど）。</param>
        public static WebCharacterInput Attach(InputField input, Func<bool> isVisible, Func<int> key, string ariaLabel, string hint,
            int softMaxLength, float textScale, bool usePrintedCode = false, Action onSubmit = null)
        {
            var bridge = input.gameObject.AddComponent<WebCharacterInput>();
            bridge.field = input;
            bridge.visible = isVisible;
            bridge.resetKey = key;
            bridge.label = ariaLabel;
            bridge.placeholder = hint;
            bridge.maxLength = softMaxLength;
            bridge.fontScale = textScale;
            bridge.printedCode = usePrintedCode;
            bridge.submit = onSubmit;
#if UNITY_WEBGL && !UNITY_EDITOR
            // Never truncate romaji or kana while the browser IME is composing.
            input.characterLimit = 0;
            input.enabled = false;
            input.textComponent.enabled = false;
            if (input.placeholder != null) input.placeholder.enabled = false;
#endif
            return bridge;
        }

        [UnityEngine.Scripting.Preserve]
        public void ReceiveText(string value)
        {
            if (field == null || !field.interactable) return;
            field.SetTextWithoutNotify(value);
            field.onValueChanged.Invoke(value);
            lastValue = null; // Reflect normalization performed by the game's validator next frame.
        }

        [UnityEngine.Scripting.Preserve]
        public void ReceiveSubmit(string value)
        {
            if (field != null && field.interactable) submit?.Invoke();
        }

        [UnityEngine.Scripting.Preserve]
        public void ReceiveFocus(string value)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (value == "1" && !focused)
            {
                previousCapture = WebGLInput.captureAllKeyboardInput;
                focused = true;
                WebGLInput.captureAllKeyboardInput = false;
            }
            else if (value != "1") RestoreKeyboard();
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        void LateUpdate()
        {
            if (field == null) return;
            string target = gameObject.name;
            if (!created) { MojiInputCreate(target, label, placeholder, maxLength, fontScale, printedCode); created = true; lastValue = null; }
            bool show = field.interactable && (visible == null || visible());
            MojiInputShow(target, show);
            if (!show) { RestoreKeyboard(); return; }
            ((RectTransform)field.transform).GetWorldCorners(corners);
            var lower = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
            var upper = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
            MojiInputRect(target, lower.x / Screen.width, 1f - upper.y / Screen.height,
                (upper.x - lower.x) / Screen.width, (upper.y - lower.y) / Screen.height);
            int key = resetKey != null ? resetKey() : 0;
            if (lastValue != field.text || lastKey != key)
            {
                MojiInputValue(target, field.text, lastKey != key);
                lastValue = field.text; lastKey = key;
            }
        }

        void RestoreKeyboard()
        {
            if (!focused) return;
            WebGLInput.captureAllKeyboardInput = previousCapture;
            focused = false;
        }

        void OnDisable()
        {
            if (created) { MojiInputDestroy(gameObject.name); created = false; }
            RestoreKeyboard();
        }
#endif
    }
}
