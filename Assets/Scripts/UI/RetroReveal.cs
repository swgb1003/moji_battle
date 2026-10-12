using UnityEngine;

namespace MojiBattle
{
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class RetroReveal : MonoBehaviour
    {
        RectTransform rect;
        CanvasGroup group;
        Vector2 home;
        float elapsed, delay, distance;
        bool captured;
        public void Configure(float stagger, float offset) { delay = stagger; distance = offset; Restart(); }
        public void Rebase() { if(rect!=null) { home=rect.anchoredPosition; captured=true; } }
        void OnEnable() { Restart(); }
        void Restart()
        {
            rect = transform as RectTransform;
            if (!captured && rect != null) { home = rect.anchoredPosition; captured = true; }
            group = GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();
            elapsed = 0;
            group.alpha = RetroMotionSettings.Reduced ? 1 : 0;
            enabled = true;
        }
        void Update()
        {
            elapsed += Time.unscaledDeltaTime;
            float t = RetroMotionSettings.Reduced ? 1 : Mathf.Clamp01((elapsed - delay) / .48f);
            float eased = 1 - Mathf.Pow(1 - t, 3);
            group.alpha = eased;
            if (rect != null) rect.anchoredPosition = home + Vector2.down * distance * (1 - eased);
            if (t >= 1) enabled = false;
        }
    }
}
