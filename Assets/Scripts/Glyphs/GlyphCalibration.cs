using UnityEngine;

namespace MojiBattle
{
    /// <summary>
    /// 特徴量の正規化範囲（5.2）。対戦相手や試合ごとに再正規化しない固定値。
    /// GlyphBaker が参照集合（ベイク済みの全文字×字体）の最小・最大から算出する。4 字体がそろうまでは provisional。
    /// </summary>
    [CreateAssetMenu(menuName = "MojiBattle/Glyph Calibration", fileName = "GlyphCalibration")]
    public class GlyphCalibration : ScriptableObject
    {
        public bool provisional = true;
        [Tooltip("下限・上限を求めた参照集合")]
        public string referenceSet = "hand-set (P0/P1)";
        public Vector2 inkRatio = new Vector2(0.04f, 0.50f);
        public Vector2 widthRatio = new Vector2(0.10f, 0.97f);
        public Vector2 heightRatio = new Vector2(0.10f, 0.97f);
        public Vector2 edgeRatio = new Vector2(2.0f, 26f);
        public Vector2 balance = new Vector2(0.55f, 1.0f);

        public NormalizedGlyphFeatures Normalize(in GlyphFeatures f) => new NormalizedGlyphFeatures
        {
            A = Norm(f.inkRatio, inkRatio),
            W = Norm(f.widthRatio, widthRatio),
            H = Norm(f.heightRatio, heightRatio),
            C = Norm(f.edgeRatio, edgeRatio),
            B = Norm(f.balance, balance),
        };

        static float Norm(float raw, Vector2 range) =>
            Mathf.Clamp01((raw - range.x) / Mathf.Max(1e-6f, range.y - range.x)) * 100f;

        static GlyphCalibration cachedDefault;

        public static GlyphCalibration Default
        {
            get
            {
                if (cachedDefault == null)
                {
                    cachedDefault = Resources.Load<GlyphCalibration>("Glyphs/GlyphCalibration");
                    if (cachedDefault == null) cachedDefault = CreateInstance<GlyphCalibration>();
                }
                return cachedDefault;
            }
        }
    }
}
