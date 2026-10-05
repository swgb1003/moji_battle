namespace MojiBattle
{
    /// <summary>
    /// カウンター: Wait → 相手の攻撃を検知 → Guard / Evade → CounterWindow → Attack。
    /// 相手の攻撃の硬直中と、攻撃シーケンスが終わってから counterWindow 秒の間は攻撃を最優先する。
    /// それ以外は相手の射程の少し外で待ち、自分からはあまり打たない（膠着は既存の膠着ブレーカーが崩す）。
    /// </summary>
    public sealed class CounterStrategy : BattleStyleStrategy
    {
        float oppAttackEndedAt = -999f;
        bool oppWasAttacking;

        public CounterStrategy(Fighter self, CustomizeBalance cb) : base(self, cb, cb.counter) { }
        public override BattleStyle Style => BattleStyle.Counter;

        public override void Observe(float time)
        {
            var o = self.Opponent.Runtime.state;
            bool attacking = o == FighterState.AttackWindup || o == FighterState.AttackActive || o == FighterState.AttackRecovery;
            if (oppWasAttacking && !attacking) oppAttackEndedAt = time;
            oppWasAttacking = attacking;
        }

        /// <summary>反撃の好機（相手の攻撃後の硬直中、または終わってから counterWindow 以内）。</summary>
        public bool InWindow(float time) =>
            self.Opponent.Runtime.state == FighterState.AttackRecovery || time - oppAttackEndedAt <= cb.counterWindow;

        public override float AttackWillingnessBonus(float time) => InWindow(time) ? 1f : 0f;
        public override bool StrikeImmediately(float time) => InWindow(time);

        public override bool PreferWait(float time, float distance) =>
            !InWindow(time) && distance > self.Opponent.AttackRange * cb.counterWaitRangeScale;

        public override string DebugState(float time) => InWindow(time) ? "反撃の好機" : "待ち";
    }
}
