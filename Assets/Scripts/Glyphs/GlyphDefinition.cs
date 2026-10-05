using UnityEngine;

namespace MojiBattle
{
    /// <summary>
    /// 文字×字体のベイク結果一式（10.1）。マスク画像（表示 Sprite の元）・特徴量・Collider 矩形・ベイク情報を持つ。
    /// GlyphBakeWindow が生成する。正規化値は GlyphCalibration に依存するため保存せず、実行時に計算する。
    /// </summary>
    public class GlyphDefinition : ScriptableObject
    {
        public string grapheme;
        public FontStyleId font;
        [Tooltip("白い字形・透明背景の 256×256 マスク。表示 Sprite と解析の両方に使う")]
        public Texture2D mask;
        public GlyphFeatures features;

        [Header("ベイク情報")]
        public string fontAssetPath;
        public string fontFamily;
        [Tooltip("フォントファイルの SHA-256。フォントを差し替えたら再ベイクが必要")]
        public string fontHash;
        [Tooltip("ベイク設定（em・余白・解析パラメータ）のハッシュ")]
        public string settingsHash;
        public string bakedAt;
        public string unityVersion;
    }
}
