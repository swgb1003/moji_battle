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
        readonly MatchRandom rangedRng;
        readonly MatchRandom spacingRng;
        float nextDecisionAt;
        float pendingAttackAt = -1f;
        float holdGuardUntil;
        float retreatUntil;
        float blockedTime;
        float stuckTime;
        bool punishing;
        float lastDistance = 999f;
        float guardBlockedUntil;
        float spacingAfterLockUntil, attackAfterLockUntil;
        float rangedSpacingUntil, rangedSpacingReadyAt, rangedSpacingTarget;
        public int RangedSpacingAttempts { get; private set; }

        /// <summary>計測用: 攻撃後に離脱した回数 / 反撃の好機に始めた攻撃の数</summary>
        public int RetreatsAfterAttack { get; private set; }
        public int WindowAttacks { get; private set; }
        public int SequencesFinished { get; private set; }

        public MoveIntent Move { get; private set; } = MoveIntent.Advance;
        /// <summary>前進が武器に阻まれている間は武器を立てて担ぐ（WeaponMotor2D が参照）。</summary>
        public bool CarryWeapon { get; private set; }
        /// <summary>落ちた武器を拾いに行く先の x（FighterMotor2D が武器が落ちている間だけ参照）。</summary>
        public float MoveTargetX { get; private set; } = float.NaN;
        public string LastDecision { get; private set; } = "";

        /// <summary>自操作の選手（null なら AI が操作）。</summary>
        public PlayerController Player { get; private set; }
        public bool IsPlayer => Player != null;
        /// <summary>練習モードのサンドバッグ: 何もしない（移動・攻撃・ガードをしない）。</summary>
        public bool Frozen;
        /// <summary>COM の強さ（自操作の相手）: 判断の間隔・受け流し・技（蹴り・フェイント・投げなど）の確率の倍率。観戦では 1。</summary>
        public float DecisionScale = 1f, ParryScale = 1f, TechniqueScale = 1f;
        /// <summary>強さを当てる前の値（PlayControl.ApplyDifficulty が使う）。</summary>
        public DifficultyBaseline Baseline;

        public PlayerController AttachPlayer()
        {
            Player = new PlayerController(self);
            return Player;
        }

        /// <summary>自操作の移動の意図（AI の判断を通さない）。</summary>
        public void Steer(MoveIntent move)
        {
            Move = move;
            CarryWeapon = false;
            MoveTargetX = float.NaN;
        }

        public FighterBrain(Fighter self, MatchRandom rng)
        {
            this.self = self;
            Rng = rng;
            rangedRng = new MatchRandom(self.Context.MatchSeed ^ (self.Id + 1) * 104729 ^ 0x52414E47);
            spacingRng = new MatchRandom(self.Context.MatchSeed ^ (self.Id + 1) * 130363 ^ 0x53504143);
        }

        CombatBalance B => self.Balance;
        bool HeavyDuel => self.HeavyTempoEnabled && self.Opponent.WeightClass == WeightClass.Heavy;
        bool HeavyPressure => HeavyDuel && self.Context.SimTime - self.Context.LastDamageTime > B.heavyDuelPressureSeconds;
        float StalemateSeconds => HeavyDuel
            ? Mathf.Min(B.stalemateSeconds, B.heavyDuelPressureSeconds) : B.stalemateSeconds;

        /// <summary>試合後半の攻めの強さ 0〜1（残り時間が少ないほど大きい）。</summary>
        float Urgency => self.Context.Overtime ? 1f : Mathf.Clamp01((self.Context.MatchClock - B.lateGameStart) / Mathf.Max(0.01f, B.lateGameRamp));
        /// <summary>まだ誰もダメージを受けていない試合は、後半ほど受け・回避をやめて打ち合う（引き分け防止）。</summary>
        bool NoDamageYet => self.Runtime.hp >= self.Runtime.maxHp && self.Opponent.Runtime.hp >= self.Opponent.Runtime.maxHp;
        float DefenseScale => (1f - (NoDamageYet ? 1f : B.lateDefenseReduction) * Urgency)
            * (HeavyPressure ? B.heavyDuelDefenseScale : 1f);

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
            float mine = self.WindupTime;
            return mine > oppRemaining + 0.05f && Rng.Chance(self.Tendency.reactionChance * DefenseScale);
        }

        /// <summary>
        /// これより近いと武器が地面・相手の字形・体に挟まって振れない。軽量・中量級は射程の一定割合まで下がって打つ。
        /// </summary>
        float MinStrikeDistance => self.Mods.customized ? CustomSpacing(B.minAttackDistance)
            : self.WeightClass == WeightClass.Heavy
            ? B.minAttackDistance
            : Mathf.Max(B.minAttackDistance, self.AttackRange * B.preferredSpacingFraction);
        float TooCloseDistance => self.Mods.customized ? CustomSpacing(B.tooCloseDistance)
            : self.WeightClass == WeightClass.Heavy
            ? B.tooCloseDistance
            : Mathf.Max(B.tooCloseDistance, self.AttackRange * B.preferredSpacingFraction);

        /// <summary>
        /// カスタマイズでは射程が字形の既定より大きく変わる（S・中央持ちは射程 1 未満）ため、
        /// 下がる距離も射程に比例させる（固定の最小距離のままだと短い武器は打てる距離が無くなる）。
        /// </summary>
        float CustomSpacing(float legacy) =>
            Mathf.Clamp(self.AttackRange * B.preferredSpacingFraction, CustomMinSpacing, Mathf.Max(CustomMinSpacing, legacy));

        /// <summary>体どうしが触れる距離（胴の幅）より少し外。</summary>
        const float CustomMinSpacing = 0.45f;
        FighterRuntime Rt => self.Runtime;
        float GuardMinDwell => B.guardMinDwell * (self.Mods.customized && self.Mods.grip == GripType.Horizontal ? B.horizontalGuardDwellScale : 1f);

        public void Idle()
        {
            Player?.Drain();
            Move = MoveIntent.Hold;
            pendingAttackAt = -1f;
            if (Rt.state == FighterState.Guard) self.SetState(FighterState.Approach);
        }

        public void Tick(float time)
        {
            if (Player != null) { Player.Tick(time); return; }
            if (Frozen) { Idle(); return; }
            self.Style?.Observe(time);
            if (time < nextDecisionAt) return;
            nextDecisionAt = time + B.aiInterval * (B.combatTempo ? B.tempoAiScale : 1f) * DecisionScale;
            Decide(time);
        }

        public void OnAttackSequenceFinished(float time)
        {
            pendingAttackAt = -1f;
            if (Player != null) return;
            if (B.combatTempo) nextDecisionAt = time;
            SequencesFinished++;
            if (AttackTechniques.IsRanged(Rt.attackStyle))
            {
                rangedSpacingUntil = 0f;
                rangedSpacingReadyAt = time + B.rangedSpacingCooldown;
            }
            else if (TryBeginRangedSpacing(time, B.rangedSpacingChance))
            {
                RetreatsAfterAttack++;
                return;
            }
            // 戦闘スタイルが離脱時間を決める（ヒット＆アウェイは原則離脱、猛攻は離脱しない）
            float? styleRetreat = self.Style?.RetreatSecondsAfterAttack(Rng);
            if (styleRetreat.HasValue ? styleRetreat.Value > 0f : Rng.Chance(self.Tendency.retreatAfterAttack))
            {
                float retreat = styleRetreat ?? Rng.Range(0.4f, 0.8f);
                // ヒット＆アウェイの離脱は戦い方そのものなので、戦闘テンポでも縮めない
                retreatUntil = time + retreat * (B.combatTempo && !styleRetreat.HasValue ? B.tempoRetreatScale : 1f);
                RetreatsAfterAttack++;
                // 軽量級は攻撃後にバックステップで離脱しやすい
                if (self.WeightClass == WeightClass.Light && !Rt.weaponDetached && time >= Rt.evadeReadyAt && self.BackSpace > B.minBackstepSpace && Rng.Chance(0.5f))
                {
                    self.StartEvade(EvadeKind.Backstep, time);
                    LastDecision = "離脱(回避)";
                    return;
                }
                LastDecision = "離脱";
            }
        }

        public void OnAttackStarted(float time)
        {
            // Record actual sequence starts, including situational actions.
            if (Rt.state != FighterState.AttackRecovery && self.Style != null && self.Style.AttackWillingnessBonus(time) > 0f)
                WindowAttacks++;
        }

        public void OnWeaponLockBroken(float time)
        {
            if (Player != null) { if (Rt.state == FighterState.Guard) self.SetState(FighterState.Approach); return; }
            pendingAttackAt = -1f;
            holdGuardUntil = 0f;
            blockedTime = stuckTime = 0f;
            spacingAfterLockUntil = time + .4f;
            attackAfterLockUntil = time + 3f;
            guardBlockedUntil = time + 1f;
            CarryWeapon = true;
            if (Rt.state == FighterState.Guard) self.SetState(FighterState.Approach);
            if (Rt.CanBeControlled) Rt.attackReadyAt = Mathf.Min(Rt.attackReadyAt, spacingAfterLockUntil);
            LastDecision = "文字の絡みをほどく";
        }

        void Decide(float time)
        {
            var s = Rt.state;
            if (s == FighterState.Feint) { Move = MoveIntent.Hold; pendingAttackAt = -1f; LastDecision = "フェイント"; return; }
            if (s == FighterState.Bind) { Move = MoveIntent.Hold; pendingAttackAt = -1f; LastDecision = "鍔迫り合い"; return; }
            if (Rt.riposteAt >= 0f) { Move = MoveIntent.Hold; LastDecision = "受け流し→反撃"; return; }
            if (!Rt.CanBeControlled) return;
            if (Rt.weaponDetached) { DecideUnarmed(time); return; }
            // 観戦者が必殺を出した: 技の届く間合いまで詰める（技の開始は BattleIntervention が行う）
            if (self.Context.Interventions?.WantsSpecial(self, time) == true)
            {
                if (s == FighterState.Guard && time >= holdGuardUntil) self.SetState(FighterState.Approach);
                pendingAttackAt = -1f; CarryWeapon = false;
                Move = MoveIntent.Advance;
                LastDecision = "必殺の間合いへ";
                return;
            }
            if (time < spacingAfterLockUntil)
            {
                Move = self.BackSpace > .5f ? MoveIntent.Retreat : MoveIntent.Hold;
                CarryWeapon = true;
                LastDecision = "間合いを取り直す";
                return;
            }
            // 連続ガードの上限（鉄壁）: 上限に達したら一度ガードを解き、しばらくガードしない
            var style = self.Style;
            if (style != null && s == FighterState.Guard && Rt.stateTime >= style.MaxGuardSeconds)
            {
                guardBlockedUntil = time + style.GuardCooldown;
                holdGuardUntil = 0f;
                self.SetState(FighterState.Approach);
                s = FighterState.Approach;
                LastDecision = "ガード解除";
            }
            if (s == FighterState.Guard && Rt.stateTime < GuardMinDwell && !self.Opponent.Runtime.weaponDetached) return;

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

            if (self.Actions.TryWallJump(time)) { pendingAttackAt = -1f; LastDecision = "壁蹴りで脱出"; return; }

            if (self.Context.Ranged != null && self.Context.Ranged.Threatens(self, out var rangedStyle, out float arrival)
                && rangedRng.Chance(tend.reactionChance * DefenseScale))
            {
                pendingAttackAt = -1f;
                if (rangedStyle == AttackStyle.GroundWave && self.IsGrounded && time >= Rt.evadeReadyAt)
                {
                    self.StartEvade(EvadeKind.HopOver, time);
                    LastDecision = "衝撃波を跳び越える";
                }
                else
                {
                    EnterGuard(time, arrival + .3f);
                    Rt.guardPsiTarget = rangedStyle == AttackStyle.GroundWave ? self.GuardPsiLow : B.guardPsi;
                    LastDecision = "飛び道具をガード";
                }
                return;
            }

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
            if (!oppDown && IsThreat(opp, d, out float timeToHit) && (s == FighterState.Guard || Rng.Chance(tend.reactionChance * DefenseScale + CheerBonus(time))))
            {
                if (s == FighterState.Guard)
                {
                    if (opp.Runtime.state == FighterState.Feint)
                    {
                        Rt.guardPsiTarget = GuardPsiFor(opp.Runtime.attackStyle);
                        Rt.guardAgainstSweep = false;
                    }
                    holdGuardUntil = Mathf.Max(holdGuardUntil, time + timeToHit + 0.2f);
                    TryArmParry(time, timeToHit);
                    return;
                }
                React(time, d, timeToHit);
                return;
            }

            if (s == FighterState.Guard)
            {
                // Keep defending a live threat above, but do not wait out an old guard
                // timer once the thrown weapon has passed and its owner is unarmed.
                // 戦闘テンポでは、相手が振り終わりの硬直に入ったら構えを解いて反撃へ移る
                bool punish = B.combatTempo && opp.Runtime.state == FighterState.AttackRecovery;
                if (time < holdGuardUntil && !opp.Runtime.weaponDetached && !punish) return;
                holdGuardUntil = 0f;
                self.SetState(FighterState.Approach);
                s = FighterState.Approach;
            }

            if (s == FighterState.Approach && time < attackAfterLockUntil && oppAttackable && self.IsGrounded)
            {
                CarryWeapon = false;
                if (d <= range * 1.1f && (d >= MinStrikeDistance || self.BackSpace <= B.wallDangerDistance) && time >= Rt.attackReadyAt)
                {
                    pendingAttackAt = -1f;
                    Rt.comboRemaining = Rng.RangeInclusive(tend.comboMin, tend.comboMax) - 1;
                    self.StartAttack(time);
                    LastDecision = "絡みをほどいて攻撃";
                    return;
                }
                Move = d < MinStrikeDistance && self.BackSpace > B.wallDangerDistance ? MoveIntent.Retreat : MoveIntent.Advance;
                LastDecision = "攻め直す";
                return;
            }

            // Defend first, and keep pressure on an unarmed opponent. Otherwise choose a
            // ranged opening before kicks, feints and the next close-range punish cycle.
            if (B.rangedSpacing && !opp.Runtime.weaponDetached)
            {
                if (TryRanged(time, s, d, oppAttackable)) return;
                if (TryRangedSpacing(time, s, d, oppAttackable)) return;
            }

            if (!opp.Runtime.weaponDetached)
            {
                if (self.Actions.TryKick(time)) { pendingAttackAt = -1f; LastDecision = "前蹴りで間合い確保"; return; }
                if (self.Actions.TryFeint(time)) { pendingAttackAt = -1f; LastDecision = "フェイント"; return; }
            }

            // 1a) 打ち上げで浮かせた相手には、落ちてくるところへ振り下ろしで追撃する
            var oRt = opp.Runtime;
            if (s == FighterState.Approach && time >= Rt.attackReadyAt && !opp.IsGrounded && oRt.launchTracking
                && time - oRt.launchedAt <= B.juggleWindow && d <= self.AttackRange * B.juggleRangeScale && oRt.state != FighterState.KO)
            {
                pendingAttackAt = -1f;
                Rt.comboRemaining = 0;
                self.StartAttack(time, AttackStyle.Overhead);
                LastDecision = "追撃(空中)";
                return;
            }

            // 1a') 武器を投げて素手の相手は好機: 様子見・離脱・ガードをせず詰めて連撃する
            if (s == FighterState.Approach && oRt.weaponDetached && oppAttackable)
            {
                pendingAttackAt = -1f;
                bool pressureBlocked = blockedTime >= B.blockedAdvanceSeconds;
                bool pressureLongBlocked = blockedTime >= B.blockedAdvanceSeconds + 1.5f;
                bool strikeReach = d <= range * 1.05f || (pressureBlocked && d <= range * 1.5f) || pressureLongBlocked;
                bool pressureTooClose = d < CustomMinSpacing;
                bool canMakeSpace = self.BackSpace > B.wallDangerDistance;
                CarryWeapon = pressureBlocked && d > range * 1.2f;
                if (strikeReach && (!pressureTooClose || !canMakeSpace) && self.IsGrounded && time >= Rt.attackReadyAt)
                {
                    blockedTime = 0f;
                    CarryWeapon = false;
                    Rt.comboRemaining = Rng.RangeInclusive(tend.comboMin, tend.comboMax) - 1;
                    self.StartAttack(time);
                    LastDecision = "素手を攻める";
                    return;
                }
                Move = pressureTooClose && canMakeSpace ? MoveIntent.Retreat
                    : d <= range * 1.05f ? MoveIntent.Hold : MoveIntent.Advance;
                LastDecision = Move == MoveIntent.Retreat ? "素手への間合い取り"
                    : Move == MoveIntent.Hold ? "素手への攻撃待ち" : "素手へ詰める";
                return;
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
                    // よろけた相手は打ち上げて叩き落とす連携を狙う（溜めを短くして間に合わせる）
                    if (ort.state == FighterState.Stagger && d >= MinStrikeDistance && Rng.Chance(B.comboLaunchChance))
                    {
                        Rt.comboRemaining = 0;
                        self.StartAttack(time, AttackStyle.Launch);
                        Rt.windupDuration *= B.comboLaunchWindup;
                        LastDecision = "連携(打ち上げ)";
                        return;
                    }
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

            // 1c) 中距離の奥の手: 膠着しているか相手に隙があれば、まれに武器を投げる
            if (TryThrow(time, s, d, range)) return;

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

            bool stalemate = time - self.Context.LastDamageTime > StalemateSeconds;
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
                float styleBonus = style != null ? style.AttackWillingnessBonus(time) : 0f;
                float pressure = style != null ? style.PressureScale : 1f;
                float willingness = tend.attackWillingness + ((stalemate ? 0.2f : 0f) + B.lateAttackBonus * Urgency) * pressure + styleBonus;
                if (HeavyPressure) willingness = Mathf.Max(willingness, B.heavyDuelMinWillingness);
                if (Rng.Chance(willingness))
                {
                    Rt.comboRemaining = Rng.RangeInclusive(tend.comboMin, tend.comboMax) - 1;
                    bool now = style != null && style.StrikeImmediately(time);
                    pendingAttackAt = now ? time : time + Rng.Range(0f, 2f * (B.combatTempo ? B.tempoTimingJitter : B.attackTimingJitter));
                    Move = MoveIntent.Hold;
                    LastDecision = "攻撃予約";
                    return;
                }
                // 重量級は攻撃しない時、構えて待つことが多い
                if (time >= guardBlockedUntil && Rng.Chance(tend.proactiveGuard * DefenseScale)) { EnterGuard(time, Rng.Range(0.4f, 0.8f)); LastDecision = "構え"; return; }
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
                        else if (!oppDown && time >= guardBlockedUntil && Rng.Chance(tend.guardBias * DefenseScale)) { EnterGuard(time, Rng.Range(0.5f, 0.9f)); LastDecision = "ガード"; }
                        else { Move = MoveIntent.Hold; LastDecision = "待機"; }
                        return;
                }
            }

            // 4b) 少し離れた間合いから跳び斬りで飛び込む（軽量・中量級）
            if (!B.rangedSpacing && TryRanged(time, s, d, oppAttackable)) return;
            if (TryDashIn(time, s, d, range, oppAttackable)) return;
            if (TryLeapIn(time, s, d, range, oppAttackable)) return;

            // 5) 接近 / 待機
            if (inRange) { Move = MoveIntent.Hold; LastDecision = "間合い"; return; }
            // 戦闘スタイル（鉄壁）: 相手の間合いの手前では字形を盾に構えて待つ
            // （戦闘テンポでは、相手も構えて待っている時は両者ガードのにらみ合いにしない）
            bool mutualGuard = B.combatTempo && opp.Runtime.state == FighterState.Guard;
            if (style != null && !stalemate && !mutualGuard && time >= guardBlockedUntil && !oppDown && style.PreferGuard(time, d))
            {
                EnterGuard(time, Rng.Range(0.8f, 1.6f));
                LastDecision = "盾構え";
                return;
            }
            // 戦闘スタイル（カウンター）: 自分からは詰めず、相手の射程の少し外で攻撃を待つ
            // （戦闘テンポでは、相手も同じく待つつもりなら待ち合いにしない）
            bool mutualWait = B.combatTempo && opp.Style != null && opp.Runtime.state == FighterState.Approach && opp.Style.PreferWait(time, d);
            if (style != null && !stalemate && !mutualWait && style.PreferWait(time, d))
            {
                Move = MoveIntent.Hold;
                LastDecision = "待ち(カウンター)";
                return;
            }
            // 相手が溜めている間は射程の外で待ち、振り終わり（硬直）を狙う
            if ((style == null || style.WaitsOutOpponentWindup) && opp.Runtime.state == FighterState.AttackWindup && d > opp.AttackRange * 0.95f && Rng.Chance(tend.reactionChance))
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

        bool RangedDistanceBand(out float min, out float max)
        {
            bool wave = AttackTechniques.CanGroundWave(self);
            min = Mathf.Max(self.AttackRange * 1.15f, 3f);
            max = (wave ? B.groundWaveRange : B.strokeShotRange) - .4f;
            return wave || AttackTechniques.CanStrokeShot(self);
        }

        float RangedSpacingStyleScale => !self.Mods.customized ? 1f
            : self.Mods.style == BattleStyle.Aggressive ? .55f
            : self.Mods.style == BattleStyle.HitAndAway ? 1.25f : 1f;

        bool TryBeginRangedSpacing(float time, float chance)
        {
            if (!B.rangedSpacing || !B.rangedAttacks || B.rangedChance <= 0f || self.Context.MatchOver || !Rt.CanBeControlled || !self.IsGrounded
                || Rt.weaponDetached || self.Opponent.Runtime.weaponDetached || self.Opponent.Runtime.IsDown
                || time < rangedSpacingReadyAt || time < Rt.rangedReadyAt - .5f
                || self.BackSpace < B.wallDangerDistance + .5f || Urgency >= .8f
                || time - self.Context.LastDamageTime > B.stalemateSeconds
                || !RangedDistanceBand(out float min, out float max)) return false;
            float safeMin = Mathf.Max(min, self.Opponent.AttackRange * 1.05f + .35f);
            if (safeMin > max - .3f || self.DistanceToOpponent >= safeMin + B.rangedSpacingMargin) return false;
            // Keep genuine counter opportunities instead of retreating from a punishable swing.
            if (self.Mods.customized && self.Mods.style == BattleStyle.Counter && self.Style.AttackWillingnessBonus(time) > 0f) return false;
            if (!spacingRng.Chance(chance * RangedSpacingStyleScale)) return false;
            rangedSpacingTarget = Mathf.Min(safeMin + B.rangedSpacingMargin, max - .2f);
            rangedSpacingUntil = time + B.rangedSpacingSeconds;
            rangedSpacingReadyAt = rangedSpacingUntil + B.rangedSpacingCooldown;
            pendingAttackAt = -1f; Rt.comboRemaining = 0; punishing = false;
            blockedTime = stuckTime = 0f;
            CarryWeapon = true; Move = MoveIntent.Retreat;
            RangedSpacingAttempts++;
            LastDecision = "遠距離攻撃の間合いを作る";
            if (self.WeightClass != WeightClass.Heavy && time >= Rt.evadeReadyAt && self.BackSpace > B.minBackstepSpace
                && spacingRng.Chance(B.rangedSpacingBackstepChance))
                self.StartEvade(EvadeKind.Backstep, time);
            return true;
        }

        bool TryRangedSpacing(float time, FighterState s, float d, bool oppAttackable)
        {
            if (!B.rangedAttacks || s != FighterState.Approach || !self.IsGrounded || !oppAttackable || self.Opponent.Runtime.IsDown) return false;
            if (time >= rangedSpacingUntil)
            {
                // A small chance in neutral also creates spacing after interrupted exchanges.
                if (time < Rt.attackReadyAt || pendingAttackAt >= 0f || !TryBeginRangedSpacing(time, B.rangedSpacingNeutralChance)) return false;
                if (Rt.state == FighterState.Evade) return true;
            }
            if (self.BackSpace < B.wallDangerDistance || Urgency >= .8f
                || time - self.Context.LastDamageTime > B.stalemateSeconds)
            {
                rangedSpacingUntil = 0f; CarryWeapon = false;
                return false;
            }
            pendingAttackAt = -1f;
            CarryWeapon = d < rangedSpacingTarget - .25f;
            Move = d < rangedSpacingTarget - .25f ? MoveIntent.Retreat
                : d > rangedSpacingTarget + .4f ? MoveIntent.Advance : MoveIntent.Hold;
            LastDecision = Move == MoveIntent.Retreat ? "遠距離攻撃の間合いを作る" : "遠距離攻撃を狙う";
            return true;
        }

        /// <summary>遠距離の間合いに入ったら一画飛ばし／衝撃波を狙う。クールダウンは共有。</summary>
        bool TryRanged(float time, FighterState s, float d, bool oppAttackable)
        {
            if (!B.rangedAttacks || s != FighterState.Approach || !self.IsGrounded || !oppAttackable
                || self.Opponent.Runtime.IsDown || Rt.weaponDetached || pendingAttackAt >= 0f
                || time < Rt.attackReadyAt || time < Rt.rangedReadyAt) return false;
            bool wave = AttackTechniques.CanGroundWave(self);
            if (!wave && !AttackTechniques.CanStrokeShot(self)) return false;
            if (d < Mathf.Max(self.AttackRange * 1.15f, 3f)
                || d > (wave ? B.groundWaveRange : B.strokeShotRange) - .4f) return false;
            // If neither side is landing anything, switch back to the normal pressure AI
            // until contact resumes instead of repeating missed or guarded projectiles.
            if (B.rangedSpacing && time - self.Context.LastDamageTime > B.stalemateSeconds) return false;
            float chance = B.rangedChance;
            if (B.rangedSpacing && self.Mods.customized && self.Mods.style == BattleStyle.Counter)
                chance *= self.Style.AttackWillingnessBonus(time) > 0f ? 1.25f : .5f;
            if (B.rangedSpacing && self.Mods.customized && self.Mods.grip == GripType.Horizontal)
                chance *= .75f; // Leave more openings to use the shield stance's stronger guard.
            if (!rangedRng.Chance(chance * TechniqueScale)) return false;
            Rt.comboRemaining = 0;
            if (B.rangedSpacing)
            {
                rangedSpacingUntil = 0f;
                Move = MoveIntent.Hold; CarryWeapon = false;
            }
            self.StartAttack(time, wave ? AttackStyle.GroundWave : AttackStyle.StrokeShot);
            LastDecision = wave ? "叩きつけて衝撃波" : "一画飛ばし";
            return true;
        }

        bool TryThrow(float time, FighterState s, float d, float range)
        {
            if (s != FighterState.Approach || !self.IsGrounded || pendingAttackAt >= 0f) return false;
            if (time < Rt.attackReadyAt || time < Rt.throwReadyAt) return false;
            if (d < range * B.throwMinRangeScale || d > B.throwMaxDistance) return false;
            var ort = self.Opponent.Runtime;
            if (ort.state == FighterState.KO) return false;
            bool stalemate = time - self.Context.LastDamageTime > B.stalemateSeconds;
            bool oppOpen = ort.state == FighterState.AttackRecovery || ort.state == FighterState.Stagger
                           || (ort.IsDown && B.allowAttackOnDowned) || ort.weaponDetached;
            if (!stalemate && !oppOpen) return false;
            float chance = StatCalculator.Lerp01(B.throwChanceLight, B.throwChanceHeavy, self.Stats.weightScore);
            if (!Rng.Chance(chance * TechniqueScale)) return false;
            Rt.comboRemaining = 0;
            self.StartAttack(time, AttackStyle.Throw);
            LastDecision = "投げ";
            return true;
        }

        /// <summary>跳び斬りで間合いを詰める。乱数は条件をすべて満たした時だけ引く。</summary>
        bool TryLeapIn(float time, FighterState s, float d, float range, bool oppAttackable)
        {
            if (s != FighterState.Approach || !self.IsGrounded || self.WeightClass == WeightClass.Heavy || !oppAttackable) return false;
            if (time < Rt.attackReadyAt || pendingAttackAt >= 0f || self.Opponent.Runtime.IsDown) return false;
            if (d < range * 1.3f || d > Mathf.Min(range * B.leapMaxRangeScale, self.LeapTravelRange)) return false;
            if (!Rng.Chance(B.leapApproachChance * TechniqueScale)) return false;
            Rt.comboRemaining = 0;
            self.StartAttack(time, AttackStyle.Leap);
            LastDecision = "跳び斬り";
            return true;
        }

        bool TryDashIn(float time, FighterState s, float d, float range, bool oppAttackable)
        {
            if (s != FighterState.Approach || !AttackTechniques.CanDash(self) || !oppAttackable || self.Opponent.Runtime.IsDown) return false;
            if (time < Rt.attackReadyAt || time < Rt.dashReadyAt || pendingAttackAt >= 0f) return false;
            if (d < Mathf.Max(range * 1.1f, 0.8f) || d > range + B.dashMaxTravel || !Rng.Chance(B.dashApproachChance * TechniqueScale)) return false;
            Rt.comboRemaining = 0;
            self.StartAttack(time, AttackStyle.DashSlash);
            LastDecision = "踏み込み斬り";
            return true;
        }

        void TryArmParry(float time, float timeToHit)
        {
            if (Rt.parryArmed || time < Rt.parryReadyAt || self.Opponent.Runtime.throwLive) return;
            float chance = self.Mods.customized && self.Mods.style == BattleStyle.Counter ? 0.8f
                : self.Mods.customized && self.Mods.style == BattleStyle.Defensive ? 0.6f : B.parryChance;
            if (Rng.Chance(Mathf.Min(.95f, chance * ParryScale) + CheerBonus(time))) self.ArmParry(time, Mathf.Max(0f, timeToHit - 0.05f));
        }

        /// <summary>声援中は予兆への反応・受け流しが冴える。</summary>
        float CheerBonus(float time) => self.Context.Interventions?.ReactionBonus(self, time) ?? 0f;

        /// <summary>相手の攻撃が届きそうか（観戦者のジャストガードの合図）。乱数は使わない。</summary>
        public bool ThreatIncoming(out float timeToHit) => IsThreat(self.Opponent, self.DistanceToOpponent, out timeToHit);

        /// <summary>観戦者のジャストガード: 来る攻撃に合わせてすぐ構える（自分の溜め・硬直・フェイントは取り消す）。</summary>
        public void ForceGuard(float time, float duration)
        {
            var o = self.Opponent.Runtime;
            if (Rt.state != FighterState.Guard && Rt.state != FighterState.Approach) self.SetState(FighterState.Approach);
            EnterGuard(time, duration);
            Rt.guardPsiTarget = GuardPsiFor(o.attackStyle);
            Rt.guardAgainstSweep = AttackTechniques.IsYaw(o.attackStyle);
            LastDecision = "ジャストガード";
        }

        /// <summary>武器を投げた後: 飛んでいる間は見送り、落ちたら拾いに行く。素手では受けられないので攻撃は避けるだけ。</summary>
        void DecideUnarmed(float time)
        {
            var opp = self.Opponent;
            pendingAttackAt = -1f;
            Rt.comboRemaining = 0;
            CarryWeapon = false;
            if (Rt.state == FighterState.Guard) self.SetState(FighterState.Approach);
            if (Rt.throwLive) { Move = MoveIntent.Hold; LastDecision = "投げ"; return; }
            // 素手では受けられないので避けるしかないが、武器を持つ時ほど身軽には動けない
            if (!opp.Runtime.IsDown && IsThreat(opp, self.DistanceToOpponent, out _) && time >= Rt.evadeReadyAt && Rng.Chance(self.Tendency.reactionChance * B.unarmedEvadeFactor))
            {
                var kind = self.BackSpace < B.minBackstepSpace && self.WeightClass != WeightClass.Heavy ? EvadeKind.HopOver : EvadeKind.Backstep;
                self.StartEvade(kind, time);
                LastDecision = "回避(素手)";
                return;
            }
            MoveTargetX = self.WeaponBody.worldCenterOfMass.x;
            Move = MoveIntent.Advance;
            LastDecision = "武器を拾う";
        }

        bool IsThreat(Fighter opp, float d, out float timeToHit)
        {
            timeToHit = 99f;
            var thrown = opp.Runtime;
            if (thrown.throwLive && !thrown.attackHadContact)
            {
                // 飛んでくる武器: こちらへ向かっていて、届くまでの時間が反応の窓に入っていれば脅威
                Vector2 wp = opp.WeaponBody.worldCenterOfMass, wv = opp.WeaponBody.linearVelocity;
                float dx = self.X - wp.x;
                if (wv.x * dx <= 0f || Mathf.Abs(dx) > 5f) return false;
                timeToHit = Mathf.Abs(dx) / Mathf.Max(1f, Mathf.Abs(wv.x));
                return timeToHit <= B.threatWindow;
            }
            // A detached weapon cannot threaten a melee swing. Actual kicks remain threats.
            if (thrown.weaponDetached && !AttackTechniques.IsKick(thrown.attackStyle)) return false;
            var os = opp.Runtime.state;
            if (AttackTechniques.IsRanged(opp.Runtime.attackStyle)) return false; // Incoming projectiles are assessed independently of their owner's current state.
            if (os == FighterState.Feint)
            {
                timeToHit = Mathf.Max(0f, B.feintDuration - opp.Runtime.stateTime);
                return d <= opp.ThreatRange + .4f;
            }
            if (os == FighterState.AttackWindup && opp.Runtime.attackStyle == AttackStyle.DashSlash)
            {
                timeToHit = Mathf.Max(0f, opp.Runtime.windupDuration - opp.Runtime.stateTime) + B.dashSlashDelay;
                return timeToHit <= B.threatWindow && d <= opp.AttackRange + B.dashMaxTravel;
            }
            // 跳び斬りは遠くから跳んでくるので、射程の外でも脅威
            if (os == FighterState.AttackWindup && opp.Runtime.attackStyle == AttackStyle.Leap)
            {
                timeToHit = Mathf.Max(0f, opp.Runtime.windupDuration - opp.Runtime.stateTime);
                return timeToHit <= B.threatWindow && d <= opp.AttackRange * B.leapMaxRangeScale + 0.5f;
            }
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
            if (self.Actions.TryDuck(time)) { pendingAttackAt = -1f; LastDecision = "しゃがみ回避"; return; }
            var tend = self.Tendency;
            bool evadeReady = time >= Rt.evadeReadyAt;
            float back = self.BackSpace;
            float wGuard = time < guardBlockedUntil ? 0f : tend.guardBias
                + (self.Stats.defense >= B.highDefenseGuardThreshold ? B.highDefenseGuardBonus : 0f)
                + (back < B.minBackstepSpace ? 0.2f : 0f);
            float wEvade = evadeReady ? tend.evadeBias * (back < B.minBackstepSpace ? 0.6f : 1f) * DefenseScale : 0f;
            // ガードの構えが間に合わない（武器を大きく回す必要がある）なら回避を優先
            var oa = self.Opponent.Runtime;
            float guardTarget = GuardPsiFor(oa.attackStyle);
            // 技ごとの受け方: 足払いは跳んで避けやすい / 盾当ては受けると崩されるので下がりやすい / 回転斬りは下がって外す
            if (oa.attackStyle == AttackStyle.LowSweep) { wEvade *= 1.6f; wGuard *= 0.6f; }
            else if (oa.attackStyle == AttackStyle.Bash) { wEvade *= 1.4f; wGuard *= 0.5f; }
            else if (oa.attackStyle == AttackStyle.Spin || oa.attackStyle == AttackStyle.Whirl) { wEvade *= 1.3f; }
            float raiseTime = Mathf.Abs(guardTarget - self.WeaponMotor.CurrentPsi) / Mathf.Lerp(700f, 250f, self.Stats.weightScore / 100f);
            if (raiseTime > timeToHit) wGuard *= 0.25f;
            // 距離が射程端に近いほど後退で外しやすい
            if (evadeReady && d > self.Opponent.AttackRange * 0.75f) wEvade += 0.15f;
            // 重量級は相手の連撃を受けながら自分の一撃を始める（反撃）。軽い打撃ではよろけない。
            float wCounter = time >= Rt.attackReadyAt && d <= self.AttackRange * B.heavyRangeSlack ? tend.counterBias : 0f;
            // 膠着中は受けに回らず打ち合いを選びやすくする
            if (time - self.Context.LastDamageTime > StalemateSeconds) { wGuard *= 0.4f; wCounter *= 1.5f; }
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
                // 足払いは跳んで避ける（重量級は跳べない）
                if (oa.attackStyle == AttackStyle.LowSweep && self.WeightClass != WeightClass.Heavy) kind = EvadeKind.HopOver;
                self.StartEvade(kind, time);
                LastDecision = kind == EvadeKind.Backstep ? "回避(後退)" : "回避(跳び越え)";
            }
            else
            {
                var o = self.Opponent.Runtime;
                float remain = o.throwLive ? timeToHit + 0.3f
                    : o.state == FighterState.AttackWindup || o.state == FighterState.Feint
                    ? (o.windupDuration - o.stateTime) + o.activeDuration
                    : Mathf.Max(0f, o.activeDuration - o.stateTime);
                EnterGuard(time, remain + 0.15f);
                // 来る攻撃の種類に合わせてガード位置を変える
                Rt.guardPsiTarget = GuardPsiFor(o.attackStyle);
                // 奥を回り込む技（横薙ぎ・足払い・回転斬り）は、それに反応して構えたガードでなければ止まらない
                Rt.guardAgainstSweep = AttackTechniques.IsYaw(o.attackStyle);
                TryArmParry(time, timeToHit);
                LastDecision = o.attackStyle == AttackStyle.Overhead ? "上段ガード" : Rt.guardAgainstSweep ? $"{AttackTechniques.Label(o.attackStyle)}をガード" : "下段ガード";
            }
        }

        /// <summary>来る技に合わせたガードの角度。</summary>
        public float GuardPsiFor(AttackStyle s)
        {
            switch (s)
            {
                case AttackStyle.Overhead:
                case AttackStyle.Smash:
                case AttackStyle.Leap: return B.guardPsiHigh;
                case AttackStyle.DashSlash:
                case AttackStyle.Riposte: return B.guardPsiHigh;
                case AttackStyle.Sweep:
                case AttackStyle.Spin:
                case AttackStyle.Whirl:
                case AttackStyle.Bash: return B.guardPsi;
                default: return self.GuardPsiLow; // 斬り上げ・刺す・足払い・打ち上げ
            }
        }

        void EnterGuard(float time, float duration)
        {
            Rt.guardPsiTarget = float.NaN;
            Rt.guardAgainstSweep = false;
            holdGuardUntil = time + Mathf.Max(GuardMinDwell, duration);
            pendingAttackAt = -1f;
            Rt.comboRemaining = 0;
            Move = MoveIntent.Hold;
            self.SetState(FighterState.Guard);
        }
    }
}
