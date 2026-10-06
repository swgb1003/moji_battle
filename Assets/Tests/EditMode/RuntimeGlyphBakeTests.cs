using NUnit.Framework;
using UnityEngine;

namespace MojiBattle.Tests
{
    /// <summary>入力した任意の文字の実行時字形化（RuntimeGlyphBaker）。</summary>
    public class RuntimeGlyphBakeTests
    {
        static CombatBalance B => CombatBalance.Default;
        static GlyphCalibration C => GlyphCalibration.Default;

        [Test]
        public void FontLibrary_Exists_WithGothicCmap()
        {
            var lib = GlyphFontLibrary.Default;
            Assert.IsNotNull(lib, "Resources/Glyphs/GlyphFontLibrary が無い（ProjectSetup を実行）");
            var e = lib.Get(FontStyleId.Gothic);
            Assert.IsNotNull(e);
            Assert.IsNotNull(lib.copyMaterial);
            foreach (var ch in "一龍あアWw★%")
                Assert.IsTrue(e.Has(ch), $"{ch} がフォントに無いと判定された");
            Assert.IsFalse(e.Has(0x0E01), "タイ文字は同梱フォントに無いはず");
            Assert.IsNull(lib.Get(FontStyleId.Mincho), "明朝は未収録");
        }

        [Test]
        public void RuntimeBake_MatchesEditorBake()
        {
            foreach (var ch in new[] { "一", "鬱", "A", "火" })
            {
                var baked = Resources.Load<GlyphDefinition>("Glyphs/Definitions/" + GlyphCatalog.ResourceName(ch, FontStyleId.Gothic));
                Assert.IsNotNull(baked, ch);
                Assert.IsTrue(RuntimeGlyphBaker.TryBake(ch, FontStyleId.Gothic, B, C, out var def, out var reason), reason);
                Assert.AreEqual(baked.features.inkRatio, def.features.inkRatio, 0.002f, $"{ch}: 黒画素率がベイク済みと違う");
                Assert.AreEqual(baked.features.inkBounds, def.features.inkBounds, $"{ch}: 外接矩形がベイク済みと違う");
                Assert.AreEqual(baked.features.colliderRects.Length, def.features.colliderRects.Length, $"{ch}: Collider 数がベイク済みと違う");
                Object.DestroyImmediate(def.texture);
            }
        }

        [Test]
        public void TypedCharacters_BecomeWeapons()
        {
            foreach (var ch in new[] { "龍", "剣", "あ", "ア", "W", "★", "鬼", "%" })
            {
                Assert.IsTrue(RuntimeGlyphBaker.TryBake(ch, FontStyleId.Gothic, B, C, out var def, out var reason), $"{ch}: {reason}");
                var f = def.features;
                Assert.Greater(f.colliderRects.Length, 0, ch);
                Assert.LessOrEqual(f.colliderRects.Length, B.maxColliders, ch);
                Assert.IsTrue(def.texture.isReadable, ch);
                var s = StatCalculator.Compute(def, B);
                Assert.That(s.attack, Is.InRange(B.atkMin, B.atkMax), ch);
                Assert.That(s.defense, Is.InRange(B.defMin, B.defMax), ch);
                Assert.That(s.speed, Is.InRange(B.spdMin, B.spdMax), ch);
                Assert.Greater(s.maxHp, 0f, ch);
                // 握り（既定・カスタマイズの中央）が画線の上
                var px = def.texture.GetPixels32();
                foreach (var grip in new[] { f.gripPoint, WeaponGripController.GripPixel(def, 0.5f) })
                    Assert.GreaterOrEqual(px[Mathf.FloorToInt(grip.y) * def.texture.width + Mathf.FloorToInt(grip.x)].a, 128, $"{ch}: 握りが画線の外");
                Object.DestroyImmediate(def.texture);
            }
        }

        [Test]
        public void InvalidInput_IsRejectedWithReason()
        {
            foreach (var s in new[] { "", " ", "　", "ab", "ก", "\U0001F600", "\n" })
            {
                Assert.IsFalse(RuntimeGlyphBaker.TryBake(s, FontStyleId.Gothic, B, C, out _, out var reason), $"「{s}」が通った");
                Assert.IsNotEmpty(reason);
            }
            Assert.IsFalse(RuntimeGlyphBaker.TryBake("龍", FontStyleId.Mincho, B, C, out _, out var r2));
            StringAssert.Contains("未収録", r2);
        }

        [Test]
        public void Catalog_CachesRuntimeGlyphs()
        {
            GlyphCatalog.ClearCache();
            Assert.IsTrue(GlyphCatalog.TryGet("刀", FontStyleId.Gothic, B, C, out var a, out var reason), reason);
            Assert.IsTrue(GlyphCatalog.TryGet("刀", FontStyleId.Gothic, B, C, out var b2, out _));
            Assert.AreSame(a, b2);
            Assert.IsTrue(a.runtimeBaked);
            Assert.IsTrue(GlyphCatalog.TryGet("鬱", FontStyleId.Gothic, B, C, out var baked, out _));
            Assert.IsFalse(baked.runtimeBaked, "ベイク済みの文字はアセットを使う");
            GlyphCatalog.ClearCache();
        }
    }
}
