using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MojiBattle
{
    public sealed class RetroButtonMotion : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
    {
        RectTransform rect; Button button; Vector2 home;
        bool hover, pressed, selected; float pulse;
        PaperFaceEffect face;
        public void ClearFocus() { hover = pressed = selected = false; pulse = 0; }
        public Color restBorder = RetroUi.Rule, focusBorder = RetroUi.Ink;
        void Awake() { rect = (RectTransform)transform; button = GetComponent<Button>(); home = rect.anchoredPosition; }
        public void Pulse() { if (!RetroMotionSettings.Reduced) pulse = 1; }
        public void Rebase() { if(rect!=null)home=rect.anchoredPosition; }
        void OnDisable() { hover = pressed = selected = false; pulse = 0; if (rect != null) { rect.anchoredPosition = home; rect.localScale = Vector3.one; } }
        void Update()
        {
            bool active = button != null && button.interactable;
            float focus = active && (hover || selected) ? 1 : 0;
            float amount = RetroMotionSettings.Reduced ? 0 : focus;
            Vector2 target = home + (pressed && active ? new Vector2(4, -4) : new Vector2(0, 4 * amount));
            float lerp = RetroMotionSettings.Reduced ? 1 : 1 - Mathf.Exp(-22 * Time.unscaledDeltaTime);
            rect.anchoredPosition = Vector2.Lerp(rect.anchoredPosition, target, lerp);
            if (face == null) face = GetComponent<PaperFaceEffect>();
            face?.SetDepth(!active ? 2 : pressed ? 1.5f : PaperUi.ButtonDepth + amount * 3);
            pulse = Mathf.MoveTowards(pulse, 0, Time.unscaledDeltaTime * 4);
            float s = 1 + .012f * amount + .018f * Mathf.Sin(pulse * Mathf.PI);
            rect.localScale = Vector3.Lerp(rect.localScale, Vector3.one * s, RetroMotionSettings.Reduced ? 1 : 1 - Mathf.Exp(-20 * Time.unscaledDeltaTime));
            var outline = GetComponent<Outline>();
            if (outline != null) outline.effectColor = focus > 0 ? focusBorder : restBorder;
        }
        public void OnPointerEnter(PointerEventData e) { hover = true; }
        public void OnPointerExit(PointerEventData e) { hover = pressed = false; }
        public void OnPointerDown(PointerEventData e) { pressed = true; }
        public void OnPointerUp(PointerEventData e) { pressed = false; Pulse(); }
        public void OnSelect(BaseEventData e) { selected = true; }
        public void OnDeselect(BaseEventData e) { selected = false; }
    }
}
