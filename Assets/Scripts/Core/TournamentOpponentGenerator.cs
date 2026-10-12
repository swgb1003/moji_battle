using System.Collections.Generic;
using UnityEngine;

namespace MojiBattle
{
    public static class TournamentOpponentGenerator
    {
        public static List<FighterBuildData> Generate(int seed)
        {
            var rng = new MatchRandom(seed).Fork(3107);
            var fonts = new List<FontStyleId>();
            var library = GlyphFontLibrary.Default;
            foreach (FontStyleId f in System.Enum.GetValues(typeof(FontStyleId)))
                if (library != null && library.Get(f) != null) fonts.Add(f);
            if (fonts.Count == 0) fonts.Add(FontStyleId.Gothic);
            var result = new List<FighterBuildData>();
            for (int i = 0; i < 7; i++)
            {
                var font = fonts[rng.RangeInclusive(0, fonts.Count - 1)];
                string character = "一";
                for (int attempt = 0; attempt < 24; attempt++)
                {
                    // Sample the same supported kanji, kana, Latin and symbol families as free character input.
                    int category = rng.RangeInclusive(0, 9);
                    int codepoint = category < 5 ? rng.RangeInclusive(0x4e00, 0x9fff)
                        : category < 7 ? rng.RangeInclusive(0x3041, 0x30fa) : rng.RangeInclusive(0x21, 0x7e);
                    string candidate = char.ConvertFromUtf32(codepoint);
                    var entry = library != null ? library.Get(font) : null;
                    if (entry != null && !entry.Has(codepoint)) continue;
                    if (GlyphCatalog.TryGet(candidate, font, CombatBalance.Default, GlyphCalibration.Default, out _, out _))
                    { character = candidate; break; }
                }
                if (!FighterCustomizer.IsAvailable(character, font, out _))
                { font = FontStyleId.Gothic; character = "一"; }
                result.Add(new FighterBuildData { character = character, fontType = font, weaponSize = WeaponSize.S,
                    gripType = (GripType)rng.RangeInclusive(0, 3), battleStyle = (BattleStyle)rng.RangeInclusive(0, 3),
                    weaponFlip = (WeaponFlip)rng.RangeInclusive(0, 3), gripPosition = rng.Range(.08f, .92f),
                    customGrip = rng.Chance(.5f), gripPoint = new Vector2(rng.Range(.08f, .92f), rng.Range(.08f, .92f)) }
                    .MaybeDualWield(result.Count));
            }
            return result;
        }
    }
}
