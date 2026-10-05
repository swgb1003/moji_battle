using UnityEngine;

namespace MojiBattle
{
    /// <summary>
    /// 握る位置（GripPoint）の算出（カスタマイズ仕様 8）。
    /// 0〜1 を字形の外接矩形の「長い方の軸」に沿って割り当て、その位置の画線（インク）の太さ方向の中央を握る。
    /// 仕様書の例は横軸（左端→右端）だが、縦長の字形（I など）は横軸だとほとんど動かないため長軸を使う。
    /// 横長・正方形の字形では仕様書どおり 0=左端 / 1=右端。
    /// </summary>
    public static class WeaponGripController
    {
        /// <summary>握り位置（マスク画素空間）。gripPosition は Clamp 済みの値を渡す。</summary>
        public static Vector2 GripPixel(GlyphDefinitionRuntime glyph, float gripPosition)
        {
            var f = glyph.features;
            var b = f.inkBounds;
            var tex = glyph.texture;
            bool alongX = b.width >= b.height;
            int lo = alongX ? b.xMin : b.yMin, hi = (alongX ? b.xMax : b.yMax) - 1;
            int target = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(lo, hi, gripPosition)), lo, hi);
            Color32[] px = tex != null && tex.isReadable ? tex.GetPixels32() : null;
            if (px == null) return f.gripPoint;
            int w = tex.width;
            bool Ink(int x, int y) => x >= 0 && y >= 0 && x < w && y < tex.height && px[y * w + x].a >= 128;
            // 目標の列（行）にインクが無ければ、近い列（行）を探す
            for (int step = 0; step <= hi - lo; step++)
            {
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    int line = target + step * sign;
                    if (line < lo || line > hi) continue;
                    if (TryCenterOfRun(line, alongX, b, f.centerOfMass, Ink, out var grip)) return grip;
                    if (step == 0) break;
                }
            }
            return f.gripPoint;
        }

        /// <summary>列（または行）上のインクの連続のうち、重心に最も近いものの中央。</summary>
        static bool TryCenterOfRun(int line, bool alongX, RectInt b, Vector2 com, System.Func<int, int, bool> ink, out Vector2 grip)
        {
            grip = default;
            int from = alongX ? b.yMin : b.xMin, to = alongX ? b.yMax : b.xMax;
            float reference = alongX ? com.y : com.x;
            float best = float.MaxValue;
            bool found = false;
            int i = from;
            while (i < to)
            {
                if (!(alongX ? ink(line, i) : ink(i, line))) { i++; continue; }
                int start = i;
                while (i < to && (alongX ? ink(line, i) : ink(i, line))) i++;
                float center = (start + i) * 0.5f;
                float d = Mathf.Abs(center - reference);
                if (d < best)
                {
                    best = d;
                    found = true;
                    grip = alongX ? new Vector2(line + 0.5f, center) : new Vector2(center, line + 0.5f);
                }
            }
            return found;
        }

        /// <summary>
        /// 握りまわりの慣性モーメント（Collider 矩形に質量を面積比で配る解析値）。
        /// 実 Rigidbody の慣性と同じ傾向で、基準構成との比（扱いにくさ）の計算に使う。
        /// </summary>
        public static float InertiaAboutGrip(GlyphFeatures f, Vector2 gripPx, float worldPerPx, float mass)
        {
            float area = 0f;
            foreach (var r in f.colliderRects) area += r.width * r.height;
            if (area <= 0f) return 0f;
            float inertia = 0f;
            foreach (var r in f.colliderRects)
            {
                float m = mass * r.width * r.height / area;
                inertia += m * ((r.width * r.width + r.height * r.height) / 12f + (r.center - gripPx).sqrMagnitude);
            }
            return inertia * worldPerPx * worldPerPx;
        }
    }
}
