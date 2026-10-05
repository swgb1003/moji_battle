using UnityEngine.SceneManagement;

namespace MojiBattle
{
    /// <summary>
    /// 画面遷移（カスタマイズ仕様 3）: タイトル → カスタマイズ（文字入力・字体・サイズ・持ち方・握る位置・スタイル）→ VS確認 → BATTLE → リザルト。
    /// リザルトは Battle シーン内のオーバーレイ（同じ設定でもう一度 / カスタマイズへ戻る）。
    /// </summary>
    public static class GameFlow
    {
        public const string TitleScene = "Title";
        public const string CustomizeScene = "Customize";
        public const string BattleScene = "Battle";

        public static void ToTitle() => SceneManager.LoadScene(TitleScene);

        public static void ToCustomize() => SceneManager.LoadScene(CustomizeScene);

        /// <summary>カスタマイズ内容（BuildRuntimeStore）で試合を始める。</summary>
        public static void ToBattle()
        {
            BuildRuntimeStore.Active = true;
            SceneManager.LoadScene(BattleScene);
        }
    }
}
