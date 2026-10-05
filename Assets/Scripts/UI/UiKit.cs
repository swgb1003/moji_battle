using UnityEngine;

namespace MojiBattle
{
    /// <summary>P1 用の簡易描画部品（線・文字・矩形）。P4 で UGUI + TextMesh Pro に置き換える。</summary>
    public static class UiKit
    {
        static Font font;
        static Material lineMaterial;
        static Sprite whiteSprite;

        /// <summary>HUD・擬音用の OS フォント（武器字形には使わない）。</summary>
        public static Font Font
        {
            get
            {
                if (font == null)
                    font = Font.CreateDynamicFontFromOSFont(new[] { "Noto Sans JP", "Yu Gothic UI", "Meiryo", "Arial" }, 64);
                return font;
            }
        }

        public static Material LineMaterial
        {
            get
            {
                if (lineMaterial == null) lineMaterial = new Material(Shader.Find("Sprites/Default"));
                return lineMaterial;
            }
        }

        public static Sprite WhiteSprite
        {
            get
            {
                if (whiteSprite == null)
                {
                    var t = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                    var px = new Color32[16];
                    for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
                    t.SetPixels32(px);
                    t.Apply();
                    whiteSprite = Sprite.Create(t, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
                }
                return whiteSprite;
            }
        }

        public static LineRenderer Line(Transform parent, string name, Color color, float width, int sortingOrder, bool loop = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = LineMaterial;
            lr.startColor = lr.endColor = color;
            lr.startWidth = lr.endWidth = width;
            lr.useWorldSpace = true;
            lr.loop = loop;
            lr.numCapVertices = 3;
            lr.numCornerVertices = 2;
            lr.sortingOrder = sortingOrder;
            lr.positionCount = 0;
            return lr;
        }

        public static TextMesh Text(Transform parent, string name, string text, Color color, float worldHeight, int sortingOrder,
            TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var tm = go.AddComponent<TextMesh>();
            tm.font = Font;
            tm.fontSize = 64;
            tm.characterSize = worldHeight * 10f / tm.fontSize; // TextMesh は 10 px = 1 unit 相当
            tm.anchor = anchor;
            tm.alignment = TextAlignment.Center;
            tm.text = text;
            tm.color = color;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = Font.material;
            mr.sortingOrder = sortingOrder;
            return tm;
        }

        /// <summary>
        /// 画面上の実ピクセル高さに合わせてフォントの描画解像度を選ぶ（縮小表示で文字が潰れないように）。
        /// worldHeight は文字のワールド高さ。カメラの描画先（画面または RenderTexture）の高さを使う。
        /// </summary>
        public static void FitText(TextMesh tm, float worldHeight, Camera cam)
        {
            float px = cam != null && cam.orthographic
                ? worldHeight / (2f * cam.orthographicSize) * cam.pixelHeight
                : 64f;
            int fontSize = Mathf.Clamp(Mathf.CeilToInt(px * 1.25f / 4f) * 4, 12, 256); // 4px 刻みでアトラス再生成を抑える
            if (tm.fontSize != fontSize) tm.fontSize = fontSize;
            tm.characterSize = worldHeight * 10f / fontSize;
        }

        public static SpriteRenderer Rect(Transform parent, string name, Color color, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = WhiteSprite;
            sr.color = color;
            sr.sortingOrder = sortingOrder;
            return sr;
        }
    }
}
