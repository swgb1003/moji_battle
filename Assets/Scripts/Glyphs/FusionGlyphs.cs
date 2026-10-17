using System.Collections.Generic;
using UnityEngine;

namespace MojiBattle
{
    /// <summary>Compose cleaned source masks; rendering, physics and stats all use these same pixels.</summary>
    public static class FusionGlyphs
    {
        const int Size = 256, CacheLimit = 48;
        sealed class Entry { public GlyphDefinitionRuntime whole; public GlyphDefinitionRuntime[] parts; }
        static readonly Dictionary<string, Entry> cache = new Dictionary<string, Entry>();
        static readonly Queue<string> order = new Queue<string>();

        public static bool SourcePart(string source, FontStyleId font, bool right, out GlyphDefinitionRuntime part, out string reason)
        {
            var build = new FighterBuildData { character = source, fontType = font, gripType = GripType.DualWield, splitMing = true };
            return SplitMingGlyphs.TryGet(build, CombatBalance.Default, GlyphCalibration.Default, right, out part, out reason);
        }

        public static bool TryGet(FusionData recipe, FontStyleId font, CombatBalance balance, GlyphCalibration calibration,
            int component, out GlyphDefinitionRuntime glyph, out string reason)
        {
            glyph = null;
            if (recipe == null) { reason = "合成データがありません。"; return false; }
            if (!recipe.Validate(out reason)) return false;
            string key = recipe.Key + "|" + font;
            if (!cache.TryGetValue(key, out var entry))
            {
                if (!SourcePart(recipe.leftSource, font, recipe.leftUseSecond, out var left, out reason)
                    || !SourcePart(recipe.rightSource, font, !recipe.rightUseFirst, out var right, out reason)) return false;
                var lb = left.features.inkBounds; var rb = right.features.inkBounds;
                float lw = Mathf.Clamp(180f * lb.width / lb.height, 40, 112) * recipe.leftWidth;
                float rw = Mathf.Clamp(180f * rb.width / rb.height, 80, 146) * recipe.rightWidth;
                var l = new Rect(-recipe.gap / 2 - lw + recipe.leftX, -90 * recipe.leftHeight + recipe.leftY, lw, 180 * recipe.leftHeight);
                var r = new Rect(recipe.gap / 2 + recipe.rightX, -90 * recipe.rightHeight + recipe.rightY, rw, 180 * recipe.rightHeight);
                if (recipe.version >= 2) Placement(recipe, lb, rb, out l, out r);
                float xmin = Mathf.Min(l.xMin, r.xMin), xmax = Mathf.Max(l.xMax, r.xMax);
                float ymin = Mathf.Min(l.yMin, r.yMin), ymax = Mathf.Max(l.yMax, r.yMax);
                float fit = Mathf.Min(1, 232f / Mathf.Max(xmax - xmin, ymax - ymin));
                var center = new Vector2((xmin + xmax) / 2, (ymin + ymax) / 2);
                Rect Fit(Rect a) => new Rect((a.position - center) * fit + Vector2.one * 128, a.size * fit);
                var pixels = new[] { Paint(left, Fit(l)), Paint(right, Fit(r)) };
                var combined = new Color32[Size * Size];
                for (int i = 0; i < combined.Length; i++) combined[i] = new Color32(255, 255, 255, (byte)Mathf.Max(pixels[0][i].a, pixels[1][i].a));
                entry = new Entry { whole = Make(combined, recipe.Character, font, balance, calibration), parts = new GlyphDefinitionRuntime[2] };
                int[] counts = { 0, 0 }; int threshold = Mathf.CeilToInt(balance.alphaThreshold * 255);
                for (int p = 0; p < 2; p++) foreach (var px in pixels[p]) if (px.a >= threshold) counts[p]++;
                float mass = StatCalculator.Compute(entry.whole, balance).weaponMass;
                var wb = entry.whole.features.inkBounds;
                for (int p = 0; p < 2; p++)
                {
                    var g = Make(pixels[p], p == 0 ? recipe.Left : recipe.Right, font, balance, calibration);
                    g.weaponSizeRatio = Mathf.Max(g.features.inkBounds.width, g.features.inkBounds.height) / (float)Mathf.Max(wb.width, wb.height);
                    g.weaponMassBasis = mass * counts[p] / Mathf.Max(1, counts[0] + counts[1]); entry.parts[p] = g;
                }
                cache.Add(key, entry); order.Enqueue(key);
                while (order.Count > CacheLimit) { string old = order.Dequeue(); Dispose(cache[old]); cache.Remove(old); }
            }
            glyph = component < 0 ? entry.whole : entry.parts[Mathf.Clamp(component, 0, 1)];
            reason = null; return true;
        }

