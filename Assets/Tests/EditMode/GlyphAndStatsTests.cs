using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace MojiBattle.Tests
{
    public class GlyphAndStatsTests
    {
        const string InitialNine = "一口山火鬱AIOX";
        static CombatBalance B => CombatBalance.Default;

        static GlyphDefinitionRuntime Load(string g, FontStyleId font = FontStyleId.Gothic)
        {
            GlyphCatalog.ClearCache();
            Assert.IsTrue(GlyphCatalog.TryGet(g, font, B, GlyphCalibration.Default, out var def, out var reason), reason);
            return def;
        }

        static IEnumerable<string> Nine => InitialNine.Select(c => c.ToString());

        /// <summary>反転: 画像・重心・Collider・握りが同じ鏡映で写り、ステータスは変わらない。2 回反転すると元に戻る。</summary>
        [TestCase(WeaponFlip.Horizontal)]
        [TestCase(WeaponFlip.Vertical)]
        [TestCase(WeaponFlip.Both)]
        public void Flip_MirrorsShapeAndKeepsStats(WeaponFlip flip)
        {
            var src = Load("火");
            var f = GlyphFlip.Apply(src, flip);
            Assert.AreNotSame(src, f);
            Assert.AreSame(f, GlyphFlip.Apply(src, flip), "同じ反転はキャッシュを返す");
            Assert.AreSame(src, GlyphFlip.Apply(src, WeaponFlip.None));
            bool h = flip != WeaponFlip.Vertical, v = flip != WeaponFlip.Horizontal;
            int w = src.texture.width, ht = src.texture.height;
            var a = src.features;
            var b = f.features;
            Assert.AreEqual(h ? w - a.centerOfMass.x : a.centerOfMass.x, b.centerOfMass.x, 1e-3f);
            Assert.AreEqual(v ? ht - a.centerOfMass.y : a.centerOfMass.y, b.centerOfMass.y, 1e-3f);
            Assert.AreEqual(a.colliderRects.Length, b.colliderRects.Length);
            Assert.AreEqual(a.inkBounds.size, b.inkBounds.size);
            // 反転した画像のインクは、反転後の外接矩形の中にある
            var px = f.texture.GetPixels32();
            for (int y = 0; y < ht; y++)
            for (int x = 0; x < w; x++)
                if (px[y * w + x].a >= 128) Assert.IsTrue(b.inkBounds.Contains(new Vector2Int(x, y)), $"({x},{y}) が外接矩形の外");
            // 握り点はインクの上
            int gx = Mathf.FloorToInt(b.gripPoint.x), gy = Mathf.FloorToInt(b.gripPoint.y);
            Assert.GreaterOrEqual(px[gy * w + gx].a, 128, "握りが画線の上にない");
            var sa = StatCalculator.Compute(src, B);
            var sb = StatCalculator.Compute(f, B);
            Assert.AreEqual(sa.ToString(), sb.ToString(), "反転でステータスは変わらない");
            var back = GlyphFlip.Apply(f, flip).features;
            Assert.AreEqual(a.centerOfMass.x, back.centerOfMass.x, 1e-3f);
            Assert.AreEqual(a.inkBounds, back.inkBounds);
        }

        [Test]
        public void InitialNineAreBakedForGothic()
        {
            foreach (var g in Nine)
            {
                var baked = Resources.Load<GlyphDefinition>("Glyphs/Definitions/" + GlyphCatalog.ResourceName(g, FontStyleId.Gothic));
                Assert.IsNotNull(baked, g);
                Assert.IsNotNull(baked.mask, g);
                Assert.IsNotEmpty(baked.fontHash, g);
                Assert.IsNotEmpty(baked.settingsHash, g);
            }
        }

        [Test]
        public void ReportFeaturesAndStats()
        {
            var sb = new StringBuilder();
            foreach (var g in Nine)
            {
                var d = Load(g);
                var f = d.features;
                var s = StatCalculator.Compute(d, B);
                var geo = FighterFactory.BuildGeometry(d, B);
                sb.AppendLine($"[GLYPH] {g}: ink={f.inkRatio:F3} w={f.widthRatio:F3} h={f.heightRatio:F3} edge={f.edgeRatio:F2} bal={f.balance:F3} " +
                              $"comps={f.componentCount} rects={f.colliderRects.Length} | {d.normalized} | {s} | class={StatCalculator.Classify(s.weightScore, B)} " +
                              $"windup={StatCalculator.Windup(s.weightScore, B):F2} len={geo.length:F2} alpha0={geo.alpha0Deg:F1}");
            }
            Debug.Log(sb.ToString());
        }

        [Test]
        public void OneIsNimble_UtsuIsHeavy()
        {
            var one = StatCalculator.Compute(Load("一"), B);
            var utsu = StatCalculator.Compute(Load("鬱"), B);
            Assert.Greater(one.speed, utsu.speed);
            Assert.Greater(utsu.attack, one.attack);
            Assert.Greater(utsu.weaponMass, one.weaponMass);
            Assert.AreEqual(WeightClass.Light, StatCalculator.Classify(one.weightScore, B));
            Assert.AreEqual(WeightClass.Heavy, StatCalculator.Classify(utsu.weightScore, B));
            float utsuTime = StatCalculator.Windup(utsu.weightScore, B) + StatCalculator.Active(utsu.weightScore, B);
            float oneTime = StatCalculator.Windup(one.weightScore, B) + StatCalculator.Active(one.weightScore, B);
            Assert.GreaterOrEqual(utsuTime, 1.5f, "鬱は長い溜め（仕様例 約1.8秒）");
            Assert.GreaterOrEqual(utsuTime / oneTime, 3f, "明確な差");
        }

        /// <summary>5.3: 9文字の能力が全員同値にならず、目標傾向（一=速度上位・重量下位、鬱=攻撃上位・速度下位）に沿う。</summary>
        [Test]
        public void NineGlyphStatsAreSpreadAndFollowTargetTendency()
        {
            var stats = Nine.ToDictionary(g => g, g => StatCalculator.Compute(Load(g), B));
            Assert.Greater(stats.Values.Max(s => s.attack) - stats.Values.Min(s => s.attack), 30, "攻撃がほぼ同値");
            Assert.Greater(stats.Values.Max(s => s.speed) - stats.Values.Min(s => s.speed), 30, "速度がほぼ同値");
            Assert.Greater(stats.Values.Max(s => s.defense) - stats.Values.Min(s => s.defense), 20, "防御がほぼ同値");
            var bySpeed = stats.OrderByDescending(kv => kv.Value.speed).Select(kv => kv.Key).ToList();
            var byAttack = stats.OrderByDescending(kv => kv.Value.attack).Select(kv => kv.Key).ToList();
            var byWeight = stats.OrderBy(kv => kv.Value.weightScore).Select(kv => kv.Key).ToList();
            Assert.Less(bySpeed.IndexOf("一"), 3, "一は速度上位");
            Assert.Less(byWeight.IndexOf("一"), 3, "一は重量下位");
            Assert.Less(byAttack.IndexOf("鬱"), 2, "鬱は攻撃上位");
            Assert.Greater(bySpeed.IndexOf("鬱"), 6, "鬱は速度下位");
            var classes = stats.Values.Select(s => StatCalculator.Classify(s.weightScore, B)).Distinct().Count();
            Assert.GreaterOrEqual(classes, 2, "重量クラスが1種類しかない");
        }

        /// <summary>同じ設定では同じ値になる（ベイク結果 = マスクの再解析）。Collider は 64 個以下。</summary>
        [Test]
        public void BakedFeaturesMatchReanalysis_AndColliderLimit()
        {
            foreach (var g in Nine)
            {
                var d = Load(g);
                var stored = d.features;
                var again = GlyphMaskAnalyzer.Analyze(GlyphMask.FromAlpha(d.texture.GetPixels32(), d.texture.width, B.alphaThreshold), AnalyzerSettings.From(B));
                Assert.LessOrEqual(stored.colliderRects.Length, 64, g);
                Assert.Greater(stored.colliderRects.Length, 0, g);
                Assert.AreEqual(stored.colliderRects.Length, again.colliderRects.Length, g);
                for (int i = 0; i < stored.colliderRects.Length; i++) Assert.AreEqual(stored.colliderRects[i], again.colliderRects[i], g);
                Assert.AreEqual(stored.inkRatio, again.inkRatio, 1e-6f, g);
                Assert.AreEqual(stored.centerOfMass, again.centerOfMass, g);
            }
        }

        /// <summary>6.1 / 12: Collider が主な画線から 3px 相当以内（マスク256px基準）。</summary>
        [Test]
        public void CollidersAlignWithGlyphWithin3px()
        {
            var sb = new StringBuilder();
            foreach (var g in Nine)
            {
                var d = Load(g);
                var mask = GlyphMaskAnalyzer.RemoveSmallComponents(
                    GlyphMask.FromAlpha(d.texture.GetPixels32(), d.texture.width, B.alphaThreshold), B.minComponentPixels, out _);
                var col = new GlyphMask(mask.Size);
                foreach (var r in d.features.colliderRects)
                    for (int y = (int)r.yMin; y < (int)r.yMax; y++)
                    for (int x = (int)r.xMin; x < (int)r.xMax; x++)
                        col[x, y] = true;
                float inkCovered = Coverage(mask, col, 3);
                float colOnInk = Coverage(col, mask, 3);
                sb.AppendLine($"[ALIGN] {g}: ink within 3px of collider = {inkCovered:P2}, collider within 3px of ink = {colOnInk:P2}");
                Assert.GreaterOrEqual(inkCovered, 0.98f, g + " ink coverage");
                Assert.GreaterOrEqual(colOnInk, 0.98f, g + " collider precision");
            }
            Debug.Log(sb.ToString());
        }

        static float Coverage(GlyphMask src, GlyphMask target, int radius)
        {
            int n = src.Size, total = 0, ok = 0;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                if (!src[x, y]) continue;
                total++;
                bool found = false;
                for (int dy = -radius; dy <= radius && !found; dy++)
                for (int dx = -radius; dx <= radius && !found; dx++)
                    if (dx * dx + dy * dy <= radius * radius && target[x + dx, y + dy]) found = true;
                if (found) ok++;
            }
            return total == 0 ? 1f : ok / (float)total;
        }

        /// <summary>最小検証ケース「口 vs 火」: 口の穴を Collider で埋めない。火は複数の部品から成る輪郭を保つ。</summary>
        [Test]
        public void KuchiHoleIsOpen_HiKeepsSeparateParts()
        {
            var kuchi = Load("口").features;
            var b = kuchi.inkBounds;
            var hole = new Rect(b.x + b.width * 0.35f, b.y + b.height * 0.35f, b.width * 0.3f, b.height * 0.3f);
            foreach (var r in kuchi.colliderRects) Assert.IsFalse(r.Overlaps(hole), $"口の穴に Collider がある: {r}");
            var hi = Load("火").features;
            Assert.GreaterOrEqual(hi.componentCount, 2, "火の点が独立した部品として残っていない");
            Assert.Greater(hi.colliderRects.Length, kuchi.colliderRects.Length, "火の方が輪郭が複雑");
        }

        [Test]
        public void HolesAreNotFilled()
        {
            var m = new GlyphMask(256);
            for (int y = 40; y < 216; y++)
            for (int x = 40; x < 216; x++)
                m[x, y] = x < 64 || x >= 192 || y < 64 || y >= 192;
            var f = GlyphMaskAnalyzer.Analyze(m, AnalyzerSettings.From(B));
            foreach (var r in f.colliderRects)
                Assert.IsFalse(r.Overlaps(new Rect(72, 72, 112, 112)), $"穴の内側に Collider がある: {r}");
        }

        [Test]
        public void ThinStrokeKeepsAtLeastOneCell()
        {
            var m = new GlyphMask(256);
            for (int x = 30; x < 220; x++) m[x, 129] = true;
            var s = AnalyzerSettings.From(B);
            s.dilateForColliders = false;
            Assert.Greater(GlyphMaskAnalyzer.Analyze(m, s).colliderRects.Length, 0);
        }

        [Test]
        public void SmallSpecksAreExcluded()
        {
            var m = new GlyphMask(256);
            for (int y = 100; y < 140; y++) for (int x = 60; x < 200; x++) m[x, y] = true;
            for (int y = 20; y < 23; y++) for (int x = 20; x < 23; x++) m[x, y] = true;
            var f = GlyphMaskAnalyzer.Analyze(m, AnalyzerSettings.From(B));
            Assert.AreEqual(1, f.componentCount);
            foreach (var r in f.colliderRects) Assert.Greater(r.yMin, 50f);
        }

        [Test]
        public void UnsupportedInputIsRejectedWithReason()
        {
            GlyphCatalog.ClearCache();
            Assert.IsFalse(GlyphCatalog.TryGet("", FontStyleId.Gothic, B, GlyphCalibration.Default, out _, out var r1));
            Assert.IsFalse(GlyphCatalog.TryGet("一鬱", FontStyleId.Gothic, B, GlyphCalibration.Default, out _, out var r2));
            // ベイク済みに無い文字は実行時に字形化される。同梱フォントに字形の無い文字だけを理由付きで断る
            Assert.IsTrue(GlyphCatalog.TryGet("猫", FontStyleId.Gothic, B, GlyphCalibration.Default, out _, out _));
            Assert.IsFalse(GlyphCatalog.TryGet("\u0E01", FontStyleId.Gothic, B, GlyphCalibration.Default, out _, out var r3));
            Assert.IsNotEmpty(r1); Assert.IsNotEmpty(r2);
            StringAssert.Contains("字形がありません", r3);
            GlyphCatalog.ClearCache();
        }
    }
}
