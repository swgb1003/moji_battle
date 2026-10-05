using System;
using System.Collections.Generic;
using UnityEngine;

namespace MojiBattle
{
    /// <summary>二値マスク（x右・y上、row-major）。</summary>
    public sealed class GlyphMask
    {
        public readonly int Size;
        public readonly bool[] Ink;

        public GlyphMask(int size)
        {
            Size = size;
            Ink = new bool[size * size];
        }

        public bool this[int x, int y]
        {
            get => x >= 0 && y >= 0 && x < Size && y < Size && Ink[y * Size + x];
            set => Ink[y * Size + x] = value;
        }

        public int Count()
        {
            int n = 0;
            for (int i = 0; i < Ink.Length; i++) if (Ink[i]) n++;
            return n;
        }

        public GlyphMask Clone()
        {
            var m = new GlyphMask(Size);
            Array.Copy(Ink, m.Ink, Ink.Length);
            return m;
        }

        /// <summary>alpha >= threshold を黒画素とする固定閾値二値化。</summary>
        public static GlyphMask FromAlpha(Color32[] pixels, int size, float threshold)
        {
            var m = new GlyphMask(size);
            byte t = (byte)Mathf.Clamp(Mathf.CeilToInt(threshold * 255f), 0, 255);
            for (int i = 0; i < pixels.Length; i++) m.Ink[i] = pixels[i].a >= t;
            return m;
        }
    }

    public struct AnalyzerSettings
    {
        public float emSizePx;
        public int colliderGrid;
        public float cellOccupancy;
        public int maxColliders;
        public int minComponentPixels;
        public bool dilateForColliders;

        public static AnalyzerSettings From(CombatBalance b) => new AnalyzerSettings
        {
            emSizePx = b.emSizePx,
            colliderGrid = b.colliderGrid,
            cellOccupancy = b.cellOccupancy,
            maxColliders = b.maxColliders,
            minComponentPixels = b.minComponentPixels,
            dilateForColliders = b.dilateForColliders,
        };
    }

    /// <summary>
    /// 字形マスクから特徴量（5.2）と Collider 矩形（6.1）を算出する。文字IDは一切参照しない。
    /// </summary>
    public static class GlyphMaskAnalyzer
    {
        public static GlyphFeatures Analyze(GlyphMask source, AnalyzerSettings s)
        {
            var mask = RemoveSmallComponents(source, s.minComponentPixels, out int components);
            int n = mask.Size;
            int count = 0;
            int xMin = n, yMin = n, xMax = -1, yMax = -1;
            double sx = 0, sy = 0;
            int edges = 0;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                if (!mask[x, y]) continue;
                count++;
                sx += x + 0.5; sy += y + 0.5;
                if (x < xMin) xMin = x;
                if (x > xMax) xMax = x;
                if (y < yMin) yMin = y;
                if (y > yMax) yMax = y;
                if (!mask[x - 1, y]) edges++;
                if (!mask[x + 1, y]) edges++;
                if (!mask[x, y - 1]) edges++;
                if (!mask[x, y + 1]) edges++;
            }

            var f = new GlyphFeatures { maskSize = n, emSizePx = s.emSizePx, componentCount = components };
            if (count == 0)
            {
                f.colliderRects = Array.Empty<Rect>();
                return f;
            }

            float em = s.emSizePx;
            f.inkRatio = count / (em * em);
            f.widthRatio = (xMax - xMin + 1) / em;
            f.heightRatio = (yMax - yMin + 1) / em;
            f.edgeRatio = edges / em;
            f.centerOfMass = new Vector2((float)(sx / count), (float)(sy / count));
            f.inkBounds = new RectInt(xMin, yMin, xMax - xMin + 1, yMax - yMin + 1);

            // 左右バランス: em中心で左右半分の黒画素量差と、重心の中心からのずれ。対称なほど 1。
            float cxEm = n * 0.5f;
            int left = 0;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                if (mask[x, y] && x + 0.5f < cxEm) left++;
            int right = count - left;
            float diff = Mathf.Abs(left - right) / (float)count;
            float comOffset = Mathf.Clamp01(Mathf.Abs(f.centerOfMass.x - cxEm) / (em * 0.5f));
            f.balance = 1f - (0.5f * diff + 0.5f * comOffset);

            f.gripPoint = FindGrip(mask, f.inkBounds);

            var colliderMask = s.dilateForColliders ? Dilate(mask) : mask;
            f.colliderRects = BuildColliderRects(colliderMask, mask, s, out int gridUsed);
            f.colliderGridUsed = gridUsed;
            return f;
        }

