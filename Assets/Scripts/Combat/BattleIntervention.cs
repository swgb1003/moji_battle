using System;
using UnityEngine;

namespace MojiBattle
{
    /// <summary>観戦者の関与（必殺技・声援）の調整値。</summary>
    [Serializable]
    public class InterventionTuning
    {
        [Tooltip("必殺ゲージ: 毎秒の自然増加 / 与えたダメージ（相手の最大HP比）あたり / 受けたダメージ（自分の最大HP比）あたり")]
        public float gaugePerSecond = 1f / 50f, gaugePerDealt = .6f, gaugePerTaken = .8f;
        [Tooltip("標準の必殺: 与ダメージ倍率 / 溜めの時間倍率 / 押してから出せる状態を待つ秒数 / 全身ぶん回しを出す距離（射程の倍率。大きい字形は武器どうしが当たって射程まで寄れない）")]
        public float specialDamage = 1.7f, specialWindupScale = .75f, specialRequestSeconds = 5f, specialWhirlReach = 1.4f;
        [Tooltip("AI の必殺技: 与ダメージ倍率 = (基本 + 威力 × 1 段階) ÷ √技の数")]
        public float customDamageBase = 1.25f, customDamagePerPower = .15f;
        [Tooltip("AI の必殺技: 溜めの時間倍率 = 基本 − 速さ × 1 段階")]
        public float customWindupBase = 1.05f, customWindupPerSpeed = .08f;
        [Tooltip("AI の必殺技: 伸び 1 段階あたりの 出せる距離の上乗せ（割合） / 踏み込みの速さ（m/s）")]
        public float customReachPerPoint = .06f, customLungePerPoint = .35f;
        [Tooltip("AI の必殺技: 次の技を出せる間合いに入るまで待つ秒数（過ぎたら連携を打ち切る）")]
        public float chainWaitSeconds = 1.2f;
        [Tooltip("必殺技 1 回で与えられるダメージの上限（相手の最大 HP 比）。段数に関係なく合計をこの範囲に収める")]
        public float specialMaxShare = .3f;
        [Tooltip("踏ん張り（必殺技を受ける側の声援）: 受けるダメージ倍率 / COM が踏ん張る確率")]
        public float braceDamageScale = .7f, comBraceChance = .5f;
        [Tooltip("声援: 効果の秒数 / 次に使えるまでの秒数 / 溜めの時間倍率 / 与ダメージ倍率 / AI の反応・受け流しの上乗せ / ゲージ増加")]
        public float cheerSeconds = 3f, cheerCooldown = 10f, cheerWindupScale = .85f, cheerDamage = 1.1f, cheerReaction = .25f, cheerGauge = .04f;
        [Tooltip("COM の使い方: ゲージが溜まってから必殺を出すまでの秒数 / 声援の使用判断の頻度（0で通常声援を無効化）")]
        public float comSpecialDelayMin = .4f, comSpecialDelayMax = 2.5f, comCheerPerSecond = .12f;
    }

    public enum InterventionKind { Special, Cheer }
    /// <summary>その陣営を操作するのは誰か。Com は AI が関与の操作も行う。Remote はオンライン対戦の相手の入力を待つ。</summary>
    public enum InterventionController { Com, Local, Remote }

    [Serializable] public struct InterventionEvent
    {
        public int fighter;
        public InterventionKind kind;
        /// <summary>必殺: 技が出た（false は間合いに入れず時間切れ）</summary>
        public bool success;
        public AttackStyle style;
        public float time;
        /// <summary>必殺: 何段目の技か（0 = 発動。カットインは 0 の時だけ）</summary>
        public int step;
        /// <summary>称号用: 発動ごとの番号・発動内の段・攻撃ID・段数。表示用の累計stepとは独立。</summary>
        public int castId, castStep, attackId, stepCount;
        /// <summary>必殺: 間合いの外から一瞬で詰めた（dashFrom から今の位置へ）</summary>
        public bool dashed;
        public Vector2 dashFrom;
        /// <summary>声援: 相手の必殺技を受ける側が踏ん張った</summary>
        public bool brace;
    }

    /// <summary>試合で使う必殺技（自作の装備技を優先し、未装備なら文字ごとの初期技）。</summary>
    public sealed class SpecialMovePlan
    {
        public string name, callout;
        public AttackStyle[] styles;
        public float damage, windupScale, reachScale, lunge;
        public SpecialMoveData move;
        public bool HasAppearance => move != null;
        public static string StandardName(string character) => BuiltinSpecialMoves.For(character).name;

