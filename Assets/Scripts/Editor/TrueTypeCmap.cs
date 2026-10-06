using System.Collections.Generic;
using System.IO;

namespace MojiBattle.EditorTools
{
    /// <summary>
    /// TrueType/OpenType の cmap を直接読み、フォント自身が字形を持つかを判定する（5.1 欠字・代替グリフの検査）。
    /// Unity の動的フォントは欠字を OS フォントで補うことがあるため、Font.HasCharacter だけに頼らない。
    /// </summary>
    public sealed class TrueTypeCmap
    {
        readonly HashSet<int> codepoints = new HashSet<int>();

        public static TrueTypeCmap Load(string path) => new TrueTypeCmap(File.ReadAllBytes(path));

        public bool Has(int codepoint) => codepoints.Contains(codepoint);

        /// <summary>字形を持つコードポイントを連続範囲（開始・終了を含む、昇順）にまとめる。</summary>
        public void ToRanges(out int[] starts, out int[] ends)
        {
            var sorted = new List<int>(codepoints);
            sorted.Sort();
            var s = new List<int>();
            var e = new List<int>();
            foreach (int c in sorted)
            {
                if (e.Count > 0 && c == e[e.Count - 1] + 1) e[e.Count - 1] = c;
                else { s.Add(c); e.Add(c); }
            }
            starts = s.ToArray();
            ends = e.ToArray();
        }

        TrueTypeCmap(byte[] d)
        {
            int numTables = U16(d, 4);
            int cmap = -1;
            for (int i = 0; i < numTables; i++)
            {
                int rec = 12 + 16 * i;
                if (d[rec] == 'c' && d[rec + 1] == 'm' && d[rec + 2] == 'a' && d[rec + 3] == 'p') { cmap = (int)U32(d, rec + 8); break; }
            }
            if (cmap < 0) return;
            int count = U16(d, cmap + 2);
            for (int i = 0; i < count; i++)
            {
                int rec = cmap + 4 + 8 * i;
                int platform = U16(d, rec), encoding = U16(d, rec + 2);
                int off = cmap + (int)U32(d, rec + 4);
                bool unicode = platform == 0 || (platform == 3 && (encoding == 1 || encoding == 10));
                if (!unicode) continue;
                int format = U16(d, off);
                if (format == 4) ReadFormat4(d, off);
                else if (format == 12) ReadFormat12(d, off);
            }
        }

        void ReadFormat4(byte[] d, int off)
        {
            int segX2 = U16(d, off + 6);
            int ends = off + 14, starts = ends + segX2 + 2, deltas = starts + segX2, ranges = deltas + segX2;
            for (int s = 0; s < segX2 / 2; s++)
            {
                int end = U16(d, ends + 2 * s), start = U16(d, starts + 2 * s);
                int delta = (short)U16(d, deltas + 2 * s), rangeOff = U16(d, ranges + 2 * s);
                for (int c = start; c <= end && c != 0xFFFF; c++)
                {
                    int glyph;
                    if (rangeOff == 0) glyph = (c + delta) & 0xFFFF;
                    else
                    {
                        int addr = ranges + 2 * s + rangeOff + 2 * (c - start);
                        glyph = U16(d, addr);
                        if (glyph != 0) glyph = (glyph + delta) & 0xFFFF;
                    }
                    if (glyph != 0) codepoints.Add(c);
                }
            }
        }

        void ReadFormat12(byte[] d, int off)
        {
            long groups = U32(d, off + 12);
            for (long g = 0; g < groups; g++)
            {
                int rec = off + 16 + (int)(12 * g);
                long start = U32(d, rec), end = U32(d, rec + 4), glyph = U32(d, rec + 8);
                for (long c = start; c <= end; c++)
                    if (glyph + (c - start) != 0) codepoints.Add((int)c);
            }
        }

        static int U16(byte[] d, int i) => (d[i] << 8) | d[i + 1];
        static long U32(byte[] d, int i) => ((long)d[i] << 24) | ((long)d[i + 1] << 16) | ((long)d[i + 2] << 8) | d[i + 3];
    }
}
