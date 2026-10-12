using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MojiBattle.Tests
{
    public class HeavyTempoTests
    {
        BattleInstance battle;
        CombatBalance balance;
        sealed class Sample
        {
            public float elapsed, quiet, recovery, firstActive = -1f, longestGap, damage, maxGrip;
            public int starts, hits;
            public string trace;
        }
        [TearDown]
        public void Cleanup()
        {
            battle?.Destroy();
            if (balance != null) UnityEngine.Object.Destroy(balance);
            Fighter.ForcedStyleForTests = null;
            SimHarness.End();
        }

        IEnumerator Measure(FighterLoadout left, FighterLoadout right, int seed, bool tempo,
            Sample sample, float seconds = 30f, bool requireHeavy = true)
        {
            balance = UnityEngine.Object.Instantiate(CombatBalance.Default);
            balance.combatTempo = false; // Isolate the original heavy-only tuning in this comparison.
            balance.heavyTempo = tempo;
            battle = BattleBuilder.Build(new MatchConfig { left = left, right = right, seed = seed, durationSeconds = seconds },
                balance, GlyphCalibration.Default, false, 0, out var error);
            Assert.IsNotNull(battle, error);
            battle.Director.enabled = false;
            if (requireHeavy)
                foreach (var f in battle.Director.Fighters) Assert.AreEqual(WeightClass.Heavy, f.WeightClass, f.Loadout.grapheme);
            var trace = new StringBuilder();
            battle.Context.Events.StateChanged += e =>
            {
                trace.Append($"{e.fighter}:{(int)e.to}:{battle.Context.SimTime:F3};");
                if (e.to == FighterState.AttackActive && sample.firstActive < 0) sample.firstActive = battle.Director.Clock;
            };
            for (int step = 0; step < Mathf.CeilToInt(seconds / TimeController.BaseFixedDelta) && !battle.Context.MatchOver; step++)
            {
                battle.Director.SimulateStep();
                if (battle.Context.MatchOver) break;
                // Count stationary waiting, guarding and post-swing downtime.
                // Falls, getting up, feints and evasion are visible action, not idle time.
                bool waiting = battle.Director.Fighters.All(f =>
                    (f.Runtime.state == FighterState.Approach || f.Runtime.state == FighterState.Guard
                        || f.Runtime.state == FighterState.AttackRecovery) && Mathf.Abs(f.Body.linearVelocity.x) < .5f);
                if (waiting) sample.quiet += TimeController.BaseFixedDelta;
                foreach (var f in battle.Director.Fighters)
                {
                    if (f.Runtime.state == FighterState.AttackRecovery) sample.recovery += TimeController.BaseFixedDelta;
                    if (!f.Runtime.weaponDetached && !f.Runtime.IsDown)
                        sample.maxGrip = Mathf.Max(sample.maxGrip, Vector2.Distance(f.GripWorld, f.Body.GetRelativePoint(f.HandLocal(f.Facing))));
                }
                if (step % 100 == 0) yield return null;
            }
            sample.elapsed = battle.Director.Clock;
            var telemetry = battle.Director.Telemetry;
            sample.longestGap = telemetry.longestIdleSeconds;
            sample.starts = telemetry.fighters.Sum(f => f.attackStarts);
            sample.hits = battle.Left.Runtime.metrics.hitsLanded + battle.Right.Runtime.metrics.hitsLanded;
            sample.damage = battle.Left.Runtime.maxHp - battle.Left.Runtime.hp + battle.Right.Runtime.maxHp - battle.Right.Runtime.hp;
            sample.trace = trace + $"hp={battle.Left.Runtime.hp:F4}/{battle.Right.Runtime.hp:F4};hits={sample.hits}";
            battle.Destroy(); battle = null;
            UnityEngine.Object.Destroy(balance); balance = null;
            yield return null;
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator HeavyDuels_ReduceWaiting_WithoutRemovingPhysicalSwings()
        {
            SimHarness.Begin(false);
            var baseline = new List<Sample>(); var tuned = new List<Sample>();
            var csv = new StringBuilder("pair,size,grip,style,seed,tempo,seconds,first_active,longest_start_gap,quiet_seconds,recovery_seconds,starts,hits,damage,max_grip\n");
            foreach (var scenario in new[]
            {
                ("鬱", "鬱", WeaponSize.L, GripType.OneHanded, BattleStyle.Aggressive),
                ("鬱", "龍", WeaponSize.XL, GripType.TwoHanded, BattleStyle.Aggressive),
                ("鬱", "龍", WeaponSize.XL, GripType.Horizontal, BattleStyle.Defensive),
                ("龍", "龍", WeaponSize.XL, GripType.TwoHanded, BattleStyle.Counter),
            })
            foreach (int seed in new[] { 739, 1518, 7109 })
            foreach (bool enabled in new[] { false, true })
            {
                var (l, r, size, grip, style) = scenario;
                var sample = new Sample();
                yield return Measure(SimHarness.Custom(l, size, grip, style: style), SimHarness.Custom(r, size, grip, style: style), seed, enabled, sample);
                (enabled ? tuned : baseline).Add(sample);
                csv.AppendLine(FormattableString.Invariant($"{l}/{r},{size},{grip},{style},{seed},{enabled},{sample.elapsed:F2},{sample.firstActive:F2},{sample.longestGap:F2},{sample.quiet:F2},{sample.recovery:F2},{sample.starts},{sample.hits},{sample.damage:F2},{sample.maxGrip:F3}"));
            }
            File.WriteAllText(Path.Combine(SimHarness.ReportDir, "heavy-tempo-comparison.csv"), csv.ToString());
            float Rate(List<Sample> samples, Func<Sample, float> metric) => samples.Sum(metric) / samples.Sum(s => s.elapsed);
            Debug.Log(FormattableString.Invariant($"[HEAVY TEMPO] quiet={Rate(baseline, s => s.quiet):F3}->{Rate(tuned, s => s.quiet):F3} recovery={Rate(baseline, s => s.recovery):F3}->{Rate(tuned, s => s.recovery):F3} starts/min={60 * Rate(baseline, s => s.starts):F2}->{60 * Rate(tuned, s => s.starts):F2} firstActive={baseline.Average(s => s.firstActive):F2}->{tuned.Average(s => s.firstActive):F2}"));
            Assert.Less(Rate(tuned, s => s.recovery), Rate(baseline, s => s.recovery) * .85f, "重量級の硬直時間を減らす。");
            Assert.Greater(Rate(tuned, s => s.starts), Rate(baseline, s => s.starts) * 1.1f, "重量級同士の攻防を増やす。");
            Assert.Less(Rate(tuned, s => s.quiet), Rate(baseline, s => s.quiet), "両者ともほぼ動かず待機・ガード・硬直している時間を減らす。");
            Assert.IsTrue(tuned.All(s => s.starts > 0 && s.hits > 0 && s.firstActive > 0), "物理的な振りと命中が成立する。");
        }

        [UnityTest]
        public IEnumerator LightDuel_KeepsSameTrace_WithTempoEnabledOrDisabled()
        {
            SimHarness.Begin(false);
            var before = new Sample(); var after = new Sample();
            var loadout = SimHarness.Custom("一", WeaponSize.S);
            yield return Measure(loadout, loadout, 902, false, before, 12f, false);
            yield return Measure(loadout, loadout, 902, true, after, 12f, false);
            Assert.AreEqual(before.trace, after.trace, "軽量級の動き・乱数・命中を変えない。");
        }

        [UnityTest]
        public IEnumerator HeavyDuel_ReplaysSameSeed_AfterDifferentHistory()
        {
            SimHarness.Begin(false);
            var first = new Sample(); var other = new Sample(); var replay = new Sample();
            var loadout = SimHarness.Custom("鬱", WeaponSize.XL, GripType.TwoHanded);
            yield return Measure(loadout, loadout, 739, true, first, 12f);
            yield return Measure(loadout, loadout, 1518, true, other, 12f);
            yield return Measure(loadout, loadout, 739, true, replay, 12f);
            Assert.AreEqual(first.trace, replay.trace);
            Assert.AreNotEqual(first.trace, other.trace);
        }
    }
}