        public static SpecialMovePlan For(Fighter f, InterventionTuning t)
        {
            var move = f.Loadout.build?.EquippedSpecial ?? BuiltinSpecialMoves.For(f.Loadout.grapheme);
            var styles = move.Styles();
            return new SpecialMovePlan
            {
                move = move, name = move.name, callout = move.callout, styles = styles,
                damage = (t.customDamageBase + t.customDamagePerPower * move.power) / Mathf.Sqrt(styles.Length),
                windupScale = Mathf.Max(.4f, t.customWindupBase - t.customWindupPerSpeed * move.speed),
                reachScale = 1f + t.customReachPerPoint * move.reach,
                lunge = t.customLungePerPoint * move.reach,
            };
        }
    }

    public sealed class InterventionSide
    {
        public InterventionController controller;
        public SpecialMovePlan plan;
        public float gauge;
        // Per-fighter run bonuses; ordinary and network matches keep identity multipliers.
        public float gaugeRateScale=1f, cheerRecoveryScale=1f, specialDamageScale=1f, cheerDamageScale=1f, braceDamageScale=1f;
        public float specialRequestUntil = -1f;
        /// <summary>直近の必殺技に属する攻撃 ID の範囲（連携中は以降の ID もすべて必殺）。</summary>
        public int specialFromId = -1, specialToId = -1;
        /// <summary>必殺技の連携中: 次に出す技の番号と、間合いを待つ期限。</summary>
        public bool chainActive;
        public int chainStep;
        public float chainWaitUntil = -1f;
        /// <summary>必中: 今の段の攻撃 ID・その段で体に当たったか。当たらないまま終われば当たりを確定させる。</summary>
        public int stepAttackId = -1;
        /// <summary>この必殺技で与えたダメージの合計（上限の判定）</summary>
        public float specialDealt;
        /// <summary>相手の必殺技に踏ん張っている（受けるダメージを減らす）</summary>
        public bool braced;
        public AttackStyle stepStyle;
        public bool stepHit;
        public float cheerUntil = -1f, cheerReadyAt;
        public float comPressAt = -1f;
        public float comCheerAt = -1f;
        public InterventionKind comKind;
        public int specials, cheers, specialSteps, forcedHits, dashes;

        public bool SpecialReady => gauge >= 1f;
        public bool CheerReady(float now) => now >= cheerReadyAt;
        public bool SpecialPending(float now) => now <= specialRequestUntil;
        public bool OwnsAttack(int attackId) => specialFromId >= 0 && attackId >= specialFromId && (attackId <= specialToId || chainActive);
    }

    /// <summary>
    /// 基本はオートバトルのまま、観戦者が 2 つの操作で戦いに関与する。
    /// 必殺技（ゲージ満タンで、カスタマイズで装備した技を出す）・声援（短い間 AI の動きが冴える）。
    /// 必殺技は 1〜4 個の技の連なり。一つ終わるたびに次を出し、相手が間合いの外なら一瞬で詰める（瞬歩）。
    /// 必殺技は必ず当たる: 技の間、相手は捕まって動けず（ガード・回避・受け流しもできない）、
    /// 一段が体に当たらないまま終われば、その段の当たりを確定させる。
    /// 物理と勝敗を持つ側（ローカル試合・オンラインのホスト）でだけ判定し、オンラインのゲストは表示と入力の送信だけを行う。
    /// MatchContext.Interventions が null の試合（テスト・COM 同士の大会試合）は従来どおり何もしない。
    /// </summary>
    public sealed class BattleIntervention
    {
        public readonly InterventionSide[] Sides = { new InterventionSide(), new InterventionSide() };
        readonly MatchDirector director;
        readonly MatchRandom rng;
        /// <summary>オンラインのゲスト: 入力をホストへ送る。設定されている間はこの端末では判定しない。</summary>
        public Action<int, InterventionKind> Forward;
        /// <summary>ゲストの表示: 最後に試合の状態を受け取った時刻（実時間）。</summary>
        public float RemoteFrameAt;

