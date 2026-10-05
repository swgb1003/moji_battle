using System;
using System.Collections.Generic;
using UnityEngine;

namespace MojiBattle
{
    /// <summary>
    /// UI からの変更を BuildData へ反映する（カスタマイズ仕様 15）。左右 2 体の設定を持ち、変更のたびに保存して Changed を通知する。
    /// </summary>
    public sealed class FighterCustomizer
    {
        public const string GlyphOrder = "一口山火鬱AIOX";

        readonly FighterBuildData[] builds = new FighterBuildData[2];
        public int Side { get; private set; }
        public FighterBuildData Current => builds[Side];
        public FighterBuildData Get(int side) => builds[side];
        /// <summary>最後に入力された文字が使えなかった理由（使えれば null）。</summary>
        public string CharacterError { get; private set; }
        public event Action Changed;

        public FighterCustomizer()
        {
            builds[0] = BuildRuntimeStore.Get(0).Clone();
            builds[1] = BuildRuntimeStore.Get(1).Clone();
            foreach (var b in builds)
                if (!IsAvailable(b.character, b.fontType, out _)) b.character = "一";
        }

        public static List<string> AvailableCharacters(FontStyleId font)
        {
            var list = GlyphCatalog.AvailableGraphemes(font);
            list.Sort((a, b) => GlyphOrder.IndexOf(a, StringComparison.Ordinal).CompareTo(GlyphOrder.IndexOf(b, StringComparison.Ordinal)));
            return list;
        }

        public static bool IsAvailable(string ch, FontStyleId font, out string reason) =>
            GlyphCatalog.TryGet(ch, font, CombatBalance.Default, GlyphCalibration.Default, out _, out reason);

        public void SelectSide(int side)
        {
            Side = Mathf.Clamp(side, 0, 1);
            CharacterError = null;
            Changed?.Invoke();
        }

        /// <summary>文字を入力。ベイク済みでない文字は理由を残して変更しない。</summary>
        public bool SetCharacter(string ch)
        {
            if (!IsAvailable(ch, Current.fontType, out var reason))
            {
                CharacterError = reason;
                Changed?.Invoke();
                return false;
            }
            CharacterError = null;
            Current.character = ch.Trim();
            Commit();
            return true;
        }

        public void SetFont(FontStyleId font)
        {
            if (!IsAvailable(Current.character, font, out var reason)) { CharacterError = reason; Changed?.Invoke(); return; }
            Current.fontType = font;
            Commit();
        }

        public void SetSize(WeaponSize s) { Current.weaponSize = s; Commit(); }
        public void SetGrip(GripType g) { Current.gripType = g; Commit(); }
        public void SetGripPosition(float p) { Current.gripPosition = Mathf.Clamp01(p); Commit(); }
        public void SetStyle(BattleStyle s) { Current.battleStyle = s; Commit(); }

        void Commit()
        {
            BuildRuntimeStore.Set(Side, Current);
            Changed?.Invoke();
        }
    }
}
