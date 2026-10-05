using UnityEngine;

namespace MojiBattle
{
    /// <summary>
    /// 転倒・復帰・KO（8.4）。転倒時は本体の回転固定を外して物理で転がす（武器ヒンジは保持）。
    /// 静止を確認してから 1.0〜2.0 秒後に起き上がる。壁際・相手と重なっている場合は安全位置へ小さく移す。
    /// </summary>
    public sealed class KnockdownController
    {
        readonly Fighter self;

        public KnockdownController(Fighter self) { this.self = self; }

        public void EnterKnockdown(float time) => Enter(time, false);
        public void EnterKO(float time) => Enter(time, true);

        void Enter(float time, bool ko)
        {
            var rt = self.Runtime;
            if (rt.state == FighterState.KO) return;
            bool wasDown = rt.state == FighterState.Knockdown;
            rt.comboRemaining = 0;
            rt.knockdownAccum = 0f;
            rt.settledAt = -1f;
            rt.recoverAt = -1f;
            if (!wasDown)
            {
                rt.knockdownStartedAt = time;
                if (!ko) rt.metrics.knockdowns++;
            }
            self.Body.constraints = RigidbodyConstraints2D.None;
            self.SetState(ko ? FighterState.KO : FighterState.Knockdown);
        }

        public void Tick(float time, float dt)
        {
            var rt = self.Runtime;
            var b = self.Balance;
            switch (rt.state)
            {
                case FighterState.Knockdown:
                {
                    var body = self.Body;
                    bool settled = body.linearVelocity.magnitude < b.settleSpeed && Mathf.Abs(body.angularVelocity) < b.settleAngularSpeed;
                    if (settled && rt.recoverAt < 0f)
                    {
                        rt.settledAt = time;
                        rt.recoverAt = time + self.Brain.Rng.Range(b.recoverDelayMin, b.recoverDelayMax);
                    }
                    else if (!settled && rt.recoverAt >= 0f && body.linearVelocity.magnitude > b.settleSpeed * 4f)
                    {
                        // 再び大きく動かされたら静止判定からやり直す
                        rt.recoverAt = -1f;
                        rt.settledAt = -1f;
                    }
                    bool timeout = rt.stateTime >= b.maxKnockdownTime;
                    if ((rt.recoverAt >= 0f && time >= rt.recoverAt) || timeout) BeginRecover(time, timeout);
                    break;
                }
                case FighterState.Recover:
                    self.Body.linearVelocity = new Vector2(0f, Mathf.Min(0f, self.Body.linearVelocity.y));
                    if (rt.stateTime >= b.recoverDuration) self.SetState(FighterState.Approach);
                    break;
            }
        }

        void BeginRecover(float time, bool timeout)
        {
            var rt = self.Runtime;
            var b = self.Balance;
            var body = self.Body;
            float half = self.Context.ArenaHalfWidth;
            float x = body.worldCenterOfMass.x;
            bool shifted = false;

            // 壁に挟まれた / 相手と重なった場合は安全位置へ小さく移してから復帰
            float margin = 0.45f;
            float clamped = Mathf.Clamp(x, -half + margin, half - margin);
            if (!Mathf.Approximately(clamped, x)) { x = clamped; shifted = true; }
            float ox = self.Opponent.X;
            if (Mathf.Abs(x - ox) < 0.5f)
            {
                float dir = x >= ox ? 1f : -1f;
                if (Mathf.Abs(x + dir * 0.6f) > half - margin) dir = -dir;
                x = Mathf.Clamp(ox + dir * 0.6f, -half + margin, half - margin);
                shifted = true;
            }

            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.rotation = 0f;
            body.position = new Vector2(x, 0.02f);
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            self.SetFacing(self.TowardOpponent);
            self.WeaponMotor.ResetPose();
            rt.launchTracking = false;
            self.SetState(FighterState.Recover);
            self.Context.Events.Raise(new RecoverEvent
            {
                fighter = self.Id,
                sinceKnockdown = time - rt.knockdownStartedAt,
                // 時間切れ（静止しないまま上限に達した）場合は「静止から」の計測に含めない
                sinceSettle = rt.settledAt >= 0f && !timeout ? time - rt.settledAt : -1f,
                shiftedToSafePosition = shifted,
                time = time,
            });
        }
    }
}