        MatchContext Ctx => director.Context;
        InterventionTuning T => Ctx.Balance.intervention;
        public bool Simulated => !director.RemotePlayback;
        /// <summary>表示用の現在時刻。ゲストは受け取った状態から実時間で進める。</summary>
        public float Now => Ctx.SimTime + (director.RemotePlayback ? Mathf.Min(.5f, Time.unscaledTime - RemoteFrameAt) : 0f);
        public InterventionSide Side(int id) => Sides[id];

        public BattleIntervention(MatchDirector director, InterventionController left, InterventionController right)
        {
            this.director = director;
            Sides[0].controller = left; Sides[1].controller = right;
            for (int i = 0; i < 2; i++) Sides[i].plan = SpecialMovePlan.For(director.Fighters[i], T);
            rng = new MatchRandom(director.Config.seed ^ 0x494E5456);
            director.Events.Hit += OnHit;
        }

        public bool IsSpecial(Fighter f) => f.Runtime.currentAttackId >= 0 && Sides[f.Id].OwnsAttack(f.Runtime.currentAttackId);
        /// <summary>必殺技の最中（演出用）。ゲストは受け取った状態で判断する。</summary>
        public bool InSpecial(int side) => Sides[side].chainActive;

        public float DamageScale(Fighter f, float time) => DamageScale(f, f.Runtime.currentAttackId, time);
        /// <summary>この攻撃（飛び道具は発射した時の ID）の与ダメージ倍率。</summary>
        public float DamageScale(Fighter f, int attackId, float time)
        {
            var s = Sides[f.Id];
            float scale = attackId >= 0 && s.OwnsAttack(attackId) ? s.plan.damage*s.specialDamageScale : 1f;
            return time <= s.cheerUntil ? scale * T.cheerDamage*s.cheerDamageScale : scale;
        }
        public float WindupScale(Fighter f, float time) => time <= Sides[f.Id].cheerUntil ? T.cheerWindupScale : 1f;
        /// <summary>声援中の AI の反応・受け流しの上乗せ。</summary>
        public float ReactionBonus(Fighter f, float time) => time <= Sides[f.Id].cheerUntil ? T.cheerReaction : 0f;
        /// <summary>必殺を出すため・連携の次の技のために間合いを詰めている（Brain が前進を選ぶ）。</summary>
        public bool WantsSpecial(Fighter f, float time)
        {
            var s = Sides[f.Id];
            return s.SpecialPending(time) || (s.chainActive && !Attacking(f.Runtime.state));
        }

        /// <summary>この端末の操作。ゲストはホストへ送るだけ。</summary>
        public void Press(int side, InterventionKind kind)
        {
            if (TimeController.Paused || director.Ended) return;
            if (Forward != null) { Forward(side, kind); return; }
            Request(side, kind, Sides[side].controller == InterventionController.Remote);
        }

        /// <summary>判定する側（ローカル・ホスト）で入力を受け付ける。</summary>
        public void Request(int side, InterventionKind kind, bool remote)
        {
            if (!Simulated || !director.Fighting) return;
            var s = Sides[side];
            float now = Ctx.SimTime;
            switch (kind)
            {
                case InterventionKind.Special:
                    if (s.SpecialReady && !s.chainActive) s.specialRequestUntil = now + T.specialRequestSeconds;
                    break;
                case InterventionKind.Cheer:
                    // 声援も踏ん張りも同じ満タンの声援ゲージを消費する。
                    bool brace = Sides[1 - side].chainActive && !s.braced;
                    if (!s.CheerReady(now) || s.braced) return;
                    if (brace) s.braced = true;
                    s.cheerUntil = now + T.cheerSeconds; s.cheerReadyAt = now + T.cheerCooldown/s.cheerRecoveryScale; s.cheers++;
                    s.comCheerAt = -1f;
                    s.gauge = Mathf.Min(1f, s.gauge + T.cheerGauge);
                    Ctx.Events.Raise(new InterventionEvent { fighter = side, kind = kind, success = true, time = now, brace = brace });
                    break;
            }
        }

        static bool Attacking(FighterState st) =>
            st == FighterState.AttackWindup || st == FighterState.AttackActive || st == FighterState.AttackRecovery;

