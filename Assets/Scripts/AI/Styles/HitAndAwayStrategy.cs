using UnityEngine;

namespace MojiBattle
{
    /// <summary>
    /// ヒット＆アウェイ: Approach → Attack → Retreat → Wait → Approach。
    /// 攻撃シーケンスが終わったら確率ではなく原則離脱する。離脱時間は重量級ほど短い（逃げ続けない）。
    /// </summary>
    public sealed class HitAndAwayStrategy : BattleStyleStrategy
    {
        public HitAndAwayStrategy(Fighter self, CustomizeBalance cb) : base(self, cb, cb.hitAndAway) { }
        public override BattleStyle Style => BattleStyle.HitAndAway;

        public override float? RetreatSecondsAfterAttack(MatchRandom rng)
        {
            float max = StatCalculator.Lerp01(cb.hitAndAwayRetreatLight, cb.hitAndAwayRetreatHeavy, self.Stats.weightScore);
            return Mathf.Max(0.1f, rng.Range(max * 0.75f, max));
        }
    }
}
