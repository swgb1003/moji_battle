using System.Collections.Generic;
using UnityEngine;

namespace MojiBattle
{
    /// <summary>Split supported kanji using their original glyphs. The class name is retained for callers.</summary>
    public static class SplitMingGlyphs
    {
        static readonly Dictionary<GlyphDefinitionRuntime, GlyphDefinitionRuntime[]> cache = new Dictionary<GlyphDefinitionRuntime, GlyphDefinitionRuntime[]>();

        public static bool TryGet(FighterBuildData build, CombatBalance balance, GlyphCalibration calibration,
            bool offhand, out GlyphDefinitionRuntime glyph, out string reason)
        {
            glyph = null;
            if (build.IsSplitMing && build.HasFusion)
            {
                bool ok = FusionGlyphs.TryGet(build.fusion, build.fontType, balance, calibration,
                    (offhand ? 1 : 0) ^ (build.splitSwapped ? 1 : 0), out glyph, out reason);
                if (ok) glyph = GlyphFlip.Apply(glyph, build.weaponFlip);
                return ok;
            }
            if (!build.IsSplitMing || !SplitKanjiCatalog.TryGet(build.character, out var entry))
            { reason = "この漢字は分離に対応していません"; return false; }
            if (!GlyphCatalog.TryGet(build.character, build.fontType, balance, calibration, out var source, out reason)) return false;
            if (!cache.TryGetValue(source, out var parts))
            {
                var tex = source.texture;
                var px = tex.GetPixels32();
                var bounds = source.features.inkBounds;
                int threshold = Mathf.CeilToInt(balance.alphaThreshold * 255f);
                var owner = Separate(source, px, threshold, entry);
                var outputs = new Color32[2][];
                var counts = new int[2];
                for (int part = 0; part < 2; part++)
                {
                    outputs[part] = new Color32[px.Length];
                    for (int i = 0; i < px.Length; i++) if (owner[i] == part) outputs[part][i] = px[i];
                    CleanFragments(outputs[part], tex.width, threshold);
                    foreach (var pixel in outputs[part]) if (pixel.a >= threshold) counts[part]++;
                }
                if (counts[0] == 0 || counts[1] == 0) { reason = "字形を二つの部品に分離できません"; return false; }
                parts = new GlyphDefinitionRuntime[2];
                float sourceMass = StatCalculator.Compute(source, balance).weaponMass;
                int totalInk = counts[0] + counts[1];
                for (int part = 0; part < 2; part++)
                {
                    var output = outputs[part];
                    var mask = GlyphMask.FromAlpha(output, tex.width, balance.alphaThreshold);
                    var features = GlyphMaskAnalyzer.Analyze(mask, AnalyzerSettings.From(balance));
                    var texture = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false, true)
                    { name = "Split_" + build.character + "_" + source.font + "_" + part, filterMode = tex.filterMode, wrapMode = TextureWrapMode.Clamp };
                    texture.SetPixels32(output); texture.Apply(false, false);
                    parts[part] = new GlyphDefinitionRuntime
                    {
                        grapheme = part == 0 ? entry.First : entry.Second, font = source.font, texture = texture,
                        features = features, normalized = calibration.Normalize(features), runtimeBaked = true,
                        weaponSizeRatio = Mathf.Max(features.inkBounds.width, features.inkBounds.height)
                            / (float)Mathf.Max(bounds.width, bounds.height),
                        weaponMassBasis = sourceMass * counts[part] / totalInk,
                    };
                }
                cache[source] = parts;
            }
            int index = (offhand ? 1 : 0) ^ (build.splitSwapped ? 1 : 0);
            glyph = GlyphFlip.Apply(parts[index], build.weaponFlip);
            reason = null;
            return true;
        }

        static int[] Separate(GlyphDefinitionRuntime source, Color32[] pixels, int threshold, SplitKanjiCatalog.Entry entry)
        {
            int width = source.texture.width;
            var b = source.features.inkBounds;
            if (entry.Layout == KanjiLayout.Enclosure) return SeparateEnclosure(pixels, width, b, threshold, entry.Inner);
            if (entry.Layout == KanjiLayout.TopBottom)
            {
                // Rotate clockwise so the existing gap search sees top as first and bottom as second.
                var rotated = new Color32[pixels.Length];
                for (int y = 0; y < width; y++) for (int x = 0; x < width; x++)
                    rotated[x * width + width - 1 - y] = pixels[y * width + x];
                var rect = new RectInt(width - b.yMax, b.xMin, b.height, b.width);
                var assigned = SeparateLinear(rotated, width, rect, threshold, 1f - entry.Divider, false, .08f);
                var result = new int[pixels.Length];
                for (int y = 0; y < width; y++) for (int x = 0; x < width; x++)
                    result[y * width + x] = assigned[x * width + width - 1 - y];
                return result;
            }
            return SeparateLinear(pixels, width, b, threshold, entry.Divider,
                source.grapheme == "明" && source.font != FontStyleId.Brush, entry.First == "亻" ? .08f : .2f);
        }