        /// <summary>MatchDirector が試合中の各ステップで呼ぶ（接触の解決の後、両者の Tick の前）。</summary>
        public void Tick(float time, float dt)
        {
            if (!director.Fighting) return;
            for (int i = 0; i < 2; i++)
            {
                var s = Sides[i];
                var f = director.Fighters[i];
                s.gauge = Mathf.Min(1f, s.gauge + T.gaugePerSecond * s.gaugeRateScale * dt);
                if (s.controller == InterventionController.Com) TickCom(i, time);
                if (s.chainActive)
                {
                    HoldVictim(f.Opponent);
                    ForceIfMissed(f, s, time, false);
                    TickChain(f, s, time);
                    continue;
                }
                if (s.SpecialPending(time)) TryStartSpecial(f, s, time);
                else if (s.specialRequestUntil >= 0f)
                {
                    // 間合いに入れないまま時間切れ。ゲージは残す
                    s.specialRequestUntil = -1f;
                    Ctx.Events.Raise(new InterventionEvent { fighter = i, kind = InterventionKind.Special, success = false, style = s.plan.styles[0], time = time });
                }
            }
        }

        void TickCom(int i, float time)
        {
            var s = Sides[i];
            if (s.comPressAt >= 0f && time >= s.comPressAt)
            {
                s.comPressAt = -1f;
                Request(i, s.comKind, false);
            }
            if (s.comPressAt < 0f && s.SpecialReady && !s.SpecialPending(time) && !s.chainActive)
            {
                s.comKind = InterventionKind.Special;
                s.comPressAt = time + rng.Range(T.comSpecialDelayMin, T.comSpecialDelayMax);
            }
            if (!s.CheerReady(time) || s.braced) { s.comCheerAt = -1f; return; }
            bool defendingSpecial = Sides[1 - i].chainActive;
            // 必殺入力と声援のタイマーを分け、満タンなら HP に関係なく声援を使う。
            if (s.comCheerAt < 0f && !defendingSpecial && T.comCheerPerSecond > 0f)
                s.comCheerAt = time + rng.Range(.4f, Mathf.Max(.4f, .5f / T.comCheerPerSecond));
            if (s.comCheerAt >= 0f && time >= s.comCheerAt)
                Request(i, InterventionKind.Cheer, false);
        }

        /// <summary>この武器では出せない技を近い技に置き換える（飛び道具は字形の大きさで決まる 等）。</summary>
        public static AttackStyle Usable(Fighter f, AttackStyle style)
        {
            var b = f.Balance;
            switch (style)
            {
                case AttackStyle.GroundWave:
                case AttackStyle.StrokeShot:
                    if (!b.rangedAttacks) return AttackTechniques.CanWhirl(f) ? AttackStyle.Whirl : AttackStyle.Thrust;
                    if (style == AttackStyle.GroundWave && !AttackTechniques.CanGroundWave(f))
                        return AttackTechniques.CanStrokeShot(f) ? AttackStyle.StrokeShot : AttackStyle.Overhead;
                    if (style == AttackStyle.StrokeShot && !AttackTechniques.CanStrokeShot(f))
                        return AttackTechniques.CanGroundWave(f) ? AttackStyle.GroundWave : AttackStyle.Thrust;
                    return style;
                case AttackStyle.Whirl: return AttackTechniques.CanWhirl(f) ? style : AttackStyle.Spin;
                case AttackStyle.DashSlash: return AttackTechniques.CanDash(f) ? style : AttackStyle.Thrust;
                case AttackStyle.Leap: return f.WeightClass != WeightClass.Heavy ? style : AttackStyle.Overhead;
                case AttackStyle.FrontKick: return b.expandedActions && f.Actions != null ? style : AttackStyle.Bash;
                default: return style;
            }
        }

        /// <summary>その技を出し始められる距離。</summary>
        float StartReach(Fighter f, AttackStyle style, SpecialMovePlan plan)
        {
            var b = f.Balance;
            float reach;
            switch (style)
            {
                case AttackStyle.GroundWave: reach = b.groundWaveRange * .9f; break;
                case AttackStyle.StrokeShot: reach = b.strokeShotRange * .9f; break;
                case AttackStyle.DashSlash: reach = f.AttackRange + b.dashMaxTravel; break;
                case AttackStyle.Leap: reach = Mathf.Max(f.AttackRange, f.LeapTravelRange); break;
                case AttackStyle.Whirl: reach = f.AttackRange * T.specialWhirlReach; break;
                default: reach = f.AttackRange; break;
            }
            return reach * plan.reachScale;
        }

