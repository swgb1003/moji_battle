using UnityEngine;

namespace MojiBattle
{
    /// <summary>
    /// 棒人間の表示（頭・胴・腕・脚の線）と攻撃予兆（軌道線・足元の溜め）。物理には関与しない。
    /// 腕は常に武器のヒンジ位置（握り）へ伸ばす。
    /// </summary>
    public sealed class StickmanView : MonoBehaviour
    {
        public static bool ShowDebugLabels;

        Fighter f;
        LineRenderer head, torso, armFront, armBack, legA, legB, arc, ring;
        TextMesh teamLabel, debugLabel;
        float walkPhase;
        const float LineWidth = 0.075f;

        public void Init(Fighter fighter)
        {
            f = fighter;
            var ink = FighterFactory.Ink;
            head = UiKit.Line(transform, "Head", ink, LineWidth, 4, loop: true);
            head.positionCount = 20;
            torso = UiKit.Line(transform, "Torso", ink, LineWidth, 4);
            armFront = UiKit.Line(transform, "ArmFront", ink, LineWidth, 6);
            armBack = UiKit.Line(transform, "ArmBack", ink, LineWidth * 0.9f, 3);
            legA = UiKit.Line(transform, "LegA", ink, LineWidth, 4);
            legB = UiKit.Line(transform, "LegB", ink, LineWidth, 4);
            var team = FighterFactory.TeamColor(f.Id);
            arc = UiKit.Line(transform, "TelegraphArc", new Color(team.r, team.g, team.b, 0.5f), 0.05f, 2);
            ring = UiKit.Line(transform, "ChargeRing", new Color(team.r, team.g, team.b, 0.6f), 0.05f, 2, loop: true);
            teamLabel = UiKit.Text(transform, "TeamLabel", f.Id == 0 ? "左" : "右", team, 0.32f, 8);
            teamLabel.fontStyle = FontStyle.Bold;
            debugLabel = UiKit.Text(transform, "DebugLabel", "", FighterFactory.Ink, 0.2f, 8);
        }

        void LateUpdate()
        {
            if (f == null || f.Body == null) return;
            var rt = f.Runtime;
            bool down = rt.IsDown;
            // 前後の物理ステップ間を補間した姿勢で描く
            float alpha = Time.fixedDeltaTime > 0f ? Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime) : 1f;
            Vector2 bodyPos = Vector2.Lerp(f.PrevBodyPos, f.CurrBodyPos, alpha);
            var bodyRot = Quaternion.Euler(0f, 0f, Mathf.LerpAngle(f.PrevBodyRot, f.CurrBodyRot, alpha));
            Vector2 weaponPos = Vector2.Lerp(f.PrevWeaponPos, f.CurrWeaponPos, alpha);
            var weaponRot = Quaternion.Euler(0f, 0f, Mathf.LerpAngle(f.PrevWeaponRot, f.CurrWeaponRot, alpha));
            f.WeaponSprite.transform.SetPositionAndRotation(weaponPos, weaponRot);
            // 横振り: 縦軸まわりに回る字形（奥・手前を向く間は細く、後ろ向きは鏡像）
            f.WeaponSprite.transform.localScale = new Vector3(Mathf.Cos(rt.sweepYaw * Mathf.Deg2Rad), 1f, 1f);
            Vector3 P(float x, float y) => (Vector3)bodyPos + bodyRot * new Vector3(x, y, 0f);

            Vector3 hip = P(0f, 0.86f), neck = P(0f, 1.42f), headC = P(0f, 1.62f), shoulder = P(0f, 1.34f);
            for (int i = 0; i < 20; i++)
            {
                float a = i / 20f * Mathf.PI * 2f;
                head.SetPosition(i, headC + bodyRot * new Vector3(Mathf.Cos(a) * 0.2f, Mathf.Sin(a) * 0.2f, 0f));
            }
            torso.positionCount = 2;
            torso.SetPosition(0, neck);
            torso.SetPosition(1, hip);

