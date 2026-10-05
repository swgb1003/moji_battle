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
        FighterState lastState;

        public float TargetPsi { get; private set; }
        public float CurrentPsi { get; private set; }
        public float LastTorque { get; private set; }

        public WeaponMotor2D(Fighter self) { this.self = self; }

        float ReadyPsi => self.Balance.ReadyPsi(self.Stats.weightScore);
        float NaturalFrequency => StatCalculator.Lerp01(self.Balance.poseFrequencyLight, self.Balance.poseFrequencyHeavy, self.Stats.weightScore);
        float AccelLimit => self.Balance.maxAngularAccel * Mathf.Lerp(1f, self.Balance.heavyAccelFactor, self.Stats.weightScore / 100f);

        /// <summary>現在の ψ（度）。</summary>
        public float MeasurePsi()
        {
            float rel = Mathf.DeltaAngle(self.Body.rotation, self.WeaponBody.rotation);
            float phi = rel * self.Facing;
            return phi + self.Weapon.alpha0Deg;
        }

        public void Tick(float dt)
        {
            var rt = self.Runtime;
            var b = self.Balance;
            CurrentPsi = MeasurePsi();
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
            float errDeg = (target - CurrentPsi) * self.Facing;
            float errRad = errDeg * Mathf.Deg2Rad;
            float relOmega = (wb.angularVelocity - self.Body.angularVelocity) * Mathf.Deg2Rad;
            float targetOmega = targetVelDeg * self.Facing * Mathf.Deg2Rad;
            float wn = NaturalFrequency;
            // 加速度の先回りは振り（有効時間）の間だけ。溜めの急加速では使わない（ヒンジを強く引っ張るため）
            float targetAlpha = rt.state == FighterState.AttackActive ? targetAccDeg * self.Facing * Mathf.Deg2Rad : 0f;
            float torque = inertia * (targetAlpha + wn * wn * errRad + 2f * b.poseDamping * wn * (targetOmega - relOmega));

            // 重力補償: 重心が握りから横にずれているほど必要トルクが大きい
            Vector2 r = wb.worldCenterOfMass - wb.position;
            float gravityTorque = wb.mass * -Physics2D.gravity.y * wb.gravityScale * r.x;
            float limit = inertia * AccelLimit * torqueScale + Mathf.Abs(gravityTorque) * b.gravityCompensation;
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
            var wb = self.WeaponBody;
            float phi = ReadyPsi - self.Weapon.alpha0Deg;
            wb.position = self.Body.GetRelativePoint(self.ShoulderLocal(self.Facing));
            wb.rotation = self.Body.rotation + phi * self.Facing;
            wb.linearVelocity = self.Body.linearVelocity;
            wb.angularVelocity = 0f;
        }
    }
}
