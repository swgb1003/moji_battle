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
        public BattleStyle battleStyle = BattleStyle.Aggressive;

        public static FighterBuildData Default(string character) => new FighterBuildData { character = character };

        public FighterBuildData Clone() => (FighterBuildData)MemberwiseClone();

        public FighterLoadout ToLoadout() => new FighterLoadout(character, fontType) { build = Clone() };

        public override string ToString() =>
            $"{character} {CustomizeLabels.Size(weaponSize)} / {CustomizeLabels.Grip(gripType)} / 握り{Mathf.RoundToInt(gripPosition * 100f)}% / {CustomizeLabels.Style(battleStyle)}";
    }

    /// <summary>UI 表示名。</summary>
    public static class CustomizeLabels
    {
        public static string Size(WeaponSize s) => s.ToString();

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