        void TryStartSpecial(Fighter f, InterventionSide s, float time)
        {
            if (!CanStep(f)) return;
            if (!StartStep(f, s, s.plan.styles[0], time, true)) return;
            s.specialRequestUntil = -1f;
            s.gauge = 0f; s.specials++;
            s.chainActive = true; s.chainStep = 1; s.chainWaitUntil = -1f;
            HoldVictim(f.Opponent);
            // 効果中の声援は既にゲージを消費済み。COM の新しい踏ん張りも通常の Request を通す。
            var d = Sides[1 - f.Id];
            d.braced = time <= d.cheerUntil;
            if (!d.braced && d.CheerReady(time) && d.controller == InterventionController.Com && rng.Chance(T.comBraceChance))
                d.comCheerAt = time;
        }

        /// <summary>
        /// 必殺技の当たりのダメージを決める（HitResolver が呼ぶ）。踏ん張っていれば減らし、1 回の必殺技の合計は相手の最大 HP の一定割合まで。
        /// 必殺技以外の攻撃はそのまま返す。
        /// </summary>
        public float ApplySpecialLimits(Fighter a, int attackId, Fighter d, float damage)
        {
            var s = Sides[a.Id];
            if (!s.OwnsAttack(attackId)) return damage;
            if (Sides[d.Id].braced) damage *= T.braceDamageScale*Sides[d.Id].braceDamageScale;
            float room = Mathf.Max(0f, T.specialMaxShare * Mathf.Min(1.5f,s.specialDamageScale) * d.Runtime.maxHp - s.specialDealt);
            damage = Mathf.Min(damage, room);
            s.specialDealt += damage;
            return damage;
        }

        /// <summary>技の間、相手は捕まって動けない（ひるみを延ばし続ける）。倒れている・宙に浮いている間はそのまま。</summary>
        static void HoldVictim(Fighter d)
        {
            var rt = d.Runtime;
            if (rt.state == FighterState.KO || rt.IsDown || !d.IsGrounded) return;
            if (rt.state != FighterState.Stagger) d.EnterStagger(.6f);
            else rt.stateTime = Mathf.Min(rt.stateTime, .05f);
        }

        /// <summary>
        /// 必中: 近接の段は振り抜き（当たりが残る時間）が過ぎた時、飛び道具・蹴りの段は技が終わった時に、
        /// 体に当たっていなければその段の当たりを確定させる。
        /// </summary>
        void ForceIfMissed(Fighter f, InterventionSide s, float time, bool stepEnded)
        {
            var rt = f.Runtime;
            if (s.stepAttackId < 0 || s.stepHit || rt.currentAttackId != s.stepAttackId) return;
            bool waitsForEnd = AttackTechniques.IsRanged(s.stepStyle) || AttackTechniques.IsKick(s.stepStyle);
            bool swung = rt.state == FighterState.AttackRecovery && rt.stateTime >= f.Balance.followThroughHitSeconds;
            if (!stepEnded && (waitsForEnd || !swung)) return;
            s.stepHit = true;
            s.forcedHits++;
            Ctx.Hits.ForceSpecialHit(f, f.Opponent, time);
        }

        bool CanStep(Fighter f)
        {
            var rt = f.Runtime;
            if (rt.weaponDetached || !f.IsGrounded || rt.IsDown || f.Opponent.Runtime.state == FighterState.KO) return false;
            return rt.state == FighterState.Approach || rt.state == FighterState.Guard;
        }

        /// <summary>連携: 前の技が終わったら次の技へ。崩された・倒れた・技を出し切った・間合いに入れないまま待ち切ったら終わる。</summary>
        void TickChain(Fighter f, InterventionSide s, float time)
        {
            var rt = f.Runtime;
            if (Attacking(rt.state)) { s.chainWaitUntil = -1f; return; }
            ForceIfMissed(f, s, time, true);
            bool more = s.chainStep < s.plan.styles.Length && !director.Context.MatchOver && f.Opponent.Runtime.state != FighterState.KO;
            if (more && (rt.state == FighterState.Approach || rt.state == FighterState.Guard))
            {
                if (s.chainWaitUntil < 0f) s.chainWaitUntil = time + T.chainWaitSeconds;
                if (CanStep(f) && StartStep(f, s, s.plan.styles[s.chainStep], time, false)) { s.chainStep++; s.chainWaitUntil = -1f; return; }
                if (time <= s.chainWaitUntil) return;
            }
            s.chainActive = false;
            s.specialToId = rt.currentAttackId;
            Sides[1 - f.Id].braced = false;
        }

