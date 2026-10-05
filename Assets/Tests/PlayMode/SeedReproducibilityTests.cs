using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MojiBattle.Tests
{
    /// <summary>同じ seed は、前に別の試合を回した後でも同じ経過・結果になる（試合ごとに専用の物理ワールドを使うため）。</summary>
    public class SeedReproducibilityTests
    {
        IEnumerator Run(int seed, System.Collections.Generic.List<string> outp)
        {
            var b = SimHarness.Build(seed);
            yield return SimHarness.RunToEnd(b, 10f);
            var r = b.Director.Result;
            outp.Add($"{r.finishReason} t={r.elapsedSeconds:F3} hp={r.hpRemaining[0]:F3}/{r.hpRemaining[1]:F3} hits={r.metrics[0].hitsLanded}/{r.metrics[1].hitsLanded}");
            b.Destroy();
            yield return null;
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator SameSeedAfterDifferentHistory()
        {
            SimHarness.Begin(false);
            var o = new System.Collections.Generic.List<string>();
            yield return Run(1518, o);
            yield return Run(1037, o);
            yield return Run(1074, o);
            yield return Run(1518, o);
            Debug.Log("[REPRO]\n" + string.Join("\n", o));
            SimHarness.End();
            Assert.AreEqual(o[0], o[3], "同じ seed の結果が前の試合の有無で変わった");
            Assert.AreNotEqual(o[0], o[1], "異なる seed で同じ結果（seed が効いていない）");
        }
    }
}
