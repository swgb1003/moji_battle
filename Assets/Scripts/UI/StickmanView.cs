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

            Vector3 grip = weaponPos;
            DrawArm(armFront, shoulder, grip, bodyRot * Vector3.down * 0.12f);
            DrawArm(armBack, shoulder + bodyRot * new Vector3(-0.05f * f.Facing, -0.02f, 0f), grip, bodyRot * Vector3.down * 0.2f);

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
                debugLabel.text = $"{rt.state}\n{f.Brain.LastDecision}";
            }
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
            // 武器の軌道線（ψ: 振りかぶり → 振り下ろし）
            const int n = 18;
            arc.positionCount = n;
            float radius = f.Weapon.length * 0.95f;
            for (int i = 0; i < n; i++)
            {
                float psi = Mathf.Lerp(rt.swingFromPsi, rt.swingToPsi, i / (n - 1f)) * Mathf.Deg2Rad;
                arc.SetPosition(i, grip + new Vector3(Mathf.Cos(psi) * f.Facing, Mathf.Sin(psi), 0f) * radius);
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