        // A low-ink path follows the gap even when a curved stroke extends under
        // the other component. The structural data limits the search to the true divider.
        static int[] SeparateLinear(Color32[] pixels, int width, RectInt b, int threshold, float divider, bool wholeMing, float search = .08f)
        {
            int guide = Mathf.RoundToInt(b.xMin + b.width * divider);
            int lo = Mathf.Max(b.xMin + 2, guide - Mathf.RoundToInt(b.width * search));
            int hi = Mathf.Min(b.xMax - 2, guide + Mathf.RoundToInt(b.width * search));
            int columns = hi - lo + 1, rows = b.height;
            var costs = new float[columns * rows]; var previous = new int[costs.Length];
            for (int row = 0; row < rows; row++)
            for (int col = 0; col < columns; col++)
            {
                int x = lo + col, y = b.yMin + row;
                float ink = 0;
                for (int dx = -1; dx <= 1; dx++) ink += pixels[y * width + x + dx].a;
                float cost = ink + .012f * (x - guide) * (x - guide);
                int best = col; float prior = 0;
                if (row > 0)
                {
                    prior = float.PositiveInfinity;
                    for (int pc = Mathf.Max(0, col - 2); pc <= Mathf.Min(columns - 1, col + 2); pc++)
                    {
                        float candidate = costs[(row - 1) * columns + pc] + Mathf.Abs(pc - col) * .8f;
                        if (candidate < prior) { prior = candidate; best = pc; }
                    }
                }
                costs[row * columns + col] = cost + prior; previous[row * columns + col] = best;
            }
            int end = 0;
            for (int c = 1; c < columns; c++) if (costs[(rows - 1) * columns + c] < costs[(rows - 1) * columns + end]) end = c;
            var cuts = new int[width];
            for (int row = rows - 1; row >= 0; row--) { cuts[b.yMin + row] = lo + end; end = previous[row * columns + end]; }
            for (int y = 0; y < b.yMin; y++) cuts[y] = cuts[b.yMin];
            for (int y = b.yMax; y < width; y++) cuts[y] = cuts[b.yMax - 1];
            var owner = new int[pixels.Length];
            for (int i = 0; i < owner.Length; i++) owner[i] = i % width < cuts[i / width] ? 0 : 1;
            // A detached stroke belongs to one component in its entirety. Repair
            // tiny incursions by the path, preserving dots and sweeping strokes.
            foreach (var component in Components(pixels, width, threshold))
            {
                int left = 0; float centre = 0; int minX = width, maxX = 0;
                foreach (int i in component)
                {
                    int x = i % width; centre += x;
                    minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                    if (owner[i] == 0) left++;
                }
                // If a detached part lies entirely on its semantic side of the guide,
                // keep it whole even when the low-ink path wandered into one of its holes.
                float cx = centre / component.Count;
                if ((cx > guide && minX >= guide - b.width * .05f) || (cx < guide && maxX <= guide + b.width * .05f))
                {
                    foreach (int i in component) owner[i] = cx < guide ? 0 : 1;
                    continue;
                }
                // In the three print fonts, 明 has two separate connected parts.
                // 月's curved foot can cross far under 日: retain it in full.
                if (!wholeMing && Mathf.Min(left, component.Count - left) > component.Count * .1f) continue;
                if (!wholeMing)
                {
                    // A thin radical can be less than 10% of a connected brush glyph.
                    // Only heal incursions close to the seam, never absorb that whole radical.
                    int minority = left < component.Count - left ? 0 : 1;
                    bool reachesOwnRegion = false; int minorityMin = width, minorityMax = 0;
                    foreach (int i in component)
                        if (owner[i] == minority)
                        {
                            minorityMin = Mathf.Min(minorityMin, i / width); minorityMax = Mathf.Max(minorityMax, i / width);
                            if (Mathf.Abs(i % width - guide) > b.width * .14f) reachesOwnRegion = true;
                        }
                    if (reachesOwnRegion || minorityMax - minorityMin > b.height * .35f) continue;
                }
                int part = wholeMing ? (centre / component.Count < guide ? 0 : 1) : (left > component.Count / 2 ? 0 : 1);
                foreach (int i in component) owner[i] = part;
            }
            AssignEdges(pixels, width, threshold, owner);
            return owner;
        }

