using System.Globalization;
using System.Text;
using UnityEngine;

namespace MojiBattle
{
    /// <summary>
    /// 入力された任意の 1 文字を実行時に字形化する（エディタのベイクと同じ描画・解析）。
    /// 同梱フォントの cmap に無い文字（OS フォントでの代替になる文字）・空白・制御文字・基本多言語面の外の文字は使わない。
    /// </summary>
    public static class RuntimeGlyphBaker
    {
        public static bool TryBake(string grapheme, FontStyleId style, CombatBalance b, GlyphCalibration calibration,
            out GlyphDefinitionRuntime def, out string reason)
        {
            def = null;
            if (!Validate(grapheme, out char ch, out reason)) return false;
            var lib = GlyphFontLibrary.Default;
            var entry = lib != null ? lib.Get(style) : null;
            if (entry == null)
            {
                reason = $"{GlyphCatalog.FontDisplayName(style)} のフォントは未収録です";
                return false;
            }
            if (!entry.Has(ch))
            {
                reason = $"「{grapheme}」は{GlyphCatalog.FontDisplayName(style)}のフォントに字形がありません";
                return false;
            }
            var pixels = GlyphRenderer.Render(entry.font, ch, Mathf.RoundToInt(b.emSizePx), lib.copyMaterial, out string renderError);
            if (pixels == null) { reason = $"「{grapheme}」を描画できません（{renderError}）"; return false; }
            var mask = GlyphMask.FromAlpha(pixels, GlyphRenderer.MaskSize, b.alphaThreshold);
            if (!GlyphRenderer.InkBounds(mask, out _)) { reason = $"「{grapheme}」は形の無い文字です"; return false; }
            var features = GlyphMaskAnalyzer.Analyze(mask, AnalyzerSettings.From(b));
            if (features.colliderRects == null || features.colliderRects.Length == 0)
            {
                reason = $"「{grapheme}」は細かすぎて武器にできません";
                return false;
            }
            var tex = new Texture2D(GlyphRenderer.MaskSize, GlyphRenderer.MaskSize, TextureFormat.RGBA32, false, true)
            {
                name = "RuntimeGlyph_" + grapheme,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            tex.SetPixels32(pixels);
            tex.Apply(false, false); // 握り位置の探索で画素を読むため読み取り可能のまま
            def = new GlyphDefinitionRuntime
            {
                grapheme = grapheme,
                font = style,
                texture = tex,
                features = features,
                normalized = calibration.Normalize(features),
                runtimeBaked = true,
            };
            reason = null;
            return true;
        }

        /// <summary>1 文字（基本多言語面）で、空白・制御文字でないこと。</summary>
        public static bool Validate(string grapheme, out char ch, out string reason)
        {
            ch = '\0';
            if (string.IsNullOrEmpty(grapheme) || string.IsNullOrWhiteSpace(grapheme)) { reason = "文字を 1 つ入力してください"; return false; }
            var s = grapheme.Normalize(NormalizationForm.FormC);
            if (new StringInfo(s).LengthInTextElements != 1) { reason = "1 文字だけ入力してください"; return false; }
            if (s.Length != 1) { reason = $"「{s}」は使えない文字です（絵文字・結合文字は未対応）"; return false; }
            ch = s[0];
            if (char.IsControl(ch) || char.IsWhiteSpace(ch)) { reason = "空白・制御文字は使えません"; return false; }
            reason = null;
            return true;
        }
    }
}
