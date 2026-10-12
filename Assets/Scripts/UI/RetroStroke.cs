using UnityEngine;

namespace MojiBattle
{
    /// <summary>Ink rules draw out along their own axis, independent of spectator time.</summary>
    public sealed class RetroStroke : MonoBehaviour
    {
        float elapsed;
        void OnEnable() { elapsed = 0; enabled = true; }
        void Update()
        {
            elapsed += Time.unscaledDeltaTime;
            float t = RetroMotionSettings.Reduced ? 1 : Mathf.Clamp01(elapsed / .52f);
            transform.localScale = new Vector3(1 - Mathf.Pow(1 - t, 3), 1, 1);
            if (t >= 1) enabled = false;
        }
    }
}
