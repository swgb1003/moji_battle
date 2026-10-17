using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace MojiBattle
{
    /// <summary>A portable recipe. No generated image or Unity object is serialized.</summary>
    [Serializable]
    public sealed class FusionData
    {
        public int version = 2;
        public string name = "";
        public string leftSource = "河", rightSource = "晴";
        public KanjiLayout layout;
        // False preserves the v1 first/second source choices when loading older JSON.
        public bool leftUseSecond, rightUseFirst;
        public float leftWidth = 1, leftHeight = 1, rightWidth = 1, rightHeight = 1;
        public float leftX, leftY, rightX, rightY;
        public float gap = 8;
        public FusionData Clone() => (FusionData)MemberwiseClone();
        public string Left => SplitKanjiCatalog.TryGet(leftSource ?? "", out var e) ? (leftUseSecond ? e.Second : e.First) : "?";
        public string Right => SplitKanjiCatalog.TryGet(rightSource ?? "", out var e) ? (rightUseFirst ? e.First : e.Second) : "?";
        public string LayoutLabel => layout == KanjiLayout.TopBottom ? "上下合成" : layout == KanjiLayout.Enclosure ? "囲み合成" : "左右合成";
        public string FirstRole => layout == KanjiLayout.TopBottom ? "上" : layout == KanjiLayout.Enclosure ? "外" : "左";
        public string SecondRole => layout == KanjiLayout.TopBottom ? "下" : layout == KanjiLayout.Enclosure ? "内" : "右";
        public static string SourceRole(KanjiLayout layout, bool second) => layout == KanjiLayout.TopBottom ? (second ? "下" : "上")
            : layout == KanjiLayout.Enclosure ? (second ? "内" : "外") : (second ? "右" : "左");
        public string DisplayName => string.IsNullOrWhiteSpace(name) ? Left + "＋" + Right : name;
        // Existing single-character systems (special moves, tournament labels) retain a Unicode identity.
        // Geometry is always built from the recipe, including when the combination has a real character.
        public string Character
        {
            get
            {
                foreach (var e in SplitKanjiCatalog.Entries)
                    if (e.Layout == layout && e.First == Left && e.Second == Right) return e.Character;
                return "合";
            }
        }
        public string Key => string.Join("|", version.ToString(), leftSource, rightSource,
            F(leftWidth), F(leftHeight), F(rightWidth), F(rightHeight), F(leftX), F(leftY), F(rightX), F(rightY), F(gap))
            + (version == 1 ? "" : "|" + (int)layout + "|" + leftUseSecond + "|" + rightUseFirst);
        static string F(float v) => v.ToString("R", CultureInfo.InvariantCulture);
        public bool Validate(out string reason)
        {
            reason = "合成データを読み込めません。";
            if ((version != 1 && version != 2) || !Source(leftSource) || !Source(rightSource) || !Enum.IsDefined(typeof(KanjiLayout), layout)) return false;
            if (version == 1 && (layout != KanjiLayout.LeftRight || leftUseSecond || rightUseFirst
                || !SplitKanjiCatalog.TryGet(leftSource, out var l) || l.Layout != KanjiLayout.LeftRight
                || !SplitKanjiCatalog.TryGet(rightSource, out var r) || r.Layout != KanjiLayout.LeftRight)) return false;
            foreach (float v in new[] { leftWidth, leftHeight, rightWidth, rightHeight }) if (!In(v, .7f, 1.3f)) return false;
            foreach (float v in new[] { leftX, rightX }) if (!In(v, -20, 20)) return false;
            foreach (float v in new[] { leftY, rightY }) if (!In(v, -24, 24)) return false;
            if (!In(gap, 0, 24)) return false;
            if (name == null || new StringInfo(name).LengthInTextElements > 12 || name.Length > 24)
            { reason = "名前は12文字以内で入力してください。"; return false; }
            foreach (char c in name) if (char.IsControl(c)) { reason = "名前に改行や制御文字は使えません。"; return false; }
            reason = null; return true;
        }
        static bool Source(string s) => !string.IsNullOrEmpty(s) && SplitKanjiCatalog.Supports(s);
        static bool In(float v, float min, float max) => !float.IsNaN(v) && !float.IsInfinity(v) && v >= min && v <= max;
        public void ResetLayout()
        {
            leftWidth = leftHeight = rightWidth = rightHeight = 1;
            leftX = leftY = rightX = rightY = 0; gap = 8;
        }
    }

    [Serializable]
    public sealed class FusionRecord
    {
        public string id;
        public FontStyleId font;
        public FusionData recipe;
        public FusionRecord Clone() => new FusionRecord { id = id, font = font, recipe = recipe?.Clone() };
        public FighterBuildData Build() => new FighterBuildData { character = recipe.Character, fusion = recipe.Clone(), fusionEnabled = true, fontType = font };
    }

    public static class FusionLibrary
    {
        const string SaveKey = "MojiBattle.Fusion.v1";
        public const int Capacity = 48;
        [Serializable] sealed class Saved { public List<FusionRecord> items = new List<FusionRecord>(); }
        public static List<FusionRecord> All()
        {
            var result = new List<FusionRecord>();
            try
            {
                var saved = JsonUtility.FromJson<Saved>(PlayerPrefs.GetString(SaveKey, ""));
                if (saved?.items != null) foreach (var item in saved.items)
                    if (item != null && !string.IsNullOrEmpty(item.id) && item.recipe != null && item.recipe.Validate(out _)
                        && Enum.IsDefined(typeof(FontStyleId), item.font) && result.Count < Capacity && !result.Exists(r => r.id == item.id)) result.Add(item.Clone());
            }
            catch (ArgumentException) { }
            return result;
        }
        public static bool Save(FusionData recipe, FontStyleId font, string id, out FusionRecord record, out string error)
        {
            record = null;
            if (recipe == null) { error = "部品を選んでください。"; return false; }
            if (!recipe.Validate(out error) || !Enum.IsDefined(typeof(FontStyleId), font)) return false;
            var list = All(); int index = list.FindIndex(r => r.id == id);
            if (index < 0 && list.Count >= Capacity) { error = "保存は48文字までです。不要な文字を削除してください。"; return false; }
            record = new FusionRecord { id = index < 0 ? Guid.NewGuid().ToString("N") : id, font = font, recipe = recipe.Clone() };
            if (string.IsNullOrWhiteSpace(record.recipe.name)) record.recipe.name = record.recipe.Left + "＋" + record.recipe.Right;
            if (index < 0) list.Add(record.Clone()); else list[index] = record.Clone();
            Write(list); error = null; return true;
        }
        public static void Delete(string id) { var list = All(); list.RemoveAll(r => r.id == id); Write(list); }
        static void Write(List<FusionRecord> items)
        { PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(new Saved { items = items })); PlayerPrefs.Save(); }
    }
}
