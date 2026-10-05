using UnityEngine;

namespace MojiBattle
{
    /// <summary>
    /// 0.15秒周期で意図を発行する AI（7.1）。力やトルクは直接加えず、状態と移動意図だけを決める。
    /// 乱数は試合専用PRNG（陣営ごとにフォーク）のみ。
    /// </summary>
    public sealed class FighterBrain
    {
        readonly Fighter self;
        public readonly MatchRandom Rng;
        float nextDecisionAt;
        float pendingAttackAt = -1f;
        float holdGuardUntil;
        float retreatUntil;
        float blockedTime;
        float stuckTime;
        bool punishing;
        float lastDistance = 999f;

        public MoveIntent Move { get; private set; } = MoveIntent.Advance;
        /// <summary>前進が武器に阻まれている間は武器を立てて担ぐ（WeaponMotor2D が参照）。</summary>
        public bool CarryWeapon { get; private set; }
        public string LastDecision { get; private set; } = "";

        public FighterBrain(Fighter self, MatchRandom rng)
        {
            this.self = self;
            Rng = rng;
        }

        CombatBalance B => self.Balance;

        /// <summary>試合後半の攻めの強さ 0〜1（残り時間が少ないほど大きい）。</summary>
        float Urgency => self.Context.Overtime ? 1f : Mathf.Clamp01((self.Context.MatchClock - B.lateGameStart) / Mathf.Max(0.01f, B.lateGameRamp));
        /// <summary>まだ誰もダメージを受けていない試合は、後半ほど受け・回避をやめて打ち合う（引き分け防止）。</summary>
        bool NoDamageYet => self.Runtime.hp >= self.Runtime.maxHp && self.Opponent.Runtime.hp >= self.Opponent.Runtime.maxHp;
        float DefenseScale => 1f - (NoDamageYet ? 1f : B.lateDefenseReduction) * Urgency;

        /// <summary>
        /// 相手が既に溜めていて、自分の溜めの方が長い（先に当てられる）なら打ち始めない。
        /// 重量級は「受けながら一撃」が持ち味なので対象外。読めるかどうかは反応率に従う。
        /// </summary>
        bool WouldBeOutpaced()
        {
            if (self.WeightClass == WeightClass.Heavy) return false;
            var o = self.Opponent.Runtime;
            if (o.state != FighterState.AttackWindup) return false;
            float oppRemaining = o.windupDuration - o.stateTime;
            float mine = StatCalculator.Windup(self.Stats.weightScore, B);
            return mine > oppRemaining + 0.05f && Rng.Chance(self.Tendency.reactionChance * DefenseScale);
        }

        /// <summary>
        /// これより近いと武器が地面・相手の字形・体に挟まって振れない。軽量・中量級は射程の一定割合まで下がって打つ。
        /// </summary>
        float MinStrikeDistance => self.WeightClass == WeightClass.Heavy
            ? B.minAttackDistance
            : Mathf.Max(B.minAttackDistance, self.AttackRange * B.preferredSpacingFraction);
        float TooCloseDistance => self.WeightClass == WeightClass.Heavy
            ? B.tooCloseDistance
            : Mathf.Max(B.tooCloseDistance, self.AttackRange * B.preferredSpacingFraction);
        FighterRuntime Rt => self.Runtime;

        public void Idle()
        {
            Move = MoveIntent.Hold;
            pendingAttackAt = -1f;
            if (Rt.state == FighterState.Guard) self.SetState(FighterState.Approach);
        }

        public void Tick(float time)
        {
            if (time < nextDecisionAt) return;
            nextDecisionAt = time + B.aiInterval;
            Decide(time);
        }

        public void OnAttackSequenceFinished(float time)
        {
            pendingAttackAt = -1f;
            if (Rng.Chance(self.Tendency.retreatAfterAttack))
            {
                retreatUntil = time + Rng.Range(0.4f, 0.8f);
                // 軽量級は攻撃後にバックステップで離脱しやすい
                if (self.WeightClass == WeightClass.Light && time >= Rt.evadeReadyAt && self.BackSpace > B.minBackstepSpace && Rng.Chance(0.5f))
                {
                    self.StartEvade(EvadeKind.Backstep, time);
                    LastDecision = "離脱(回避)";
                    return;
                }
                LastDecision = "離脱";
            }
        }