        /// <summary>外接矩形の左下寄り（幅・高さの6%内側）に最も近いインク画素を探し、その画線の太さ方向の中央を握りにする。</summary>
        static Vector2 FindGrip(GlyphMask m, RectInt b)
        {
            float tx = b.xMin + 0.06f * b.width, ty = b.yMin + 0.06f * b.height;
            float best = float.MaxValue;
            int bx = b.xMin, by = b.yMin;
            for (int y = b.yMin; y < b.yMax; y++)
            for (int x = b.xMin; x < b.xMax; x++)
            {
                if (!m[x, y]) continue;
                float d = (x - tx) * (x - tx) + (y - ty) * (y - ty);
                if (d < best) { best = d; bx = x; by = y; }
            }
            int y0 = by, y1 = by;
            while (m[bx, y0 - 1]) y0--;
            while (m[bx, y1 + 1]) y1++;
            int x0 = bx, x1 = bx;
            while (m[x0 - 1, by]) x0--;
            while (m[x1 + 1, by]) x1++;
            int vertical = y1 - y0 + 1, horizontal = x1 - x0 + 1;
            // 画線の太さ方向（短い方の連続）にだけ中央へ寄せる。握りは常にインク上で、長い方向の端寄りに残る
            if (vertical <= horizontal) return new Vector2(bx + 0.5f, (y0 + y1 + 1) * 0.5f);
            return new Vector2((x0 + x1 + 1) * 0.5f, by + 0.5f);
        }

