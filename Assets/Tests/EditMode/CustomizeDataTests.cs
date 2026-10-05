using NUnit.Framework;
using UnityEngine;

namespace MojiBattle.Tests
{
    /// <summary>カスタマイズのデータ基盤・握る位置・補正計算（カスタマイズ仕様 5 / 8 / 9 / 11）。</summary>
    public class CustomizeDataTests
    {
        static CombatBalance B => CombatBalance.Default;
        static CustomizeBalance CB => CustomizeBalance.Default;

        static GlyphDefinitionRuntime Glyph(string ch)
        {
            Assert.IsTrue(GlyphCatalog.TryGet(ch, FontStyleId.Gothic, B, GlyphCalibration.Default, out var g, out var reason), reason);
            return g;
        }

        [Test]
        public void Defaults_MatchSpec()
        {
            var b = FighterBuildData.Default("一");
            Assert.AreEqual(WeaponSize.M, b.weaponSize);
            Assert.AreEqual(GripType.OneHanded, b.gripType);
            Assert.AreEqual(0.5f, b.gripPosition);
            Assert.AreEqual(BattleStyle.Aggressive, b.battleStyle);
            Assert.AreEqual(FontStyleId.Gothic, b.fontType);
            var copy = b.Clone();
            copy.weaponSize = WeaponSize.XL;
            Assert.AreEqual(WeaponSize.M, b.weaponSize, "Clone が元を書き換える");
            Assert.IsNull(new FighterLoadout("一", FontStyleId.Gothic).build, "カスタマイズ無しの既定が変わった");
            Assert.IsNotNull(b.ToLoadout().build);
        }

        [Test]
        public void SizeTable_MatchesSpec()
        {
            float[] scale = { 0.75f, 1f, 1.3f, 1.65f }, mass = { 0.65f, 1f, 1.45f, 2.1f }, atk = { 1.2f, 1f, 0.85f, 0.68f }, move = { 1.1f, 1f, 0.92f, 0.82f };
            for (int i = 0; i < 4; i++)
            {
                var s = CB.Size((WeaponSize)i);
                Assert.AreEqual(scale[i], s.scale, 1e-4f);
                Assert.AreEqual(mass[i], s.massMultiplier, 1e-4f);
                Assert.AreEqual(atk[i], s.attackSpeed, 1e-4f);
                Assert.AreEqual(move[i], s.moveSpeed, 1e-4f);
            }
        }

        [Test]
        public void GripPosition_IsClamped_AndAlwaysOnInk()
        {
            Assert.AreEqual(0.08f, CB.ClampGrip(0f), 1e-5f);
            Assert.AreEqual(0.92f, CB.ClampGrip(1f), 1e-5f);
            foreach (var ch in FighterCustomizer.GlyphOrder)
            {
                var g = Glyph(ch.ToString());
                var px = g.texture.GetPixels32();
                foreach (float p in new[] { 0f, 0.25f, 0.5f, 0.75f, 1f })
                {
                    var grip = WeaponGripController.GripPixel(g, CB.ClampGrip(p));
                    int x = Mathf.FloorToInt(grip.x), y = Mathf.FloorToInt(grip.y);
                    Assert.GreaterOrEqual(px[y * g.texture.width + x].a, 128, $"{ch} p={p}: 握りが画線の外 ({grip})");
                }
            }
        }

        [Test]
        public void GripPosition_ChangesLeverArmAndHandling_NotStats()
        {
            var g = Glyph("一");
            var stats = StatCalculator.Compute(g, B);
            FighterModifiers Mods(float p)
            {
                var build = new FighterBuildData { character = "一", gripPosition = p };
                var geo = FighterFactory.ResolveWeapon(build, g, stats, B, CB, out float mass);
                return FighterModifiers.From(build, g, stats, geo, mass, B, CB);
            }
            var center = Mods(0.5f);
            var edge = Mods(0f);
            Assert.Greater(edge.leverArm, center.leverArm + 0.5f);
            Assert.Less(edge.handlingAccel, center.handlingAccel);
            // 握る位置は数値補正を持たない（攻撃速度・ダメージ・ガードは同じ）
            Assert.AreEqual(center.attackSpeed, edge.attackSpeed);
            Assert.AreEqual(center.damage, edge.damage);
            Assert.AreEqual(center.guardStability, edge.guardStability);
        }

        [Test]
        public void FinalStats_SizeTimesGripType()
        {
            var g = Glyph("火");
            var stats = StatCalculator.Compute(g, B);
            var build = new FighterBuildData { character = "火", weaponSize = WeaponSize.L, gripType = GripType.TwoHanded };
            var geo = FighterFactory.ResolveWeapon(build, g, stats, B, CB, out float mass);
            var m = FighterModifiers.From(build, g, stats, geo, mass, B, CB);
            Assert.AreEqual(stats.weaponMass * 1.45f, mass, 1e-4f, "武器質量 = 字形 × サイズ");
            Assert.AreEqual(0.85f * 0.80f, m.attackSpeed, 1e-4f, "攻撃速度 = サイズ × 持ち方");
            Assert.AreEqual(0.92f * 0.90f, m.moveSpeed, 1e-4f, "移動速度 = サイズ × 持ち方");
            Assert.AreEqual(1.3f * B.weaponMaxSide, geo.maxSide, 1e-3f, "最大辺 = M × サイズ");
            var id = FighterModifiers.Identity;
            Assert.IsFalse(id.customized);
            Assert.AreEqual(1f, id.attackSpeed);
            Assert.AreEqual(1f, id.handlingAccel);
        }

        [Test]
        public void Styles_ChangeAiWeights_NotStats()
        {
            var t = B.TendencyFor(WeightClass.Light);
            var agg = CB.aggressive;
            var def = CB.defensive;
            Assert.Greater(agg.attackWillingness, def.attackWillingness);
            Assert.Greater(def.guardBiasScale, agg.guardBiasScale);
            Assert.AreEqual(1f, CB.hitAndAway.retreatAfterAttack);
            Assert.AreEqual(0.6f, CB.counterWindow, 1e-5f);
            Assert.AreEqual(2.5f, CB.defensiveMaxGuardSeconds, 1e-5f);
            Assert.AreEqual(0.5f, CB.defensiveGuardCooldown, 1e-5f);
            var copy = t.Clone();
            copy.attackWillingness = 0f;
            Assert.AreNotEqual(0f, B.TendencyFor(WeightClass.Light).attackWillingness, "Clone が重量クラスの既定を書き換える");
        }
    }
}
