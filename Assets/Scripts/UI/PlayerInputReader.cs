using UnityEngine;

namespace MojiBattle
{
    /// <summary>
    /// 自操作のキーボードの読み取り（矢印キー＋Z・X・C・V・A。必殺 S・声援 D は InterventionHud）。
    /// 押した操作は PlayerInput に溜め、試合の計算（PlayerController）が固定ステップごとに取り出す。
    /// 同じ向きの矢印を素早く 2 回押すと、後ろ向きならバックステップ。
    /// </summary>
    public sealed class PlayerInputReader : MonoBehaviour
    {
        public const float DoubleTapSeconds = .25f;
        PlayerInput input;
        float lastLeftAt = -1f, lastRightAt = -1f;

        public void Bind(PlayerInput target) => input = target;

        void Update()
        {
            if (input == null) return;
            if (SettingsScreen.BlocksInput || ControlsGuide.BlocksInput || TimeController.Paused) { input.Left = input.Right = input.Up = input.Down = input.GuardHeld = false; return; }
            input.Left = Input.GetKey(KeyCode.LeftArrow);
            input.Right = Input.GetKey(KeyCode.RightArrow);
            input.Up = Input.GetKey(KeyCode.UpArrow);
            input.Down = Input.GetKey(KeyCode.DownArrow);
            input.GuardHeld = Input.GetKey(KeyCode.C);
            float now = Time.unscaledTime;
            if (Input.GetKeyDown(KeyCode.LeftArrow)) { if (now - lastLeftAt <= DoubleTapSeconds) { input.Press(PlayerButton.Backstep, -1, 0); lastLeftAt = -1f; } else lastLeftAt = now; }
            if (Input.GetKeyDown(KeyCode.RightArrow)) { if (now - lastRightAt <= DoubleTapSeconds) { input.Press(PlayerButton.Backstep, 1, 0); lastRightAt = -1f; } else lastRightAt = now; }
            if (Input.GetKeyDown(KeyCode.UpArrow)) input.Press(PlayerButton.Jump);
            if (Input.GetKeyDown(KeyCode.DownArrow)) input.Press(PlayerButton.Duck);
            if (Input.GetKeyDown(KeyCode.Z)) input.Press(PlayerButton.Slash);
            if (Input.GetKeyDown(KeyCode.X)) input.Press(PlayerButton.Tech);
            if (Input.GetKeyDown(KeyCode.C)) input.Press(PlayerButton.Guard);
            if (Input.GetKeyDown(KeyCode.V)) input.Press(PlayerButton.Kick);
            if (Input.GetKeyDown(KeyCode.A)) input.Press(PlayerButton.Ranged);
        }
    }
}
