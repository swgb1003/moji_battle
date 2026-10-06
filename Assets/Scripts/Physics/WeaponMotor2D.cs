using UnityEngine;

namespace MojiBattle
{
    /// <summary>
    /// HingeJoint2D でつながった武器を、目標角へのトルク制御（PD＋重力補償、上限付き）で動かす（6.2）。
    /// 攻撃中に武器 Transform の角度を直接書き換えない。反作用トルクは本体へ返す。
    /// 角度 ψ は「握り→重心ベクトルの、前方水平からの角度」（向きに依存しない姿勢表現）。
    /// </summary>
    public sealed class WeaponMotor2D
    {
        readonly Fighter self;
        float windupStartPsi;
        float lastTarget = float.NaN;
        float lastTargetVelDeg;
        float stallTime;
        FighterState lastState;

        public float TargetPsi { get; private set; }
        public float CurrentPsi { get; private set; }
        public float LastTorque { get; private set; }
        /// <summary>目標から大きくずれたまま回らない（相手の字形・地面などに押さえ込まれている）。</summary>
        public bool Stalled => stallTime > 0.25f;

        public WeaponMotor2D(Fighter self) { this.self = self; }

        float ReadyPsi => self.ReadyPsi;
        float NaturalFrequency => StatCalculator.Lerp01(self.Balance.poseFrequencyLight, self.Balance.poseFrequencyHeavy, self.Stats.weightScore);
        /// <summary>
        /// 最大角加速度。字形の重量クラスで決まり、カスタマイズでは「同じ筋力で慣性が大きいほど遅い」比（handlingAccel）を掛ける。
        /// 端持ち・大型の武器は振り始めが遅く、復帰も遅くなる（数値の時間補正ではなく慣性から）。
        /// </summary>
        float AccelLimit => self.Balance.maxAngularAccel * Mathf.Lerp(1f, self.Balance.heavyAccelFactor, self.Stats.weightScore / 100f)
                            * self.Mods.handlingAccel * (self.Mods.customized && self.Context.Customize != null ? self.Context.Customize.handlingAccelScale : 1f);

        /// <summary>現在の ψ（度）。</summary>
        public float MeasurePsi()
        {
            float rel = Mathf.DeltaAngle(self.Body.rotation, self.WeaponBody.rotation);
            float phi = rel * self.Facing;
            // -180〜180° に正規化（基準角が ±180° 付近の字形で読み値が 360° ずれ、逆回りに回り込むのを防ぐ）
            return Mathf.DeltaAngle(0f, phi + self.Weapon.alpha0Deg);
        }

