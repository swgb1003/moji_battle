using UnityEngine;

namespace MojiBattle
{
    /// <summary>本体 Rigidbody2D の移動（接近・待機・離脱・踏み込み）と回避の力。毎 FixedUpdate。</summary>
    public sealed class FighterMotor2D
    {
        readonly Fighter self;

        public FighterMotor2D(Fighter self) { this.self = self; }

        public void Tick(float dt)
        {
            var rt = self.Runtime;
            var b = self.Balance;
            if (rt.IsDown || rt.state == FighterState.Evade || rt.state == FighterState.Stagger) return;
            if (!self.IsGrounded) return;

            int toward = self.TowardOpponent;
            float walk = self.WalkSpeed;
            float target = 0f;
            switch (rt.state)
            {
                case FighterState.Approach:
                    switch (self.Brain.Move)
                    {
                        case MoveIntent.Advance: target = walk * toward; break;
                        case MoveIntent.Retreat: target = -walk * 0.85f * toward; break;
                    }
                    break;
                case FighterState.AttackWindup:
                    if (rt.attackStyle == AttackStyle.Thrust) target = -walk * 0.35f * toward; // 半歩引く
                    else target = walk * (self.WeightClass == WeightClass.Heavy ? 0.3f : 0.25f) * toward;
                    break;
                case FighterState.AttackActive:
                    if (rt.attackStyle == AttackStyle.Thrust) return; // 踏み込みの勢いを保つ
                    // 振りの間は踏み込みを維持（重い武器の反動で後ろへ流されないよう踏ん張る）
                    target = Mathf.Max(walk * 0.6f, StatCalculator.Lerp01(b.stepInSpeedLight, b.stepInSpeedHeavy, self.Stats.weightScore)) * toward;
                    break;
            }

            // 背後の壁に押し付けない
            if (Mathf.Sign(target) != toward && target != 0f && self.BackSpace < 0.35f) target = 0f;

            float totalMass = self.Body.mass + self.WeaponBody.mass;
            float vx = self.Body.linearVelocity.x;
            float force = Mathf.Clamp(totalMass * (target - vx) * b.moveAccelGain, -totalMass * b.maxMoveAccel, totalMass * b.maxMoveAccel);
            self.Body.AddForce(new Vector2(force, 0f), ForceMode2D.Force);
        }

        /// <summary>突きの踏み込み。</summary>
        public void ApplyLunge()
        {
            var b = self.Balance;
            float speed = StatCalculator.Lerp01(b.lungeSpeedLight, b.lungeSpeedHeavy, self.Stats.weightScore);
            if (!self.IsGrounded) return;
            float dv = self.TowardOpponent * speed - self.Body.linearVelocity.x;
            self.AddVelocity(new Vector2(dv, 0f));
        }

        /// <summary>振りの踏み込み（重量級ほど大きく一歩出る）。</summary>
        public void ApplyStepIn()
        {
            var b = self.Balance;
            float speed = StatCalculator.Lerp01(b.stepInSpeedLight, b.stepInSpeedHeavy, self.Stats.weightScore);
            if (speed <= 0f || !self.IsGrounded) return;
            float dv = self.TowardOpponent * speed - self.Body.linearVelocity.x;
            self.AddVelocity(new Vector2(dv, 0f));
        }

        /// <summary>回避。無敵時間はなく、物理的な位置移動だけで攻撃を外す（7.3）。</summary>
        public void ApplyEvadeImpulse(EvadeKind kind)
        {
            var b = self.Balance;
            int toward = self.TowardOpponent;
            Vector2 v = kind == EvadeKind.Backstep
                ? new Vector2(-toward * b.backstepSpeed, b.backstepHop)
                : new Vector2(toward * b.hopOverSpeedX, b.hopOverSpeedY);
            self.AddVelocity(v - self.Body.linearVelocity);
        }
    }
}
