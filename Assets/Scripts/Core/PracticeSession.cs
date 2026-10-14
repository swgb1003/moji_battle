namespace MojiBattle
{
    /// <summary>
    /// 練習モード: 自分と相手の構成を選び、自操作で技を試す。HP は減らず時間切れもない。
    /// 相手は試合の始めはサンドバッグ（動かない）。戦闘画面の右下で強さを変えると動き出す。
    /// </summary>
    public static class PracticeSession
    {
        public static bool Active;
        /// <summary>今の相手の強さ（試合を始めるたびにサンドバッグへ戻す）。</summary>
        public static ComDifficulty Difficulty = ComDifficulty.Sandbag;
        /// <summary>右下の切り替えで選べる強さ（サンドバッグ → 初心者 … 真・mojiFighter。真は解放後のみ）。</summary>
        public static readonly ComDifficulty[] Choices =
        {
            ComDifficulty.Sandbag, ComDifficulty.Beginner, ComDifficulty.Weak, ComDifficulty.Normal,
            ComDifficulty.Strong, ComDifficulty.MojiFighter, ComDifficulty.TrueMojiFighter,
        };
    }
}
