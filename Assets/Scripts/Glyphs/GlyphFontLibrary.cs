using System;
using UnityEngine;

namespace MojiBattle
{
    /// <summary>
    /// 実行時の字形ベイクに使う同梱フォント（字体ごと）と、各フォントが自前で字形を持つ文字の一覧（cmap の範囲）。
    /// Unity の動的フォントは欠字を OS フォントで補うことがあるため、ベイク前に cmap で判定する。
    /// ProjectSetup / GlyphBaker が Resources/Glyphs/GlyphFontLibrary.asset を作る。
    /// </summary>
    [CreateAssetMenu(menuName = "MojiBattle/Glyph Font Library", fileName = "GlyphFontLibrary")]
    public class GlyphFontLibrary : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public FontStyleId style;
            public Font font;
            public string fontFamily;
            [Tooltip("フォントが字形を持つコードポイントの範囲（開始・終了を含む、昇順）")]
            public int[] rangeStart = new int[0], rangeEnd = new int[0];

            public bool Has(int codepoint)
            {
                int lo = 0, hi = rangeStart.Length - 1;
                while (lo <= hi)
                {
                    int mid = (lo + hi) >> 1;
                    if (codepoint < rangeStart[mid]) hi = mid - 1;
                    else if (codepoint > rangeEnd[mid]) lo = mid + 1;
                    else return true;
                }
                return false;
            }
        }

        public Entry[] fonts = new Entry[0];
        [Tooltip("字形コピー用（Hidden/MojiBattle/GlyphCopy）。参照してビルドへ含める")]
        public Material copyMaterial;

        public Entry Get(FontStyleId style)
        {
            foreach (var e in fonts)
                if (e != null && e.style == style && e.font != null) return e;
            return null;
        }

        static GlyphFontLibrary cached;

        public static GlyphFontLibrary Default
        {
            get
            {
                if (cached == null) cached = Resources.Load<GlyphFontLibrary>("Glyphs/GlyphFontLibrary");
                return cached;
            }
        }
    }
}
