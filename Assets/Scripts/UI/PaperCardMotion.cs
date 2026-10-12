using UnityEngine;
using UnityEngine.EventSystems;

namespace MojiBattle
{
    public sealed class PaperCardMotion : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        RectTransform rect;
        Vector2 home;
        Quaternion rotation;
        Vector3 scale;
        PaperDepthGraphic surface;
        bool hover;
        float focus;
        void Awake()
        {
            rect = (RectTransform)transform; home = rect.anchoredPosition;
            rotation = rect.localRotation; scale = rect.localScale;
        }
        public void Rebase() { home = rect.anchoredPosition; rotation = rect.localRotation; scale = rect.localScale; }
        void LateUpdate()
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            bool keyboard = selected != null && selected.transform.IsChildOf(transform);
            bool focused = !SettingsScreen.IsOpen && (hover || keyboard);
            bool reduced = RetroMotionSettings.Reduced;
            focus = Mathf.Lerp(focus, focused ? 1 : 0, reduced ? 1 : 1 - Mathf.Exp(-19 * Time.unscaledDeltaTime));
            if (surface == null)
                foreach (Transform child in transform)
                { var candidate = child.GetComponent<PaperDepthGraphic>(); if (candidate != null && candidate.fullPanel) { surface = candidate; break; } }
            surface?.SetElevation(focus * 5);
            var reveal = GetComponent<RetroReveal>();
            if (reveal != null && reveal.enabled) return;
            rect.anchoredPosition = home + Vector2.up * (reduced ? 0 : focus * 8);
            rect.localRotation = rotation * Quaternion.Euler(0, 0, reduced ? 0 : -.35f * focus);
            rect.localScale = scale * (1 + (reduced ? 0 : .007f * focus));
        }
        public void OnPointerEnter(PointerEventData e) => hover = true;
        public void OnPointerExit(PointerEventData e) => hover = false;
        void OnDisable()
        {
            hover = false; focus = 0; surface?.SetElevation(0);
            if (rect == null) return;
            rect.anchoredPosition = home; rect.localRotation = rotation; rect.localScale = scale;
        }
    }
}