        static void Placement(FusionData recipe, RectInt first, RectInt second, out Rect l, out Rect r)
        {
            Vector2 Contain(RectInt b, float w, float h) => new Vector2(b.width, b.height) * Mathf.Min(w / b.width, h / b.height);
            if (recipe.layout == KanjiLayout.TopBottom)
            {
                var a = Vector2.Scale(Contain(first, 184, 96), new Vector2(recipe.leftWidth, recipe.leftHeight));
                var b = Vector2.Scale(Contain(second, 184, 96), new Vector2(recipe.rightWidth, recipe.rightHeight));
                l = new Rect(-a.x / 2 + recipe.leftX, recipe.gap / 2 + recipe.leftY, a.x, a.y);
                r = new Rect(-b.x / 2 + recipe.rightX, -recipe.gap / 2 - b.y + recipe.rightY, b.x, b.y);
                return;
            }
            if (recipe.layout == KanjiLayout.Enclosure)
            {
                var a = Vector2.Scale(Contain(first, 216, 216), new Vector2(recipe.leftWidth, recipe.leftHeight));
                l = new Rect(-a.x / 2 + recipe.leftX, -a.y / 2 + recipe.leftY, a.x, a.y);
                var inner = new Rect(.22f, .22f, .56f, .56f);
                if (!recipe.leftUseSecond && SplitKanjiCatalog.TryGet(recipe.leftSource, out var source) && source.Layout == KanjiLayout.Enclosure)
                    inner = source.Inner;
                var hole = new Rect(l.xMin + inner.xMin * a.x, l.yMin + inner.yMin * a.y, inner.width * a.x, inner.height * a.y);
                var b = Vector2.Scale(Contain(second, Mathf.Max(20, hole.width - recipe.gap), Mathf.Max(20, hole.height - recipe.gap)), new Vector2(recipe.rightWidth, recipe.rightHeight));
                r = new Rect(hole.center - b / 2 + new Vector2(recipe.rightX, recipe.rightY), b);
                return;
            }
            var left = Vector2.Scale(Contain(first, 112, 180), new Vector2(recipe.leftWidth, recipe.leftHeight));
            var right = Vector2.Scale(Contain(second, 146, 180), new Vector2(recipe.rightWidth, recipe.rightHeight));
            l = new Rect(-recipe.gap / 2 - left.x + recipe.leftX, -left.y / 2 + recipe.leftY, left.x, left.y);
            r = new Rect(recipe.gap / 2 + recipe.rightX, -right.y / 2 + recipe.rightY, right.x, right.y);
        }

        static Color32[] Paint(GlyphDefinitionRuntime src, Rect target)
        {
            var result = new Color32[Size * Size]; var b = src.features.inkBounds;
            // Sample only inside the cropped ink bounds; bilinear alpha keeps all four fonts smooth.
            for (int y = Mathf.Max(0, Mathf.FloorToInt(target.yMin)); y < Mathf.Min(Size, Mathf.CeilToInt(target.yMax)); y++)
            for (int x = Mathf.Max(0, Mathf.FloorToInt(target.xMin)); x < Mathf.Min(Size, Mathf.CeilToInt(target.xMax)); x++)
            {
                float u = (x + .5f - target.xMin) / target.width, v = (y + .5f - target.yMin) / target.height;
                if (u < 0 || u > 1 || v < 0 || v > 1) continue;
                float alpha = src.texture.GetPixelBilinear((b.xMin + u * b.width) / src.texture.width, (b.yMin + v * b.height) / src.texture.height).a;
                result[y * Size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255));
            }
            return result;
        }
        static GlyphDefinitionRuntime Make(Color32[] pixels, string label, FontStyleId font, CombatBalance balance, GlyphCalibration calibration)
        {
            var features = GlyphMaskAnalyzer.Analyze(GlyphMask.FromAlpha(pixels, Size, balance.alphaThreshold), AnalyzerSettings.From(balance));
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false, true)
                { name = "Fusion_" + label, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels32(pixels); texture.Apply(false, false);
            return new GlyphDefinitionRuntime { grapheme = label, font = font, texture = texture, features = features, normalized = calibration.Normalize(features), runtimeBaked = true };
        }
        static void Dispose(Entry e)
        {
            foreach (var g in new[] { e.whole, e.parts[0], e.parts[1] }) { GlyphFlip.Forget(g); if (g.texture != null) Object.Destroy(g.texture); }
        }
        public static void ClearCache() { foreach (var e in cache.Values) Dispose(e); cache.Clear(); order.Clear(); }
    }
}
