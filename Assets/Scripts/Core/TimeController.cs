using UnityEngine;

namespace MojiBattle
{
    /// <summary>
    /// timeScale / fixedDeltaTime を一元管理する。観戦速度・一時停止・KOスロー・ヒットストップはすべてここを通す。
    /// 物理ステップ幅は常に基準値（0.02）を維持し、速度変更は「1フレームあたりのステップ数」だけを変える。
    /// そのため観戦操作はAI・物理の結果に影響しない。
    /// </summary>
    public static class TimeController
    {
        public const float BaseFixedDelta = 0.02f;
        public const float HitStopScale = 0.05f;

        static float spectatorSpeed = 1f;
        static bool paused;
        static float koScale = 1f;
        static float hitStopRemaining;

        public static float SpectatorSpeed => spectatorSpeed;
        public static bool Paused => paused;
        public static bool KoSlowActive => koScale < 1f;
        public static bool HitStopActive => hitStopRemaining > 0f;
        /// <summary>ヒットストップ等の演出を無効化する（大量シミュレーション用）。</summary>
        public static bool PresentationEffectsEnabled = true;

        public static void ResetAll()
        {
            spectatorSpeed = 1f;
            paused = false;
            koScale = 1f;
            hitStopRemaining = 0f;
            Apply();
        }

        public static void SetSpectatorSpeed(float speed)
        {
            spectatorSpeed = Mathf.Max(0.01f, speed);
            Apply();
        }

        public static void SetPaused(bool value)
        {
            paused = value;
            Apply();
        }

        public static void SetKoSlow(bool on, float scale)
        {
            koScale = on ? Mathf.Clamp(scale, 0.01f, 1f) : 1f;
            Apply();
        }

        public static void TriggerHitStop(float unscaledSeconds)
        {
            if (!PresentationEffectsEnabled) return;
            hitStopRemaining = Mathf.Max(hitStopRemaining, unscaledSeconds);
            Apply();
        }

        /// <summary>毎フレーム unscaled 時間で呼ぶ。</summary>
        public static void Tick(float unscaledDeltaTime)
        {
            if (hitStopRemaining > 0f && !paused)
            {
                hitStopRemaining -= unscaledDeltaTime;
                if (hitStopRemaining <= 0f) { hitStopRemaining = 0f; Apply(); }
            }
        }

        static void Apply()
        {
            float s = paused ? 0f : spectatorSpeed * koScale * (hitStopRemaining > 0f ? HitStopScale : 1f);
            Time.timeScale = s;
            Time.fixedDeltaTime = BaseFixedDelta;
        }
    }
}
