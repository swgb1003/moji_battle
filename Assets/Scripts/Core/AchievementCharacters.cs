using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace MojiBattle
{
    public static class AchievementCharacters
    {
        static Dictionary<int,int> strokes;
        public static int Strokes(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            if (strokes == null)
            {
                strokes = new Dictionary<int,int>();
                var data = Resources.Load<TextAsset>("Achievements/KanjiStrokes");
                if (data != null) foreach (var line in data.text.Split('\n'))
                {
                    var fields = line.Trim().Split('\t');
                    if (fields.Length == 2 && int.TryParse(fields[0],NumberStyles.HexNumber,CultureInfo.InvariantCulture,out int code)
                        && int.TryParse(fields[1],out int count)) strokes[code] = count;
                }
            }
            return strokes.TryGetValue(char.ConvertToUtf32(s,0),out int n) ? n : 0;
        }
        public static bool IsKana(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            int c=char.ConvertToUtf32(s,0);
            return (c>=0x3041 && c<=0x3096) || (c>=0x30A1 && c<=0x30FA) || (c>=0xFF66 && c<=0xFF9D);
        }
        public static bool IsLatinOrDigit(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            int c=char.ConvertToUtf32(s,0);
            return (c>=65 && c<=90) || (c>=97 && c<=122) || (c>=48 && c<=57)
                || (c>=0xFF21 && c<=0xFF3A) || (c>=0xFF41 && c<=0xFF5A) || (c>=0xFF10 && c<=0xFF19);
        }
        public static bool IsSymbol(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            var category=CharUnicodeInfo.GetUnicodeCategory(s,0);
            return category==UnicodeCategory.MathSymbol || category==UnicodeCategory.OtherSymbol
                || category==UnicodeCategory.CurrencySymbol || category==UnicodeCategory.ModifierSymbol
                || category==UnicodeCategory.OtherPunctuation || category==UnicodeCategory.DashPunctuation
                || category==UnicodeCategory.OpenPunctuation || category==UnicodeCategory.ClosePunctuation;
        }
    }
}
