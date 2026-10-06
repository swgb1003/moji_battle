using System.Collections.Generic;
using UnityEngine;

namespace MojiBattle
{
    /// <summary>武器として持つ字形の反転（カスタマイズ）。</summary>
    public enum WeaponFlip { None, Horizontal, Vertical, Both }

    /// <summary>
    /// 字形を左右・上下に反転した字形を作る。画像と解析結果（重心・Collider・外接矩形・既定の握り）を同じ鏡映で写すので、
    /// 表示・当たり判定・握る位置・重心がすべて反転後の形に従う。ステータス（黒画素量・幅・高さ・輪郭・左右バランス）は鏡映で変わらない。
    /// </summary>
    public static class GlyphFlip
    {
        static readonly Dictionary<(GlyphDefinitionRuntime, WeaponFlip), GlyphDefinitionRuntime> cache =
            new Dictionary<(GlyphDefinitionRuntime, WeaponFlip), GlyphDefinitionRuntime>();
        static readonly Dictionary<GlyphDefinitionRuntime, GlyphDefinitionRuntime> sources =
            new Dictionary<GlyphDefinitionRuntime, GlyphDefinitionRuntime>();

        /// <summary>反転した字形の元の字形（反転していなければ null）。</summary>
        public static GlyphDefinitionRuntime SourceOf(GlyphDefinitionRuntime glyph) =>
            glyph != null && sources.TryGetValue(glyph, out var src) ? src : null;

        public static GlyphDefinitionRuntime Apply(GlyphDefinitionRuntime src, WeaponFlip flip)
        {
            if (src == null || flip == WeaponFlip.None) return src;
            if (cache.TryGetValue((src, flip), out var hit)) return hit;
            bool h = flip == WeaponFlip.Horizontal || flip == WeaponFlip.Both;
            bool v = flip == WeaponFlip.Vertical || flip == WeaponFlip.Both;
            var tex = src.texture;
            int w = tex.width, ht = tex.height;
            var px = tex.GetPixels32();
            var outPx = new Color32[px.Length];
            for (int y = 0; y < ht; y++)
            for (int x = 0; x < w; x++)
                outPx[(v ? ht - 1 - y : y) * w + (h ? w - 1 - x : x)] = px[y * w + x];
            var flipped = new Texture2D(w, ht, TextureFormat.RGBA32, false, true)
            {
                name = tex.name + "_" + flip,
                filterMode = tex.filterMode,
                wrapMode = TextureWrapMode.Clamp,
            };
            flipped.SetPixels32(outPx);
            flipped.Apply(false, false); // 握り位置の探索で画素を読むため読み取り可能のまま

            var f = src.features;
            Vector2 P(Vector2 p) => new Vector2(h ? w - p.x : p.x, v ? ht - p.y : p.y);
            f.centerOfMass = P(f.centerOfMass);
            f.gripPoint = P(f.gripPoint);
            var b = f.inkBounds;
            f.inkBounds = new RectInt(h ? w - b.xMax : b.xMin, v ? ht - b.yMax : b.yMin, b.width, b.height);
            var rects = new Rect[f.colliderRects.Length];
            for (int i = 0; i < rects.Length; i++)
            {
                var r = f.colliderRects[i];
                rects[i] = new Rect(h ? w - r.xMax : r.xMin, v ? ht - r.yMax : r.yMin, r.width, r.height);
            }
            f.colliderRects = rects;

            var def = new GlyphDefinitionRuntime
            {
                grapheme = src.grapheme,
                font = src.font,
                texture = flipped,
                features = f,
                normalized = src.normalized,
                runtimeBaked = src.runtimeBaked,
            };
            cache[(src, flip)] = def;
            sources[def] = src;
            return def;
        }

        public static void ClearCache()
        {
            cache.Clear();
            sources.Clear();
        }
    }
}
