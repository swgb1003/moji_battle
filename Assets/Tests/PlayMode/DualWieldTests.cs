using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MojiBattle.Tests
{
    /// <summary>二刀流（S・M サイズのみ、もう一本の文字を選べる）の受け入れ確認。</summary>
    public class DualWieldTests
    {
        [TearDown]
        public void TearDown() => SimHarness.End();

        static FighterLoadout Dual(string main, string off, WeaponSize size = WeaponSize.M, BattleStyle style = BattleStyle.Aggressive) =>
            new FighterBuildData { character = main, offhandCharacter = off, weaponSize = size, gripType = GripType.DualWield, battleStyle = style }.ToLoadout();

        [Test]
        public void OnlySmallAndMediumSizesCanDualWield()
        {
            var b = new FighterBuildData { character = "火", gripType = GripType.DualWield, weaponSize = WeaponSize.S };
            Assert.IsTrue(b.IsDualWield);
            Assert.AreEqual("火", b.OffhandGlyph, "空欄なら同じ文字");
            b.offhandCharacter = "刀";
            Assert.AreEqual("刀", b.OffhandGlyph);
            Assert.AreEqual("二刀流・刀", CustomizeLabels.Grip(b));
            b.weaponSize = WeaponSize.L;
            Assert.IsFalse(b.IsDualWield, "L では二刀流にならない");
            Assert.IsNull(b.OffhandGlyph);
            Assert.AreEqual(4, (int)GripType.DualWield, "保存済みの持ち方の値を変えない");
        }

        [Test]
        public void DualWieldProfile_IsLighterPerBlade()
        {
            var cb = CustomizeBalance.Default;
            var dual = cb.Grip(GripType.DualWield);
            var one = cb.Grip(GripType.OneHanded);
            Assert.Less(dual.damage, one.damage);
            Assert.Greater(dual.attackSpeed, one.attackSpeed);
            Assert.Less(dual.guardStability, one.guardStability);
            Assert.AreEqual(1, dual.comboBonus);
            Assert.AreEqual(0.5f, cb.dualSecondHitDamage, 1e-4f);
        }

        [UnityTest]
        public IEnumerator Battle_BuildsOffhandWithItsOwnGlyph()
        {
            SimHarness.Begin(false);
            var battle = SimHarness.Build(4100, Dual("火", "刀", WeaponSize.S), SimHarness.Custom("山", WeaponSize.L));
            var f = battle.Left;
            Assert.IsNotNull(f.Offhand, "二刀流は副武器を持つ");
            Assert.AreEqual("刀", f.Offhand.Glyph.grapheme);
            Assert.IsTrue(f.Mods.dualWield);
            Assert.IsNull(battle.Right.Offhand);
            Assert.Greater(f.Offhand.RectCount, 0);
            Assert.IsEmpty(f.transform.parent.GetComponentsInChildren<Rigidbody2D>().Where(rb => rb != f.Body && rb != f.WeaponBody),
                "副武器は物理に加えない（押し合わず、計算も重くしない）");
            // L サイズの二刀流は作らない
            var large = SimHarness.Build(4101, Dual("火", "刀", WeaponSize.L), SimHarness.Custom("山"));
            Assert.IsNull(large.Left.Offhand);
            large.Destroy();
            yield return null;
            // 副武器は手元に付いて主武器に追従する
            TimeController.SetSpectatorSpeed(4f);
            float until = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < until) yield return null;
            Vector2 hand = f.Body.GetRelativePoint(f.HandLocal(f.Facing));
            Assert.Less((f.Offhand.Grip - hand).magnitude, .2f, "副武器の握りが手から離れている");
            battle.Destroy();
        }

        /// <summary>二刀流どうし・二刀流と通常の構成で、試合が最後まで成立し、副武器の当たりが出る。</summary>
        [UnityTest]
        [Timeout(600000)]
        public IEnumerator Battles_FinishAndOffhandLands()
        {
            SimHarness.Begin(false);
            int offhandHits = 0, secondHits = 0;
            float offhandDamage = 0f;
            var pairs = new[]
            {
                (Dual("火", "刀", WeaponSize.S), SimHarness.Custom("山", WeaponSize.M)),
                (Dual("剣", "一", WeaponSize.M), Dual("口", "口", WeaponSize.S, BattleStyle.Counter)),
                (SimHarness.Custom("鬱", WeaponSize.M, GripType.TwoHanded), Dual("水", "火", WeaponSize.M)),
            };
            int seed = 4200;
            foreach (var (l, r) in pairs)
            {
                var battle = SimHarness.Build(seed++, l, r);
                var main = new System.Collections.Generic.HashSet<long>();
                battle.Context.Events.Hit += e =>
                {
                    long key = ((long)e.attackId << 8) | (uint)e.defender;
                    if (!e.offhand) { main.Add(key); return; }
                    offhandHits++; offhandDamage += e.damage;
                    if (main.Contains(key)) secondHits++;
                };
                yield return SimHarness.RunToEnd(battle, 10f);
                Assert.IsTrue(battle.Director.Ended, $"{l.grapheme} vs {r.grapheme}: 試合が終わらない");
                var t = battle.Director.Telemetry;
                Assert.Greater(t.fighters[0].attackStarts + t.fighters[1].attackStarts, 5);
                Assert.AreEqual(0, t.duplicateBodyHits, "副武器も 1 スイング 1 回まで");
                battle.Destroy();
                yield return null;
            }
            Assert.Greater(offhandHits, 0, "副武器が一度も当たらない");
            Assert.Greater(offhandDamage, 0f);
            Debug.Log($"[DualWield] offhand hits {offhandHits}, after main {secondHits}, damage {offhandDamage:F1}");
        }

        /// <summary>大会の COM 試合（描画なしの高速計算）でも、二刀流の試合が普段どおりの速さで終わる。</summary>
        [UnityTest]
        [Timeout(300000)]
        public IEnumerator ComputerSimulation_DualWieldFinishesQuickly()
        {
            SimHarness.Begin(false);
            var cost = new float[2];
            foreach (bool dual in new[] { false, true })
            {
                var l = dual ? Dual("火", "火") : SimHarness.Custom("火");
                var r = dual ? Dual("水", "水", WeaponSize.M, BattleStyle.Counter) : SimHarness.Custom("水", style: BattleStyle.Counter);
                var battle = SimHarness.Build(4400, l, r);
                ComputerMatchSimulation.Configure(battle);
                var watch = System.Diagnostics.Stopwatch.StartNew();
                int steps = 0;
                while (!battle.Director.Ended && steps < 20000) { battle.Director.SimulateStep(); steps++; }
                watch.Stop();
                cost[dual ? 1 : 0] = (float)watch.Elapsed.TotalMilliseconds / Mathf.Max(1, steps);
                Debug.Log($"[DualWield] computer sim dual={dual}: steps {steps}, {watch.Elapsed.TotalMilliseconds / Mathf.Max(1, steps):F3} ms/step, ended {battle.Director.Ended}");
                Assert.IsTrue(battle.Director.Ended, $"dual={dual}: 試合が終わらない");
                battle.Destroy();
                yield return null;
            }
            Assert.Less(cost[1], cost[0] * 1.6f + .3f, "二刀流で COM 試合の計算が重くなりすぎている");
        }

        /// <summary>同じ文字・同じサイズの片手と比べて、二刀流が強すぎも弱すぎもしない（左右を入れ替えて対戦）。</summary>
        [UnityTest]
        [Timeout(1200000)]
        public IEnumerator Balance_DualWieldAgainstOneHanded()
        {
            SimHarness.Begin(false);
            string[] glyphs = { "火", "山", "一", "口", "刀", "剣" };
            int dualWins = 0, oneWins = 0, draws = 0, seed = 4300, offHits = 0, mainHits = 0;
            var log = new System.Text.StringBuilder();
            foreach (var g in glyphs)
            foreach (var size in new[] { WeaponSize.S, WeaponSize.M })
            for (int side = 0; side < 2; side++)
            {
                var dual = Dual(g, g, size);
                var one = SimHarness.Custom(g, size);
                var battle = side == 0 ? SimHarness.Build(seed++, dual, one) : SimHarness.Build(seed++, one, dual);
                int dualId = side;
                battle.Context.Events.Hit += e => { if (e.attacker != dualId) return; if (e.offhand) offHits++; else mainHits++; };
                yield return SimHarness.RunToEnd(battle, 10f);
                var dualFighter = side == 0 ? battle.Left : battle.Right;
                var oneFighter = side == 0 ? battle.Right : battle.Left;
                float d = dualFighter.Runtime.hp / dualFighter.Runtime.maxHp, o = oneFighter.Runtime.hp / oneFighter.Runtime.maxHp;
                if (Mathf.Abs(d - o) < 1e-3f) draws++; else if (d > o) dualWins++; else oneWins++;
                log.Append($"{g}{size}{(side == 0 ? "L" : "R")} dual{d:P0} one{o:P0}; ");
                battle.Destroy();
                yield return null;
            }
            float rate = (dualWins + draws * .5f) / Mathf.Max(1, dualWins + oneWins + draws);
            Debug.Log($"[DualWield] vs one-handed: win {dualWins} / loss {oneWins} / draw {draws} = {rate:P0} (main hits {mainHits}, offhand hits {offHits})\n{log}");
            Assert.That(rate, Is.InRange(.3f, .7f), $"二刀流の勝率 {rate:P0} が片手と釣り合っていない");
        }

        [UnityTest]
        public IEnumerator Customizer_DualWieldFollowsSize()
        {
            GameFlow.ToCustomize(); yield return null; yield return null;
            var screen = Object.FindAnyObjectByType<CustomizeScreen>();
            var c = screen.Customizer;
            c.SelectSide(0);
            c.SetCharacter("火");
            c.SetSize(WeaponSize.L);
            c.SetGrip(GripType.DualWield);
            Assert.AreNotEqual(GripType.DualWield, c.Current.gripType, "L では二刀流を選べない");
            c.SetSize(WeaponSize.S);
            c.SetGrip(GripType.DualWield);
            Assert.AreEqual(GripType.DualWield, c.Current.gripType);
            Assert.IsTrue(c.SetOffhandCharacter("刀"));
            Assert.AreEqual("刀", c.Current.OffhandGlyph);
            yield return null;
            Assert.IsTrue(GameObject.Find("OffhandInput").activeInHierarchy, "二刀流ではもう一本の入力欄を出す");
            yield return new WaitForSecondsRealtime(.6f);
            MenuUiTests.Capture("customize_dual_wield");
            c.SetSize(WeaponSize.XL);
            Assert.AreEqual(GripType.OneHanded, c.Current.gripType, "大きいサイズにしたら片手に戻す");
            Assert.IsNotNull(c.GripNotice);
            yield return null;
            Assert.IsNull(GameObject.Find("OffhandInput"), "二刀流でなければ入力欄を隠す");
            // 対戦画面でも副武器を描く
            c.SetSize(WeaponSize.M);
            c.SetGrip(GripType.DualWield);
            GameFlow.ToBattle(); yield return null; yield return null;
            var boot = Object.FindAnyObjectByType<BattleBootstrap>();
            Assert.IsNotNull(boot.Battle.Left.Offhand);
            Assert.IsTrue(boot.Battle.Left.Offhand.Sprite.enabled);
            float until = Time.realtimeSinceStartup + 4f;
            while (Time.realtimeSinceStartup < until) yield return null;
            MenuUiTests.Capture("battle_dual_wield");
            GameFlow.ToCustomize(); yield return null; yield return null;
            c = Object.FindAnyObjectByType<CustomizeScreen>().Customizer;
            c.SetGrip(GripType.OneHanded);
        }
    }
}
