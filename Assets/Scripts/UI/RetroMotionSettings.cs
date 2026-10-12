using UnityEngine;

namespace MojiBattle
{
    public sealed class RetroMotionSettings : MonoBehaviour
    {
        public static bool Reduced => PlayerPrefs.GetInt("MenuReducedMotion", 0) != 0;
        void Start() { GameSettings.Changed += UpdateHint; UpdateHint(); }
        void OnDestroy() => GameSettings.Changed -= UpdateHint;
        void UpdateHint()
        {
            foreach (var label in GetComponentsInChildren<UnityEngine.UI.Text>(true))
                if (label.name == "MotionHint") label.text = Reduced ? "F6  動きを戻す" : "F6  動きを抑える";
        }
        void Update()
        {
            if (!SettingsScreen.BlocksInput && Input.GetKeyDown(KeyCode.F6))
            {
                GameSettings.SetMenuMotion(!Reduced);
            }
        }
    }
}