        public void Tick(float dt)
        {
            var rt = self.Runtime;
            var b = self.Balance;
            CurrentPsi = MeasurePsi();
            // 投げて手を離れている間は何もしない（飛ぶ・転がるのは物理のまま）
            if (rt.weaponDetached) { LastTorque = 0f; stallTime = 0f; lastTarget = float.NaN; return; }
            float torqueScale;
            float target;
            switch (rt.state)
            {
                case FighterState.Guard:
                    target = float.IsNaN(rt.guardPsiTarget) ? b.guardPsi : rt.guardPsiTarget; torqueScale = 1f; break;
                case FighterState.AttackWindup:
                {
                    if (rt.stateTime <= dt * 1.01f) windupStartPsi = CurrentPsi;
                    float t = Mathf.Clamp01(rt.stateTime / Mathf.Max(0.05f, rt.windupDuration * 0.7f));
                    target = Mathf.Lerp(windupStartPsi, rt.swingFromPsi, t * t * (3f - 2f * t));
                    // 溜めは押し込まない（溜め中の接触は物理衝突のみ）
                    torqueScale = 0.5f;
                    break;
                }
                case FighterState.AttackActive:
                {
                    float t = Mathf.Clamp01(rt.stateTime / Mathf.Max(0.02f, rt.activeDuration));
                    // 加速しながら振り抜く（終端付近で最速 = 当たる瞬間が一番重い）
                    target = Mathf.Lerp(rt.swingFromPsi, rt.swingToPsi, t * t);
                    torqueScale = 1f;
                    break;
                }
                case FighterState.AttackRecovery:
                    // 振り切った後はしばらく武器が低い位置に残る（重量級ほど隙が大きい）
                    if (rt.stateTime < rt.recoveryDuration * self.Balance.followThroughFraction)
                    { target = rt.swingToPsi; torqueScale = 0.35f; }
                    else { target = ReadyPsi; torqueScale = 0.5f; }
                    break;
                case FighterState.Stagger:
                    target = ReadyPsi; torqueScale = 0.3f; break;
                case FighterState.Knockdown:
                case FighterState.KO:
                    target = CurrentPsi; torqueScale = 0f; break;
                default:
                    // 待機中は軽く持つだけ（踏ん張らない）。重い一撃を受けると武器ごと押し退けられる。
                    // 武器同士がぶつかって前進できない時だけ、武器を立てて担いで障害を越える
                    target = self.Brain.CarryWeapon ? Mathf.Max(ReadyPsi, b.carryPsi) : ReadyPsi;
                    // 戦闘スタイル（鉄壁）: 相手が近ければ字形を相手へ向けて構える
                    float stylePsi = self.Style != null ? self.Style.IdlePsiOverride() : float.NaN;
                    if (!float.IsNaN(stylePsi) && !self.Brain.CarryWeapon) target = stylePsi;
                    torqueScale = self.Balance.idleHoldTorqueScale; break;
            }
            // 目標角の速度（同じ状態が続いている間だけ）。PD の微分項を目標速度との差にして振りの遅れを無くす
            bool continuing = !float.IsNaN(lastTarget) && lastState == rt.state;
            float targetVelDeg = continuing ? (target - lastTarget) / Mathf.Max(1e-4f, dt) : 0f;
            // 目標角の加速度（短い振りでも遅れずに軌道を追うため、慣性×加速度を先に加える）
            float targetAccDeg = continuing ? (targetVelDeg - lastTargetVelDeg) / Mathf.Max(1e-4f, dt) : 0f;
            lastTargetVelDeg = targetVelDeg;
            lastTarget = target;
            lastState = rt.state;
            TargetPsi = target;
            if (self.Context.SimTime < rt.weaponLimpUntil) torqueScale = 0f;
            if (torqueScale <= 0f) { LastTorque = 0f; ClampAngularSpeed(); return; }

            var wb = self.WeaponBody;
            float inertia = self.Weapon.InertiaAtGrip(wb);
            // 目標の相対角（本体基準）。ψ の差を向きで符号変換する。
            float errDeg = Mathf.DeltaAngle(CurrentPsi, target) * self.Facing;
            float errRad = errDeg * Mathf.Deg2Rad;
            float relOmega = (wb.angularVelocity - self.Body.angularVelocity) * Mathf.Deg2Rad;
            float targetOmega = targetVelDeg * self.Facing * Mathf.Deg2Rad;
            // 失速保護: 目標から大きくずれたまま回らない（相手の字形などに押さえ込まれている）間はトルクを弱め、
            // ヒンジを静的に引き伸ばし続けないようにする
            bool stalled = Mathf.Abs(errDeg) > 25f && Mathf.Abs(relOmega) < 0.6f;
            stallTime = stalled ? stallTime + dt : 0f;
            torqueScale *= Mathf.Lerp(1f, 0.3f, Mathf.Clamp01(stallTime / 0.3f));
            float wn = NaturalFrequency;
            // 加速度の先回りは振り（有効時間）の間だけ。溜めの急加速では使わない（ヒンジを強く引っ張るため）
            float targetAlpha = rt.state == FighterState.AttackActive ? targetAccDeg * self.Facing * Mathf.Deg2Rad : 0f;
            float torque = inertia * (targetAlpha + wn * wn * errRad + 2f * b.poseDamping * wn * (targetOmega - relOmega));

            // 重力補償: 重心が握りから横にずれているほど必要トルクが大きい
            Vector2 r = wb.worldCenterOfMass - wb.position;
            float gravityTorque = wb.mass * -Physics2D.gravity.y * wb.gravityScale * r.x;
            // 持ち方の「攻撃時トルク」は溜め・振りの間だけ
            bool attacking = rt.state == FighterState.AttackWindup || rt.state == FighterState.AttackActive;
            float limit = inertia * AccelLimit * torqueScale * (attacking ? self.Mods.attackTorque : 1f) + Mathf.Abs(gravityTorque) * b.gravityCompensation;
            torque += gravityTorque * b.gravityCompensation;
            torque = Mathf.Clamp(torque, -limit, limit);

            wb.AddTorque(torque, ForceMode2D.Force);
            self.Body.AddTorque(-torque, ForceMode2D.Force); // 反作用（転倒中のみ有効。通常時は回転固定）
            LastTorque = torque;
            ClampAngularSpeed();
        }

        void ClampAngularSpeed()
        {
            var wb = self.WeaponBody;
            float max = self.Balance.weaponMaxAngularSpeedDeg;
            if (Mathf.Abs(wb.angularVelocity) > max) wb.angularVelocity = Mathf.Sign(wb.angularVelocity) * max;
        }

        /// <summary>開始時・起き上がり時のみ、武器を安全姿勢（構え）へ初期化する。</summary>
        public void ResetPose()
        {
            if (self.Runtime.weaponDetached) return;
            var wb = self.WeaponBody;
            float phi = ReadyPsi - self.Weapon.alpha0Deg;
            self.Runtime.handOffset = Vector2.zero;
            self.Hinge.connectedAnchor = self.HandLocal(self.Facing);
            wb.position = self.Body.GetRelativePoint(self.HandLocal(self.Facing));
            wb.rotation = self.Body.rotation + phi * self.Facing;
            wb.linearVelocity = self.Body.linearVelocity;
            wb.angularVelocity = 0f;
        }
    }
}
