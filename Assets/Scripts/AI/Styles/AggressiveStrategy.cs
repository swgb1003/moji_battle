namespace MojiBattle
{
    /// <summary>
    /// 猛攻: 相手に張り付いて攻撃回数を増やす（目安 Attack 45% / Approach 30% / Guard 10% / Evade 10% / Retreat 5%）。
    /// 射程外なら詰め、射程内なら攻撃を最優先。攻撃後も離脱しない。HP が減っても方針を変えない。
    /// </summary>
    public sealed class AggressiveStrategy : BattleStyleStrategy
    {
        public AggressiveStrategy(Fighter self, CustomizeBalance cb) : base(self, cb, cb.aggressive) { }
        public override BattleStyle Style => BattleStyle.Aggressive;
        public override float? RetreatSecondsAfterAttack(MatchRandom rng) => 0f;
        public override bool WaitsOutOpponentWindup => false;
    }
}
