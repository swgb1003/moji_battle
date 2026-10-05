using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MojiBattle.Tests
{
    [Category("Debug")]
    [Explicit("調整用の診断。-testFilter で明示実行する")]
    public class TraceDebug
    {
        [UnityTest]
        public IEnumerator TraceOneMatch()
        {
            SimHarness.Begin(false);
            MatchTelemetry.Trace = true;
            int seed = int.TryParse(System.Environment.GetEnvironmentVariable("TRACE_SEED"), out var sd) ? sd : 1037;
            var battle = SimHarness.Build(seed, left: System.Environment.GetEnvironmentVariable("TRACE_LEFT") ?? "一",
                right: System.Environment.GetEnvironmentVariable("TRACE_RIGHT") ?? "鬱");
            var contacts = new System.Collections.Generic.List<string>();
            yield return SimHarness.RunToEnd(battle, 10f);
            File.WriteAllLines(Path.Combine(SimHarness.ReportDir, $"trace_{seed}.txt"), battle.Director.Telemetry.log);
            var dg = battle.Context.Hits.Diag;
            Debug.Log($"[TRACE] contacts={battle.Context.Hits.TotalContactsReported} L(outside,resolved,body,weapon,slow)={dg[0,0]},{dg[0,1]},{dg[0,2]},{dg[0,3]},{dg[0,4]} R={dg[1,0]},{dg[1,1]},{dg[1,2]},{dg[1,3]},{dg[1,4]}");
            MatchTelemetry.Trace = false;
            battle.Destroy();
            SimHarness.End();
        }
    }
}
