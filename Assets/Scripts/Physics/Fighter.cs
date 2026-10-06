using UnityEngine;

namespace MojiBattle
{
    /// <summary>試合1回分の共有コンテキスト。</summary>
    public sealed class MatchContext
    {
        public CombatBalance Balance;
        public CombatEvents Events;
        public HitResolver Hits;
        public EnvironmentImpactResolver Env;
        public float SimTime;
        public float ArenaHalfWidth;
        public bool MatchOver;
        public bool AiEnabled = true;
        public float LastDamageTime;
        /// <summary>カスタマイズの調整値（カスタマイズ無しの試合でも参照だけはできる）。</summary>
        public CustomizeBalance Customize;
        /// <summary>試合時計（経過秒）。KO 演出中は止まる。</summary>
        public float MatchClock;
        /// <summary>延長戦中（AI は最大限攻める）。</summary>
        public bool Overtime;
        /// <summary>どちらかが最後に攻撃を始めた時刻（膠着の検出用）。</summary>
        public float LastAttackStartTime;
        /// <summary>衝突の復帰（重なっていれば離れてから戻す）。MatchDirector が設定する。</summary>
        public System.Action<Collider2D, Collider2D> RestoreCollision;
        /// <summary>武器どうしの衝突の有無を今の状態に合わせて反映する。MatchDirector が設定する。</summary>
        public System.Action ApplyWeaponGate;
        /// <summary>武器を瞬間的に動かした後、相手に重なった組だけ離れるまで衝突を外す。MatchDirector が設定する。</summary>
        public System.Action<Fighter> SeparateWeaponOverlaps;
    }

    /// <summary>字形から作った武器の幾何情報（握り原点・右向き基準のワールド単位）。</summary>
    public sealed class WeaponGeometry
    {
        public float scale;          // world units / px
        public Vector2 gripPx;
        public Vector2 comLocal;
        public Rect boundsLocal;
        public float maxSide;
        public float length;         // 握りから最遠の Collider 角まで
        public float alpha0Deg;      // 握り→重心ベクトルの角度
        public int colliderCount;

        public Vector2 PxToLocal(Vector2 px) => (px - gripPx) * scale;

        public float InertiaAtGrip(Rigidbody2D rb) => rb.inertia + rb.mass * comLocal.sqrMagnitude;
    }

