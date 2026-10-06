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
    /// <summary>カスタマイズ（サイズ・握る位置・持ち方・戦闘スタイル）の受け入れ確認（カスタマイズ仕様 23）。</summary>
    public class CustomizePlayModeTests
    {
        static readonly StringBuilder report = new StringBuilder();

        [TearDown]
        public void TearDown() => SimHarness.End();

        static Rect WeaponColliderBounds(Fighter f)
        {
            float xMin = float.MaxValue, yMin = float.MaxValue, xMax = float.MinValue, yMax = float.MinValue;
            foreach (var c in f.WeaponColliders)
            {
                var b = (BoxCollider2D)c;
                xMin = Mathf.Min(xMin, b.offset.x - b.size.x * 0.5f); xMax = Mathf.Max(xMax, b.offset.x + b.size.x * 0.5f);
                yMin = Mathf.Min(yMin, b.offset.y - b.size.y * 0.5f); yMax = Mathf.Max(yMax, b.offset.y + b.size.y * 0.5f);
            }
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        [UnityTest]
        public IEnumerator Size_UpdatesSpriteColliderMassAndRange()
        {
            SimHarness.Begin(false);
            var battle = SimHarness.Build(101, SimHarness.Custom("一", WeaponSize.S), SimHarness.Custom("一", WeaponSize.XL));
            yield return null;
            var s = battle.Left;
            var xl = battle.Right;
            var cb = CustomizeBalance.Default;
            float expected = cb.sizeXL.scale / cb.sizeS.scale;
            var bs = WeaponColliderBounds(s);
            var bx = WeaponColliderBounds(xl);
            Assert.AreEqual(expected, bx.width / bs.width, 0.03f, "Collider がサイズに追従しない");
            Assert.AreEqual(expected, xl.WeaponSprite.sprite.bounds.size.x / s.WeaponSprite.sprite.bounds.size.x, 0.03f, "見た目がサイズに追従しない");
            // 見た目と Collider が同じ縮尺（字形の幅 ≒ Collider の幅）
            foreach (var f in new[] { s, xl })
            {
                float inkW = f.Glyph.features.inkBounds.width * f.Weapon.scale;
                Assert.AreEqual(inkW, WeaponColliderBounds(f).width, inkW * 0.1f, "見た目と Collider の幅が違う");
            }
            Assert.AreEqual(cb.sizeXL.massMultiplier / cb.sizeS.massMultiplier, xl.WeaponBody.mass / s.WeaponBody.mass, 0.01f, "質量がサイズに追従しない");
            Assert.Greater(xl.AttackRange, s.AttackRange * 1.5f, "射程（AI の適正距離）がサイズに追従しない");
            Assert.Greater(xl.Weapon.length, s.Weapon.length * 1.8f);
            battle.Destroy();
        }

        [UnityTest]
        public IEnumerator GripPosition_MovesGripAlongGlyph_AndChangesHandling()
        {
            SimHarness.Begin(false);
            var battle = SimHarness.Build(102, SimHarness.Custom("一", WeaponSize.XL, gripPosition: 0f), SimHarness.Custom("一", WeaponSize.XL, gripPosition: 0.5f));
            yield return null;
            var edge = battle.Left;
            var center = battle.Right;
            var cb = CustomizeBalance.Default;
            var ink = edge.Glyph.features.inkBounds;
            // 0 は内部で 0.08 へ Clamp。握りは字形の端寄り、中央持ちは中央
            float edgeT = (edge.Weapon.gripPx.x - ink.xMin) / ink.width;
            float centerT = (center.Weapon.gripPx.x - ink.xMin) / ink.width;
            Assert.AreEqual(cb.gripClampMin, edgeT, 0.03f, "端持ちの握り位置");
            Assert.AreEqual(0.5f, centerT, 0.03f, "中央持ちの握り位置");
            Assert.Greater(edge.Mods.leverArm, center.Mods.leverArm + 1f, "端持ちの leverArm が長くない");
            Assert.Less(edge.Mods.handlingAccel, center.Mods.handlingAccel * 0.6f, "端持ちが扱いにくくなっていない");
            Assert.Greater(edge.Weapon.length, center.Weapon.length * 1.5f, "端持ちのリーチが長くない");
            // 縦長の字形（I）は長軸（縦）に沿って握る位置が動く
            battle.Destroy();
            var b2 = SimHarness.Build(103, SimHarness.Custom("I", gripPosition: 0f), SimHarness.Custom("I", gripPosition: 1f));
            yield return null;
            Assert.Greater(b2.Right.Weapon.gripPx.y - b2.Left.Weapon.gripPx.y, b2.Left.Glyph.features.inkBounds.height * 0.7f, "縦長の字形で握り位置が動かない");
            b2.Destroy();
        }

        [UnityTest]
        public IEnumerator GripTypes_ApplyModifiers()
        {
            SimHarness.Begin(false);
            var cb = CustomizeBalance.Default;
            var a = SimHarness.Build(104, SimHarness.Custom("火", grip: GripType.OneHanded), SimHarness.Custom("火", grip: GripType.TwoHanded));
            var c = SimHarness.Build(105, SimHarness.Custom("火", grip: GripType.Reverse), SimHarness.Custom("火", grip: GripType.Horizontal));
            yield return null;
            Fighter one = a.Left, two = a.Right, rev = c.Left, hor = c.Right;
            Assert.AreEqual(cb.oneHanded.attackSpeed / cb.twoHanded.attackSpeed, two.WindupTime / one.WindupTime, 0.01f, "両手持ちの攻撃速度");
            Assert.Greater(two.Mods.guardStability, one.Mods.guardStability);
            Assert.Greater(two.Mods.attackTorque, one.Mods.attackTorque);
            Assert.Greater(two.Mods.knockbackResistance, 1f);
            Assert.Less(two.WalkSpeed, one.WalkSpeed);
            Assert.AreEqual(one.AttackRange * cb.reverse.effectiveReach, rev.AttackRange, 0.01f, "逆手の有効リーチ");
            Assert.Less(rev.WindupTime, one.WindupTime, "逆手は速い");
            Assert.AreEqual(cb.reverse.readyPsi, rev.ReadyPsi, 0.01f);
            Assert.Greater(hor.Mods.guardRange, 1f);
            Assert.Greater(hor.Mods.guardStability, two.Mods.guardStability);
            Assert.Greater(hor.Tendency.guardBias, one.Tendency.guardBias, "横持ちは AI がガードを選びやすい");
            a.Destroy();
            c.Destroy();
        }

        sealed class Agg
        {
            public string label;
            public int matches, wins;
            public float seconds, attacks, sequences, finished, cycle, guardSeconds, retreats, windowAttacks, omega, tip, swings, impulse, hits, damage, windup, windups;
            public float maxStretch, stretchedSteps;
            public int[] stretchStates = new int[10];
            public float PerMin(float v) => v / Mathf.Max(1f, seconds / 60f);
        }

        static IEnumerator RunSet(Agg agg, FighterLoadout left, FighterLoadout right, int seeds, int seedBase)
        {
            for (int s = 0; s < seeds; s++)
            {
                var battle = SimHarness.Build(seedBase + s * 13, left, right);
                yield return SimHarness.RunToEnd(battle, 10f);
                Assert.IsTrue(battle.Director.Ended, $"{agg.label}: 試合が終わらない");
                var t = battle.Director.Telemetry.fighters[0];
                var f = battle.Left;
                agg.matches++;
                if (battle.Director.Result.winner == 0) agg.wins++;
                agg.seconds += battle.Director.Result.elapsedSeconds;
                agg.attacks += t.attackStarts;
                agg.sequences += t.attackSequences;
                agg.guardSeconds += t.stateSeconds[(int)FighterState.Guard];
                agg.retreats += f.Brain.RetreatsAfterAttack;
                agg.finished += f.Brain.SequencesFinished;
                agg.cycle += t.stateSeconds[(int)FighterState.AttackWindup] + t.stateSeconds[(int)FighterState.AttackActive] + t.stateSeconds[(int)FighterState.AttackRecovery];
                agg.windowAttacks += f.Brain.WindowAttacks;
                agg.omega += t.peakSwingOmegaSum;
                agg.tip += t.peakTipSpeedSum;
                agg.swings += t.peakSwingCount;
                agg.impulse += t.hitImpulses.Sum();
                agg.hits += t.hitImpulses.Count;
                agg.damage += t.hitDamages.Sum();
                agg.windup += t.windups.Sum();
                agg.windups += t.windups.Count;
                agg.maxStretch = Mathf.Max(agg.maxStretch, t.maxHingeStretch);
                agg.stretchedSteps += t.stretchedSteps;
                for (int k = 0; k < 10; k++) agg.stretchStates[k] += t.stretchStateSteps[k];
                battle.Destroy();
                yield return null;
            }
        }

        static string Row(Agg a) =>
            $"| {a.label} | {a.wins}/{a.matches} | {a.PerMin(a.attacks):F1} | {a.cycle / Mathf.Max(1f, a.attacks):F2} | {a.omega / Mathf.Max(1, a.swings):F0} | {a.tip / Mathf.Max(1, a.swings):F1} | " +
            $"{a.impulse / Mathf.Max(1, a.hits):F1} | {a.damage / a.matches:F0} | {a.guardSeconds / Mathf.Max(1f, a.seconds):P0} | {a.retreats / Mathf.Max(1f, a.finished):P0} | " +
            $"{a.windowAttacks / Mathf.Max(1f, a.sequences):P0} | {a.maxStretch:F2} ({a.stretchedSteps / Mathf.Max(1f, a.seconds * 50f):P1} {TopStates(a)}) |";

        static string TopStates(Agg a) => string.Join(",", Enumerable.Range(0, 10).Where(k => a.stretchStates[k] > 0)
            .OrderByDescending(k => a.stretchStates[k]).Take(2).Select(k => $"{(FighterState)k}:{a.stretchStates[k]}"));

        const string Header = "| 左の構成 | 勝ち | 攻撃/分 | 1回の攻撃の所要 s | 振りの最大角速度 °/s | 切っ先の最大速度 | 平均衝撃 | 与ダメ/試合 | ガード時間 | 攻撃後の離脱 | 好機で始めた攻撃 | 握りの最大ずれ |\n|---|---|---|---|---|---|---|---|---|---|---|---|";

        [UnityTest]
        [Timeout(1200000)]
        public IEnumerator SizeAndGrip_ChangeSwingFeel_AndStayStable()
        {
            SimHarness.Begin(false);
            const int seeds = 4;
            var opp = new FighterLoadout("口", FontStyleId.Gothic); // 相手は P2 既定（カスタマイズ無し）で固定
            var sets = new List<Agg>();
            foreach (var (label, lo) in new[]
            {
                ("一 カスタマイズ無し", new FighterLoadout("一", FontStyleId.Gothic)),
                ("一 S 中央", SimHarness.Custom("一", WeaponSize.S)),
                ("一 M 中央", SimHarness.Custom("一", WeaponSize.M)),
                ("一 XL 中央", SimHarness.Custom("一", WeaponSize.XL)),
                ("一 XL 端", SimHarness.Custom("一", WeaponSize.XL, gripPosition: 0f)),
                ("一 XL 端 両手", SimHarness.Custom("一", WeaponSize.XL, GripType.TwoHanded, 0f)),
                ("鬱 XL 端 両手", SimHarness.Custom("鬱", WeaponSize.XL, GripType.TwoHanded, 0f)),
            })
            {
                var agg = new Agg { label = label };
                yield return RunSet(agg, lo, opp, seeds, 7000);
                sets.Add(agg);
            }
            report.AppendLine("## サイズ・握る位置（相手: 口 カスタマイズ無し、各 " + seeds + " 試合）").AppendLine().AppendLine(Header);
            foreach (var a in sets) report.AppendLine(Row(a));
            report.AppendLine();
            WriteReport();
            Agg S = sets[1], XL = sets[3], edge = sets[4];
            float Omega(Agg a) => a.omega / Mathf.Max(1, a.swings);
            float Imp(Agg a) => a.impulse / Mathf.Max(1, a.hits);
            float Cycle(Agg a) => a.cycle / Mathf.Max(1f, a.attacks);
            float Windup(Agg a) => a.windup / Mathf.Max(1, a.windups);
            Assert.Less(Windup(S), Windup(XL) * 0.6f, "S の方が溜めが短いはず");
            Assert.Less(Cycle(S), Cycle(XL), "S の方が 1 回の攻撃が速く終わるはず");
            Assert.Greater(Imp(XL), Imp(S) * 1.5f, "XL の方が衝撃が大きいはず");
            Assert.Greater(Omega(XL), Omega(edge), "端持ちは中央持ちより回転が遅いはず");
            // 握りのずれ（0.2 超）の時間がカスタマイズ無しの基準の 2 倍・1% のどちらか大きい方を超えない
            float Stretch(Agg a) => a.stretchedSteps / Mathf.Max(1f, a.seconds * 50f);
            float limit = Mathf.Max(0.01f, Stretch(sets[0]) * 2f);
            foreach (var a in sets) Assert.Less(Stretch(a), limit, $"{a.label}: 握りが外れかけている時間が長い（物理の破綻）");
        }

        [UnityTest]
        [Timeout(1200000)]
        public IEnumerator BattleStyles_AreDistinguishable()
        {
            SimHarness.Begin(false);
            const int seeds = 5;
            var opp = new FighterLoadout("口", FontStyleId.Gothic); // 相手は P2 既定（カスタマイズ無し）で固定
            var sets = new Dictionary<BattleStyle, Agg>();
            foreach (BattleStyle st in System.Enum.GetValues(typeof(BattleStyle)))
            {
                // 握りは端寄り（槍の構え）で揃え、スタイルだけを変える
                var agg = new Agg { label = "一 M 片手 端 " + CustomizeLabels.Style(st) };
                yield return RunSet(agg, SimHarness.Custom("一", gripPosition: 0.1f, style: st), opp, seeds, 8000);
                sets[st] = agg;
            }
            var hor = new Agg { label = "一 M 横持ち 端 カウンター" };
            yield return RunSet(hor, SimHarness.Custom("一", grip: GripType.Horizontal, gripPosition: 0.1f, style: BattleStyle.Counter), opp, seeds, 8000);
            report.AppendLine("## 戦闘スタイル（相手: 口 カスタマイズ無し、各 " + seeds + " 試合）").AppendLine().AppendLine(Header);
            foreach (var a in sets.Values) report.AppendLine(Row(a));
            report.AppendLine(Row(hor));
            report.AppendLine();
            WriteReport();

            var agg2 = sets[BattleStyle.Aggressive];
            float Guard(Agg a) => a.guardSeconds / Mathf.Max(1f, a.seconds);
            float Retreat(Agg a) => a.retreats / Mathf.Max(1f, a.finished);
            float Window(Agg a) => a.windowAttacks / Mathf.Max(1f, a.sequences); // 攻撃を始めた判断（連撃は 1 回）のうち好機で始めた割合
            foreach (var a in sets.Values)
                if (a != agg2) Assert.Greater(agg2.PerMin(agg2.attacks), a.PerMin(a.attacks), $"猛攻の手数が {a.label} より多いはず");
            Assert.Greater(Retreat(sets[BattleStyle.HitAndAway]), 0.85f, "H&A が攻撃後に離脱しない");
            Assert.Less(Retreat(agg2), 0.05f, "猛攻が離脱している");
            foreach (var a in sets.Values)
                if (a != sets[BattleStyle.Defensive]) Assert.Greater(Guard(sets[BattleStyle.Defensive]), Guard(a), $"鉄壁のガードが {a.label} より少ない");
            // カウンターは攻撃のかなりの割合を相手の攻撃直後に出す（他のスタイルは好機を数えないので 0）。
            Assert.Greater(Window(sets[BattleStyle.Counter]), 0.3f, "カウンターの攻撃が相手の攻撃後に集中していない");
            Assert.Greater(Guard(hor), Guard(sets[BattleStyle.Counter]), "横持ちはガードが増えるはず（同じカウンターの片手と比べて）");
        }

        /// <summary>入力した任意の文字（実行時に字形化）どうしで、試合が最後まで成立する。</summary>
        [UnityTest]
        [Timeout(600000)]
        public IEnumerator TypedCharacters_FightToTheEnd()
        {
            SimHarness.Begin(false);
            var pairs = new[] { ("龍", "剣"), ("あ", "W"), ("★", "鬼") };
            int seed = 9100;
            foreach (var (l, r) in pairs)
            {
                var battle = SimHarness.Build(seed++, SimHarness.Custom(l), SimHarness.Custom(r, style: BattleStyle.Defensive));
                Assert.IsTrue(battle.Left.Glyph.runtimeBaked && battle.Right.Glyph.runtimeBaked);
                Assert.LessOrEqual(battle.Left.WeaponColliders.Length, CombatBalance.Default.maxColliders);
                yield return SimHarness.RunToEnd(battle, 10f);
                Assert.IsTrue(battle.Director.Ended, $"{l} vs {r}: 試合が終わらない");
                var t = battle.Director.Telemetry;
                Assert.Greater(t.fighters[0].attackStarts + t.fighters[1].attackStarts, 5, $"{l} vs {r}: ほとんど攻撃しない");
                Assert.AreEqual(0, t.duplicateBodyHits);
                battle.Destroy();
                yield return null;
            }
        }

        /// <summary>反転して持った武器: 字形・重心が反転し、試合が最後まで成立する。</summary>
        [UnityTest]
        [Timeout(600000)]
        public IEnumerator FlippedWeapons_FightToTheEnd()
        {
            SimHarness.Begin(false);
            var pairs = new[] { ("火", WeaponFlip.Horizontal, "山", WeaponFlip.Vertical), ("龍", WeaponFlip.Both, "鬼", WeaponFlip.None) };
            int seed = 9200;
            foreach (var (l, lf, r, rf) in pairs)
            {
                var lb = SimHarness.Custom(l);
                lb.build.weaponFlip = lf;
                var rb = SimHarness.Custom(r, style: BattleStyle.Defensive);
                rb.build.weaponFlip = rf;
                var battle = SimHarness.Build(seed++, lb, rb);
                GlyphCatalog.TryGet(l, FontStyleId.Gothic, CombatBalance.Default, GlyphCalibration.Default, out var plain, out _);
                var flipped = battle.Left.Glyph;
                Assert.AreNotSame(plain, flipped, $"{l}: 反転した字形を使っていない");
                int w = plain.texture.width;
                if (lf != WeaponFlip.Vertical)
                    Assert.AreEqual(w - plain.features.centerOfMass.x, flipped.features.centerOfMass.x, 1e-3f, $"{l}: 重心が左右反転していない");
                yield return SimHarness.RunToEnd(battle, 10f);
                Assert.IsTrue(battle.Director.Ended, $"{l} vs {r}: 試合が終わらない");
                var t = battle.Director.Telemetry;
                Assert.Greater(t.fighters[0].attackStarts + t.fighters[1].attackStarts, 5, $"{l} vs {r}: ほとんど攻撃しない");
                Assert.AreEqual(0, t.duplicateBodyHits);
                battle.Destroy();
                yield return null;
            }
        }

        /// <summary>診断用: カスタマイズ構成 1 試合の詳細ログ（Reports/trace_custom.txt）。</summary>
        [UnityTest, Explicit("診断用")]
        public IEnumerator TraceCustom()
        {
            SimHarness.Begin(false);
            MatchTelemetry.Trace = true;
            var lines = new List<string>();
            for (int s = 0; s < 4; s++)
            {
                var battle = SimHarness.Build(8000 + s * 13, SimHarness.Custom("一", gripPosition: 0.1f, style: BattleStyle.Counter), new FighterLoadout("口", FontStyleId.Gothic));
                yield return SimHarness.RunToEnd(battle, 10f);
                lines.Add($"=== seed {7000 + s * 13}");
                lines.AddRange(battle.Director.Telemetry.log);
                battle.Destroy();
                yield return null;
            }
            File.WriteAllLines(Path.Combine(SimHarness.ReportDir, "trace_custom.txt"), lines);
            MatchTelemetry.Trace = false;
        }

        static void WriteReport()
        {
            var path = Path.Combine(SimHarness.ReportDir, "customize_check.md");
            File.WriteAllText(path, "# カスタマイズの効き方（PlayMode 計測）\n\n" + report, Encoding.UTF8);
            Debug.Log("[CUSTOMIZE]\n" + report);
        }
    }
}
