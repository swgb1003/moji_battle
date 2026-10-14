using System.Collections.Generic;

namespace MojiBattle
{
    /// <summary>自操作のボタン。斬る=Z / 技=X / ガード=C / 蹴り=V / 飛び道具・投げ=A / 必殺=S / 声援=D / 跳ぶ=↑ / しゃがむ=↓。</summary>
    public enum PlayerButton { Slash, Tech, Guard, Kick, Ranged, Special, Cheer, Jump, Duck, Backstep }

    /// <summary>押した瞬間のボタンと、その時に押していた方向（画面の左右 -1/0/1・上下 -1/0/1）。</summary>
    public struct PlayerPress
    {
        public PlayerButton button;
        public int screenX, y;
        public PlayerPress(PlayerButton button, int screenX, int y) { this.button = button; this.screenX = screenX; this.y = y; }
    }

    /// <summary>
    /// 自操作の入力（試合の計算から見た入力）。キーボードの読み取りは UI 側（PlayerInputReader）が行い、
    /// 押した操作を溜めておく。PlayerController が固定ステップごとに取り出して使う。
    /// </summary>
    public sealed class PlayerInput
    {
        public bool Left, Right, Up, Down, GuardHeld;
        readonly Queue<PlayerPress> presses = new Queue<PlayerPress>();

        public int ScreenX => (Right ? 1 : 0) - (Left ? 1 : 0);
        public int Y => (Up ? 1 : 0) - (Down ? 1 : 0);

        public void Press(PlayerButton button) => Press(button, ScreenX, Y);
        public void Press(PlayerButton button, int screenX, int y)
        {
            presses.Enqueue(new PlayerPress(button, screenX, y));
            while (presses.Count > 16) presses.Dequeue();
        }
        public bool TryTake(out PlayerPress press)
        {
            if (presses.Count == 0) { press = default; return false; }
            press = presses.Dequeue();
            return true;
        }
        public void Clear() { presses.Clear(); Left = Right = Up = Down = GuardHeld = false; }
    }
}
