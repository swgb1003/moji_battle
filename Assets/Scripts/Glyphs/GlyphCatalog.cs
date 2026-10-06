using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace MojiBattle
{
    /// <summary>文字×字体の字形一式（実行時表現）。P2 で ScriptableObject の GlyphDefinition へ置き換える。</summary>
    public sealed class GlyphDefinitionRuntime
    {
        public string grapheme;
        public FontStyleId font;
        public Texture2D texture;
        public GlyphFeatures features;
        public NormalizedGlyphFeatures normalized;
        /// <summary>入力された文字を実行時に字形化した（ベイク済みアセットではない）</summary>
        public bool runtimeBaked;
    }

    /// <summary>
    /// 文字×字体から字形を引く。ファイル名は Unicode コードポイント列から決めるため、文字IDを決め打ちしない。
    /// ベイク済みの GlyphDefinition（Resources/Glyphs/Definitions/u{hex}_{font}）を読み、正規化だけ実行時に行う。
    /// ベイク済みに無い文字は、同梱フォントから実行時に字形化する（RuntimeGlyphBaker。描画・解析はエディタのベイクと同じ）。
    /// </summary>
    public static class GlyphCatalog
    {
        const string Folder = "Glyphs/Definitions/";
        static readonly Dictionary<(string, FontStyleId), GlyphDefinitionRuntime> cache = new Dictionary<(string, FontStyleId), GlyphDefinitionRuntime>();
        static readonly Dictionary<(string, FontStyleId), string> failed = new Dictionary<(string, FontStyleId), string>();

        /// <summary>ベイク済みで使える文字（字体ごと）。</summary>
        public static List<string> AvailableGraphemes(FontStyleId font)
        {
            var list = new List<string>();
            foreach (var d in Resources.LoadAll<GlyphDefinition>(Folder.TrimEnd('/')))
                if (d.font == font && !list.Contains(d.grapheme)) list.Add(d.grapheme);
            return list;
        }

        public static string FontKey(FontStyleId f)
        {
            switch (f)
            {
                case FontStyleId.Mincho: return "mincho";
                case FontStyleId.RoundedGothic: return "rounded";
                case FontStyleId.Brush: return "brush";
                default: return "gothic";
            }
        }

        public static string FontDisplayName(FontStyleId f)
        {
            switch (f)
            {
                case FontStyleId.Mincho: return "明朝";
                case FontStyleId.RoundedGothic: return "丸ゴシック";
                case FontStyleId.Brush: return "筆文字";
                default: return "ゴシック";
            }
        }

        public static string ResourceName(string grapheme, FontStyleId font)
        {
            var sb = new StringBuilder();
            var e = StringInfo.GetTextElementEnumerator(grapheme.Normalize(NormalizationForm.FormC));
            e.MoveNext();
            string element = e.GetTextElement();
            for (int i = 0; i < element.Length; i++)
            {
                int cp = char.ConvertToUtf32(element, i);
                if (char.IsHighSurrogate(element[i])) i++;
                if (sb.Length > 0) sb.Append('_');
                sb.Append('u').Append(cp.ToString("x4"));
            }
            sb.Append('_').Append(FontKey(font));
            return sb.ToString();
        }

        public static bool TryGet(string grapheme, FontStyleId font, CombatBalance balance, GlyphCalibration calibration,
            out GlyphDefinitionRuntime def, out string reason)
        {
            def = null;
            if (string.IsNullOrEmpty(grapheme)) { reason = "文字が空です"; return false; }
            var normalized = grapheme.Normalize(NormalizationForm.FormC);
            if (new StringInfo(normalized).LengthInTextElements != 1) { reason = "1 文字だけ入力してください"; return false; }
            if (cache.TryGetValue((normalized, font), out def)) { reason = null; return true; }

            if (failed.TryGetValue((normalized, font), out reason)) return false;
            var baked = Resources.Load<GlyphDefinition>(Folder + ResourceName(normalized, font));
            if (baked == null || baked.mask == null)
            {
                if (!RuntimeGlyphBaker.TryBake(normalized, font, balance, calibration, out def, out reason))
                {
                    failed[(normalized, font)] = reason;
                    return false;
                }
                cache[(normalized, font)] = def;
                return true;
            }
            if (baked.features.colliderRects == null || baked.features.colliderRects.Length == 0) { reason = "字形が空です（欠字）"; return false; }
            def = new GlyphDefinitionRuntime
            {
                grapheme = normalized,
                font = font,
                texture = baked.mask,
                features = baked.features,
                normalized = calibration.Normalize(baked.features),
            };
            cache[(normalized, font)] = def;
            reason = null;
            return true;
        }

        public static void ClearCache()
        {
            cache.Clear();
            failed.Clear();
        }
    }
}