        void Decide(float time)
        {
            var s = Rt.state;
            if (!Rt.CanBeControlled) return;
            if (s == FighterState.Guard && Rt.stateTime < B.guardMinDwell) return;

            var opp = self.Opponent;
            var tend = self.Tendency;
            float d = self.DistanceToOpponent;
            float range = self.AttackRange;
            bool oppDown = opp.Runtime.IsDown;
            // 前進が阻まれている時間（予約攻撃の待ち中も数える）
            // 相手が逃げている（後退・回避中）なら追跡であって「阻まれ」ではない
            bool oppFleeing = opp.Runtime.state == FighterState.Evade || opp.Brain.Move == MoveIntent.Retreat;
            bool pushing = s == FighterState.Approach && (Move == MoveIntent.Advance || pendingAttackAt >= 0f) && d < range * 2.5f && lastDistance - d < 0.03f
                           && !oppFleeing;
            blockedTime = pushing ? blockedTime + B.aiInterval : 0f;
            lastDistance = d;
            // 追撃可なら転倒中の相手も攻撃対象（KO後は不可）
            bool oppAttackable = opp.Runtime.state != FighterState.KO && (!oppDown || B.allowAttackOnDowned);

            // 0) 膠着の安全策: 両者が長く攻撃していなければ、距離に関係なく打って崩す
            if (s == FighterState.Approach && self.IsGrounded && time >= Rt.attackReadyAt && opp.Runtime.state != FighterState.KO
                && self.Context.SimTime - self.Context.LastAttackStartTime > B.idleBreakSeconds)
            {
                pendingAttackAt = -1f;
                self.StartAttack(time);
                LastDecision = "膠着を崩す";
                return;
            }

            // 1) 相手の攻撃予兆に反応（ガード / 回避）
            // 試合後半は予兆への反応（受け・回避）も減り、打ち合いになりやすい
            if (!oppDown && IsThreat(opp, d, out float timeToHit) && (s == FighterState.Guard || Rng.Chance(tend.reactionChance * DefenseScale)))
            {
                if (s == FighterState.Guard) { holdGuardUntil = Mathf.Max(holdGuardUntil, time + timeToHit + 0.2f); return; }
                React(time, d, timeToHit);
                return;
            }

            if (s == FighterState.Guard)
            {
                if (time < holdGuardUntil) return;
                self.SetState(FighterState.Approach);
            }

            // 1b) 相手の攻撃後の硬直に差し込む（回避→反撃）。射程外なら踏み込んでから打つ。
            var ort = opp.Runtime;
            // 相手の攻撃後の硬直・よろけ（弾き負けを含む）に差し込む
            bool oppOpen = ort.state == FighterState.AttackRecovery || ort.state == FighterState.Stagger;
            if (!oppDown && oppOpen && time >= Rt.attackReadyAt)
            {
                float remain = Mathf.Max(0f, ort.state == FighterState.Stagger
                    ? ort.stateDurationOverride - ort.stateTime
                    : ort.recoveryDuration - ort.stateTime);
                float strikeRange = self.AttackRange * (self.WeightClass == WeightClass.Heavy ? B.heavyRangeSlack : 1.1f);
                if (d < MinStrikeDistance && self.BackSpace > B.wallDangerDistance)
                {
                    Move = MoveIntent.Retreat;
                    LastDecision = "間合い取り";
                    return;
                }
                if (d <= strikeRange)
                {
                    punishing = false;
                    pendingAttackAt = -1f;
                    Rt.comboRemaining = Rng.RangeInclusive(tend.comboMin, tend.comboMax) - 1;
                    self.StartAttack(time);
                    LastDecision = "差し込み";
                    return;
                }
                if (punishing || d <= strikeRange + self.WalkSpeed * remain * B.punishReachFactor)
                {
                    if (!punishing) punishing = Rng.Chance(0.85f);
                    if (punishing)
                    {
                        pendingAttackAt = -1f;
                        Move = MoveIntent.Advance;
                        LastDecision = "踏み込み";
                        return;
                    }
                }
            }
            else punishing = false;

            // 2) 予約済み攻撃（開始タイミングに ±jitter）
            if (pendingAttackAt >= 0f)
            {
                bool longBlockedNow = blockedTime >= B.blockedAdvanceSeconds + 1.5f;
                if ((d > range * 1.5f && !longBlockedNow) || (d > range * 1.3f && blockedTime < B.blockedAdvanceSeconds) || !oppAttackable) { pendingAttackAt = -1f; Rt.comboRemaining = 0; }
                else if (time >= pendingAttackAt)
                {
                    pendingAttackAt = -1f;
                    if (d < MinStrikeDistance && self.BackSpace > B.wallDangerDistance) { Rt.comboRemaining = 0; Move = MoveIntent.Retreat; LastDecision = "間合い取り"; return; }
                    if (WouldBeOutpaced()) { Rt.comboRemaining = 0; Move = MoveIntent.Hold; LastDecision = "溜めを見て待つ"; return; }
                    // 阻まれ判定は直近のものだけ有効、かつ射程の 1.5 倍以内（古い判定で遠くから振らない）
                    bool blockedNow = (blockedTime >= B.blockedAdvanceSeconds && d <= range * 1.5f) || blockedTime >= B.blockedAdvanceSeconds + 1.5f;
                    if (d <= range * 1.05f || blockedNow)
                    {
                        blockedTime = 0f;
                        self.StartAttack(time);
                        LastDecision = Rt.comboRemaining > 0 ? $"連撃x{Rt.comboRemaining + 1}" : "攻撃";
                        return;
                    }
                    Rt.comboRemaining = 0;
                }
                else { Move = MoveIntent.Hold; return; }
            }

            bool stalemate = time - self.Context.LastDamageTime > B.stalemateSeconds;
            // 前進しているのに距離が縮まらない（相手の武器・体に阻まれている）なら、届く範囲とみなして攻撃する
            // 相手へ向かう速度が出ていない（止まっている・押し戻されている）時だけ「阻まれている」とみなす。
            // 相手が下がって距離が縮まらないだけなら追い続ける
            float slack = self.WeightClass == WeightClass.Heavy ? B.heavyRangeSlack : 1f;
            // 射程外で阻まれていたら武器を立てて詰める。射程付近まで来たら構えに戻す
            if (blockedTime >= B.blockedAdvanceSeconds && d > range * 1.2f) CarryWeapon = true;
            else if (d <= range * 1.2f || s != FighterState.Approach) CarryWeapon = false;
            // 阻まれている時: 射程 1.5 倍以内なら打つ。それより遠ければ武器を立てて詰める。2 秒以上動けなければ距離に関係なく打って崩す
            bool blocked = blockedTime >= B.blockedAdvanceSeconds;
            bool longBlocked = blockedTime >= B.blockedAdvanceSeconds + 1.5f;
            bool inRange = d <= range * slack || (blocked && d <= range * 1.5f) || longBlocked;
            bool tooClose = d < TooCloseDistance;
            bool wallBehind = self.BackSpace < B.wallDangerDistance;

            // 3) 間合い内で安定姿勢なら攻撃
            if (inRange && d >= MinStrikeDistance && oppAttackable && time >= Rt.attackReadyAt && time >= retreatUntil && !WouldBeOutpaced())
            {
                float willingness = tend.attackWillingness + (stalemate ? 0.2f : 0f) + B.lateAttackBonus * Urgency;
                if (Rng.Chance(willingness))
                {
                    Rt.comboRemaining = Rng.RangeInclusive(tend.comboMin, tend.comboMax) - 1;
                    pendingAttackAt = time + Rng.Range(0f, 2f * B.attackTimingJitter);
                    Move = MoveIntent.Hold;
                    LastDecision = "攻撃予約";
                    return;
                }
                // 重量級は攻撃しない時、構えて待つことが多い
                if (Rng.Chance(tend.proactiveGuard * DefenseScale)) { EnterGuard(time, Rng.Range(0.4f, 0.8f)); LastDecision = "構え"; return; }
            }

            // 3b) 下がろうとしても動けない（引っ掛かり・壁際）なら跳び越えるか、そのまま打つ
            bool notMoving = Move != MoveIntent.Hold && Mathf.Abs(self.Body.linearVelocity.x) < 0.25f;
            stuckTime = s == FighterState.Approach && notMoving ? stuckTime + B.aiInterval : 0f;
            if (stuckTime >= 0.6f && oppAttackable && time >= Rt.attackReadyAt)
            {
                stuckTime = 0f;
                // 跳び越えは密着しているか背後が壁の時だけ。離れていれば打って崩す
                bool hemmedIn = d < TooCloseDistance * 1.5f || self.BackSpace < B.wallDangerDistance;
                if (hemmedIn && self.WeightClass != WeightClass.Heavy && time >= Rt.evadeReadyAt && Rng.Chance(0.5f)) { self.StartEvade(EvadeKind.HopOver, time); LastDecision = "跳び越え(脱出)"; return; }
                self.StartAttack(time);
                LastDecision = "密着から打つ";
                return;
            }

            // 4) 接近しすぎ・壁際・攻撃後
            if (tooClose || (wallBehind && inRange) || time < retreatUntil)
            {
                switch (self.WeightClass)
                {
                    case WeightClass.Light:
                    case WeightClass.Medium:
                        // 壁を背に密着されたら跳び越えて脱出を最優先。跳べなければ密着のまま打ち返す（待機して殴られ続けない）
                        bool cornered = wallBehind && tooClose;
                        if (wallBehind && time >= Rt.evadeReadyAt && Rng.Chance(cornered ? 0.9f : tend.evadeBias * 0.6f))
                        {
                            self.StartEvade(EvadeKind.HopOver, time);
                            LastDecision = cornered ? "跳び越え(脱出)" : "跳び越え";
                            return;
                        }
                        if (cornered && oppAttackable && time >= Rt.attackReadyAt)
                        {
                            Rt.comboRemaining = Rng.RangeInclusive(tend.comboMin, tend.comboMax) - 1;
                            self.StartAttack(time);
                            LastDecision = "密着で打ち返す";
                            return;
                        }
                        Move = wallBehind ? MoveIntent.Hold : MoveIntent.Retreat;
                        LastDecision = self.WeightClass == WeightClass.Light ? "離脱" : "間合い調整";
                        return;
                    default:
                        // 膠着中は密着でガードを固めず打つ
                        if (stalemate && oppAttackable && time >= Rt.attackReadyAt) { self.StartAttack(time); LastDecision = "密着から打つ"; }
                        else if (!oppDown && Rng.Chance(tend.guardBias * DefenseScale)) { EnterGuard(time, Rng.Range(0.5f, 0.9f)); LastDecision = "ガード"; }
                        else { Move = MoveIntent.Hold; LastDecision = "待機"; }
                        return;
                }
            }

            // 5) 接近 / 待機
            if (inRange) { Move = MoveIntent.Hold; LastDecision = "間合い"; return; }
            // 相手が溜めている間は射程の外で待ち、振り終わり（硬直）を狙う
            if (opp.Runtime.state == FighterState.AttackWindup && d > opp.AttackRange * 0.95f && Rng.Chance(tend.reactionChance))
            {
                Move = d < opp.ThreatRange + 0.6f ? MoveIntent.Retreat : MoveIntent.Hold;
                LastDecision = "溜めを待つ";
                return;
            }
            if (tend.waitsNearCenter && !stalemate && Urgency < 0.3f)
            {
                // 中央より少し手前で待機して相手の接近を誘う
                float holdLine = B.heavyHoldDistanceFromCenter;
                float distFromCenterOnOwnSide = -self.TowardOpponent * self.X;
                bool beyondCenterSide = self.TowardOpponent * (0f - self.X) < 0f; // 既に中央を越えている
                Move = (!beyondCenterSide && distFromCenterOnOwnSide > holdLine) ? MoveIntent.Advance : MoveIntent.Hold;
                LastDecision = Move == MoveIntent.Hold ? "待ち構え" : "前進";
                return;
            }
            Move = MoveIntent.Advance;
            LastDecision = stalemate ? "前進(膠着打開)" : "接近";
        }