        bool StartStep(Fighter f, InterventionSide s, AttackStyle wanted, float time, bool first)
        {
            var rt = f.Runtime;
            var style = Usable(f, wanted);
            float reach = StartReach(f, style, s.plan);
            Vector2 from = f.Body.position;
            // 瞬歩: 間合いの外なら一瞬で相手の前へ詰める（壁の内側に収める）
            bool dashed = f.DistanceToOpponent > reach && Dash(f, Mathf.Min(reach * .8f, f.AttackRange * .85f));
            if (rt.state == FighterState.Guard) f.SetState(FighterState.Approach);
            if (AttackTechniques.IsRanged(style)) rt.rangedReadyAt = 0f; // 必殺の飛び道具は通常の再使用間隔を待たない
            int before = rt.currentAttackId;
            rt.comboRemaining = 0;
            f.StartAttack(time, style, specialMove: true);
            if (rt.currentAttackId == before || rt.state != FighterState.AttackWindup) return false;
            rt.windupDuration *= s.plan.windupScale;
            if (first) { s.specialFromId = rt.currentAttackId; s.specialDealt = 0f; }
            s.specialToId = rt.currentAttackId;
            s.stepAttackId = rt.currentAttackId; s.stepStyle = rt.attackStyle; s.stepHit = false;
            if (dashed) s.dashes++;
            Ctx.Events.Raise(new InterventionEvent { fighter = f.Id, kind = InterventionKind.Special, success = true,
                style = rt.attackStyle, time = time, step = s.specialSteps, dashed = dashed, dashFrom = from,
                castId = first ? s.specials + 1 : s.specials, castStep = first ? 0 : s.chainStep,
                attackId = rt.currentAttackId, stepCount = s.plan.styles.Length });
            s.specialSteps++;
            // 伸び: 離れた相手へ踏み込む（飛び道具・跳ぶ技は自前の動きのまま）
            if (s.plan.lunge > 0f && !AttackTechniques.IsRanged(style) && style != AttackStyle.Leap && style != AttackStyle.DashSlash
                && f.DistanceToOpponent > f.AttackRange * .7f)
                f.AddVelocity(new Vector2(f.TowardOpponent * s.plan.lunge, 0f));
            return true;
        }

        /// <summary>相手の手前（距離 gap）へ体と武器ごと移す。動けない時は false。</summary>
        bool Dash(Fighter f, float gap)
        {
            var o = f.Opponent;
            float limit = Ctx.ArenaHalfWidth - .4f;
            float side = f.X <= o.X ? -1f : 1f;
            float x = Mathf.Clamp(o.X + side * gap, -limit, limit);
            if (Mathf.Abs(x - o.X) < gap * .5f) x = Mathf.Clamp(o.X - side * gap, -limit, limit); // 壁際なら反対側へ回り込む
            var delta = new Vector2(x - f.X, 0f);
            if (Mathf.Abs(delta.x) < .05f) return false;
            f.Body.position += delta;
            f.WeaponBody.position += delta;
            f.Body.transform.position += (Vector3)delta;
            f.WeaponBody.transform.position += (Vector3)delta;
            f.Body.linearVelocity = f.WeaponBody.linearVelocity = Vector2.zero;
            f.InitFacing(o.X >= x ? 1 : -1);
            return true;
        }

        void OnHit(HitEvent e)
        {
            var hs = Sides[e.attacker];
            if (hs.chainActive && e.attackId >= hs.stepAttackId && hs.stepAttackId >= 0) hs.stepHit = true;
            if (!Simulated) return;
            float share = e.damage / Mathf.Max(1f, director.Fighters[e.defender].Runtime.maxHp);
            // 必殺の当たりでは次のゲージを溜めない（連発させない）
            if (!Sides[e.attacker].OwnsAttack(e.attackId))
                Sides[e.attacker].gauge = Mathf.Min(1f, Sides[e.attacker].gauge + share * T.gaugePerDealt);
            Sides[e.defender].gauge = Mathf.Min(1f, Sides[e.defender].gauge + share * T.gaugePerTaken);
        }
    }
}