            // 武器を投げた後は手を前に下ろす
            Vector3 grip = rt.weaponDetached ? P(0.3f * f.Facing, 1.0f) : (Vector3)weaponPos;
            DrawArm(armFront, shoulder, grip, bodyRot * Vector3.down * 0.12f);
            // 両手持ち: 添え手は握りから重心方向の少し先（表示のみ。物理の接続は握り1点）
            Vector3 backHand = f.Mods.customized && f.Mods.grip == GripType.TwoHanded && !rt.weaponDetached ? SecondHand(f, weaponPos, weaponRot) : grip;
            DrawArm(armBack, shoulder + bodyRot * new Vector3(-0.05f * f.Facing, -0.02f, 0f), backHand, bodyRot * Vector3.down * 0.2f);

            // 脚: 接地中は歩行サイクル、空中は畳む、転倒中は胴に沿って伸ばす
            float vx = f.Body.linearVelocity.x;
            if (!down && f.IsGrounded) walkPhase += Mathf.Abs(vx) * Time.deltaTime * 4.2f;
            float swing = f.IsGrounded && !down ? Mathf.Clamp01(Mathf.Abs(vx) / 1.5f) * 0.24f : 0.12f;
            float s = Mathf.Sin(walkPhase);
            if (down)
            {
                DrawLeg(legA, hip, P(0.12f, 0.02f), P(0.08f, 0.45f));
                DrawLeg(legB, hip, P(-0.12f, 0.02f), P(-0.06f, 0.45f));
            }
            else if (!f.IsGrounded)
            {
                DrawLeg(legA, hip, P(0.2f * f.Facing, 0.25f), P(0.25f * f.Facing, 0.55f));
                DrawLeg(legB, hip, P(-0.12f * f.Facing, 0.1f), P(-0.02f * f.Facing, 0.48f));
            }
            else
            {
                float stance = rt.state == FighterState.Guard || rt.state == FighterState.AttackWindup ? 0.2f : 0.08f;
                Vector3 fa = P(stance * f.Facing + s * swing, 0.0f), fb = P(-stance * f.Facing - s * swing, 0.0f);
                DrawLeg(legA, hip, fa, Vector3.Lerp(hip, fa, 0.5f) + new Vector3(0.06f * f.Facing, 0f, 0f));
                DrawLeg(legB, hip, fb, Vector3.Lerp(hip, fb, 0.5f) + new Vector3(0.06f * f.Facing, 0f, 0f));
            }

            DrawTelegraph(grip, bodyPos);

            var cam = Camera.main;
            UiKit.FitText(teamLabel, 0.32f, cam);
            teamLabel.transform.position = headC + Vector3.up * 0.45f;
            teamLabel.transform.rotation = Quaternion.identity;
            debugLabel.gameObject.SetActive(ShowDebugLabels);
            if (ShowDebugLabels)
            {
                UiKit.FitText(debugLabel, 0.2f, cam);
                debugLabel.transform.position = headC + Vector3.up * 0.8f;
                debugLabel.text = DebugText(f);
            }
        }

        /// <summary>両手持ちの添え手の位置（握りから重心方向へ secondHandOffset、重心までが上限）。</summary>
        public static Vector3 SecondHand(Fighter f, Vector2 weaponPos, Quaternion weaponRot)
        {
            Vector2 com = new Vector2(f.Weapon.comLocal.x * f.Facing, f.Weapon.comLocal.y);
            float len = com.magnitude;
            if (len < 1e-3f) return weaponPos;
            float offset = Mathf.Min(len, f.Context.Customize != null ? f.Context.Customize.secondHandOffset : 0.32f);
            return (Vector3)weaponPos + weaponRot * (Vector3)(com / len * offset);
        }

        /// <summary>D キーの AI 表示: 状態・判断、カスタマイズ時はスタイル・サイズ・持ち方・質量・てこの長さ・距離・射程。</summary>
        static string DebugText(Fighter f)
        {
            var rt = f.Runtime;
            string s = $"{rt.state}  {f.Brain.LastDecision}";
            var m = f.Mods;
            if (!m.customized) return s;
            string style = f.Style != null ? $"{CustomizeLabels.Style(m.style)} {f.Style.DebugState(f.Context.SimTime)}" : "";
            return $"{s}\n{style}\n{m.size}/{CustomizeLabels.Grip(m.grip)} 質量{f.WeaponBody.mass:F1} てこ{m.leverArm:F2} 扱{m.handlingAccel:F2}\n距離{f.DistanceToOpponent:F1} 射程{f.AttackRange:F1}";
        }