        bool IsThreat(Fighter opp, float d, out float timeToHit)
        {
            timeToHit = 99f;
            var os = opp.Runtime.state;
            if (os == FighterState.AttackWindup)
            {
                timeToHit = Mathf.Max(0f, opp.Runtime.windupDuration - opp.Runtime.stateTime);
                return timeToHit <= B.threatWindow && d <= opp.ThreatRange + 0.4f;
            }
            if (os == FighterState.AttackActive)
            {
                timeToHit = 0f;
                return d <= opp.ThreatRange + 0.3f;
            }
            return false;
        }

        void React(float time, float d, float timeToHit)
        {
            var tend = self.Tendency;
            bool evadeReady = time >= Rt.evadeReadyAt;
            float back = self.BackSpace;
            float wGuard = tend.guardBias
                + (self.Stats.defense >= B.highDefenseGuardThreshold ? B.highDefenseGuardBonus : 0f)
                + (back < B.minBackstepSpace ? 0.2f : 0f);
            float wEvade = evadeReady ? tend.evadeBias * (back < B.minBackstepSpace ? 0.6f : 1f) * DefenseScale : 0f;
            // ガードの構えが間に合わない（武器を大きく回す必要がある）なら回避を優先
            var oa = self.Opponent.Runtime;
            float guardTarget = oa.attackStyle == AttackStyle.Overhead ? B.guardPsiHigh : B.GuardPsiLow(self.Stats.weightScore);
            float raiseTime = Mathf.Abs(guardTarget - self.WeaponMotor.CurrentPsi) / Mathf.Lerp(700f, 250f, self.Stats.weightScore / 100f);
            if (raiseTime > timeToHit) wGuard *= 0.25f;
            // 距離が射程端に近いほど後退で外しやすい
            if (evadeReady && d > self.Opponent.AttackRange * 0.75f) wEvade += 0.15f;
            // 重量級は相手の連撃を受けながら自分の一撃を始める（反撃）。軽い打撃ではよろけない。
            float wCounter = time >= Rt.attackReadyAt && d <= self.AttackRange * B.heavyRangeSlack ? tend.counterBias : 0f;
            // 膠着中は受けに回らず打ち合いを選びやすくする
            if (time - self.Context.LastDamageTime > B.stalemateSeconds) { wGuard *= 0.4f; wCounter *= 1.5f; }
            // 試合後半は受けより打ち合いを選びやすい
            wGuard *= DefenseScale;
            wCounter *= 1f + Urgency;
            float r = Rng.Value() * (wGuard + wEvade + wCounter);
            if (r >= wGuard + wEvade)
            {
                pendingAttackAt = -1f;
                Rt.comboRemaining = 0;
                self.StartAttack(time);
                LastDecision = "反撃";
                return;
            }
            if (r < wEvade)
            {
                var kind = back < B.minBackstepSpace && self.WeightClass != WeightClass.Heavy ? EvadeKind.HopOver : EvadeKind.Backstep;
                self.StartEvade(kind, time);
                LastDecision = kind == EvadeKind.Backstep ? "回避(後退)" : "回避(跳び越え)";
            }
            else
            {
                var o = self.Opponent.Runtime;
                float remain = o.state == FighterState.AttackWindup
                    ? (o.windupDuration - o.stateTime) + o.activeDuration
                    : Mathf.Max(0f, o.activeDuration - o.stateTime);
                EnterGuard(time, remain + 0.15f);
                // 来る攻撃の種類に合わせてガード位置を変える
                Rt.guardPsiTarget = o.attackStyle == AttackStyle.Overhead ? B.guardPsiHigh : B.GuardPsiLow(self.Stats.weightScore);
                LastDecision = o.attackStyle == AttackStyle.Overhead ? "上段ガード" : "下段ガード";
            }
        }

        void EnterGuard(float time, float duration)
        {
            Rt.guardPsiTarget = float.NaN;
            holdGuardUntil = time + Mathf.Max(B.guardMinDwell, duration);
            pendingAttackAt = -1f;
            Rt.comboRemaining = 0;
            Move = MoveIntent.Hold;
            self.SetState(FighterState.Guard);
        }
    }
}
