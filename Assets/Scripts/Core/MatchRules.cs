namespace MojiBattle
{
    /// <summary>勝敗規則の純粋関数。</summary>
    public static class MatchRules
    {
        public const int NoOutcome = -2;
        public const int Draw = -1;

        /// <summary>同一物理ステップ解決後のHPからKO判定。両者0以下なら引き分け。</summary>
        public static int DecideKo(float hpLeft, float hpRight)
        {
            bool l = hpLeft <= 0f, r = hpRight <= 0f;
            if (l && r) return Draw;
            if (l) return 1;
            if (r) return 0;
            return NoOutcome;
        }

        /// <summary>時間切れは残HP割合が高い側。同率（1e-4以内）なら引き分け。</summary>
        public static int DecideTimeUp(float hpLeft, float maxLeft, float hpRight, float maxRight)
        {
            float a = maxLeft > 0f ? hpLeft / maxLeft : 0f;
            float b = maxRight > 0f ? hpRight / maxRight : 0f;
            if (System.Math.Abs(a - b) <= 1e-4f) return Draw;
            return a > b ? 0 : 1;
        }
    }
}
