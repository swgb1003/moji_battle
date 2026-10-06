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
    public class BattlePlayModeTests
    {
        BattleInstance battle;

        [TearDown]
        public void TearDown()
        {
            battle?.Destroy();
            battle = null;
            SimHarness.End();
        }

        /// <summary>P0: 武器が手から離れず振れ、双方が倒れず移動できる。</summary>
        [UnityTest]
        public IEnumerator P0_WeaponStaysInHand_FightersMoveUpright()
        {
            SimHarness.Begin(false);
            battle = SimHarness.Build(3);
            var fs = new[] { battle.Left, battle.Right };
            var startX = fs.Select(f => f.X).ToArray();
            float maxGripError = 0f, maxUprightTilt = 0f;
            string gripInfo = "";
            var minPsi = new[] { 999f, 999f };
            var maxPsi = new[] { -999f, -999f };
            var maxTravel = new float[2];
            TimeController.SetSpectatorSpeed(1f);
            int lastStep = -1;
            while (battle.Director.Clock < 12f && !battle.Director.Ended)
            {
                yield return null;
                if (battle.Director.StepCount == lastStep) continue;
                lastStep = battle.Director.StepCount;
                for (int i = 0; i < 2; i++)
                {
                    var f = fs[i];
                    Vector2 shoulder = f.Body.GetRelativePoint(f.HandLocal(f.Facing)); // 手（刺す・足払いで動く接続点）
                    float ge = f.Runtime.weaponDetached ? 0f : Vector2.Distance(shoulder, f.WeaponBody.position); // 投げて手を離れている間は対象外
                    if (ge > maxGripError) { maxGripError = ge; gripInfo = $"{f.Loadout.grapheme} t={battle.Director.Clock:F2} state={f.Runtime.state} style={f.Runtime.attackStyle} hand={f.Runtime.handOffset} anchor={f.Hinge.connectedAnchor} wlocal={f.Body.GetPoint(f.WeaponBody.position)} yaw={f.Runtime.sweepYaw:F0} limp={battle.Context.SimTime < f.Runtime.weaponLimpUntil} pass={f.Runtime.weaponPassThrough} opp={f.Opponent.Runtime.state} d={f.DistanceToOpponent:F2} wv={f.WeaponBody.linearVelocity.magnitude:F1} bv={f.Body.linearVelocity.magnitude:F1}"; }
                    if (!f.Runtime.IsDown && f.Runtime.state != FighterState.Recover)
                        maxUprightTilt = Mathf.Max(maxUprightTilt, Mathf.Abs(Mathf.DeltaAngle(0f, f.Body.rotation)));
                    float psi = f.WeaponMotor.MeasurePsi();
                    minPsi[i] = Mathf.Min(minPsi[i], psi);
                    maxPsi[i] = Mathf.Max(maxPsi[i], psi);
                    maxTravel[i] = Mathf.Max(maxTravel[i], Mathf.Abs(f.X - startX[i]));
                }
            }
            Debug.Log($"[P0] gripAt=({gripInfo}) maxGripError={maxGripError:F3} tilt={maxUprightTilt:F2} psiRange L={maxPsi[0] - minPsi[0]:F0} R={maxPsi[1] - minPsi[1]:F0} travel L={maxTravel[0]:F1} R={maxTravel[1]:F1}");
            Assert.Less(maxGripError, 0.2f, "武器が手（ヒンジ位置）から離れない（重い衝突時のジョイントの伸び 0.2 未満）");
            Assert.Less(maxUprightTilt, 1f, "通常状態では倒れない");
            Assert.Greater(maxPsi[0] - minPsi[0], 60f, "一 が振れている");
            Assert.Greater(maxPsi[1] - minPsi[1], 60f, "鬱 が振れている");
            Assert.Greater(maxTravel[0], 1.5f, "左が移動");
            Assert.Greater(maxTravel[1], 1.0f, "右が移動");
        }

        [System.Serializable]
        class SweepReport
        {
            public int matches, winsLeft, winsRight, draws, koFinishes, timeUps;
            public int duplicateBodyHits, envOutsideLaunchWindow, envDamageEvents;
            public float maxLongestNoDamage, maxLongestIdle;
            public float[] avgAttacks = new float[2], avgHits = new float[2], avgGuardEvents = new float[2], avgEvades = new float[2];
            public float[] avgKnockdowns = new float[2], avgDamageDealt = new float[2], avgHitImpulse = new float[2], avgWindup = new float[2];
            public float[] guardTimeFraction = new float[2], approachTimeFraction = new float[2], attackTimeFraction = new float[2];
            public float[] avgCriticals = new float[2], avgGrazes = new float[2], avgHopOvers = new float[2], avgMaxCombo = new float[2];
            public float[] maxKnockbackDistance = new float[2];
            public float[] guardEntriesPerAttack = new float[2], attacksPerSequence = new float[2], jammedSwingRate = new float[2];
            public float recoverSettleMin = 99f, recoverSettleMax = -1f;
            public int recoverCount, recoverTimeouts, safeShifts;
            public float avgScriptStepMs, maxScriptStepMs;
            public List<MatchTelemetry> perMatch = new List<MatchTelemetry>();
        }

        /// <summary>最小検証ケース「一 vs 鬱、同一字体、seedを20件程度変更」。</summary>
        [UnityTest]
        [Timeout(1800000)]
        public IEnumerator P1_OneVsUtsu_SeedSweep()
        {
            SimHarness.Begin(false);
            int seeds = int.TryParse(System.Environment.GetEnvironmentVariable("SWEEP_SEEDS"), out var n) ? n : 20;
            var rep = new SweepReport();
            float stepMsSum = 0f;
            int stepCount = 0;
            for (int s = 1; s <= seeds; s++)
            {
                battle = SimHarness.Build(1000 + s * 37);
                yield return SimHarness.RunToEnd(battle, 10f);
                Assert.IsTrue(battle.Director.Ended, $"seed {1000 + s * 37} が終了しない");
                var t = battle.Director.Telemetry;
                rep.perMatch.Add(t);
                stepMsSum += t.totalStepMilliseconds;
                stepCount += t.steps;
                rep.maxScriptStepMs = Mathf.Max(rep.maxScriptStepMs, t.maxStepMilliseconds);
                battle.Destroy();
                battle = null;
                yield return null;
            }

            rep.matches = seeds;
            foreach (var t in rep.perMatch)
            {
                if (t.winner == 0) rep.winsLeft++;
                else if (t.winner == 1) rep.winsRight++;
                else rep.draws++;
                if (t.finishReason.Contains("KO")) rep.koFinishes++; else rep.timeUps++;
                rep.duplicateBodyHits += t.duplicateBodyHits;
                rep.envOutsideLaunchWindow += t.envDamageOutsideLaunchWindow;
                rep.envDamageEvents += t.envDamageEvents;
                rep.maxLongestNoDamage = Mathf.Max(rep.maxLongestNoDamage, t.longestNoDamageSeconds);
                rep.maxLongestIdle = Mathf.Max(rep.maxLongestIdle, t.longestIdleSeconds);
                for (int i = 0; i < 2; i++)
                {
                    var f = t.fighters[i];
                    rep.avgAttacks[i] += f.attackStarts / (float)seeds;
                    rep.avgGuardEvents[i] += f.guardEvents / (float)seeds;
                    rep.avgEvades[i] += f.evadeEvents / (float)seeds;
                    rep.avgHits[i] += f.hitDamages.Count / (float)seeds;
                    rep.avgDamageDealt[i] += f.hitDamages.Sum() / seeds;
                    rep.avgMaxCombo[i] += f.maxComboHits / (float)seeds;
                    rep.guardEntriesPerAttack[i] += f.guardEntries / (float)Mathf.Max(1, f.attackSequences) / seeds;
                    rep.attacksPerSequence[i] += f.attackStarts / (float)Mathf.Max(1, f.attackSequences) / seeds;
                    rep.jammedSwingRate[i] += f.jammedSwings / (float)Mathf.Max(1, f.swings) / seeds;
                    float total = f.stateSeconds.Sum();
                    rep.guardTimeFraction[i] += f.stateSeconds[(int)FighterState.Guard] / total / seeds;
                    rep.approachTimeFraction[i] += f.stateSeconds[(int)FighterState.Approach] / total / seeds;
                    rep.attackTimeFraction[i] += (f.stateSeconds[(int)FighterState.AttackWindup] + f.stateSeconds[(int)FighterState.AttackActive]
                                                  + f.stateSeconds[(int)FighterState.AttackRecovery]) / total / seeds;
                    foreach (var r in f.recoverSinceSettle)
                    {
                        rep.recoverSettleMin = Mathf.Min(rep.recoverSettleMin, r);
                        rep.recoverSettleMax = Mathf.Max(rep.recoverSettleMax, r);
                    }
                    rep.recoverCount += f.recoverSinceKnockdown.Count;
                    rep.recoverTimeouts += f.recoverSinceKnockdown.Count - f.recoverSinceSettle.Count;
                    rep.safeShifts += f.safeShifts;
                }
            }
            var allImp = new[] { new List<float>(), new List<float>() };
            var allWind = new[] { new List<float>(), new List<float>() };
            for (int i = 0; i < 2; i++)
            {
                foreach (var t in rep.perMatch)
                {
                    allImp[i].AddRange(t.fighters[i].hitImpulses);
                    allWind[i].AddRange(t.fighters[i].windups);
                }
                rep.avgHitImpulse[i] = allImp[i].Count > 0 ? allImp[i].Average() : 0f;
                rep.avgWindup[i] = allWind[i].Count > 0 ? allWind[i].Average() : 0f;
            }
            // 結果メトリクスは MatchResult 側から
            rep.avgScriptStepMs = stepCount > 0 ? stepMsSum / stepCount : 0f;
            WriteReport(rep);

            var L = 0; var R = 1;
            // 両者が seed により勝ち得る
            Assert.GreaterOrEqual(rep.winsLeft, 1, "一 が1回も勝てない");
            Assert.GreaterOrEqual(rep.winsRight, 1, "鬱 が1回も勝てない");
            // 二重ダメージなし・環境ダメージは吹っ飛び後のみ
            Assert.AreEqual(0, rep.duplicateBodyHits, "同一スイングの二重本体ダメージ");
            Assert.AreEqual(0, rep.envOutsideLaunchWindow, "吹っ飛び外の環境ダメージ");
            // 無限膠着がない: 両者が攻撃を出さない時間が 10 秒未満。
            // （ダメージが動かない時間は、回避・ガードが続く攻防でも伸びるため判定に使わずレポートにのみ出す）
            Assert.Less(rep.maxLongestIdle, 10f, "両者が10秒以上攻撃しない膠着がある");
            // 一の勝率の目安 10〜40%（品質確認の目標。勝率を固定する処理は作らない）
            float oneRate = rep.winsLeft / (float)rep.matches;
            Assert.That(oneRate, Is.InRange(0.05f, 0.45f), $"一の勝率 {oneRate:P0} が目安から大きく外れている");
            // 接近・攻撃・ガード・回避をそれぞれ行う
            for (int i = 0; i < 2; i++)
            {
                Assert.Greater(rep.approachTimeFraction[i], 0f);
                Assert.Greater(rep.avgAttacks[i], 0f, $"{i} 攻撃なし");
                Assert.Greater(rep.guardTimeFraction[i], 0f, $"{i} ガードなし");
                Assert.Greater(rep.avgEvades[i], 0f, $"{i} 回避なし");
            }
            // 軽量・重量で行動傾向が違う / 一は軽快、鬱は長い溜めと強い衝撃
            // 仕様 7.1「軽量: 攻撃/離脱が多い」
            Assert.Greater(rep.avgAttacks[L], rep.avgAttacks[R], "一の方が攻撃回数が多い");
            Assert.Greater(rep.avgAttacks[L] + rep.avgEvades[L], (rep.avgAttacks[R] + rep.avgEvades[R]) * 1.5f, "一の方が攻撃+離脱が明確に多い");
            Assert.Greater(rep.avgEvades[L], rep.avgEvades[R], "一の方が回避が多い");
            // 重量: 待機/ガード/一撃 — 攻撃判断1回あたりのガード回数が多く、連撃しない
            Assert.Greater(rep.guardEntriesPerAttack[R], rep.guardEntriesPerAttack[L], "鬱の方が（攻撃に対して）ガードを選びやすい");
            // 軽量: 連撃・離脱 — 1回の攻撃判断で複数回打つ
            Assert.Greater(rep.attacksPerSequence[L], 1.4f, "一は連撃する");
            Assert.Less(rep.attacksPerSequence[R], 1.2f, "鬱は単発");
            Assert.Greater(rep.avgWindup[R], rep.avgWindup[L] * 2.5f, "鬱の溜めが長い");
            Assert.Greater(rep.avgHitImpulse[R], rep.avgHitImpulse[L] * 2f, "鬱の衝撃が強い");
            // 転倒から 1〜2 秒で復帰（静止確認から）
            Assert.Greater(rep.recoverCount, 0, "転倒が一度も起きない");
            Assert.GreaterOrEqual(rep.recoverSettleMin, 0.99f);
            Assert.LessOrEqual(rep.recoverSettleMax, 2.01f);
            Assert.Greater(rep.koFinishes, 0, "KO決着がない");
        }

        static void WriteReport(SweepReport rep)
        {
            var dir = SimHarness.ReportDir;
            File.WriteAllText(Path.Combine(dir, "p1_seed_sweep.json"), JsonUtility.ToJson(rep, true), Encoding.UTF8);
            var sb = new StringBuilder();
            sb.AppendLine("# P1 一 vs 鬱 seed sweep (Gothic, provisional glyph masks)");
            sb.AppendLine();
            sb.AppendLine($"- matches: {rep.matches} / 一 wins {rep.winsLeft} / 鬱 wins {rep.winsRight} / draws {rep.draws} (一 win rate {rep.winsLeft * 100f / rep.matches:F0}%)");
            sb.AppendLine($"- finishes: KO {rep.koFinishes} / time up {rep.timeUps}");
            sb.AppendLine($"- duplicate body hits: {rep.duplicateBodyHits} / env damage events {rep.envDamageEvents} (outside launch window {rep.envOutsideLaunchWindow})");
            sb.AppendLine($"- longest gap with no attack started by either side (stalemate metric, max over matches): {rep.maxLongestIdle:F1}s");
            sb.AppendLine($"- longest no-damage gap (max over matches): {rep.maxLongestNoDamage:F1}s");
            sb.AppendLine($"- knockdown recoveries: {rep.recoverCount} (settle→stand {rep.recoverSettleMin:F2}–{rep.recoverSettleMax:F2}s, timeouts {rep.recoverTimeouts}, safe shifts {rep.safeShifts})");
            sb.AppendLine($"- script cost per fixed step: avg {rep.avgScriptStepMs:F3} ms / max {rep.maxScriptStepMs:F2} ms");
            sb.AppendLine();
            sb.AppendLine("| metric (avg per match) | 一 (left) | 鬱 (right) |");
            sb.AppendLine("|---|---|---|");
            void Row(string n, float[] v, string fmt = "F1") => sb.AppendLine($"| {n} | {v[0].ToString(fmt)} | {v[1].ToString(fmt)} |");
            Row("attacks started", rep.avgAttacks);
            Row("body hits landed", rep.avgHits);
            Row("damage dealt by hits", rep.avgDamageDealt);
            Row("successful guards", rep.avgGuardEvents);
            Row("evades", rep.avgEvades);
            Row("max combo hits", rep.avgMaxCombo);
            Row("avg windup (s)", rep.avgWindup, "F2");
            Row("avg hit impulse", rep.avgHitImpulse, "F2");
            Row("guard time fraction", rep.guardTimeFraction, "P1");
            Row("guard entries per attack sequence", rep.guardEntriesPerAttack, "F2");
            Row("attacks per sequence (combo length)", rep.attacksPerSequence, "F2");
            Row("jammed swings (moved < 40% of planned arc)", rep.jammedSwingRate, "P1");
            Row("approach/hold time fraction", rep.approachTimeFraction, "P1");
            Row("attack time fraction", rep.attackTimeFraction, "P1");
            sb.AppendLine();
            sb.AppendLine("| seed | result | time | HP 一 | HP 鬱 | hits 一/鬱 | guards 一/鬱 | evades 一/鬱 | env | no-dmg gap | idle gap |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (var t in rep.perMatch)
            {
                string w = t.winner == 0 ? "一" : t.winner == 1 ? "鬱" : "draw";
                sb.AppendLine($"| {t.seed} | {w} {t.finishReason} | {t.elapsed:F1} | {t.hpRemaining[0]:F0} | {t.hpRemaining[1]:F0} | " +
                              $"{t.fighters[0].hitDamages.Count}/{t.fighters[1].hitDamages.Count} | {t.fighters[0].guardEvents}/{t.fighters[1].guardEvents} | " +
                              $"{t.fighters[0].evadeEvents}/{t.fighters[1].evadeEvents} | {t.envDamageEvents} | {t.longestNoDamageSeconds:F1} | {t.longestIdleSeconds:F1} |");
            }
            File.WriteAllText(Path.Combine(dir, "p1_seed_sweep.md"), sb.ToString(), Encoding.UTF8);
            Debug.Log("[SWEEP]\n" + sb);
        }

        /// <summary>停止・観戦速度変更は戦闘AIへの操作にならず、解除後に物理時間が基準へ戻る。</summary>
        [UnityTest]
        [Timeout(600000)]
        public IEnumerator SpectatorControls_DoNotAffectOutcome()
        {
            SimHarness.Begin(false);
            const int seed = 4242;
            battle = SimHarness.Build(seed);
            yield return SimHarness.RunToEnd(battle, 1f);
            var a = battle.Director.Result;
            int stepsA = battle.Director.StepCount;
            battle.Destroy();
            yield return null;

            battle = SimHarness.Build(seed);
            TimeController.SetSpectatorSpeed(1f);
            int frame = 0;
            while (!battle.Director.Ended && frame < 100000)
            {
                frame++;
                if (frame == 120) TimeController.SetPaused(true);
                if (frame == 180) { Assert.AreEqual(0f, Time.timeScale); TimeController.SetPaused(false); }
                if (frame == 200) TimeController.SetSpectatorSpeed(0.5f);
                if (frame == 400) TimeController.SetSpectatorSpeed(2f);
                if (frame == 700) TimeController.SetPaused(true);
                if (frame == 705) TimeController.SetPaused(false);
                if (frame == 900) TimeController.SetSpectatorSpeed(1f);
                yield return null;
            }
            var b = battle.Director.Result;
            Debug.Log($"[DETERMINISM] A: {a.finishReason} w={a.winner} t={a.elapsedSeconds:F2} hp={a.hpRemaining[0]:F2}/{a.hpRemaining[1]:F2} | " +
                      $"B: {b.finishReason} w={b.winner} t={b.elapsedSeconds:F2} hp={b.hpRemaining[0]:F2}/{b.hpRemaining[1]:F2}");
            Assert.AreEqual(a.winner, b.winner);
            Assert.AreEqual(a.finishReason, b.finishReason);
            Assert.AreEqual(a.elapsedSeconds, b.elapsedSeconds, 1e-3f);
            Assert.AreEqual(a.hpRemaining[0], b.hpRemaining[0], 1e-2f);
            Assert.AreEqual(a.hpRemaining[1], b.hpRemaining[1], 1e-2f);
            Assert.AreEqual(a.metrics[0].hitsLanded, b.metrics[0].hitsLanded);
            Assert.AreEqual(a.metrics[1].hitsLanded, b.metrics[1].hitsLanded);
            Assert.AreEqual(TimeController.BaseFixedDelta, Time.fixedDeltaTime, 1e-6f);
            // 試合が早く決着すると観戦速度の操作の途中で終わるため、時間倍率が観戦速度どおりであることだけを見る
            Assert.AreEqual(TimeController.SpectatorSpeed, Time.timeScale, 1e-6f);
        }

        /// <summary>KO 時は timeScale=0.25 で約0.7秒の演出、解除後は基準値へ戻る。</summary>
        [UnityTest]
        [Timeout(900000)]
        public IEnumerator KoSlowMotion_ThenTimeRestored()
        {
            SimHarness.Begin(true);
            bool sawKo = false;
            for (int s = 0; s < 12 && !sawKo; s++)
            {
                battle = SimHarness.Build(500 + s);
                TimeController.SetSpectatorSpeed(2f);
                float minScale = 99f;
                int koFrames = 0;
                while (!battle.Director.Ended)
                {
                    yield return null;
                    if (battle.Director.KoInProgress && !battle.Director.Ended && !TimeController.HitStopActive)
                    {
                        minScale = Mathf.Min(minScale, Time.timeScale);
                        koFrames++;
                        Assert.AreEqual(TimeController.BaseFixedDelta, Time.fixedDeltaTime, 1e-6f);
                    }
                }
                if (battle.Director.Result.finishReason.Contains("KO"))
                {
                    sawKo = true;
                    Debug.Log($"[KO] seed={500 + s} minScale={minScale:F3} koFrames={koFrames}");
                    Assert.AreEqual(0.25f * 2f, minScale, 1e-3f, "KO スロー (×観戦速度)");
                    Assert.Greater(koFrames, 0);
                    Assert.AreEqual(2f, Time.timeScale, 1e-4f, "解除後は観戦速度へ戻る");
                    Assert.AreEqual(TimeController.BaseFixedDelta, Time.fixedDeltaTime, 1e-6f);
                }
                battle.Destroy();
                battle = null;
                yield return null;
            }
            Assert.IsTrue(sawKo, "KO 決着の試合が見つからない");
        }

        /// <summary>投げ: 離れた相手へ武器を投げて当てる。落ちた武器は拾うまで手に戻らず、拾えば握りへ戻る。</summary>
        [UnityTest]
        public IEnumerator Throw_HitsAtRange_WeaponReturnsOnlyWhenPickedUp()
        {
            SimHarness.Begin(false);
            battle = SimHarness.Build(5, countdown: 1000f); // カウントダウン中は AI 停止
            var l = battle.Left;
            var r = battle.Right;
            l.Body.position = new Vector2(r.X - 4.5f, 0.02f);
            l.WeaponMotor.ResetPose();
            yield return null;
            HitEvent? hit = null;
            battle.Context.Events.Hit += e => { if (e.attacker == 0 && e.style == AttackStyle.Throw) hit = e; };
            TimeController.SetSpectatorSpeed(1f);
            float hpBefore = r.Runtime.hp;
            l.StartAttack(battle.Context.SimTime, AttackStyle.Throw);
            int frames = 0;
            while (hit == null && frames++ < 400) yield return null;
            Assert.IsNotNull(hit, "投げた武器が相手に当たらない");
            Debug.Log($"[THROW] {l.Loadout.grapheme} dmg={hit.Value.damage:F1} vN={hit.Value.vN:F1} part={hit.Value.part} at d={Mathf.Abs(hit.Value.point.x - l.X):F2}");
            Assert.Less(r.Runtime.hp, hpBefore);
            Assert.IsTrue(l.Runtime.weaponDetached);
            Assert.IsFalse(l.Hinge.enabled, "投げた武器はヒンジから外れている");

            frames = 0;
            while (l.Runtime.throwLive && frames++ < 400) yield return null;
            for (int i = 0; i < 90; i++) yield return null;
            Assert.IsTrue(l.Runtime.weaponDetached, "拾うまでは手に戻らない");
            Assert.Greater(Vector2.Distance(l.WeaponBody.position, l.Body.GetRelativePoint(l.HandLocal(l.Facing))), 1f, "武器は手元から離れた所に落ちている");

            // 落ちた武器の所へ立つと拾う（相手は離れた所へ移す）
            Vector2 w = l.WeaponBody.worldCenterOfMass;
            float half = battle.Context.ArenaHalfWidth;
            float wx = Mathf.Clamp(w.x, -half + 0.5f, half - 0.5f);
            if (Mathf.Abs(r.X - wx) < 1.5f)
            {
                r.Body.position = new Vector2(wx > 0f ? wx - 3f : wx + 3f, 0.02f);
                r.Body.linearVelocity = Vector2.zero;
            }
            l.Body.position = new Vector2(wx, 0.02f);
            l.Body.linearVelocity = Vector2.zero;
            frames = 0;
            while (l.Runtime.weaponDetached && frames++ < 120) yield return null;
            Assert.IsFalse(l.Runtime.weaponDetached, "落ちた武器の所へ行っても拾わない");
            Assert.IsTrue(l.Hinge.enabled);
            for (int i = 0; i < 30; i++) yield return null;
            float gripError = Vector2.Distance(l.Body.GetRelativePoint(l.HandLocal(l.Facing)), l.WeaponBody.position);
            Assert.Less(gripError, 0.2f, "拾った武器が握りに戻る");
        }

        /// <summary>壁際で転倒 → 安全位置へ小さく移して 1〜2 秒で復帰し、姿勢が破綻しない。</summary>
        [UnityTest]
        public IEnumerator WallSideKnockdown_RecoversSafely()
        {
            SimHarness.Begin(false);
            battle = SimHarness.Build(77, countdown: 1000f); // カウントダウン中は AI 停止
            var f = battle.Left;
            float half = battle.Context.ArenaHalfWidth;
            f.Body.position = new Vector2(-half + 0.25f, 0.02f);
            f.WeaponMotor.ResetPose();
            yield return null;
            RecoverEvent? rec = null;
            battle.Context.Events.Recovered += e => { if (e.fighter == 0) rec = e; };
            f.Knockdown.EnterKnockdown(battle.Context.SimTime);
            f.Body.AddForceAtPosition(new Vector2(-6f, 3f), f.HeadWorld, ForceMode2D.Impulse);
            TimeController.SetSpectatorSpeed(1f);
            int frames = 0;
            while (rec == null && frames++ < 400) yield return null;
            Assert.IsNotNull(rec, "復帰しない");
            var r = rec.Value;
            Debug.Log($"[WALL] settle+{r.sinceSettle:F2}s down+{r.sinceKnockdown:F2}s shifted={r.shiftedToSafePosition} x={f.X:F2}");
            Assert.That(r.sinceSettle, Is.InRange(1.0f, 2.0f));
            for (int i = 0; i < 30; i++) yield return null;
            Assert.AreEqual(0f, f.Body.rotation, 0.01f);
            Assert.GreaterOrEqual(f.X, -half + 0.2f, "壁にめり込まない");
            Assert.Less(Mathf.Abs(f.Body.position.y), 0.2f, "地面に立っている");
            Assert.AreEqual(FighterState.Approach, f.Runtime.state);
        }

        /// <summary>
        /// 叩きつけ: 相手の武器で高く持ち上げられてから落ちるとダメージ。同じ高さから自分で落ちただけならダメージなし。
        /// 持ち上げ状態は実戦と同じ判定（相手の武器との接触）で作るため、相手の武器を体の真下へ置いて上へ押し上げる。
        /// </summary>
        [UnityTest]
        public IEnumerator LiftedAndDropped_TakesSlamDamage_PlainFallDoesNot()
        {
            SimHarness.Begin(false);
            float[] dmg = new float[2];
            int[] slams = new int[2];
            for (int run = 0; run < 2; run++)
            {
                bool lift = run == 0;
                battle = SimHarness.Build(21, countdown: 1000f); // AI 停止
                // この確認は武器ごと突き上げる状況を作るため、武器どうしを常に衝突させる（接触ゲートを開ける）
                CombatBalance.Default.weaponsCollideOnlyWhenEngaged = false;
                battle.Context.ApplyWeaponGate();
                var victim = battle.Left;
                var lifter = battle.Right;
                TimeController.SetSpectatorSpeed(1f);
                for (int i = 0; i < 10; i++) yield return null;
                float hp0 = victim.Runtime.hp;
                if (lift)
                {
                    // 相手の武器を被害者の足元に置き、両方を上へ速く持ち上げてから離す（武器ごと突き上げられた状態）
                    victim.Body.position = new Vector2(0f, 0.6f);
                    lifter.Body.position = new Vector2(2.5f, 0.02f);
                    lifter.WeaponBody.position = new Vector2(0f, 0.2f);
                    victim.AddVelocity(new Vector2(0f, 9f) - victim.Body.linearVelocity);
                    lifter.WeaponBody.linearVelocity = new Vector2(0f, 9f);
                    for (int i = 0; i < 3; i++) yield return null;
                }
                else
                {
                    victim.Body.position = new Vector2(0f, 4f);
                    victim.WeaponMotor.ResetPose();
                }
                for (int i = 0; i < 150; i++) yield return null;
                dmg[run] = hp0 - victim.Runtime.hp;
                slams[run] = victim.Runtime.metrics.slamsTaken;
                battle.Destroy();
                battle = null;
                CombatBalance.Default.weaponsCollideOnlyWhenEngaged = true;
                yield return null;
            }
            Debug.Log($"[SLAM] lifted: dmg={dmg[0]:F1} slams={slams[0]} / plain fall: dmg={dmg[1]:F1} slams={slams[1]}");
            Assert.AreEqual(0f, dmg[1], 1e-4f, "持ち上げられずに落ちただけでダメージが入った");
            Assert.AreEqual(0, slams[1]);
            Assert.AreEqual(1, slams[0], "持ち上げられて落ちたのに叩きつけにならない");
            Assert.Greater(dmg[0], 0f);
        }

        [UnityTest]
        public IEnumerator TimeUp_DecidedByHpRatio()
        {
            SimHarness.Begin(false);
            battle = SimHarness.Build(9, duration: 4f);
            yield return SimHarness.RunToEnd(battle, 4f);
            var r = battle.Director.Result;
            Assert.IsTrue(r.finishReason.StartsWith("TIME_UP") || r.finishReason.StartsWith("SUDDEN_DEATH") || r.finishReason.Contains("KO"), r.finishReason);
            if (r.finishReason.StartsWith("SUDDEN_DEATH"))
            {
                // 延長戦は 4 秒の時点で同率だった時だけ。最初のダメージか上限 30 秒で決着
                Assert.Greater(r.elapsedSeconds, 4f - 0.03f);
                Assert.LessOrEqual(r.elapsedSeconds, 4f + CombatBalance.Default.suddenDeathMaxSeconds + 0.05f);
                if (r.finishReason == "SUDDEN_DEATH") Assert.GreaterOrEqual(r.winner, 0);
            }
            if (r.finishReason.StartsWith("TIME_UP"))
            {
                Assert.AreEqual(4f, r.elapsedSeconds, 0.03f);
                int expected = MatchRules.DecideTimeUp(r.hpRemaining[0], r.maxHp[0], r.hpRemaining[1], r.maxHp[1]);
                Assert.AreEqual(expected, r.winner);
            }
        }

        /// <summary>延長戦: 時間切れで両者ノーダメージなら延長し、最初のダメージで決着する。</summary>
        [UnityTest]
        public IEnumerator EvenTimeUp_GoesToSuddenDeath()
        {
            SimHarness.Begin(false);
            battle = SimHarness.Build(11, duration: 0.5f);
            yield return SimHarness.RunToEnd(battle, 4f);
            var r = battle.Director.Result;
            Assert.IsTrue(battle.Director.Overtime, "0.5 秒で両者ノーダメージなのに延長戦にならない");
            StringAssert.StartsWith("SUDDEN_DEATH", r.finishReason);
            if (r.finishReason == "SUDDEN_DEATH")
            {
                Assert.GreaterOrEqual(r.winner, 0);
                Assert.IsTrue(r.hpRemaining[0] < r.maxHp[0] || r.hpRemaining[1] < r.maxHp[1], "ダメージなしで決着している");
            }
        }

        [UnityTest]
        public IEnumerator SimultaneousKo_IsDraw()
        {
            SimHarness.Begin(false);
            battle = SimHarness.Build(10);
            TimeController.SetSpectatorSpeed(1f);
            for (int i = 0; i < 30; i++) yield return null;
            battle.Left.Runtime.hp = 0f;
            battle.Right.Runtime.hp = -2f;
            for (int i = 0; i < 10 && !battle.Director.Ended; i++) yield return null;
            Assert.IsTrue(battle.Director.Ended);
            Assert.AreEqual(-1, battle.Director.Result.winner);
            Assert.AreEqual("DOUBLE_KO", battle.Director.Result.finishReason);
        }
    }
}