        /// <summary>8近傍の連結成分のうち面積が閾値未満のものを除外（筆文字の飛沫対策）。</summary>
        public static GlyphMask RemoveSmallComponents(GlyphMask src, int minPixels, out int keptComponents)
        {
            int n = src.Size;
            var result = src.Clone();
            var label = new int[n * n];
            var stack = new Stack<int>();
            var pixels = new List<int>();
            keptComponents = 0;
            int next = 0;
            for (int i = 0; i < n * n; i++)
            {
                if (!src.Ink[i] || label[i] != 0) continue;
                next++;
                pixels.Clear();
                stack.Push(i);
                label[i] = next;
                while (stack.Count > 0)
                {
                    int p = stack.Pop();
                    pixels.Add(p);
                    int px = p % n, py = p / n;
                    for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int qx = px + dx, qy = py + dy;
                        if (qx < 0 || qy < 0 || qx >= n || qy >= n) continue;
                        int q = qy * n + qx;
                        if (src.Ink[q] && label[q] == 0) { label[q] = next; stack.Push(q); }
                    }
                }
                if (pixels.Count < minPixels)
                    foreach (int p in pixels) result.Ink[p] = false;
                else
                    keptComponents++;
            }
            return result;
        }

        public static GlyphMask Dilate(GlyphMask src)
        {
            int n = src.Size;
            var r = new GlyphMask(n);
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                r[x, y] = src[x, y] || src[x - 1, y] || src[x + 1, y] || src[x, y - 1] || src[x, y + 1];
            return r;
        }

        /// <summary>
        /// 面積平均で grid×grid に縮小し、占有率 >= 閾値のセルを実体とする。
        /// 横方向の連続セルをランにまとめ、同じ範囲のランが続く行を縦に結合して矩形化する（6.1）。
        /// 上限を超えたら同解像度で最大矩形の貪欲分割を試し、それでも超える場合は
        /// 「画線から3pxより離れた画素の増加が最小」の矩形対から統合して上限に収める。穴は埋めない。
        /// </summary>
        public static Rect[] BuildColliderRects(GlyphMask colliderMask, GlyphMask inkMask, AnalyzerSettings s, out int gridUsed)
        {
            int grid = Mathf.Max(4, s.colliderGrid);
            gridUsed = grid;
            int cell = colliderMask.Size / grid;
            var solid = SolidCells(colliderMask, grid, s.cellOccupancy);
            var cells = RowRunRects(solid, grid);
            if (cells.Count > s.maxColliders) cells = GreedyMaxRects(solid, grid);
            var px = new List<RectInt>(cells.Count);
            foreach (var c in cells) px.Add(TightenToInk(colliderMask, new RectInt(c.x * cell, c.y * cell, c.width * cell, c.height * cell)));
            if (px.Count > s.maxColliders) MergeToLimit(px, inkMask, s.maxColliders);
            px.Sort((a, b) => a.y != b.y ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
            var result = new Rect[px.Count];
            for (int i = 0; i < px.Count; i++) result[i] = new Rect(px[i].x, px[i].y, px[i].width, px[i].height);
            return result;
        }

        /// <summary>画線から radius px より遠い画素の2次元累積和で、矩形の「はみ出し量」を O(1) で数える。</summary>
        sealed class FarField
        {
            readonly int n;
            readonly int[] sum;

            public FarField(GlyphMask ink, int radius)
            {
                n = ink.Size;
                var near = new bool[n * n];
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    if (!ink[x, y]) continue;
                    for (int dy = -radius; dy <= radius; dy++)
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        int qx = x + dx, qy = y + dy;
                        if (dx * dx + dy * dy <= radius * radius && qx >= 0 && qy >= 0 && qx < n && qy < n) near[qy * n + qx] = true;
                    }
                }
                sum = new int[(n + 1) * (n + 1)];
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    sum[(y + 1) * (n + 1) + x + 1] = sum[y * (n + 1) + x + 1] + sum[(y + 1) * (n + 1) + x] - sum[y * (n + 1) + x] + (near[y * n + x] ? 0 : 1);
            }

            public int Count(RectInt r) =>
                sum[r.yMax * (n + 1) + r.xMax] - sum[r.yMin * (n + 1) + r.xMax] - sum[r.yMax * (n + 1) + r.xMin] + sum[r.yMin * (n + 1) + r.xMin];
        }

        static RectInt Union(RectInt a, RectInt b)
        {
            int x0 = Mathf.Min(a.xMin, b.xMin), y0 = Mathf.Min(a.yMin, b.yMin);
            int x1 = Mathf.Max(a.xMax, b.xMax), y1 = Mathf.Max(a.yMax, b.yMax);
            return new RectInt(x0, y0, x1 - x0, y1 - y0);
        }

        static void MergeToLimit(List<RectInt> rects, GlyphMask ink, int max)
        {
            var far = new FarField(ink, 3);
            while (rects.Count > max)
            {
                long bestCost = long.MaxValue;
                int bi = -1, bj = -1;
                RectInt bestU = default;
                for (int i = 0; i < rects.Count; i++)
                for (int j = i + 1; j < rects.Count; j++)
                {
                    var u = Union(rects[i], rects[j]);
                    int growth = u.width * u.height - rects[i].width * rects[i].height - rects[j].width * rects[j].height;
                    long cost = (long)(far.Count(u) - far.Count(rects[i]) - far.Count(rects[j])) * 1000000L + growth;
                    if (cost < bestCost) { bestCost = cost; bi = i; bj = j; bestU = u; }
                }
                rects.RemoveAt(bj);
                rects.RemoveAt(bi);
                rects.RemoveAll(r => r.xMin >= bestU.xMin && r.yMin >= bestU.yMin && r.xMax <= bestU.xMax && r.yMax <= bestU.yMax);
                rects.Add(bestU);
            }
        }

        /// <summary>
        /// セル単位の矩形を、その内側にあるインクの外接矩形まで画素単位で締める。
        /// 解像度を下げた場合でも Collider が画線からはみ出さないようにする（個数は変えない）。
        /// </summary>
        static RectInt TightenToInk(GlyphMask mask, RectInt r)
        {
            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = -1, y1 = -1;
            for (int y = r.yMin; y < r.yMax; y++)
            for (int x = r.xMin; x < r.xMax; x++)
            {
                if (!mask[x, y]) continue;
                if (x < x0) x0 = x;
                if (x > x1) x1 = x;
                if (y < y0) y0 = y;
                if (y > y1) y1 = y;
            }
            if (x1 < 0) return r;
            return new RectInt(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
        }

        static bool[] SolidCells(GlyphMask mask, int grid, float occupancy)
        {
            int n = mask.Size;
            int cell = n / grid;
            var occ = new float[grid * grid];
            for (int gy = 0; gy < grid; gy++)
            for (int gx = 0; gx < grid; gx++)
            {
                int c = 0;
                for (int y = gy * cell; y < (gy + 1) * cell; y++)
                for (int x = gx * cell; x < (gx + 1) * cell; x++)
                    if (mask[x, y]) c++;
                occ[gy * grid + gx] = c / (float)(cell * cell);
            }
            var solid = new bool[grid * grid];
            for (int i = 0; i < solid.Length; i++) solid[i] = occ[i] >= occupancy;
            EnsureThinComponentsHaveCells(mask, grid, cell, occ, solid);
            return solid;
        }

        /// <summary>仕様 6.1 の基本手順: 行ごとのランを作り、同じ範囲のランが続く行を縦に結合。</summary>
        static List<RectInt> RowRunRects(bool[] solid, int grid)
        {
            // 行ごとのラン → 縦結合
            var open = new Dictionary<(int x0, int x1), RectInt>();
            var done = new List<RectInt>();
            for (int gy = 0; gy < grid; gy++)
            {
                var runs = new List<(int, int)>();
                int gx = 0;
                while (gx < grid)
                {
                    if (!solid[gy * grid + gx]) { gx++; continue; }
                    int start = gx;
                    while (gx < grid && solid[gy * grid + gx]) gx++;
                    runs.Add((start, gx - 1));
                }
                var nextOpen = new Dictionary<(int, int), RectInt>();
                foreach (var run in runs)
                {
                    if (open.TryGetValue(run, out var r))
                    {
                        r.height += 1;
                        nextOpen[run] = r;
                        open.Remove(run);
                    }
                    else nextOpen[run] = new RectInt(run.Item1, gy, run.Item2 - run.Item1 + 1, 1);
                }
                foreach (var kv in open) done.Add(kv.Value);
                open = nextOpen;
            }
            foreach (var kv in open) done.Add(kv.Value);
            done.Sort((a, b) => a.y != b.y ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
            return done;
        }

        /// <summary>未被覆の実体セルから最大面積の矩形を繰り返し取り出す（重なりなし）。</summary>
        static List<RectInt> GreedyMaxRects(bool[] solid, int grid)
        {
            var left = (bool[])solid.Clone();
            var result = new List<RectInt>();
            var heights = new int[grid];
            while (true)
            {
                int bestArea = 0;
                RectInt best = default;
                System.Array.Clear(heights, 0, grid);
                for (int y = 0; y < grid; y++)
                {
                    for (int x = 0; x < grid; x++) heights[x] = left[y * grid + x] ? heights[x] + 1 : 0;
                    for (int x = 0; x < grid; x++)
                    {
                        int h = int.MaxValue;
                        for (int x2 = x; x2 < grid && heights[x2] > 0; x2++)
                        {
                            h = System.Math.Min(h, heights[x2]);
                            int area = h * (x2 - x + 1);
                            if (area > bestArea) { bestArea = area; best = new RectInt(x, y - h + 1, x2 - x + 1, h); }
                        }
                    }
                }
                if (bestArea == 0) break;
                for (int y = best.yMin; y < best.yMax; y++)
                for (int x = best.xMin; x < best.xMax; x++)
                    left[y * grid + x] = false;
                result.Add(best);
            }
            result.Sort((a, b) => a.y != b.y ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
            return result;
        }

        /// <summary>占有率閾値で実体セルを1つも持たない細い連結成分に、最も占有率の高いセルを与える。</summary>
        static void EnsureThinComponentsHaveCells(GlyphMask mask, int grid, int cell, float[] occ, bool[] solid)
        {
            // セル単位の4近傍連結（インクを含むセル）で成分を作る
            var hasInk = new bool[grid * grid];
            for (int i = 0; i < occ.Length; i++) hasInk[i] = occ[i] > 0f;
            var visited = new bool[grid * grid];
            var stack = new Stack<int>();
            var members = new List<int>();
            for (int i = 0; i < hasInk.Length; i++)
            {
                if (!hasInk[i] || visited[i]) continue;
                members.Clear();
                stack.Push(i); visited[i] = true;
                bool anySolid = false;
                float maxOcc = 0f;
                while (stack.Count > 0)
                {
                    int p = stack.Pop();
                    members.Add(p);
                    if (solid[p]) anySolid = true;
                    if (occ[p] > maxOcc) maxOcc = occ[p];
                    int px = p % grid, py = p / grid;
                    void Visit(int qx, int qy)
                    {
                        if (qx < 0 || qy < 0 || qx >= grid || qy >= grid) return;
                        int q = qy * grid + qx;
                        if (hasInk[q] && !visited[q]) { visited[q] = true; stack.Push(q); }
                    }
                    Visit(px - 1, py); Visit(px + 1, py); Visit(px, py - 1); Visit(px, py + 1);
                }
                if (anySolid || maxOcc <= 0f) continue;
                foreach (int p in members)
                    if (occ[p] >= maxOcc * 0.5f) solid[p] = true;
            }
        }
    }
}