        static int[] SeparateEnclosure(Color32[] pixels, int width, RectInt bounds, int threshold, Rect inner)
        {
            int x0 = Mathf.RoundToInt(bounds.xMin + bounds.width * inner.xMin);
            int x1 = Mathf.RoundToInt(bounds.xMin + bounds.width * inner.xMax);
            int y0 = Mathf.RoundToInt(bounds.yMin + bounds.height * inner.yMin);
            int y1 = Mathf.RoundToInt(bounds.yMin + bounds.height * inner.yMax);
            // Fit each side to the low-ink gap between the surrounding frame and its contents.
            int FindGap(int guide, bool vertical, int start, int end, int radius)
            {
                int best = guide; float minimum = float.PositiveInfinity;
                for (int p = guide - radius; p <= guide + radius; p++)
                {
                    float cost = (p - guide) * (p - guide) * .7f;
                    for (int q = start; q <= end; q++)
                        for (int d = -1; d <= 1; d++)
                            cost += pixels[vertical ? q * width + p + d : (p + d) * width + q].a;
                    if (cost < minimum) { minimum = cost; best = p; }
                }
                return best;
            }
            int rx = Mathf.Max(2, Mathf.RoundToInt(bounds.width * .06f));
            int ry = Mathf.Max(2, Mathf.RoundToInt(bounds.height * .06f));
            int left = FindGap(x0, true, y0, y1, rx), right = FindGap(x1, true, y0, y1, rx);
            int bottom = FindGap(y0, false, left, right, ry), top = FindGap(y1, false, left, right, ry);
            var owner = new int[pixels.Length];
            for (int i = 0; i < owner.Length; i++)
            {
                int x = i % width, y = i / width;
                owner[i] = x > left && x < right && y > bottom && y < top ? 1 : 0;
            }
            foreach (var component in Components(pixels, width, threshold))
            {
                int inside = 0;
                int minX = width, maxX = 0, minY = width, maxY = 0;
                foreach (int i in component)
                {
                    if (owner[i] == 1) inside++;
                    minX = Mathf.Min(minX, i % width); maxX = Mathf.Max(maxX, i % width);
                    minY = Mathf.Min(minY, i / width); maxY = Mathf.Max(maxY, i / width);
                }
                // Brush frames sometimes have an independent curved side that leans
                // into the rectangular interior. Retain that entire side in the frame.
                bool frameSide = maxY - minY > bounds.height * .60f && maxX - minX < bounds.width * .25f
                    && (maxX < bounds.xMin + bounds.width * .30f || minX > bounds.xMin + bounds.width * .70f);
                if (frameSide) { foreach (int i in component) owner[i] = 0; continue; }
                if (Mathf.Min(inside, component.Count - inside) > component.Count * .1f) continue;
                int part = inside > component.Count / 2 ? 1 : 0;
                foreach (int i in component) owner[i] = part;
            }
            AssignEdges(pixels, width, threshold, owner);
            return owner;
        }

        static void AssignEdges(Color32[] pixels, int width, int threshold, int[] owner)
        {
            // Assign antialiasing only around its own solid strokes.
            var queue = new Queue<int>(); var visited = new bool[owner.Length];
            for (int i = 0; i < owner.Length; i++) if (pixels[i].a >= threshold) { queue.Enqueue(i); visited[i] = true; }
            while (queue.Count > 0)
            {
                int p = queue.Dequeue();
                foreach (int next in Neighbours(p, width))
                    if (!visited[next]) { visited[next] = true; owner[next] = owner[p]; queue.Enqueue(next); }
            }
        }

        static IEnumerable<int> Neighbours(int p, int width)
        {
            int x = p % width, y = p / width;
            if (x > 0) yield return p - 1;
            if (x + 1 < width) yield return p + 1;
            if (y > 0) yield return p - width;
            if (y + 1 < width) yield return p + width;
        }
        static List<List<int>> Components(Color32[] pixels, int width, int threshold)
        {
            var result = new List<List<int>>(); var visited = new bool[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                if (visited[i] || pixels[i].a < threshold) continue;
                var component = new List<int> { i }; visited[i] = true;
                for (int n = 0; n < component.Count; n++)
                    foreach (int next in Neighbours(component[n], width))
                        if (!visited[next] && pixels[next].a >= threshold) { visited[next] = true; component.Add(next); }
                result.Add(component);
            }
            return result;
        }

        static void CleanFragments(Color32[] pixels, int width, int threshold)
        {
            int ink = 0; foreach (var p in pixels) if (p.a >= threshold) ink++;
            int minimum = Mathf.Max(24, Mathf.CeilToInt(ink * .008f));
            var keep = new bool[pixels.Length];
            foreach (var component in Components(pixels, width, threshold))
                if (component.Count >= minimum) foreach (int i in component) keep[i] = true;
            var edge = new bool[keep.Length];
            for (int i = 0; i < keep.Length; i++)
            {
                if (!keep[i]) continue;
                int x = i % width, y = i / width;
                for (int dy = -2; dy <= 2; dy++)
                for (int dx = -2; dx <= 2; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx >= 0 && nx < width && ny >= 0 && ny < width && dx * dx + dy * dy <= 4) edge[ny * width + nx] = true;
                }
            }
            for (int i = 0; i < pixels.Length; i++)
                if (!edge[i] || (pixels[i].a >= threshold && !keep[i])) pixels[i] = default;
        }

        public static void ClearCache()
        {
            foreach (var parts in cache.Values)
                foreach (var part in parts) if (part.texture != null) Object.Destroy(part.texture);
            cache.Clear();
        }
    }
}
