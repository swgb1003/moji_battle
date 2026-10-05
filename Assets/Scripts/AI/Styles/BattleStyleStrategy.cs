using UnityEngine;

namespace MojiBattle
{
    /// <summary>
    /// 戦闘スタイル（カスタマイズ仕様 7）の基底。ステータスへは掛けず、既存 AI（FighterBrain）の判断へ方針を差し込む。
    /// - Apply: 重量クラスの AI 重みをスタイルの重みへ置き換える（攻撃意欲・ガード・回避・離脱・連撃・反応）
    /// - 各フック: 攻撃後の離脱、攻撃の好機、待ち、連続ガードの上限、待機中の構え
    /// FighterBrain はスタイルごとの if 文を持たず、このフックだけを呼ぶ。
    /// </summary>
    public abstract class BattleStyleStrategy
    {
        protected readonly Fighter self;
        protected readonly CustomizeBalance cb;
        protected readonly StyleProfile profile;

        protected BattleStyleStrategy(Fighter self, CustomizeBalance cb, StyleProfile profile)
        {
            this.self = self;
            this.cb = cb;
            this.profile = profile;
        }

        public abstract BattleStyle Style { get; }

        public static BattleStyleStrategy Create(Fighter f, BattleStyle style, CustomizeBalance cb)
        {
            switch (style)
            {
                case BattleStyle.HitAndAway: return new HitAndAwayStrategy(f, cb);
                case BattleStyle.Counter: return new CounterStrategy(f, cb);
                case BattleStyle.Defensive: return new DefensiveStrategy(f, cb);
                default: return new AggressiveStrategy(f, cb);
            }
        }

        /// <summary>重量クラスの AI 重みを、このスタイルの重みへ置き換えたコピー。</summary>
        public virtual ClassTendency Apply(ClassTendency t, float guardBiasBonus)
        {
            var p = profile;
            var r = t.Clone();
            r.attackWillingness = p.attackWillingness;
            r.guardBias = Mathf.Clamp01(t.guardBias * p.guardBiasScale + guardBiasBonus);
            r.evadeBias = Mathf.Clamp01(t.evadeBias * p.evadeBiasScale);
            r.proactiveGuard = Mathf.Clamp01(p.proactiveGuard + guardBiasBonus * 0.5f);
            r.retreatAfterAttack = p.retreatAfterAttack;
            r.comboMax = Mathf.Max(1, t.comboMax + p.comboBonus);
            r.comboMin = Mathf.Clamp(t.comboMin, 1, r.comboMax);
            r.reactionChance = Mathf.Clamp01(t.reactionChance + p.reactionBonus);
            r.counterBias = p.counterBias;
            if (p.waitsNearCenter >= 0) r.waitsNearCenter = p.waitsNearCenter == 1;
            return r;
        }

        /// <summary>攻撃シーケンス後の離脱秒数。null なら既定（離脱確率 retreatAfterAttack で判定）。</summary>
        public virtual float? RetreatSecondsAfterAttack(MatchRandom rng) => null;

        /// <summary>間合い内で攻撃を選ぶ確率への加算（好機）。</summary>
        public virtual float AttackWillingnessBonus(float time) => 0f;

        /// <summary>好機なので攻撃開始の揺らぎを待たずに打つ。</summary>
        public virtual bool StrikeImmediately(float time) => false;

        /// <summary>射程外で自分から詰めずに待つ（true なら Hold / 近すぎれば下がる）。</summary>
        public virtual bool PreferWait(float time, float distance) => false;

        /// <summary>射程外・相手の間合いの手前でガードを構えて待つ（鉄壁）。</summary>
        public virtual bool PreferGuard(float time, float distance) => false;

        /// <summary>相手が溜めている間は射程の外で待つ（既定の読み）。猛攻は待たずに詰める。</summary>
        public virtual bool WaitsOutOpponentWindup => true;

        /// <summary>連続ガードの上限秒数と、上限後に再びガードできるまでの秒数。</summary>
        public virtual float MaxGuardSeconds => float.PositiveInfinity;
        public virtual float GuardCooldown => 0f;

        /// <summary>待機中の構え ψ（NaN なら持ち方・重量クラスの既定）。</summary>
        public virtual float IdlePsiOverride() => float.NaN;

        public virtual void OnAttackStarted(float time) { }

        /// <summary>毎物理ステップの観察（相手の状態の変化を見逃さないため、AI 判断の周期とは別に呼ぶ）。</summary>
        public virtual void Observe(float time) { }

        /// <summary>デバッグ表示用の補足（好機・待ちなど）。</summary>
        public virtual string DebugState(float time) => "";
    }
}