    /// <summary>
    /// 棒人間1体のルート。状態遷移と各サブシステム（Brain/Motor/WeaponMotor/Knockdown）の更新順を持つ。
    /// FixedUpdate は持たず、MatchDirector が左→右の固定順で Tick する（コールバック順序に依存しない）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Fighter : MonoBehaviour
    {
        public int Id;
        public int Facing { get; private set; } = 1;
        public FighterLoadout Loadout;
        public FighterStats Stats;
        public WeightClass WeightClass;
        public GlyphDefinitionRuntime Glyph;
        public WeaponGeometry Weapon;
        public Rigidbody2D Body, WeaponBody;
        public HingeJoint2D Hinge;
        public Transform WeaponGeometryRoot;
        public SpriteRenderer WeaponSprite;
        public Collider2D[] BodyColliders, WeaponColliders;
        /// <summary>武器 Collider の基準（右向き・横振りの奥行き 0 の時の 中心x, 中心y, 幅, 高さ）</summary>
        public Vector4[] WeaponColliderBase;
        float appliedYawCos = 1f;
        public Fighter Opponent;
        public MatchContext Context;
        public FighterRuntime Runtime;
        public FighterBrain Brain;
        public FighterMotor2D Motor;
        public WeaponMotor2D WeaponMotor;
        public KnockdownController Knockdown;
        public StickmanView View;
        public float AttackRange, WalkSpeed;
        /// <summary>カスタマイズ補正（無しなら Identity）。</summary>
        public FighterModifiers Mods = FighterModifiers.Identity;
        /// <summary>戦闘スタイルの AI 方針（カスタマイズ無しなら null = 重量クラスの既定の動き）。</summary>
        public BattleStyleStrategy Style;
        ClassTendency tendency;
        public bool IsGrounded { get; private set; }
        public float LastTurnTime = -10f;

        // 物理ステップ直前の運動状態（接触の法線相対速度 vN の算出に使う）
        public Vector2 PreBodyVelocity, PreWeaponVelocity, PreWeaponCom;
        public float PreWeaponAngVelRad;

        readonly ContactPoint2D[] contactBuffer = new ContactPoint2D[24];

        public CombatBalance Balance => Context.Balance;
        public float X => Body.position.x;
        public Vector2 ShoulderLocal(int facing) => new Vector2(Balance.shoulderLocal.x * facing, Balance.shoulderLocal.y);
        /// <summary>手（武器のヒンジの接続点）。肩 + 技による腕の伸び・屈み（右向き基準のずれを向きで反転）。</summary>
        public Vector2 HandLocal(int facing) => ShoulderLocal(facing) + new Vector2(Runtime.handOffset.x * facing, Runtime.handOffset.y);
        public Vector2 GripWorld => WeaponBody.position;
        public Vector2 ChestWorld => Body.GetRelativePoint(new Vector2(0f, 1.15f));
        public Vector2 HeadWorld => Body.GetRelativePoint(new Vector2(0f, 1.62f));
        public int TowardOpponent => Opponent.X >= X ? 1 : -1;
        public float DistanceToOpponent => Mathf.Abs(Opponent.X - X);
        /// <summary>背後（向きと反対側）の壁までの距離。</summary>
        public float BackSpace => Facing > 0 ? X + Context.ArenaHalfWidth : Context.ArenaHalfWidth - X;
        public ClassTendency Tendency => tendency ?? Balance.TendencyFor(WeightClass);
        public void SetTendency(ClassTendency t) => tendency = t;
        /// <summary>待機姿勢 ψ（持ち方で上書き、無ければ重量クラスの既定）。</summary>
        public float ReadyPsi => float.IsNaN(Mods.readyPsi) ? Balance.ReadyPsi(Stats.weightScore) : Mods.readyPsi;
        /// <summary>溜め・振り・硬直の時間（サイズ・持ち方の攻撃速度を反映）。</summary>
        public float WindupTime => StatCalculator.Windup(Stats.weightScore, Balance) / Mods.attackSpeed;
        public float ActiveTime => StatCalculator.Active(Stats.weightScore, Balance) / Mods.attackSpeed;
        public float RecoveryTime => StatCalculator.Recovery(Stats.weightScore, Balance) / Mods.attackSpeed;
        /// <summary>切っ先が地面に届かない最も低い ψ（武器が長いほど高い）。</summary>
        public float GroundPsi => -Mathf.Asin(Mathf.Clamp01((Balance.shoulderLocal.y - 0.15f) / Mathf.Max(0.01f, Weapon.length))) * Mathf.Rad2Deg + 5f;
        /// <summary>
        /// 下段ガードの ψ。カスタマイズで長くした武器は地面に突き立てない高さまで上げる
        /// （地面に押し付けたままトルクを掛け続けると握りが外れかける）。
        /// </summary>
        public float GuardPsiLow => Mods.customized ? Mathf.Max(Balance.GuardPsiLow(Stats.weightScore), GroundPsi) : Balance.GuardPsiLow(Stats.weightScore);
        /// <summary>相手から見た自分の攻撃の脅威距離（射程＋振りの間の踏み込み）。</summary>
        public float ThreatRange =>
            AttackRange + StatCalculator.Lerp01(Balance.stepInSpeedLight, Balance.stepInSpeedHeavy, Stats.weightScore)
            * ActiveTime;

        public void SetState(FighterState s)
        {
            var rt = Runtime;
            if (rt.state == s && s != FighterState.AttackWindup) return;
            var prev = rt.state;
            if (prev == FighterState.AttackActive && rt.weaponPassThrough) SetWeaponPassThrough(false);
            rt.prevState = prev;
            rt.state = s;
            rt.stateTime = 0f;
            Context.Events.Raise(new StateChangeEvent { fighter = Id, from = prev, to = s, time = Context.SimTime });
        }

        public void Tick(float time, float dt)
        {
            var rt = Runtime;
            var b = Balance;
            rt.stateTime += dt;
            rt.Decay(dt, b);
            UpdateGrounded();
            TrackLaunch(time);
            TrackLift(time, dt);

            TryChainSmash(time);
            DetectSmashHit();
            switch (rt.state)
            {
                case FighterState.AttackWindup:
                    if (rt.stateTime >= rt.windupDuration && WindupReady())
                    {
                        SetState(FighterState.AttackActive);
                        if (rt.attackStyle == AttackStyle.Thrust) Motor.ApplyLunge();
                        else if (rt.attackStyle == AttackStyle.Bash) Motor.ApplyDash(b.bashDashSpeed);
                        else if (rt.attackStyle != AttackStyle.Spin) Motor.ApplyStepIn();
                    }
                    break;
                case FighterState.AttackActive:
                    if (rt.attackStyle == AttackStyle.Throw)
                    {
                        if (rt.stateTime >= rt.activeDuration * b.throwReleaseFraction) ReleaseThrow(time);
                    }
                    else if (rt.stateTime >= rt.activeDuration && SwingFinished()) EndActive(time);
                    break;
                case FighterState.AttackRecovery:
                    if (rt.stateTime >= rt.recoveryDuration && RecoveredPose()) EndRecovery(time);
                    break;
                case FighterState.Evade:
                    if ((rt.stateTime >= b.evadeMinTime && IsGrounded) || rt.stateTime >= b.evadeMaxTime)
                    {
                        rt.evadeEndedAt = time;
                        SetState(FighterState.Approach);
                    }
                    break;
                case FighterState.Stagger:
                    if (rt.stateTime >= rt.stateDurationOverride && (IsGrounded || rt.stateTime >= rt.stateDurationOverride * 3f)) SetState(FighterState.Approach);
                    break;
                case FighterState.Knockdown:
                case FighterState.Recover:
                case FighterState.KO:
                    Knockdown.Tick(time, dt);
                    break;
            }

            UpdateThrownWeapon(time);
            UpdateSweep(dt);
            UpdateHand(dt);
            ClampSpeeds();
            if (Context.MatchOver || !Context.AiEnabled) Brain.Idle();
            else Brain.Tick(time);
            UpdateFacing(time);
            Motor.Tick(dt);
            WeaponMotor.Tick(dt);
        }

        /// <summary>
        /// カスタマイズした武器は、振りかぶり・振り抜きを時間ではなく武器の実際の角度で終える（最大で予定の数倍まで待つ）。
        /// 重い・長い・端を握った武器ほどトルクに対して慣性が大きく、実際に振りが遅くなる。カスタマイズ無しは従来どおり時間で終える。
        /// </summary>
        bool WindupReady()
        {
            if (!Mods.customized) return true;
            var rt = Runtime;
            var cb = Context.Customize;
            // 押さえ込まれて回らないなら待たない（押し続けると握りが外れかける）
            return Mathf.Abs(Mathf.DeltaAngle(WeaponMotor.CurrentPsi, rt.swingFromPsi)) <= cb.windupReadyToleranceDeg
                   || WeaponMotor.Stalled || rt.stateTime >= rt.windupDuration * cb.maxPhaseStretch;
        }

        bool SwingFinished()
        {
            if (!Mods.customized) return true;
            var rt = Runtime;
            var cb = Context.Customize;
            if (rt.attackHadContact || rt.attackStyle == AttackStyle.Thrust || rt.attackStyle == AttackStyle.Sweep || WeaponMotor.Stalled || rt.stateTime >= rt.activeDuration * cb.maxPhaseStretch) return true;
            float span = rt.swingToPsi - rt.swingFromPsi;
            if (Mathf.Abs(span) < 1f) return true;
            float progress = (WeaponMotor.CurrentPsi - rt.swingFromPsi) / span;
            return progress >= cb.swingFinishProgress;
        }

        /// <summary>カスタマイズした武器は、構えへ戻るまで（最大で予定の数倍）硬直が続く。長い・重い武器ほど戻りが遅い。</summary>
        bool RecoveredPose()
        {
            if (!Mods.customized || Runtime.weaponDetached) return true;
            var rt = Runtime;
            var cb = Context.Customize;
            return Mathf.Abs(Mathf.DeltaAngle(WeaponMotor.CurrentPsi, ReadyPsi)) <= cb.recoverReadyToleranceDeg
                   || WeaponMotor.Stalled || rt.stateTime >= rt.recoveryDuration * cb.maxPhaseStretch;
        }

        public void StartAttack(float time) => StartAttack(time, ForcedStyleForTests);

        /// <summary>確認用（撮影・テスト）: 指定すると攻撃は常にこの技になる。通常は null。</summary>
        public static AttackStyle? ForcedStyleForTests;

        /// <summary>攻撃を始める。forced を指定すると技を選ばずにその振りで打つ（浮かせた相手への追撃など）。</summary>
        public void StartAttack(float time, AttackStyle? forced)
        {
            var rt = Runtime;
            var b = Balance;
            if (rt.weaponDetached) return;
            int m = Stats.weightScore;
            rt.launchConnectedAt = -1f;
            rt.currentAttackId = rt.NextAttackId(Id);
            rt.attackHadContact = false;
            rt.clashedThisAttack = false;
            rt.windupDuration = WindupTime;
            rt.activeDuration = ActiveTime;
            rt.recoveryDuration = RecoveryTime;
            // 逆手: 回避の直後は素早く切り返す
            if (Mods.postEvadeAttackSpeed != 1f && Context.Customize != null && time - rt.evadeEndedAt <= Context.Customize.postEvadeWindow)
                rt.windupDuration /= Mods.postEvadeAttackSpeed;
            Style?.OnAttackStarted(time);

            // 狙いの上下位置（PRNG）。相手の武器が高い位置にあれば下から、低ければ上から狙いやすい。
            // 相手の現在位置を狙うが命中は保証しない。
            bool oppWeaponHigh = Opponent.WeaponBody.worldCenterOfMass.y > Opponent.ChestWorld.y + 0.3f;
            bool heavy = WeightClass == WeightClass.Heavy;
            float pRising = heavy ? (oppWeaponHigh ? b.risingWhenHighHeavy : b.risingWhenLowHeavy)
                                  : (oppWeaponHigh ? b.risingWhenHighLight : b.risingWhenLowLight);
            float pThrust = StatCalculator.Lerp01(b.thrustChanceLight, b.thrustChanceHeavy, m);
            // 扱いにくい（慣性の大きい）武器は突きを繰り出しにくい
            if (Mods.customized) pThrust *= Mathf.Min(1f, Mods.handlingAccel);
            // 近すぎると突きは勢いがつかない（刺さったまま押すだけになる）ので振りを選ぶ
            if (DistanceToOpponent < AttackRange * 0.6f) pThrust = 0f;
            rt.attackStyle = Brain.Rng.Chance(pThrust) ? AttackStyle.Thrust
                : Brain.Rng.Chance(pRising) ? AttackStyle.Rising : AttackStyle.Overhead;
            // 相手がガードを構えていれば、その逆の高さを狙う
            if (Opponent.Runtime.state == FighterState.Guard && Brain.Rng.Chance(0.7f))
            {
                float g = float.IsNaN(Opponent.Runtime.guardPsiTarget) ? b.guardPsi : Opponent.Runtime.guardPsiTarget;
                rt.attackStyle = g < 30f ? AttackStyle.Overhead : (pThrust > 0.3f ? AttackStyle.Thrust : AttackStyle.Rising);
            }
            // 状況に合う技へ置き換える（横薙ぎ・足払い・盾当て・打ち上げ・回転斬り）
            var special = AttackTechniques.ChooseSpecial(this, Brain.Rng.Value());
            if (special.HasValue) rt.attackStyle = special.Value;
            if (forced.HasValue) rt.attackStyle = forced.Value;
            if (rt.attackStyle == AttackStyle.Throw || rt.attackStyle == AttackStyle.Smash) rt.comboRemaining = 0;
            var tech = AttackTechniques.Tuning(rt.attackStyle, b);
            if (tech != null)
            {
                rt.windupDuration *= tech.windup;
                rt.activeDuration *= tech.active;
                rt.recoveryDuration *= tech.recovery;
            }
            if (AttackTechniques.IsYaw(rt.attackStyle) || rt.attackStyle == AttackStyle.Bash)
            {
                // 横薙ぎ・足払い・回転斬り: 字形を立てたまま（ψ = 字形の基準角）、奥行き方向に回す
                // 盾当て: 字形を前に立てて構えたまま体当たり
                rt.swingFromPsi = rt.swingToPsi = rt.attackStyle == AttackStyle.Bash ? b.guardPsi : Weapon.alpha0Deg;
                rt.metrics.attacksStarted++;
                Context.LastAttackStartTime = time;
                SetState(FighterState.AttackWindup);
                return;
            }
            Vector2 target;
            if (rt.attackStyle == AttackStyle.Thrust)
            {
                // 突きは相手の武器の下をくぐる胴の下部を主に狙う
                float r = Brain.Rng.Value();
                target = r < Tendency.aimHeadChance * 0.3f ? Opponent.HeadWorld
                    : Opponent.Body.GetRelativePoint(new Vector2(0f, r < 0.85f ? 0.95f : 0.5f));
            }
            else if (rt.attackStyle == AttackStyle.Overhead)
                target = Brain.Rng.Chance(Tendency.aimHeadChance) ? Opponent.HeadWorld : Opponent.ChestWorld;
            else if (rt.attackStyle == AttackStyle.Launch || rt.attackStyle == AttackStyle.Throw || rt.attackStyle == AttackStyle.Smash)
                target = Opponent.ChestWorld;
            else
                target = Brain.Rng.Chance(0.5f) ? Opponent.Body.GetRelativePoint(new Vector2(0f, 0.45f)) : Opponent.ChestWorld;
            Vector2 grip = Body.GetRelativePoint(ShoulderLocal(Facing));
            float aim = Mathf.Atan2(target.y - grip.y, Mathf.Max(0.3f, Mathf.Abs(target.x - grip.x))) * Mathf.Rad2Deg;
            float arc = StatCalculator.Lerp01(b.swingArcLight, b.swingArcHeavy, m);
            if (rt.attackStyle == AttackStyle.Thrust)
            {
                // 刺す: 武器は狙いへ向けたまま、溜めで手を引き、有効時間に腕を伸ばして踏み込む
                rt.swingFromPsi = aim;
                rt.swingToPsi = aim;
            }
            else if (rt.attackStyle == AttackStyle.Smash)
            {
                // 叩き落とし: 浮いた相手の上まで振りかぶり、地面の手前まで一気に振り下ろす（重さに関係なく素早く）
                rt.swingFromPsi = Mathf.Max(aim + 70f, 110f);
                rt.swingToPsi = Mathf.Max(GroundPsi, aim - 60f);
                rt.smashAimPsi = aim;
                rt.windupDuration = Mathf.Min(rt.windupDuration, b.smashMaxWindup);
                rt.activeDuration = Mathf.Min(rt.activeDuration, b.smashMaxActive);
            }
            else if (rt.attackStyle == AttackStyle.Throw)
            {
                // 投げ: 頭の後ろへ振りかぶり、前へ振り出す途中で手を離す
                rt.swingFromPsi = Mathf.Max(aim + 100f, 125f);
                rt.swingToPsi = aim + 15f;
            }
            else if (rt.attackStyle == AttackStyle.Launch)
            {
                // 打ち上げ: 低い位置から頭上まで大きく跳ね上げる（字形の角で地面を突かない高さから）
                rt.swingFromPsi = Mathf.Max(GroundPsi + 15f, b.risingMinPsi + 10f);
                rt.swingToPsi = Mathf.Max(aim + 75f, 85f);
            }
            else if (rt.attackStyle == AttackStyle.Overhead)
            {
                rt.swingFromPsi = aim + arc;
                // 大きい字形ほど振り抜きを浅くする（低く振り抜くと手前の地面に当たって届かない）
                rt.swingToPsi = aim - b.strikeOvershoot * Mathf.Lerp(1f, 0.2f, m / 100f);
                // カスタマイズで長くした武器は、振り下ろしの終わりを地面の手前で止める
                if (Mods.customized) rt.swingToPsi = Mathf.Max(rt.swingToPsi, GroundPsi);
            }
            else
            {
                // 武器の長さから、切っ先が地面に刺さらない開始角を求める
                float groundPsi = GroundPsi;
                // 開始角より下は斬り上げでは届かないので、その場合は胴を狙う
                if (aim < groundPsi + 15f)
                {
                    Vector2 chest = Opponent.ChestWorld;
                    aim = Mathf.Max(groundPsi + 15f, Mathf.Atan2(chest.y - grip.y, Mathf.Max(0.3f, Mathf.Abs(chest.x - grip.x))) * Mathf.Rad2Deg);
                }
                rt.swingFromPsi = Mathf.Max(aim - arc * 0.7f, b.risingMinPsi, groundPsi);
                rt.swingToPsi = aim + b.strikeOvershoot * 0.8f;
                // 長い武器は地面で開始角が持ち上がり振り幅が小さくなるので、上へ振り抜いて同じ振り幅を確保する
                if (Mods.customized) rt.swingToPsi = Mathf.Max(rt.swingToPsi, rt.swingFromPsi + arc * 0.7f);
            }
            rt.metrics.attacksStarted++;
            Context.LastAttackStartTime = time;
            SetState(FighterState.AttackWindup);
        }

        /// <summary>
        /// 横振りの奥行き角を進める。溜めで後ろへ回し（0→180°）、振りで前へ（180°→振り抜き）、硬直で戻す。
        /// 字形は縦軸まわりに回るので、表示と Collider の横幅が cos(角度) で伸び縮みする（奥・手前を向く間は細い）。
        /// </summary>
        void UpdateSweep(float dt)
        {
            var rt = Runtime;
            var b = Balance;
            float prev = rt.sweepYaw;
            bool sweeping = rt.attackStyle == AttackStyle.Sweep || rt.attackStyle == AttackStyle.LowSweep;
            bool spinning = rt.attackStyle == AttackStyle.Spin;
            float yaw;
            switch (rt.state)
            {
                // 回転斬り: 少し引いてから一回転（前→奥→後ろ→手前→前）、振り終わりで正面へ戻す
                case FighterState.AttackWindup when spinning:
                    yaw = Mathf.Lerp(0f, -25f, Mathf.Clamp01(rt.stateTime / Mathf.Max(0.05f, rt.windupDuration)));
                    break;
                case FighterState.AttackActive when spinning:
                {
                    float t = Mathf.Clamp01(rt.stateTime / Mathf.Max(0.02f, rt.activeDuration));
                    yaw = Mathf.Lerp(-25f, 385f, Mathf.Pow(t, 1.3f));
                    break;
                }
                case FighterState.AttackRecovery when spinning:
                    if (prev > 180f) prev -= 360f; // 一回転した分を戻す（角速度に跳ねを出さない）
                    yaw = Mathf.MoveTowards(prev, 0f, 120f * dt);
                    break;
                case FighterState.AttackWindup when sweeping:
                {
                    float t = Mathf.Clamp01(rt.stateTime / Mathf.Max(0.05f, rt.windupDuration));
                    yaw = 180f * t * t * (3f - 2f * t);
                    break;
                }
                case FighterState.AttackActive when sweeping:
                {
                    float t = Mathf.Clamp01(rt.stateTime / Mathf.Max(0.02f, rt.activeDuration));
                    yaw = Mathf.Lerp(180f, b.sweepFollowThroughYaw, t * t);
                    break;
                }
                case FighterState.AttackRecovery when sweeping:
                {
                    float t = Mathf.Clamp01(rt.stateTime / Mathf.Max(0.05f, rt.recoveryDuration * 0.6f));
                    yaw = Mathf.Lerp(b.sweepFollowThroughYaw, 0f, t * t * (3f - 2f * t));
                    break;
                }
                default:
                    yaw = Mathf.MoveTowards(prev, 0f, 720f * dt);
                    break;
            }
            rt.sweepYaw = yaw;
            rt.sweepYawRate = (yaw - prev) / Mathf.Max(1e-4f, dt);
            ApplyWeaponYaw(false);
        }

        /// <summary>
        /// 手（ヒンジの接続点）を技に合わせて動かす。刺す: 溜めで引き、振りで伸ばす。足払い: 屈んで膝の高さへ。盾当て: 低く構えて前へ。
        /// 接続点を動かすと武器は関節に引かれて動く（Transform を直接動かさない）。
        /// </summary>
        void UpdateHand(float dt)
        {
            var rt = Runtime;
            var b = Balance;
            if (rt.weaponDetached) { rt.handOffset = Vector2.zero; return; }
            Vector2 target = Vector2.zero;
            float speed = 3f;
            bool attacking = rt.state == FighterState.AttackWindup || rt.state == FighterState.AttackActive;
            if (attacking && !rt.IsDown)
            {
                switch (rt.attackStyle)
                {
                    case AttackStyle.Thrust:
                        bool extend = rt.state == FighterState.AttackActive;
                        target = new Vector2(extend ? b.stabReach : -b.stabPullBack, 0f);
                        speed = extend ? b.stabExtendSpeed : 2.5f;
                        break;
                    case AttackStyle.LowSweep:
                        // 字形を立てた時に握りより下へ出る分だけ、地面に当たらないよう下げ幅を抑える
                        float below = Mathf.Max(0f, -Weapon.boundsLocal.yMin);
                        float drop = Mathf.Clamp(b.shoulderLocal.y - 0.12f - below, 0f, b.lowSweepHandDrop);
                        target = new Vector2(0.05f, -drop);
                        speed = 4f;
                        break;
                    case AttackStyle.Bash:
                        target = new Vector2(rt.state == FighterState.AttackActive ? 0.15f : -0.1f, -0.15f);
                        break;
                }
            }
            if (rt.IsDown) { rt.handOffset = Vector2.zero; speed = 0f; }
            // 武器が相手や地面に止められて手だけ先へ行きそうなら、伸ばすのをやめて手を武器の位置へ寄せる（関節を引き伸ばさない）
            Vector2 weaponLocal = Body.GetPoint(WeaponBody.position);
            Vector2 weaponOffset = new Vector2((weaponLocal.x - ShoulderLocal(Facing).x) * Facing, weaponLocal.y - ShoulderLocal(Facing).y);
            if ((weaponOffset - rt.handOffset).magnitude > HandStretchLimit) { target = weaponOffset; speed = Mathf.Max(speed, 12f); }
            var next = Vector2.MoveTowards(rt.handOffset, target, speed * dt);
            if ((next - rt.handOffset).sqrMagnitude < 1e-8f && speed > 0f) return;
            rt.handOffset = next;
            Hinge.connectedAnchor = HandLocal(Facing);
        }

        const float HandStretchLimit = 0.1f;

        /// <summary>武器 Collider を向き（左右）と横振りの奥行き角に合わせる。</summary>
        public void ApplyWeaponYaw(bool force)
        {
            if (WeaponColliderBase == null) return;
            // 手を離れた武器は投げた時の向きのまま（拾った時に今の向きへ合わせ直す）
            if (Runtime.weaponDetached && !force) return;
            float c = Mathf.Cos(Runtime.sweepYaw * Mathf.Deg2Rad);
            if (!force && Mathf.Abs(c - appliedYawCos) < 1e-3f) return;
            appliedYawCos = c;
            for (int i = 0; i < WeaponColliders.Length; i++)
            {
                var box = (BoxCollider2D)WeaponColliders[i];
                var basis = WeaponColliderBase[i];
                box.offset = new Vector2(basis.x * Facing * c, basis.y);
                box.size = new Vector2(Mathf.Max(0.02f, basis.z * Mathf.Abs(c)), basis.w);
            }
        }

        /// <summary>このスイングの間だけ相手の武器との衝突を外す（叩き落として振り抜く / ガード崩しの追撃）。</summary>
        public void SetWeaponPassThrough(bool on)
        {
            if (Runtime.weaponPassThrough == on) return;
            Runtime.weaponPassThrough = on;
            // 実際の衝突の切り替えは MatchDirector の武器接触ゲート（物理ステップ直前に反映）
            Context.ApplyWeaponGate?.Invoke();
        }

        /// <summary>
        /// 連携: 自分の打ち上げで相手が浮いたら、振りの残りと硬直を打ち切って叩き落としへつなぐ。
        /// 相手が既に着地した・届かない・KO なら何もしない（通常の硬直のまま）。
        /// </summary>
        void TryChainSmash(float time)
        {
            var rt = Runtime;
            if (rt.launchConnectedAt < 0f || rt.attackStyle != AttackStyle.Launch) return;
            if (rt.state != FighterState.AttackActive && rt.state != FighterState.AttackRecovery) { rt.launchConnectedAt = -1f; return; }
            // 待っている間に硬直が終わってしまわないよう、硬直の残りを延ばす（つながなければ通常どおり戻る）
            if (rt.state == FighterState.AttackRecovery) rt.recoveryDuration = Mathf.Max(rt.recoveryDuration, rt.stateTime + Time.fixedDeltaTime * 2f);
            var b = Balance;
            float since = time - rt.launchConnectedAt;
            if (since < b.smashChainDelay) return;
            // 頂点付近まで待ってから振る（上昇中に振ると下をくぐってしまう）
            if (Opponent.Body.linearVelocity.y > b.smashApexSpeed && since < b.smashChainTimeout) return;
            rt.launchConnectedAt = -1f;
            var ort = Opponent.Runtime;
            float reach = (b.shoulderLocal.x + Weapon.length) * b.smashChainReach;
            if (ort.state == FighterState.KO || Opponent.IsGrounded || DistanceToOpponent > reach || Context.MatchOver) return;
            rt.comboRemaining = 0;
            StartAttack(time, AttackStyle.Smash);
            // 浮いた相手の手前（武器の長さの一定割合）へ詰める・離れる（真下に潜ると叩き落とした相手が自分の上に落ちる）
            float gap = DistanceToOpponent - Weapon.length * b.smashStepFraction;
            float v = Mathf.Clamp(gap / Mathf.Max(0.05f, rt.windupDuration + rt.activeDuration * 0.5f), -b.smashStepSpeed, b.smashStepSpeed);
            AddVelocity(new Vector2(TowardOpponent * v - Body.linearVelocity.x, 0f));
        }

        /// <summary>
        /// 叩き落としの当たり: 武器が狙いの角度まで振り下ろされた時、浮いた相手の胸が武器の届く範囲にあれば、胸への上からの当たりとして報告する。
        /// 浮いた相手は動きが速く字形の当たり判定ではすり抜けやすいため、連携の締めはこの判定で決める（解決・ダメージは通常の当たりと同じ）。
        /// </summary>
        void DetectSmashHit()
        {
            var rt = Runtime;
            if (rt.state != FighterState.AttackActive || rt.attackStyle != AttackStyle.Smash || rt.attackHadContact) return;
            if (Context.Hits.Ledger.IsResolved(rt.currentAttackId, Opponent.Id)) return;
            if (WeaponMotor.CurrentPsi > rt.smashAimPsi + 10f) return;
            if (Opponent.IsGrounded || Opponent.Runtime.state == FighterState.KO) return;
            Vector2 chest = Opponent.ChestWorld;
            if (Vector2.Distance(GripWorld, chest) > Weapon.length + Balance.smashReachSlack) return;
            Context.Hits.Report(new HitContact
            {
                attacker = this, target = Opponent, targetIsWeapon = false, part = BodyPart.Torso,
                point = chest, normal = Vector2.down, relativeVelocity = Vector2.down * Balance.smashHitSpeed,
                myColliderId = WeaponColliders[0].GetInstanceID(), otherColliderId = Opponent.BodyColliders[1].GetInstanceID(),
            });
        }

        /// <summary>
        /// 投げの手放し: ヒンジを外し、相手の胸へ届く放物線の初速と回転を与える。
        /// 以後、武器が飛んでいる間（throwLive）だけ攻撃として当たる。
        /// </summary>
        void ReleaseThrow(float time)
        {
            var rt = Runtime;
            var b = Balance;
            SetWeaponPassThrough(false);
            rt.weaponDetached = true;
            rt.throwLive = true;
            rt.thrownAt = time;
            rt.throwReadyAt = time + b.throwCooldown;
            rt.handOffset = Vector2.zero;
            Hinge.enabled = false;
            int m = Stats.weightScore;
            float speed = StatCalculator.Lerp01(b.throwSpeedLight, b.throwSpeedHeavy, m);
            if (Mods.customized) speed *= Mathf.Sqrt(Mathf.Min(1f, Mods.handlingAccel));
            float g = -Physics2D.gravity.y * WeaponBody.gravityScale;
            WeaponBody.linearVelocity = BallisticVelocity(WeaponBody.worldCenterOfMass, Opponent.ChestWorld, speed, g);
            WeaponBody.angularVelocity = -Facing * StatCalculator.Lerp01(b.throwSpinLight, b.throwSpinHeavy, m);
            SetState(FighterState.AttackRecovery);
        }

        /// <summary>初速 speed で from から to へ届く低い放物線の速度。届かなければ 45° で投げる。</summary>
        public static Vector2 BallisticVelocity(Vector2 from, Vector2 to, float speed, float g)
        {
            float dx = to.x - from.x, dy = to.y - from.y;
            float x = Mathf.Max(0.05f, Mathf.Abs(dx));
            float v2 = speed * speed;
            float disc = v2 * v2 - g * (g * x * x + 2f * dy * v2);
            float angle = disc >= 0f ? Mathf.Atan((v2 - Mathf.Sqrt(disc)) / (g * x)) : Mathf.PI * 0.25f;
            angle = Mathf.Clamp(angle, -15f * Mathf.Deg2Rad, 60f * Mathf.Deg2Rad);
            return new Vector2(Mathf.Cos(angle) * (dx < 0f ? -1f : 1f), Mathf.Sin(angle)) * speed;
        }

        /// <summary>投げた武器: 当たるか止まったら飛行を終え、落ちた武器の近くに立てば拾う（拾えないまま長引けば手元へ戻す）。</summary>
        void UpdateThrownWeapon(float time)
        {
            var rt = Runtime;
            if (!rt.weaponDetached) return;
            var b = Balance;
            if (rt.throwLive)
            {
                bool settled = time - rt.thrownAt > 0.3f && WeaponBody.linearVelocity.magnitude < b.throwSettleSpeed;
                if (!rt.attackHadContact && !settled && time - rt.thrownAt <= b.throwMaxFlight) return;
                rt.throwLive = false;
                if (!rt.attackHadContact) rt.metrics.attacksWhiffed++;
                SetWeaponPassThrough(false);
                Context.ApplyWeaponGate?.Invoke();
                return;
            }
            if (rt.IsDown || rt.state == FighterState.Recover) return;
            Vector2 w = WeaponBody.worldCenterOfMass;
            bool reachable = rt.state == FighterState.Approach && IsGrounded && Mathf.Abs(w.x - X) <= b.throwPickupRadius && w.y < 1.2f;
            if (reachable || time - rt.thrownAt > b.throwRetrieveTimeout) ReattachWeapon();
        }

        /// <summary>武器を手に戻す。投げた時の向きのままの字形・重心・Collider を今の向きへ合わせ直す。</summary>
        void ReattachWeapon()
        {
            var rt = Runtime;
            rt.weaponDetached = false;
            rt.throwLive = false;
            rt.sweepYaw = 0f;
            ApplyWeaponYaw(true);
            WeaponSprite.flipX = Facing < 0;
            WeaponBody.centerOfMass = new Vector2(Weapon.comLocal.x * Facing, Weapon.comLocal.y);
            WeaponMotor.ResetPose();
            Hinge.enabled = true;
            Context.ApplyWeaponGate?.Invoke();
        }

        void EndActive(float time)
        {
            var rt = Runtime;
            var b = Balance;
            SetWeaponPassThrough(false);
            bool whiff = !rt.attackHadContact;
            if (whiff)
            {
                rt.metrics.attacksWhiffed++;
                if (WeightClass == WeightClass.Heavy) rt.recoveryDuration *= b.heavyWhiffRecoveryMultiplier;
                // 重心のずれた重い武器を空振りした反動で持ち主がよろける（物理量から決定、乱数なし）
                float relW = PreWeaponAngVelRad - Body.angularVelocity * Mathf.Deg2Rad;
                float momentum = Mathf.Abs(relW) * Weapon.InertiaAtGrip(WeaponBody);
                if (momentum >= b.heavyWhiffStaggerMomentum)
                {
                    rt.comboRemaining = 0;
                    EnterStagger(0.35f);
                    return;
                }
            }
            SetState(FighterState.AttackRecovery);
        }

        void EndRecovery(float time)
        {
            var rt = Runtime;
            // 連撃を続ける前に相手の溜めを確認し、気づけば中断して対応する（反応率に従う）
            var ort = Opponent.Runtime;
            // 相手の溜めが自分の次の一撃より先に終わる時だけ中断する（遅い溜めには打ち勝てる）
            float myNextHit = WindupTime + 0.1f;
            bool oppCharging = ort.state == FighterState.AttackWindup && ort.windupDuration - ort.stateTime < myNextHit;
            if (rt.comboRemaining > 0 && oppCharging && Brain.Rng.Chance(Tendency.reactionChance)) rt.comboRemaining = 0;
            bool oppAttackable = ort.state != FighterState.KO && (!ort.IsDown || Balance.allowAttackOnDowned);
            // 連撃の継続距離は武器の最遠点基準（AI の射程は控えめなので、それより広く取る）
            float comboReach = Balance.shoulderLocal.x + Weapon.length + 0.3f;
            if (rt.comboRemaining > 0 && DistanceToOpponent <= comboReach && oppAttackable && !Context.MatchOver)
            {
                rt.comboRemaining--;
                StartAttack(time);
                return;
            }
            rt.comboRemaining = 0;
            rt.attackReadyAt = time + (WeightClass == WeightClass.Light ? Balance.attackCooldownLight : Balance.attackCooldownHeavy);
            SetState(FighterState.Approach);
            Brain.OnAttackSequenceFinished(time);
        }

        public void EnterStagger(float duration)
        {
            Runtime.stateDurationOverride = duration;
            Runtime.comboRemaining = 0;
            SetState(FighterState.Stagger);
        }

        public void StartEvade(EvadeKind kind, float time)
        {
            var rt = Runtime;
            var b = Balance;
            if (!IsGrounded) return;
            rt.evadeKind = kind;
            rt.evadeReadyAt = time + b.evadeCooldown;
            rt.metrics.evades++;
            if (kind == EvadeKind.Backstep) rt.metrics.backsteps++; else rt.metrics.hopOvers++;
            SetState(FighterState.Evade);
            Motor.ApplyEvadeImpulse(kind);
            Context.Events.Raise(new EvadeEvent { fighter = Id, kind = kind, time = time });
        }

        void UpdateFacing(float time)
        {
            var s = Runtime.state;
            if (s != FighterState.Approach && s != FighterState.Guard) return;
            if (!IsGrounded || DistanceToOpponent < 0.15f || Runtime.throwLive) return;
            int want = TowardOpponent;
            // 落ちた武器を拾いに行く間はそちらを向く
            if (Runtime.WeaponLoose)
            {
                float dx = WeaponBody.worldCenterOfMass.x - X;
                if (Mathf.Abs(dx) > Balance.throwPickupRadius * 0.5f) want = dx >= 0f ? 1 : -1;
            }
            if (want != Facing && time - LastTurnTime > 0.25f)
            {
                LastTurnTime = time;
                SetFacing(want);
            }
        }

        public void InitFacing(int f) => Facing = f;

        /// <summary>本体と武器に同じ速度変化を与える（本体だけ動かしてヒンジが伸びるのを防ぐ）。</summary>
        public void AddVelocity(Vector2 dv)
        {
            Body.linearVelocity += dv;
            WeaponBody.linearVelocity += dv;
        }

        void ClampSpeeds()
        {
            var b = Balance;
            if (Body.linearVelocity.sqrMagnitude > b.maxBodySpeed * b.maxBodySpeed)
                Body.linearVelocity = Body.linearVelocity.normalized * b.maxBodySpeed;
            if (WeaponBody.linearVelocity.sqrMagnitude > b.maxWeaponSpeed * b.maxWeaponSpeed)
                WeaponBody.linearVelocity = WeaponBody.linearVelocity.normalized * b.maxWeaponSpeed;
        }

        /// <summary>向き反転。武器の字形・重心・ヒンジ位置を左右反転する（姿勢の初期化扱い）。</summary>
        public void SetFacing(int f)
        {
            if (f == Facing) return;
            // 手を離れた武器には触らない（拾った時に向きを合わせる）
            if (Runtime.weaponDetached) { Facing = f; return; }
            float rel = Mathf.DeltaAngle(Body.rotation, WeaponBody.rotation);
            float relW = WeaponBody.angularVelocity - Body.angularVelocity;
            Facing = f;
            // Collider は offset を左右反転（Transform を介さないので補間中の姿勢を物理へ書き戻さない）
            ApplyWeaponYaw(true);
            WeaponSprite.flipX = f < 0;
            WeaponBody.centerOfMass = new Vector2(Weapon.comLocal.x * f, Weapon.comLocal.y);
            Hinge.connectedAnchor = HandLocal(f);
            WeaponBody.position = Body.GetRelativePoint(HandLocal(f));
            WeaponBody.rotation = Body.rotation - rel;
            WeaponBody.angularVelocity = Body.angularVelocity - relW;
            // 反対側へ移した武器が相手にめり込むと、押し出しで相手が弾き飛ばされる
            Context.SeparateWeaponOverlaps?.Invoke(this);
        }

        void UpdateGrounded()
        {
            // 足が地面付近で上下に動いていなければ接地扱い（武器が地面に突っかえて体がわずかに浮いた場合も含む）
            float vy = Mathf.Abs(Body.linearVelocity.y);
            IsGrounded = !Runtime.IsDown && ((Body.position.y < 0.3f && vy < 0.5f) || (Body.position.y < 1.2f && vy < 0.2f));
            if (IsGrounded || Runtime.IsDown) return;
            // 地面以外（相手の字形や体）の上に立っている場合も接地扱い。乗ったまま動けなくなるのを防ぐ
            int n = Body.GetContacts(contactBuffer);
            for (int i = 0; i < n; i++)
            {
                var c = contactBuffer[i];
                bool mine = c.collider != null && c.collider.attachedRigidbody == Body;
                float up = mine ? -c.normal.y : c.normal.y;
                if (Mathf.Abs(c.normal.y) > 0.6f && (up > 0.6f || Mathf.Abs(Body.linearVelocity.y) < 0.3f))
                {
                    IsGrounded = true;
                    return;
                }
            }
        }

        /// <summary>
        /// 相手の武器に持ち上げられているかを判定する（叩きつけダメージ用）。
        /// 相手の武器に触れていて、上向きに動いている／足が高く浮いている時。自分の回避ジャンプ中は除く。
        /// 持ち上げが終わって接地が 0.3 秒続いたら、その持ち上げは終了。
        /// </summary>
        void TrackLift(float time, float dt)
        {
            var rt = Runtime;
            var b = Balance;
            bool touchingOppWeapon = false, pushedUp = false;
            if (rt.state != FighterState.Evade)
            {
                int n = Body.GetContacts(contactBuffer);
                for (int i = 0; i < n; i++)
                {
                    var c = contactBuffer[i];
                    var other = c.collider != null && c.collider.attachedRigidbody == Body ? c.otherCollider : c.collider;
                    if (other == null || other.attachedRigidbody != Opponent.WeaponBody) continue;
                    touchingOppWeapon = true;
                    // 相手の武器が接触点で上向きに動いている＝押し上げられている（乗っているだけは含めない）
                    if (Opponent.WeaponBody.GetPointVelocity(c.point).y >= b.liftMinUpSpeed) pushedUp = true;
                }
            }
            // 開始: 押し上げられて上昇している。継続: 一度持ち上げられた後、相手の武器に触れたまま浮いている
            bool lifted = (pushedUp && Body.linearVelocity.y >= 1f)
                          || (rt.liftActive && touchingOppWeapon && Body.position.y >= b.liftMinHeight * 0.5f);
            if (lifted)
            {
                if (!rt.liftActive) { rt.liftActive = true; rt.slamUsed = false; rt.liftPeakY = 0f; }
                rt.liftedAt = time; // 持ち上げられている間は更新し続け、離れた時点から猶予を数える
            }
            if (rt.liftActive) rt.liftPeakY = Mathf.Max(rt.liftPeakY, Body.position.y);
            bool onGround = Body.position.y < 0.3f && Mathf.Abs(Body.linearVelocity.y) < 0.5f;
            rt.groundedSince = onGround ? rt.groundedSince + dt : 0f;
            if (rt.liftActive && !lifted && (rt.groundedSince >= 0.3f || time - rt.liftedAt > b.slamWindow)) rt.liftActive = false;
        }

        /// <summary>固着解消などで意図的に落とす時は、持ち上げ（叩きつけ判定）を取り消す。</summary>
        public void CancelLift()
        {
            Runtime.liftActive = false;
            Runtime.slamUsed = true;
        }

        void TrackLaunch(float time)
        {
            var rt = Runtime;
            if (!rt.launchTracking) return;
            float d = Mathf.Abs(X - rt.launchOriginX);
            if (d > rt.metrics.maxKnockbackDistance) rt.metrics.maxKnockbackDistance = d;
            if (time - rt.launchedAt > Balance.launchWindow && IsGrounded) rt.launchTracking = false;
        }

        public void MarkLaunched(float time)
        {
            var rt = Runtime;
            rt.launchedAt = time;
            rt.launchWallUsed = false;
            rt.launchGroundUsed = false;
            rt.launchOriginX = X;
            rt.launchTracking = true;
        }

        // 描画補間用: 直前2ステップの姿勢
        public Vector2 PrevBodyPos, CurrBodyPos, PrevWeaponPos, CurrWeaponPos;
        public float PrevBodyRot, CurrBodyRot, PrevWeaponRot, CurrWeaponRot;

        /// <summary>物理ステップ直後に呼ぶ。表示はこの2姿勢の間を補間する（物理には書き戻さない）。</summary>
        public void CapturePose()
        {
            PrevBodyPos = CurrBodyPos; PrevBodyRot = CurrBodyRot;
            PrevWeaponPos = CurrWeaponPos; PrevWeaponRot = CurrWeaponRot;
            CurrBodyPos = Body.position; CurrBodyRot = Body.rotation;
            CurrWeaponPos = WeaponBody.position; CurrWeaponRot = WeaponBody.rotation;
            // 大きな瞬間移動（起き上がり・向き反転）は補間しない
            if ((CurrBodyPos - PrevBodyPos).sqrMagnitude > 4f) { PrevBodyPos = CurrBodyPos; PrevBodyRot = CurrBodyRot; PrevWeaponPos = CurrWeaponPos; PrevWeaponRot = CurrWeaponRot; }
        }

        public void CachePreStep()
        {
            PreBodyVelocity = Body.linearVelocity;
            PreWeaponVelocity = WeaponBody.linearVelocity;
            PreWeaponCom = WeaponBody.worldCenterOfMass;
            PreWeaponAngVelRad = WeaponBody.angularVelocity * Mathf.Deg2Rad;
        }

        /// <summary>ステップ直前の武器上の点の速度。</summary>
        public Vector2 PreWeaponPointVelocity(Vector2 p)
        {
            Vector2 r = p - PreWeaponCom;
            return PreWeaponVelocity + PreWeaponAngVelRad * new Vector2(-r.y, r.x);
        }

        /// <summary>武器上の点を、右向き基準の武器ローカル座標（握り原点）へ。</summary>
        public Vector2 WeaponLocalPoint(Vector2 world)
        {
            Vector2 local = WeaponBody.GetPoint(world);
            local.x *= Facing;
            return local;
        }
    }
}
