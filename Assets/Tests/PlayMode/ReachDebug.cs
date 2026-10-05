using System.Collections;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MojiBattle.Tests
{
    [Category("Debug")]
    [Explicit("調整用の診断。-testFilter で明示実行する")]
    public class ReachDebug
    {
        [UnityTest]
        public IEnumerator MeasureHeavyReach()
        {
            SimHarness.Begin(false);
            var battle = SimHarness.Build(1037);
            TimeController.SetSpectatorSpeed(1f);
            var sb = new StringBuilder();
            float minDist = 99f, dStart = 0f, minWeaponGap = 99f, maxPsiErr = 0f;
            int lastStep = -1;
            bool active = false;
            var h = battle.Right; var l = battle.Left;
            while (!battle.Director.Ended && battle.Director.Clock < 40f)
            {
                yield return null;
                if (battle.Director.StepCount == lastStep) continue;
                lastStep = battle.Director.StepCount;
                bool a = h.Runtime.state == FighterState.AttackActive;
                if (a && !active) { minDist = 99f; minWeaponGap = 99f; dStart = h.DistanceToOpponent; maxPsiErr = 0f; }
                if (a)
                {
                    foreach (var wc in h.WeaponColliders)
                    {
                        foreach (var bc in l.BodyColliders) { var dd = Physics2D.Distance(wc, bc); if (dd.isValid) minDist = Mathf.Min(minDist, dd.distance); }
                        foreach (var lw in l.WeaponColliders) { var dd = Physics2D.Distance(wc, lw); if (dd.isValid) minWeaponGap = Mathf.Min(minWeaponGap, dd.distance); }
                    }
                    maxPsiErr = Mathf.Max(maxPsiErr, Mathf.Abs(h.WeaponMotor.TargetPsi - h.WeaponMotor.CurrentPsi));
                }
                if (!a && active)
                    sb.AppendLine($"t={battle.Director.Clock:F2} dStart={dStart:F2} dEnd={h.DistanceToOpponent:F2} minBodyGap={minDist:F2} minWeaponGap={minWeaponGap:F2} psiEnd={h.WeaponMotor.CurrentPsi:F0} target={h.Runtime.swingToPsi:F0} maxErr={maxPsiErr:F0} Lstate={l.Runtime.state} style={h.Runtime.attackStyle}");
                active = a;
            }
            Debug.Log("[REACH]\n" + sb);
            battle.Destroy();
            SimHarness.End();
        }
    }
}
