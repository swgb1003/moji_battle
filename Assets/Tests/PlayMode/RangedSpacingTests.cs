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
    public class RangedSpacingTests
    {
        BattleInstance battle;
        CombatBalance balance;
        sealed class Sample
        {
            public float elapsed, close, distance, retreats, quiet, damage, longestGap;
            public int starts, ranged, launches, hits, rangedHits, winner;
            public string trace;
        }
        [TearDown]
        public void Cleanup()
        {
            battle?.Destroy();
            if (balance != null) UnityEngine.Object.Destroy(balance);
            SimHarness.End();
        }

        IEnumerator Measure(FighterLoadout left, FighterLoadout right, int seed, Sample sample, float seconds = 60)
        {
            balance = UnityEngine.Object.Instantiate(CombatBalance.Default);
            battle = BattleBuilder.Build(new MatchConfig { left = left, right = right, seed = seed, durationSeconds = seconds },
                balance, GlyphCalibration.Default, false, 0, out var error);
            Assert.IsNotNull(battle, error);
            battle.Director.enabled = false;
            var trace = new StringBuilder();
            battle.Context.Events.StateChanged += e => trace.Append($"{e.fighter}:{(int)e.to}:{(int)battle.Director.Fighters[e.fighter].Runtime.attackStyle}:{e.time:F3};");
            for (int step = 0; step < (seconds + balance.suddenDeathMaxSeconds + 2) / TimeController.BaseFixedDelta && !battle.Context.MatchOver; step++)
            {
                battle.Director.SimulateStep();
                float dt = TimeController.BaseFixedDelta;
                float d = battle.Left.DistanceToOpponent;
                sample.distance += d * dt;
                if (d < 2f) sample.close += dt;
                foreach (var f in battle.Director.Fighters)
                    if (f.Runtime.state == FighterState.Approach && f.IsGrounded
                        && f.Body.linearVelocity.x * f.TowardOpponent < -.25f) sample.retreats += dt;
                if (battle.Director.Fighters.All(f => (f.Runtime.state == FighterState.Approach || f.Runtime.state == FighterState.Guard
                    || f.Runtime.state == FighterState.AttackRecovery) && Mathf.Abs(f.Body.linearVelocity.x) < .5f)) sample.quiet += dt;
                if (step % 100 == 0) yield return null;
            }
            Assert.IsTrue(battle.Director.Ended, "Spacing must not stop a match from finishing.");
            sample.elapsed = battle.Director.Clock;
            foreach (var t in battle.Director.Telemetry.fighters)
            {
                sample.starts += t.attackStarts;
                sample.ranged += t.styleStarts[(int)AttackStyle.StrokeShot] + t.styleStarts[(int)AttackStyle.GroundWave];
                sample.rangedHits += t.styleHits[(int)AttackStyle.StrokeShot] + t.styleHits[(int)AttackStyle.GroundWave];
            }
            sample.launches = battle.Context.Ranged.LaunchCount;
            sample.hits = battle.Left.Runtime.metrics.hitsLanded + battle.Right.Runtime.metrics.hitsLanded;
            sample.damage = battle.Left.Runtime.maxHp - battle.Left.Runtime.hp + battle.Right.Runtime.maxHp - battle.Right.Runtime.hp;
            sample.longestGap = battle.Director.Telemetry.longestIdleSeconds;
            sample.winner = battle.Director.Result.winner;
            sample.trace = trace + $"hp={battle.Left.Runtime.hp:F4}/{battle.Right.Runtime.hp:F4};launches={sample.launches}";
            battle.Destroy(); battle = null;
            UnityEngine.Object.Destroy(balance); balance = null;
            yield return null;
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator NaturalBattles_RecordSpacingAndAttackMix()
        {
            SimHarness.Begin(false);
            var samples = new List<Sample>();
            var csv = new StringBuilder("build,seed,seconds,mean_distance,close_seconds,retreat_seconds,quiet_seconds,starts,ranged_starts,launches,hits,ranged_hits,damage,longest_attack_gap,winner\n");
            foreach (var (name, left, right) in new[]
            {
                ("light_aggressive", SimHarness.Custom("一", WeaponSize.S), SimHarness.Custom("火", WeaponSize.S)),
                ("medium_aggressive", SimHarness.Custom("山"), SimHarness.Custom("口")),
                ("heavy_aggressive", SimHarness.Custom("鬱", WeaponSize.XL), SimHarness.Custom("龍", WeaponSize.XL)),
                ("mixed", SimHarness.Custom("一", WeaponSize.S), SimHarness.Custom("鬱", WeaponSize.L)),
                ("hit_and_away", SimHarness.Custom("火", style: BattleStyle.HitAndAway), SimHarness.Custom("口")),
                ("counter", SimHarness.Custom("山", style: BattleStyle.Counter), SimHarness.Custom("火")),
                ("defensive", SimHarness.Custom("口", grip: GripType.Horizontal, style: BattleStyle.Defensive), SimHarness.Custom("火")),
                ("tournament_S", SimHarness.Custom("山", WeaponSize.S, GripType.Reverse, .15f, BattleStyle.Counter),
                    SimHarness.Custom("鬱", WeaponSize.S, GripType.TwoHanded, .85f, BattleStyle.HitAndAway))
            })
            foreach (int seed in new[] { 739, 902, 1518, 4201 })
            {
                var sample = new Sample();
                yield return Measure(left, right, seed, sample);
                samples.Add(sample);
                csv.AppendLine(FormattableString.Invariant($"{name},{seed},{sample.elapsed:F2},{sample.distance / sample.elapsed:F3},{sample.close:F2},{sample.retreats:F2},{sample.quiet:F2},{sample.starts},{sample.ranged},{sample.launches},{sample.hits},{sample.rangedHits},{sample.damage:F2},{sample.longestGap:F2},{sample.winner}"));
            }
            File.WriteAllText(Path.Combine(SimHarness.ReportDir, "ranged-spacing-natural.csv"), csv.ToString());
            float elapsed = samples.Sum(s => s.elapsed);
            Debug.Log(FormattableString.Invariant($"[SPACING] matches={samples.Count} ranged_share={(float)samples.Sum(s => s.ranged) / samples.Sum(s => s.starts):F3} distance={samples.Sum(s => s.distance) / elapsed:F3} close={samples.Sum(s => s.close) / elapsed:F3} retreat={samples.Sum(s => s.retreats) / (2 * elapsed):F3} quiet={samples.Sum(s => s.quiet) / elapsed:F3} launches/min={60 * samples.Sum(s => s.launches) / elapsed:F2} hits/min={60 * samples.Sum(s => s.hits) / elapsed:F2} draws={samples.Count(s => s.winner < 0)}"));
            Assert.IsTrue(samples.All(s => s.starts > 0 && s.damage > 0));
            float rangedShare = (float)samples.Sum(s => s.ranged) / samples.Sum(s => s.starts);
            Assert.That(rangedShare, Is.InRange(.10f, .35f), "Ranged attacks should appear regularly while melee remains the majority.");
            Assert.Less(samples.Sum(s => s.close) / elapsed, .49f, "Reduce constant close-range clustering.");
            // Combat tempo deliberately shortens ordinary retreats (tempoRetreatScale), so the tempo threshold is slightly lower.
            Assert.Greater(samples.Sum(s => s.retreats) / (2 * elapsed), balance == null && CombatBalance.Default.combatTempo ? .06f : .065f, "Show actual movement away from opponents.");
            Assert.Less(samples.Sum(s => s.quiet) / elapsed, .12f, "Spacing must not turn into passive waiting.");
            Assert.Greater(60 * samples.Sum(s => s.hits) / elapsed, 11f, "Maintain the pace of landed attacks.");
            Assert.AreEqual(0, samples.Count(s => s.winner < 0), "Avoid repeated draws.");
        }

        [UnityTest]
        public IEnumerator SameSeed_ReplaysSpacingAfterDifferentMatch()
        {
            SimHarness.Begin(false);
            var left = SimHarness.Custom("火", style: BattleStyle.HitAndAway);
            var right = SimHarness.Custom("鬱", WeaponSize.L);
            var first = new Sample(); var other = new Sample(); var replay = new Sample();
            yield return Measure(left, right, 902, first, 20);
            yield return Measure(left, right, 1518, other, 20);
            yield return Measure(left, right, 902, replay, 20);
            Assert.AreEqual(first.trace, replay.trace);
            Assert.AreNotEqual(first.trace, other.trace);
            Assert.Greater(first.launches, 0);
        }

        void BuildClose(bool wave, bool corner = false)
        {
            balance = UnityEngine.Object.Instantiate(CombatBalance.Default);
            balance.expandedActions = false;
            balance.idleBreakSeconds = 1000;
            balance.rangedSpacingChance = balance.rangedSpacingNeutralChance = balance.rangedChance = 1;
            balance.rangedSpacingBackstepChance = 0;
            battle = BattleBuilder.Build(new MatchConfig
                { left = SimHarness.Custom(wave ? "鬱" : "一", wave ? WeaponSize.L : WeaponSize.S),
                    right = SimHarness.Custom("一", WeaponSize.S), seed = 739, durationSeconds = 60 }, balance, GlyphCalibration.Default, false, 0, out var error);
            Assert.IsNotNull(battle, error);
            battle.Director.enabled = false;
            foreach (var f in battle.Director.Fighters)
            {
                f.Runtime.attackReadyAt = f.Runtime.rangedReadyAt = f.Runtime.evadeReadyAt = 1000;
                f.SetTendency(new ClassTendency { attackWillingness = 0, reactionChance = 0, guardBias = 0,
                    proactiveGuard = 0, comboMin = 1, comboMax = 1 });
            }
            for (int i = 0; i < 30; i++) battle.Director.SimulateStep();
            float leftX = corner ? -balance.arenaHalfWidth + .7f : -1f;
            float d = corner ? battle.Left.AttackRange * .9f : 2f;
            foreach (var f in battle.Director.Fighters)
            {
                var shift = new Vector2((f.Id == 0 ? leftX : leftX + d) - f.X, 0);
                f.Body.position += shift; f.WeaponBody.position += shift;
                f.Body.linearVelocity = f.WeaponBody.linearVelocity = Vector2.zero;
            }
            battle.Right.WalkSpeed = 0;
            for (int i = 0; i < 5; i++) battle.Director.SimulateStep();
            battle.Left.Runtime.attackReadyAt = battle.Left.Runtime.rangedReadyAt = 0;
        }

        [UnityTest]
        public IEnumerator CloseFighter_CreatesDistanceAndReleasesBothKindsOfProjectile()
        {
            SimHarness.Begin(false);
            foreach (bool wave in new[] { false, true })
            {
                BuildClose(wave);
                float initial = battle.Left.DistanceToOpponent, launchDistance = 0;
                battle.Context.Events.RangedLaunch += e =>
                {
                    if (e.fighter == 0) launchDistance = battle.Left.DistanceToOpponent;
                };
                for (int i = 0; i < 350 && launchDistance == 0; i++)
                {
                    battle.Director.SimulateStep();
                    if (i % 50 == 0) yield return null;
                }
                Assert.Greater(battle.Left.Brain.RangedSpacingAttempts, 0);
                Assert.Greater(launchDistance, initial + .8f, "Walk out of melee before launching a projectile.");
                Assert.GreaterOrEqual(launchDistance, 2.8f);
                battle.Destroy(); battle = null; UnityEngine.Object.Destroy(balance); balance = null;
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator CorneredFighter_UsesMeleeInsteadOfBackingIntoWall()
        {
            SimHarness.Begin(false); BuildClose(false, true);
            battle.Left.SetTendency(new ClassTendency { attackWillingness = 1, reactionChance = 0,
                proactiveGuard = 0, comboMin = 1, comboMax = 1 });
            for (int i = 0; i < 50 && battle.Left.Runtime.attackSerial == 0; i++) battle.Director.SimulateStep();
            Assert.Greater(battle.Left.Runtime.attackSerial, 0);
            Assert.IsFalse(AttackTechniques.IsRanged(battle.Left.Runtime.attackStyle));
            Assert.AreEqual(0, battle.Left.Brain.RangedSpacingAttempts);
            yield break;
        }

        [UnityTest]
        public IEnumerator UnreachableSpacing_ExpiresWithoutRepeatingRetreat()
        {
            SimHarness.Begin(false); BuildClose(false);
            battle.Left.WalkSpeed = 0;
            for (int i = 0; i < 200; i++) battle.Director.SimulateStep();
            Assert.AreEqual(1, battle.Left.Brain.RangedSpacingAttempts, "Only one bounded attempt before the spacing cooldown.");
            Assert.IsFalse(battle.Left.Brain.LastDecision.StartsWith("遠距離攻撃"), "Resume normal fighting after the attempt expires.");
            yield break;
        }

        [UnityTest]
        public IEnumerator RangedCooldownOrDisabled_ContinuesMeleeWithoutSpacingWait()
        {
            SimHarness.Begin(false);
            foreach (bool disabled in new[] { false, true })
            {
                BuildClose(false);
                balance.rangedAttacks = !disabled;
                battle.Left.Runtime.rangedReadyAt = 1000;
                battle.Left.SetTendency(new ClassTendency { attackWillingness = 1, reactionChance = 0,
                    proactiveGuard = 0, comboMin = 1, comboMax = 1 });
                for (int i = 0; i < 150 && battle.Left.Runtime.attackSerial == 0; i++) battle.Director.SimulateStep();
                Assert.Greater(battle.Left.Runtime.attackSerial, 0, "Use an available melee attack rather than waiting for a projectile.");
                Assert.IsFalse(AttackTechniques.IsRanged(battle.Left.Runtime.attackStyle));
                Assert.AreEqual(0, battle.Left.Brain.RangedSpacingAttempts);
                battle.Destroy(); battle = null; UnityEngine.Object.Destroy(balance); balance = null;
                yield return null;
            }
        }
    }
}