        static void DrawArm(LineRenderer lr, Vector3 shoulder, Vector3 hand, Vector3 elbowBias)
        {
            lr.positionCount = 3;
            lr.SetPosition(0, shoulder);
            lr.SetPosition(1, Vector3.Lerp(shoulder, hand, 0.5f) + elbowBias);
            lr.SetPosition(2, hand);
        }

        static void DrawLeg(LineRenderer lr, Vector3 hip, Vector3 foot, Vector3 knee)
        {
            lr.positionCount = 3;
            lr.SetPosition(0, hip);
            lr.SetPosition(1, knee);
            lr.SetPosition(2, foot);
        }

        void DrawTelegraph(Vector3 grip, Vector3 feet)
        {
            var rt = f.Runtime;
            if (rt.state != FighterState.AttackWindup)
            {
                arc.positionCount = 0;
                ring.positionCount = 0;
                return;
            }
            float progress = Mathf.Clamp01(rt.stateTime / Mathf.Max(0.01f, rt.windupDuration));
            const int n = 18;
            arc.positionCount = n;
            float radius = f.Weapon.length * 0.95f;
            if (rt.attackStyle == AttackStyle.Sweep || rt.attackStyle == AttackStyle.LowSweep)
            {
                // 横薙ぎ・足払いの軌道線: 体の後ろから手前を回って前へ（水平の楕円。足払いは膝の高さ）
                Vector3 c = rt.attackStyle == AttackStyle.LowSweep ? new Vector3(grip.x, feet.y + 0.35f, 0f) : grip;
                for (int i = 0; i < n; i++)
                {
                    float a = Mathf.Lerp(Mathf.PI, -0.35f, i / (n - 1f));
                    arc.SetPosition(i, c + new Vector3(Mathf.Cos(a) * f.Facing * radius, -Mathf.Sin(a) * 0.22f * radius - 0.05f, 0f));
                }
            }
            else if (rt.attackStyle == AttackStyle.Spin)
            {
                // 回転斬り: 体の周りを一周する楕円
                for (int i = 0; i < n; i++)
                {
                    float a = i / (n - 1f) * Mathf.PI * 2f;
                    arc.SetPosition(i, grip + new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * 0.22f * radius, 0f));
                }
            }
            else if (rt.attackStyle == AttackStyle.Thrust || rt.attackStyle == AttackStyle.Bash)
            {
                // 刺す: 狙いへ一直線 / 盾当て: 胸の高さで前へ
                float psi = (rt.attackStyle == AttackStyle.Bash ? 0f : rt.swingToPsi) * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(psi) * f.Facing, Mathf.Sin(psi), 0f);
                Vector3 from = rt.attackStyle == AttackStyle.Bash ? (Vector3)f.ChestWorld : grip;
                float len = rt.attackStyle == AttackStyle.Bash ? 1.6f : radius + f.Balance.stabReach + 0.6f;
                for (int i = 0; i < n; i++) arc.SetPosition(i, from + dir * (len * i / (n - 1f)));
            }
            else
            {
                // 武器の軌道線（ψ: 振りかぶり → 振り下ろし）
                for (int i = 0; i < n; i++)
                {
                    float psi = Mathf.Lerp(rt.swingFromPsi, rt.swingToPsi, i / (n - 1f)) * Mathf.Deg2Rad;
                    arc.SetPosition(i, grip + new Vector3(Mathf.Cos(psi) * f.Facing, Mathf.Sin(psi), 0f) * radius);
                }
            }
            var team = FighterFactory.TeamColor(f.Id);
            arc.startColor = arc.endColor = new Color(team.r, team.g, team.b, 0.25f + 0.5f * progress);
            // 足元の溜め
            const int m = 24;
            ring.positionCount = m;
            float rx = 0.25f + 0.55f * progress, ry = 0.06f + 0.06f * progress;
            for (int i = 0; i < m; i++)
            {
                float a = i / (float)m * Mathf.PI * 2f;
                ring.SetPosition(i, feet + new Vector3(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry + 0.02f, 0f));
            }
        }
    }
}
