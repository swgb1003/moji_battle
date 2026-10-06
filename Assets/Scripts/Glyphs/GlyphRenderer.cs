using UnityEngine;

namespace MojiBattle
{
    /// <summary>
    /// 字形を 256×256 の白マスクへ描画する（5.1）。エディタのベイク（GlyphBaker）と実行時のベイク（RuntimeGlyphBaker）で同じ処理を使う。
    /// em 枠は [Margin, Margin+em]、基準線は em 枠下端から 0.12 em 上。横方向は送り幅の中心を em 枠の中心へ合わせる（横伸縮しない）。
    /// </summary>
    public static class GlyphRenderer
    {
        public const int MaskSize = 256;
        /// <summary>em 枠の左下の余白（em 枠 = [8, 8+em]）</summary>
        public const int Margin = 8;
        /// <summary>em 枠の下端は基準線の 0.12 em 下（CJK の仮想ボディ）</summary>
        public const float IdeographicDescent = 0.12f;

        public const string CopyShaderName = "Hidden/MojiBattle/GlyphCopy";

        public static Color32[] Render(Font font, char ch, int emSize, Material copyMaterial, out string error)
        {
            error = null;
            string s = ch.ToString();
            font.RequestCharactersInTexture(s, emSize, FontStyle.Normal);
            if (!font.GetCharacterInfo(ch, out var ci, emSize, FontStyle.Normal))
            {
                error = "字形情報を取得できない";
                return null;
            }
            if (copyMaterial == null)
            {
                error = "字形コピー用のシェーダーが無い";
                return null;
            }
            copyMaterial.mainTexture = font.material.mainTexture;

            var rt = RenderTexture.GetTemporary(MaskSize, MaskSize, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            GL.Clear(true, true, new Color(0f, 0f, 0f, 0f));
            GL.PushMatrix();
            GL.LoadPixelMatrix(0, MaskSize, 0, MaskSize);
            copyMaterial.SetPass(0);
            float originX = Mathf.Round(Margin + (emSize - ci.advance) * 0.5f);
            float baseline = Mathf.Round(Margin + IdeographicDescent * emSize);
            float x0 = originX + ci.minX, x1 = originX + ci.maxX;
            float y0 = baseline + ci.minY, y1 = baseline + ci.maxY;
            GL.Begin(GL.QUADS);
            GL.TexCoord(ci.uvBottomLeft); GL.Vertex3(x0, y0, 0f);
            GL.TexCoord(ci.uvTopLeft); GL.Vertex3(x0, y1, 0f);
            GL.TexCoord(ci.uvTopRight); GL.Vertex3(x1, y1, 0f);
            GL.TexCoord(ci.uvBottomRight); GL.Vertex3(x1, y0, 0f);
            GL.End();
            GL.PopMatrix();

            var tex = new Texture2D(MaskSize, MaskSize, TextureFormat.RGBA32, false, true);
            tex.ReadPixels(new Rect(0, 0, MaskSize, MaskSize), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            var px = tex.GetPixels32();
            if (Application.isPlaying) Object.Destroy(tex); else Object.DestroyImmediate(tex);
            return px;
        }

        public static bool InkBounds(GlyphMask m, out RectInt bounds)
        {
            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = -1, y1 = -1;
            for (int y = 0; y < m.Size; y++)
            for (int x = 0; x < m.Size; x++)
            {
                if (!m[x, y]) continue;
                if (x < x0) x0 = x;
                if (x > x1) x1 = x;
                if (y < y0) y0 = y;
                if (y > y1) y1 = y;
            }
            bounds = x1 < 0 ? default : new RectInt(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
            return x1 >= 0;
        }
    }
}
