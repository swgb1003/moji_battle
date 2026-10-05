namespace MojiBattle
{
    /// <summary>
    /// 鉄壁: 文字を盾にしてガードから反撃する（目安 Guard 40% / Attack 20% / Approach 15% / Evade 10% / Wait 15%）。
    /// 相手が近ければ字形を相手へ向けて構える。永久ガード防止に連続ガードは最大 2.5 秒、その後 0.5 秒はガードしない。
    /// </summary>
    public sealed class DefensiveStrategy : BattleStyleStrategy
    {
        public DefensiveStrategy(Fighter self, CustomizeBalance cb) : base(self, cb, cb.defensive) { }
        public override BattleStyle Style => BattleStyle.Defensive;
        public override float MaxGuardSeconds => cb.defensiveMaxGuardSeconds;
        public override float GuardCooldown => cb.defensiveGuardCooldown;

        public override bool PreferGuard(float time, float distance) =>
            distance <= self.Opponent.ThreatRange + cb.defensiveFaceMargin;

        public override float IdlePsiOverride() =>
            self.DistanceToOpponent <= self.Opponent.ThreatRange + cb.defensiveFaceMargin ? self.Balance.guardPsi : float.NaN;
    }
}
