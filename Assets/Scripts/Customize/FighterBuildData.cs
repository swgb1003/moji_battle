using System;
using UnityEngine;

namespace MojiBattle
{
    public enum WeaponSize { S, M, L, XL }
    public enum GripType { OneHanded, TwoHanded, Reverse, Horizontal }
    public enum BattleStyle { Aggressive, HitAndAway, Counter, Defensive }

    /// <summary>
    /// 1ファイターのカスタマイズ内容（カスタマイズ仕様 5）。
    /// 字体は既存の FontStyleId をそのまま使う（仕様書の FontType に相当）。
    /// </summary>
    [Serializable]
    public class FighterBuildData
    {
        public string character = "一";
        public FontStyleId fontType = FontStyleId.Gothic;
        public WeaponSize weaponSize = WeaponSize.M;
        public GripType gripType = GripType.OneHanded;
        [Tooltip("0=字形の外接矩形の端（左／下）、0.5=中央、1=反対の端。内部では安全範囲へ Clamp する")]
        [Range(0f, 1f)] public float gripPosition = 0.5f;
        [Tooltip("true なら gripPoint（プレビューの字形をクリックして選んだ場所）を握る。false なら gripPosition（長軸上の位置）")]
        public bool customGrip;
        [Tooltip("握る場所。字形の外接矩形内の正規化座標（x: 左0→右1、y: 下0→上1）")]
        public Vector2 gripPoint = new Vector2(0.5f, 0.5f);
        public BattleStyle battleStyle = BattleStyle.Aggressive;
        [Tooltip("字形を反転して持つ（なし / 左右 / 上下 / 両方 = 180°回転）。握る場所は反転後の字形で選ぶ")]
        public WeaponFlip weaponFlip = WeaponFlip.None;

        public static FighterBuildData Default(string character) => new FighterBuildData { character = character };

        public FighterBuildData Clone() => (FighterBuildData)MemberwiseClone();

        public FighterLoadout ToLoadout() => new FighterLoadout(character, fontType) { build = Clone() };

        public override string ToString() =>
            $"{character} {CustomizeLabels.Size(weaponSize)} / {CustomizeLabels.Grip(gripType)} / {CustomizeLabels.GripPlace(this)} / {CustomizeLabels.Flip(weaponFlip)} / {CustomizeLabels.Style(battleStyle)}";
    }

    /// <summary>UI 表示名。</summary>
    public static class CustomizeLabels
    {
        public static string Size(WeaponSize s) => s.ToString();

        /// <summary>握る場所の表示（クリックで選んだ場所は横・縦の位置、長軸指定は割合）。</summary>
        public static string GripPlace(FighterBuildData b) =>
            b.customGrip ? $"握り 横{Mathf.RoundToInt(b.gripPoint.x * 100f)}%・縦{Mathf.RoundToInt(b.gripPoint.y * 100f)}%"
                         : $"握り{Mathf.RoundToInt(b.gripPosition * 100f)}%";

        public static string Grip(GripType g)
        {
            switch (g)
            {
                case GripType.TwoHanded: return "両手";
                case GripType.Reverse: return "逆手";
                case GripType.Horizontal: return "横持ち";
                default: return "片手";
            }
        }

        public static string Flip(WeaponFlip f)
        {
            switch (f)
            {
                case WeaponFlip.Horizontal: return "左右反転";
                case WeaponFlip.Vertical: return "上下反転";
                case WeaponFlip.Both: return "180°回転";
                default: return "反転なし";
            }
        }

        public static string Style(BattleStyle s)
        {
            switch (s)
            {
                case BattleStyle.HitAndAway: return "ヒット＆アウェイ";
                case BattleStyle.Counter: return "カウンター";
                case BattleStyle.Defensive: return "鉄壁";
                default: return "猛攻";
            }
        }

        public static string StyleShort(BattleStyle s) => s == BattleStyle.HitAndAway ? "H&A" : Style(s);
    }
}
