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
    /// <summary>P2 完了の目安「一/口/山/火/鬱/A/I/O/X が文字ごとに違う戦い方をする」を総当たりで確認する。</summary>
    public class RoundRobinTests
    {
        const string Nine = "一口山火鬱AIOX";

        class GlyphAgg
        {
            public string g;
            public string weightClass;
            public int matches, wins, kos, draws;
            public float whiffs, clashes, guardedByOpp, distAtActiveSum; public int distCount;
            public float attacks, evades, guards, hits, damage, impulseSum, windupSum, sequences, jammed, swings, hopOvers, grazes, envImpacts;
            public int impulseCount, windupCount;
            public float seconds;
        }

        [UnityTest]
        [Timeout(3600000)]
        public IEnumerator P2_NineGlyphRoundRobin()
        {
            SimHarness.Begin(false);
            int seedsPerPair = int.TryParse(System.Environment.GetEnvironmentVariable("RR_SEEDS"), out var n) ? n : 3;
            var agg = Nine.ToDictionary(c => c.ToString(), c => new GlyphAgg { g = c.ToString() });
            var pairLines = new List<string>();
            int dup = 0, envOutside = 0, unfinished = 0, draws = 0, zeroDamage = 0, koTotal = 0, total = 0, suddenDeaths = 0;
            float damageTotal = 0f;
            float maxIdle = 0f;
            string maxIdleCase = "";
            var special = new StringBuilder();
            int pairIndex = 0;
            for (int i = 0; i < Nine.Length; i++)
            for (int j = i + 1; j < Nine.Length; j++)
            {
                pairIndex++;
                int leftWins = 0, rightWins = 0;
                for (int s = 0; s < seedsPerPair; s++)
                {
                    // 左右の有利不利を打ち消すため seed ごとに左右を入れ替える
                    bool swap = s % 2 == 1;
                    string l = (swap ? Nine[j] : Nine[i]).ToString(), r = (swap ? Nine[i] : Nine[j]).ToString();
                    int seed = 5000 + pairIndex * 31 + s * 7;
                    var battle = SimHarness.Build(seed, left: l, right: r);
                    yield return SimHarness.RunToEnd(battle, 10f);
                    if (!battle.Director.Ended) unfinished++;
                    var t = battle.Director.Telemetry;
                    var res = battle.Director.Result;
                    dup += t.duplicateBodyHits;
                    total++;
                    if (res.winner < 0) draws++;
                    if (res.finishReason.StartsWith("SUDDEN_DEATH")) suddenDeaths++;
                    if (res.finishReason.Contains("KO")) koTotal++;
                    float dmgBoth = (res.maxHp[0] - res.hpRemaining[0]) + (res.maxHp[1] - res.hpRemaining[1]);
                    damageTotal += dmgBoth;
                    if (dmgBoth < 0.01f) { zeroDamage++; special.AppendLine($"- no damage: {l} vs {r} seed {seed} | attacks {t.fighters[0].attackStarts}/{t.fighters[1].attackStarts} clashes {battle.Left.Runtime.metrics.clashes}/{battle.Right.Runtime.metrics.clashes} whiffs {battle.Left.Runtime.metrics.attacksWhiffed}/{battle.Right.Runtime.metrics.attacksWhiffed} guards {battle.Left.Runtime.metrics.guards}/{battle.Right.Runtime.metrics.guards} evades {t.fighters[0].evadeEvents}/{t.fighters[1].evadeEvents}"); }
                    envOutside += t.envDamageOutsideLaunchWindow;
                    if (t.longestIdleSeconds > maxIdle) { maxIdle = t.longestIdleSeconds; maxIdleCase = $"{l} vs {r} seed {seed}"; }
                    for (int k = 0; k < 2; k++)
                    {
                        var a = agg[k == 0 ? l : r];
                        var f = t.fighters[k];
                        a.weightClass = f.weightClass;
                        a.matches++;
                        if (res.winner == k) { a.wins++; if (res.finishReason.Contains("KO")) a.kos++; }
                        if (res.winner < 0) a.draws++;
                        a.seconds += res.elapsedSeconds;
                        a.attacks += f.attackStarts;
                        a.evades += f.evadeEvents;
                        a.guards += f.guardEntries;
                        a.sequences += f.attackSequences;
                        a.hits += f.hitDamages.Count;
                        a.damage += f.hitDamages.Sum();
                        a.impulseSum += f.hitImpulses.Sum();
                        a.impulseCount += f.hitImpulses.Count;
                        a.windupSum += f.windups.Sum();
                        a.windupCount += f.windups.Count;
                        a.jammed += f.jammedSwings;
                        a.swings += f.swings;
                        a.grazes += battle.Director.Fighters[k].Runtime.metrics.grazes;
                        a.hopOvers += battle.Director.Fighters[k].Runtime.metrics.hopOvers;
                        a.envImpacts += battle.Director.Fighters[k].Opponent.Runtime.metrics.envImpacts;
                        var mk = battle.Director.Fighters[k].Runtime.metrics;
                        a.whiffs += mk.attacksWhiffed;
                        a.clashes += mk.clashes;
                        a.guardedByOpp += battle.Director.Fighters[k].Opponent.Runtime.metrics.guards;
                        a.distAtActiveSum += f.distAtActiveSum; a.distCount += f.distAtActiveCount;
                    }
                    if (res.winner == 0) { if (l == Nine[i].ToString()) leftWins++; else rightWins++; }
                    else if (res.winner == 1) { if (r == Nine[j].ToString()) rightWins++; else leftWins++; }
                    // 最小検証ケース
                    if ((l == "山" && r == "一") || (l == "一" && r == "山") || (l == "口" && r == "火") || (l == "火" && r == "口"))
                    {
                        int yamaOrKuchi = (l == "山" || l == "口") ? 0 : 1;
                        var m0 = battle.Director.Fighters[0].Runtime.metrics;
                        var m1 = battle.Director.Fighters[1].Runtime.metrics;
                        special.AppendLine($"- {l} vs {r} (seed {seed}): {res.finishReason} winner={(res.winner < 0 ? "draw" : res.winner == 0 ? l : r)} | " +
                                           $"guards {m0.guards}/{m1.guards}, grazes {m0.grazes}/{m1.grazes}, clashes {m0.clashes}/{m1.clashes}, hits {m0.hitsLanded}/{m1.hitsLanded}, colliders {battle.Left.WeaponColliders.Length}/{battle.Right.WeaponColliders.Length}");
                    }
                    battle.Destroy();
                    yield return null;
                }
                pairLines.Add($"| {Nine[i]} vs {Nine[j]} | {leftWins} - {rightWins} |");
            }

            string summary = $"- matches: {total} / draws: {draws} ({draws * 100f / Mathf.Max(1, total):F0}%) / sudden death: {suddenDeaths} / no damage at all: {zeroDamage} / KO: {koTotal} ({koTotal * 100f / Mathf.Max(1, total):F0}%) / avg total damage per match: {damageTotal / Mathf.Max(1, total):F0}";
            WriteReport(agg, pairLines, special.ToString(), dup, envOutside, maxIdle, maxIdleCase, seedsPerPair, summary);

            Assert.AreEqual(0, unfinished, "終わらない試合");
            Assert.AreEqual(0, dup, "二重本体ダメージ");
            Assert.AreEqual(0, envOutside, "吹っ飛び外の環境ダメージ");
            Assert.Less(maxIdle, 10f, $"膠着: {maxIdleCase}");
            // 文字ごとに違う戦い方: 溜め・手数・衝撃・勝率がばらける
            var list = agg.Values.ToList();
            float Rate(GlyphAgg a, float v) => v / Mathf.Max(1f, a.seconds / 60f);
            var windups = list.Select(a => a.windupSum / Mathf.Max(1, a.windupCount)).ToList();
            var attackRates = list.Select(a => Rate(a, a.attacks)).ToList();
            var impulses = list.Select(a => a.impulseSum / Mathf.Max(1, a.impulseCount)).ToList();
            var winRates = list.Select(a => a.wins / (float)a.matches).ToList();
            Assert.Greater(windups.Max() - windups.Min(), 0.6f, "溜めの差が小さい");
            Assert.Greater(attackRates.Max() / Mathf.Max(0.1f, attackRates.Min()), 1.5f, "手数の差が小さい");
            Assert.Greater(impulses.Max() / Mathf.Max(0.1f, impulses.Min()), 2f, "衝撃の差が小さい");
            Assert.Greater(winRates.Max() - winRates.Min(), 0.2f, "勝率がほぼ同じ（字形差が出ていない）");
            Assert.IsTrue(list.All(a => a.wins > 0), "一度も勝てない文字がある: " + string.Join(",", list.Where(a => a.wins == 0).Select(a => a.g)));
        }

        static void WriteReport(Dictionary<string, GlyphAgg> agg, List<string> pairs, string special, int dup, int envOutside, float maxIdle, string maxIdleCase, int seedsPerPair, string summary)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# P2 round robin — 9 glyphs (Gothic), 36 pairs × {seedsPerPair} seeds");
            sb.AppendLine();
            sb.AppendLine(summary);
            sb.AppendLine($"- duplicate body hits: {dup} / env damage outside launch window: {envOutside}");
            sb.AppendLine($"- longest gap with no attack by either side: {maxIdle:F1}s ({maxIdleCase})");
            sb.AppendLine();
            sb.AppendLine("| 文字 | 重量 | 勝率 | KO勝 | 攻撃/分 | 連撃 | 回避/分 | 跳び越え/分 | ガード/攻撃 | 命中率 | 空振り率 | 弾き率 | 被ガード率 | 振り始め距離/射程 | 与ダメ/試合 | 平均衝撃 | 溜め s | 振りの固着 | かすり/試合 | 壁床ダメ/試合 |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (var a in agg.Values.OrderByDescending(a => a.wins / (float)a.matches))
            {
                float min = Mathf.Max(1f, a.seconds / 60f);
                sb.AppendLine($"| {a.g} | {a.weightClass} | {a.wins * 100f / a.matches:F0}% | {a.kos} | {a.attacks / min:F1} | {a.attacks / Mathf.Max(1f, a.sequences):F2} | " +
                              $"{a.evades / min:F1} | {a.hopOvers / min:F2} | {a.guards / Mathf.Max(1f, a.sequences):F2} | {a.hits / Mathf.Max(1f, a.attacks):P0} | " +
                              $"{a.whiffs / Mathf.Max(1f, a.attacks):P0} | {a.clashes / Mathf.Max(1f, a.attacks):P0} | {a.guardedByOpp / Mathf.Max(1f, a.attacks):P0} | {a.distAtActiveSum / Mathf.Max(1, a.distCount):F2} | {a.damage / a.matches:F0} | " +
                              $"{a.impulseSum / Mathf.Max(1, a.impulseCount):F1} | {a.windupSum / Mathf.Max(1, a.windupCount):F2} | {a.jammed / Mathf.Max(1f, a.swings):P0} | {a.grazes / a.matches:F1} | {a.envImpacts / a.matches:F2} |");
            }
            sb.AppendLine();
            sb.AppendLine("## 対戦成績（各組の勝ち数）");
            sb.AppendLine();
            sb.AppendLine("| 組 | 勝ち |");
            sb.AppendLine("|---|---|");
            foreach (var p in pairs) sb.AppendLine(p);
            sb.AppendLine();
            sb.AppendLine("## 最小検証ケース（山 vs 一、口 vs 火）");
            sb.AppendLine();
            sb.Append(special);
            File.WriteAllText(Path.Combine(SimHarness.ReportDir, "p2_round_robin.md"), sb.ToString(), Encoding.UTF8);
            Debug.Log("[RR]\n" + sb);
        }
    }
}
