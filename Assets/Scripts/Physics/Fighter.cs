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
        /// <summary>試合時計（経過秒）。KO 演出中は止まる。</summary>
        public float MatchClock;
        /// <summary>延長戦中（AI は最大限攻める）。</summary>
        public bool Overtime;
        /// <summary>どちらかが最後に攻撃を始めた時刻（膠着の検出用）。</summary>
        public float LastAttackStartTime;
        /// <summary>衝突の復帰（重なっていれば離れてから戻す）。MatchDirector が設定する。</summary>
        public System.Action<Collider2D, Collider2D> RestoreCollision;
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
        public Fighter Opponent;
        public MatchContext Context;
        public FighterRuntime Runtime;
        public FighterBrain Brain;
        public FighterMotor2D Motor;
        public WeaponMotor2D WeaponMotor;
        public KnockdownController Knockdown;
        public StickmanView View;
        public float AttackRange, WalkSpeed;
        public bool IsGrounded { get; private set; }
        public float LastTurnTime = -10f;

        // 物理ステップ直前の運動状態（接触の法線相対速度 vN の算出に使う）
        public Vector2 PreBodyVelocity, PreWeaponVelocity, PreWeaponCom;
        public float PreWeaponAngVelRad;

        readonly ContactPoint2D[] contactBuffer = new ContactPoint2D[24];

        public CombatBalance Balance => Context.Balance;
        public float X => Body.position.x;
        public Vector2 ShoulderLocal(int facing) => new Vector2(Balance.shoulderLocal.x * facing, Balance.shoulderLocal.y);
        public Vector2 GripWorld => WeaponBody.position;
        public Vector2 ChestWorld => Body.GetRelativePoint(new Vector2(0f, 1.15f));
        public Vector2 HeadWorld => Body.GetRelativePoint(new Vector2(0f, 1.62f));
        public int TowardOpponent => Opponent.X >= X ? 1 : -1;
        public float DistanceToOpponent => Mathf.Abs(Opponent.X - X);
        /// <summary>背後（向きと反対側）の壁までの距離。</summary>
        public float BackSpace => Facing > 0 ? X + Context.ArenaHalfWidth : Context.ArenaHalfWidth - X;
        public ClassTendency Tendency => Balance.TendencyFor(WeightClass);
        /// <summary>相手から見た自分の攻撃の脅威距離（射程＋振りの間の踏み込み）。</summary>
        public float ThreatRange =>
            AttackRange + StatCalculator.Lerp01(Balance.stepInSpeedLight, Balance.stepInSpeedHeavy, Stats.weightScore)
            * StatCalculator.Active(Stats.weightScore, Balance);

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

            switch (rt.state)
            {
                case FighterState.AttackWindup:
                    if (rt.stateTime >= rt.windupDuration)
                    {
                        SetState(FighterState.AttackActive);
                        if (rt.attackStyle == AttackStyle.Thrust) Motor.ApplyLunge();
                        else Motor.ApplyStepIn();
                    }
                    break;
                case FighterState.AttackActive:
                    if (rt.stateTime >= rt.activeDuration) EndActive(time);
                    break;
                case FighterState.AttackRecovery:
                    if (rt.stateTime >= rt.recoveryDuration) EndRecovery(time);
                    break;
                case FighterState.Evade:
                    if ((rt.stateTime >= b.evadeMinTime && IsGrounded) || rt.stateTime >= b.evadeMaxTime)
                        SetState(FighterState.Approach);
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

            ClampSpeeds();
            if (Context.MatchOver || !Context.AiEnabled) Brain.Idle();
            else Brain.Tick(time);
            UpdateFacing(time);
            Motor.Tick(dt);
            WeaponMotor.Tick(dt);
        }

        public void StartAttack(float time)
        {
            var rt = Runtime;
            var b = Balance;
            int m = Stats.weightScore;
            rt.currentAttackId = rt.NextAttackId(Id);
            rt.attackHadContact = false;
            rt.clashedThisAttack = false;
            rt.windupDuration = StatCalculator.Windup(m, b);
            rt.activeDuration = StatCalculator.Active(m, b);
            rt.recoveryDuration = StatCalculator.Recovery(m, b);

            // 狙いの上下位置（PRNG）。相手の武器が高い位置にあれば下から、低ければ上から狙いやすい。
            // 相手の現在位置を狙うが命中は保証しない。
            bool oppWeaponHigh = Opponent.WeaponBody.worldCenterOfMass.y > Opponent.ChestWorld.y + 0.3f;
            bool heavy = WeightClass == WeightClass.Heavy;
            float pRising = heavy ? (oppWeaponHigh ? b.risingWhenHighHeavy : b.risingWhenLowHeavy)
                                  : (oppWeaponHigh ? b.risingWhenHighLight : b.risingWhenLowLight);
            float pThrust = StatCalculator.Lerp01(b.thrustChanceLight, b.thrustChanceHeavy, m);
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
            else
                target = Brain.Rng.Chance(0.5f) ? Opponent.Body.GetRelativePoint(new Vector2(0f, 0.45f)) : Opponent.ChestWorld;
            Vector2 grip = Body.GetRelativePoint(ShoulderLocal(Facing));
            float aim = Mathf.Atan2(target.y - grip.y, Mathf.Max(0.3f, Mathf.Abs(target.x - grip.x))) * Mathf.Rad2Deg;
            float arc = StatCalculator.Lerp01(b.swingArcLight, b.swingArcHeavy, m);
            if (rt.attackStyle == AttackStyle.Thrust)
            {
                // 突き: 武器は狙いへ向けたまま、溜めで半歩引いて有効時間に踏み込む
                rt.swingFromPsi = aim;
                rt.swingToPsi = aim;
            }
            else if (rt.attackStyle == AttackStyle.Overhead)
            {
                rt.swingFromPsi = aim + arc;
                // 大きい字形ほど振り抜きを浅くする（低く振り抜くと手前の地面に当たって届かない）
                rt.swingToPsi = aim - b.strikeOvershoot * Mathf.Lerp(1f, 0.2f, m / 100f);
            }
            else
            {
                // 武器の長さから、切っ先が地面に刺さらない開始角を求める
                float groundPsi = -Mathf.Asin(Mathf.Clamp01((b.shoulderLocal.y - 0.15f) / Mathf.Max(0.01f, Weapon.length))) * Mathf.Rad2Deg + 5f;
                // 開始角より下は斬り上げでは届かないので、その場合は胴を狙う
                if (aim < groundPsi + 15f)
                {
                    Vector2 chest = Opponent.ChestWorld;
                    aim = Mathf.Max(groundPsi + 15f, Mathf.Atan2(chest.y - grip.y, Mathf.Max(0.3f, Mathf.Abs(chest.x - grip.x))) * Mathf.Rad2Deg);
                }
                rt.swingFromPsi = Mathf.Max(aim - arc * 0.7f, b.risingMinPsi, groundPsi);
                rt.swingToPsi = aim + b.strikeOvershoot * 0.8f;
            }
            rt.metrics.attacksStarted++;
            Context.LastAttackStartTime = time;
            SetState(FighterState.AttackWindup);
        }

        /// <summary>このスイングの間だけ相手の武器との衝突を外す（叩き落として振り抜く / ガード崩しの追撃）。</summary>
        public void SetWeaponPassThrough(bool on)
        {
            if (Runtime.weaponPassThrough == on) return;
            Runtime.weaponPassThrough = on;
            foreach (var mine in WeaponColliders)
            foreach (var theirs in Opponent.WeaponColliders)
            {
                if (on || Context.RestoreCollision == null) Physics2D.IgnoreCollision(mine, theirs, on);
                else Context.RestoreCollision(mine, theirs);
            }
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
            float myNextHit = StatCalculator.Windup(Stats.weightScore, Balance) + 0.1f;
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
            if (!IsGrounded || DistanceToOpponent < 0.15f) return;
            if (TowardOpponent != Facing && time - LastTurnTime > 0.25f)
            {
                LastTurnTime = time;
                SetFacing(TowardOpponent);
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
            float rel = Mathf.DeltaAngle(Body.rotation, WeaponBody.rotation);
            float relW = WeaponBody.angularVelocity - Body.angularVelocity;
            Facing = f;
            // Collider は offset を左右反転（Transform を介さないので補間中の姿勢を物理へ書き戻さない）
            foreach (var c in WeaponColliders) c.offset = new Vector2(-c.offset.x, c.offset.y);
            WeaponSprite.flipX = f < 0;
            WeaponBody.centerOfMass = new Vector2(Weapon.comLocal.x * f, Weapon.comLocal.y);
            Hinge.connectedAnchor = ShoulderLocal(f);
            WeaponBody.position = Body.GetRelativePoint(ShoulderLocal(f));
            WeaponBody.rotation = Body.rotation - rel;
            WeaponBody.angularVelocity = Body.angularVelocity - relW;
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
