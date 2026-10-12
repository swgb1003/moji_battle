using System.Collections;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MojiBattle.Tests
{
    public class LeapDebug
    {
        [UnityTest]
        public IEnumerator TraceLeap()
        {
            SimHarness.Begin(false);
            var battle = SimHarness.Build(13, countdown: 1000f, left: "火", right: "山");
            var a = battle.Left;
            var d = battle.Right;
            Vector2 to = new Vector2(a.X + a.AttackRange * 1.8f, 0.02f);
            Vector2 delta = to - d.Body.position;
            d.Body.position += delta; d.WeaponBody.position += delta;
            d.Body.transform.position += (Vector3)delta; d.WeaponBody.transform.position += (Vector3)delta;
            yield return null;
            var sb = new StringBuilder();
            a.StartAttack(battle.Context.SimTime, AttackStyle.Leap);
            int last = -1, n = 0;
            while (n < 70)
            {
                yield return null;
                if (battle.Director.StepCount == last) continue;
                last = battle.Director.StepCount; n++;
                var rt = a.Runtime;
                var wm = a.WeaponMotor;
                sb.AppendLine($"t={battle.Context.SimTime:F2} {rt.state} st={rt.stateTime:F2} psi={wm.CurrentPsi:F0} tgt={wm.TargetPsi:F0} tq={wm.LastTorque:F0} stall={wm.Stalled} w={a.WeaponBody.angularVelocity:F0} ay={a.Body.position.y:F2} ax={a.X:F2} grip=({a.GripWorld.x:F2},{a.GripWorld.y:F2}) stretch={(a.WeaponBody.position - a.Body.GetRelativePoint(a.HandLocal(a.Facing))).magnitude:F2} limp={battle.Context.SimTime < rt.weaponLimpUntil}");
            }
            Debug.Log("[LEAPDBG]\n" + sb);
            battle.Destroy();
            SimHarness.End();
        }
    }
}
