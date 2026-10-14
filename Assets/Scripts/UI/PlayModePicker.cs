using UnityEngine;
using UnityEngine.UI;

namespace MojiBattle
{
    /// <summary>
    /// 対戦前画面の「観戦／自操作」と COM の強さの切り替え（各モード共通）。選んだ値は設定に保存して次回も使う。
    /// 幅 600・高さ 60。自操作の時だけ強さ（◀ 名前 ▶）を出す。「真・mojiFighter」は解放するまで選べない。
    /// </summary>
    public sealed class PlayModePicker : MonoBehaviour
    {
        public const float Width = 600, Height = 60;
        UguiKit.ChoiceButton watch, self, prev, next, guide;
        Text level;

        public static PlayModePicker Build(Transform parent, float x, float y)
        {
            var root = RetroUi.Group(parent, "PlayModePicker", x, y, Width, Height);
            var picker = root.gameObject.AddComponent<PlayModePicker>();
            picker.watch = RetroUi.Button(root, "PlayModeWatch", "観戦", 0, 0, 112, Height, 26, () => picker.Set(false), shadow: false);
            picker.self = RetroUi.Button(root, "PlayModeSelf", "自操作", 116, 0, 124, Height, 26, () => picker.Set(true), shadow: false);
            picker.prev = RetroUi.Button(root, "PlayModeEasier", "◀", 252, 0, 52, Height, 26, () => picker.Step(-1), shadow: false);
            RetroUi.Block(root, "PlayModeLevelPlate", 306, 0, 200, Height, RetroUi.Paper);
            RetroUi.Frame(root, 306, 0, 200, Height, RetroUi.Ink, 2);
            picker.level = RetroUi.Label(root, "PlayModeLevel", "", 310, 0, 192, Height, 24, null, TextAnchor.MiddleCenter);
            picker.level.horizontalOverflow = HorizontalWrapMode.Wrap; picker.level.verticalOverflow = VerticalWrapMode.Truncate;
            picker.level.resizeTextForBestFit = true; picker.level.resizeTextMinSize = 16; picker.level.resizeTextMaxSize = 24;
            picker.next = RetroUi.Button(root, "PlayModeHarder", "▶", 508, 0, 52, Height, 26, () => picker.Step(1), shadow: false);
            picker.guide = RetroUi.Button(root, "PlayModeGuide", "?", 564, 0, 36, Height, 26, ControlsGuide.Open, shadow: false);
            picker.Refresh();
            return picker;
        }

        void Set(bool on) { PlayControl.SetSelfControl(on); Refresh(); }

        void Step(int dir)
        {
            var levels = PlayControl.Levels;
            int i = System.Array.IndexOf(levels, PlayControl.Difficulty);
            for (int n = i + dir; n >= 0 && n < levels.Length; n += dir)
                if (PlayControl.Available(levels[n])) { PlayControl.SetDifficulty(levels[n]); break; }
            Refresh();
        }

        public void Refresh()
        {
            bool on = GameSettings.SelfControl;
            RetroUi.Selected(watch, !on, RetroUi.Ink);
            RetroUi.Selected(self, on, RetroUi.Red);
            var levels = PlayControl.Levels;
            int i = System.Array.IndexOf(levels, PlayControl.Difficulty);
            bool lastLocked = i + 1 < levels.Length && !PlayControl.Available(levels[i + 1]);
            level.text = on ? "COM：" + PlayControl.Label(PlayControl.Difficulty) : "COM 同士";
            level.color = on ? RetroUi.Ink : RetroUi.Muted;
            prev.button.interactable = on && i > 0;
            next.button.interactable = on && i + 1 < levels.Length && !lastLocked;
            prev.label.color = prev.button.interactable ? RetroUi.Ink : RetroUi.Muted;
            next.label.color = next.button.interactable ? RetroUi.Ink : RetroUi.Muted;
        }
    }
}
