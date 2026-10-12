using System;
using UnityEngine;

namespace MojiBattle
{
    public enum WeaponSize { S, M, L, XL }
    /// <summary>DualWield（二刀流）は末尾に追加。保存済みの値（0〜3）はそのまま読める。</summary>
    public enum GripType { OneHanded, TwoHanded, Reverse, Horizontal, DualWield }
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
        [Tooltip("true なら gripPoint（プレビューでドラッグして選んだ場所）を握る。false なら gripPosition（長軸上の位置）")]
        public bool customGrip;
        [Tooltip("握る場所。字形の外接矩形内の正規化座標（x: 左0→右1、y: 下0→上1）")]
        public Vector2 gripPoint = new Vector2(0.5f, 0.5f);
        public BattleStyle battleStyle = BattleStyle.Aggressive;
        [Tooltip("字形を反転して持つ（なし / 左右 / 上下 / 両方 = 180°回転）。握る場所は反転後の字形で選ぶ")]
        public WeaponFlip weaponFlip = WeaponFlip.None;
        [Tooltip("装備中の必殺技（AI が技名から設計したもの）。名前が空なら文字に合わせた初期必殺技")]
        public SpecialMoveData special;
        [Tooltip("二刀流の副武器（反対の手）の文字。空なら主武器と同じ文字")]
        public string offhandCharacter = "";

        /// <summary>二刀流を選べるサイズ（S・M のみ）。</summary>
        public static bool DualWieldAllowed(WeaponSize s) => s == WeaponSize.S || s == WeaponSize.M;
        public bool IsDualWield => gripType == GripType.DualWield && DualWieldAllowed(weaponSize);
        /// <summary>副武器の文字（二刀流でなければ null）。</summary>
        public string OffhandGlyph => !IsDualWield ? null : string.IsNullOrEmpty(offhandCharacter) ? character : offhandCharacter;

        public static FighterBuildData Default(string character) => new FighterBuildData { character = character };

        /// <summary>COM の構成: S・M サイズなら一定の割合で二刀流にする（同じ文字を両手に）。
        /// 大会表などの乱数列を変えないよう、文字と salt の決定的なハッシュで決める。</summary>
        public FighterBuildData MaybeDualWield(int salt, float chance = .2f)
        {
            if (!DualWieldAllowed(weaponSize) || string.IsNullOrEmpty(character)) return this;
            uint h = 2166136261u;
            unchecked
            {
                foreach (char c in character) h = (h ^ c) * 16777619u;
                h = (h ^ (uint)salt) * 16777619u;
                h ^= h >> 13; h *= 0x5bd1e995u; h ^= h >> 15;
            }
            if ((h % 1000u) < chance * 1000f) { gripType = GripType.DualWield; offhandCharacter = ""; }
            return this;
        }

        public FighterBuildData Clone()
        {
            var c = (FighterBuildData)MemberwiseClone();
            c.special = special?.Clone();
            return c;
        }

        /// <summary>この文字で出せる装備中の必殺技（無い・別の文字のものなら null）。</summary>
        public SpecialMoveData EquippedSpecial => SpecialMoveRules.Sanitize(special, character);

        public FighterLoadout ToLoadout() => new FighterLoadout(character, fontType) { build = Clone() };

        public override string ToString() =>
            $"{character}{(IsDualWield ? "・" + OffhandGlyph : "")} {CustomizeLabels.Size(weaponSize)} / {CustomizeLabels.Grip(gripType)} / {CustomizeLabels.GripPlace(this)} / {CustomizeLabels.Flip(weaponFlip)} / {CustomizeLabels.Style(battleStyle)}";
    }

    /// <summary>UI 表示名。</summary>
    public static class CustomizeLabels
    {
        public static string Size(WeaponSize s) => s.ToString();

        /// <summary>握る場所の表示（ドラッグで選んだ場所は横・縦の位置、長軸指定は割合）。</summary>
        public static string GripPlace(FighterBuildData b) =>
            b.customGrip ? $"握り 横{Mathf.RoundToInt(b.gripPoint.x * 100f)}%・縦{Mathf.RoundToInt(b.gripPoint.y * 100f)}%"
                         : $"握り{Mathf.RoundToInt(b.gripPosition * 100f)}%";

        /// <summary>構成の持ち方。二刀流はもう一本の文字も添える（例: 二刀流・刀）。</summary>
        public static string Grip(FighterBuildData b) => b.IsDualWield ? "二刀流・" + b.OffhandGlyph : Grip(b.gripType);

        public static string Grip(GripType g)
        {
            switch (g)
            {
                case GripType.TwoHanded: return "両手";
                case GripType.Reverse: return "逆手";
                case GripType.Horizontal: return "横持ち";
                case GripType.DualWield: return "二刀流";
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
