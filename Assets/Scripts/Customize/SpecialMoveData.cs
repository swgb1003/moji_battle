using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace MojiBattle
{
    public enum SpecialElement { None, Fire, Thunder, Ice, Wind, Water, Earth, Light, Dark, Flower, Poison }
    public enum SpecialEffect { Burst, Ring, Pillar, Slash, Spiral, Rain }

    /// <summary>
    /// AI が技名から設計した必殺技。7文字以内。通常文字は武器の文字を含め、合成文字は自由に命名する。
    /// 動き（steps）は既存の技の連なりで、物理と当たり判定はゲーム側が持つ。値は文字列のまま保存し、使う時に解釈する（未知の値は無難な既定へ）。
    /// </summary>
    [Serializable]
    public class SpecialMoveData
    {
        public string name = "", character = "", reading = "";
        public bool fusionEnabled;
        public FusionData fusion;
        [Tooltip("出す順の技（AttackStyle の名前）。1〜4 個")] public string[] steps = Array.Empty<string>();
        [Tooltip("威力 / 速さ / 伸び。各 1〜5、合計 9 まで")] public int power = 3, speed = 3, reach = 3;
        public string element = "none", effect = "burst", primary = "", secondary = "", callout = "", description = "";

        public bool IsSet => !string.IsNullOrEmpty(name);
        public SpecialMoveData Clone()
        {
            var c = (SpecialMoveData)MemberwiseClone();
            c.steps = steps != null ? (string[])steps.Clone() : Array.Empty<string>();
            c.fusion = fusion?.Clone();
            return c;
        }
        public bool SameWeapon(string ch, FusionData recipe) => character == ch && (recipe == null ? !fusionEnabled
            : fusionEnabled && fusion != null && fusion.Validate(out _) && fusion.Key == recipe.Key);
        public bool SameAs(SpecialMoveData other) => other != null && other.name == name
            && fusionEnabled == other.fusionEnabled && SameWeapon(other.character, other.fusionEnabled ? other.fusion : null);

        /// <summary>出せる技の並び（未知の名前は除く。空なら踏み込み斬り）。</summary>
        public AttackStyle[] Styles()
        {
            var list = new List<AttackStyle>();
            if (steps != null)
                foreach (var s in steps)
                    if (list.Count < SpecialMoveRules.MaxSteps && SpecialMoveRules.TryStyle(s, out var style)) list.Add(style);
            if (list.Count == 0) list.Add(AttackStyle.DashSlash);
            return list.ToArray();
        }

        public SpecialElement Element => SpecialMoveRules.ParseElement(element);
        public SpecialEffect Effect => SpecialMoveRules.ParseEffect(effect);
        public Color PrimaryColor => SpecialMoveRules.ParseColor(primary, SpecialMoveRules.ElementColor(Element, false));
        public Color SecondaryColor => SpecialMoveRules.ParseColor(secondary, SpecialMoveRules.ElementColor(Element, true));
        public string StepsLabel
        {
            get
            {
                var names = new List<string>();
                foreach (var s in Styles()) names.Add(AttackTechniques.Label(s) + (s == AttackStyle.Launch ? "→叩き落とし" : ""));
                return string.Join(" → ", names);
            }
        }
    }

    public static class SpecialMoveRules
    {
        public const int MaxName = 7, MaxSteps = 4, MaxStat = 5, StatBudget = 9, MaxPerCharacter = 3;

        /// <summary>必殺技に使える動き（武器を手放す・相手の構えに依存する技は除く）。</summary>
        public static readonly AttackStyle[] Allowed =
        {
            AttackStyle.Overhead, AttackStyle.Rising, AttackStyle.Thrust, AttackStyle.Sweep, AttackStyle.LowSweep, AttackStyle.Bash,
            AttackStyle.Launch, AttackStyle.Spin, AttackStyle.Leap, AttackStyle.DashSlash, AttackStyle.Whirl,
            AttackStyle.GroundWave, AttackStyle.StrokeShot, AttackStyle.FrontKick,
        };

        public static bool TryStyle(string name, out AttackStyle style)
        {
            style = default;
            return !string.IsNullOrEmpty(name) && Enum.TryParse(name, false, out style) && Array.IndexOf(Allowed, style) >= 0;
        }

        public static List<string> Graphemes(string s)
        {
            var list = new List<string>();
            if (string.IsNullOrEmpty(s)) return list;
            var e = StringInfo.GetTextElementEnumerator(s);
            while (e.MoveNext()) list.Add(e.GetTextElement());
            return list;
        }

        /// <summary>技名の条件を確かめる。使えれば null、使えなければ理由。</summary>
        public static string CheckName(string name, string character, bool composed = false)
        {
            name = name?.Trim() ?? "";
            if (name.Length == 0) return "技名を入力してください";
            foreach (char c in name) if (char.IsControl(c) || c == '<' || c == '>') return "使えない記号が入っています";
            var g = Graphemes(name);
            if (g.Count > MaxName) return $"技名は{MaxName}文字以内にしてください（いま{g.Count}文字）";
            if (!composed && !g.Contains(character)) return $"技名に「{character}」を入れてください";
            return null;
        }

        /// <summary>サーバーと同じ規則で丸める（友人対戦では相手の装備もサーバーが丸めて届く）。</summary>
        public static SpecialMoveData Sanitize(SpecialMoveData m, string character, FusionData fusion = null)
        {
            if (m == null || !m.IsSet || (fusion != null && !fusion.Validate(out _))) return null;
            // Old equipped moves can be rebound to this build; unbound library entries stay ordinary.
            if (m.fusionEnabled && (fusion == null || !m.SameWeapon(character, fusion))) return null;
            if (CheckName(m.name, character, fusion != null && m.fusionEnabled) != null) return null;
            if (!string.IsNullOrEmpty(m.character) && m.character != character) return null; // 別の文字のために作った技
            var c = m.Clone();
            c.name = c.name.Trim(); c.character = character;
            c.fusionEnabled = fusion != null; c.fusion = fusion?.Clone();
            var styles = new List<string>();
            foreach (var s in c.Styles()) styles.Add(s.ToString());
            c.steps = styles.ToArray();
            int[] stats = { Mathf.Clamp(c.power, 1, MaxStat), Mathf.Clamp(c.speed, 1, MaxStat), Mathf.Clamp(c.reach, 1, MaxStat) };
            while (stats[0] + stats[1] + stats[2] > StatBudget)
            {
                int top = 0;
                for (int i = 1; i < 3; i++) if (stats[i] > stats[top]) top = i;
                stats[top]--;
            }
            c.power = stats[0]; c.speed = stats[1]; c.reach = stats[2];
            return c;
        }

        public static SpecialElement ParseElement(string s)
        {
            switch (s)
            {
                case "fire": return SpecialElement.Fire;
                case "thunder": return SpecialElement.Thunder;
                case "ice": return SpecialElement.Ice;
                case "wind": return SpecialElement.Wind;
                case "water": return SpecialElement.Water;
                case "earth": return SpecialElement.Earth;
                case "light": return SpecialElement.Light;
                case "dark": return SpecialElement.Dark;
                case "flower": return SpecialElement.Flower;
                case "poison": return SpecialElement.Poison;
                default: return SpecialElement.None;
            }
        }

        public static SpecialEffect ParseEffect(string s)
        {
            switch (s)
            {
                case "ring": return SpecialEffect.Ring;
                case "pillar": return SpecialEffect.Pillar;
                case "slash": return SpecialEffect.Slash;
                case "spiral": return SpecialEffect.Spiral;
                case "rain": return SpecialEffect.Rain;
                default: return SpecialEffect.Burst;
            }
        }

        public static string ElementLabel(SpecialElement e)
        {
            switch (e)
            {
                case SpecialElement.Fire: return "炎";
                case SpecialElement.Thunder: return "雷";
                case SpecialElement.Ice: return "氷";
                case SpecialElement.Wind: return "風";
                case SpecialElement.Water: return "水";
                case SpecialElement.Earth: return "土";
                case SpecialElement.Light: return "光";
                case SpecialElement.Dark: return "闇";
                case SpecialElement.Flower: return "花";
                case SpecialElement.Poison: return "毒";
                default: return "墨";
            }
        }

        public static Color ElementColor(SpecialElement e, bool secondary)
        {
            string[] c;
            switch (e)
            {
                case SpecialElement.Fire: c = new[] { "#E2462B", "#F6B23C" }; break;
                case SpecialElement.Thunder: c = new[] { "#F2D43A", "#6A5CD8" }; break;
                case SpecialElement.Ice: c = new[] { "#7FD3F0", "#E8F7FF" }; break;
                case SpecialElement.Wind: c = new[] { "#5DBE8A", "#D8F2E4" }; break;
                case SpecialElement.Water: c = new[] { "#2F7DD8", "#9ED0F5" }; break;
                case SpecialElement.Earth: c = new[] { "#8A5A2B", "#C9A26B" }; break;
                case SpecialElement.Light: c = new[] { "#F7E58C", "#FFFFFF" }; break;
                case SpecialElement.Dark: c = new[] { "#4B2A6B", "#B05CE0" }; break;
                case SpecialElement.Flower: c = new[] { "#F08FB0", "#FFE3EC" }; break;
                case SpecialElement.Poison: c = new[] { "#7FBF3F", "#5B2A7A" }; break;
                default: c = new[] { "#20211F", "#8A8578" }; break;
            }
            ColorUtility.TryParseHtmlString(c[secondary ? 1 : 0], out var color);
            return color;
        }

        public static Color ParseColor(string hex, Color fallback) =>
            !string.IsNullOrEmpty(hex) && hex.Length == 7 && hex[0] == '#' && ColorUtility.TryParseHtmlString(hex, out var c) ? c : fallback;
    }

    /// <summary>登録した必殺技（一文字につき 3 つまで）。PlayerPrefs に JSON で保存する。</summary>
    public static class SpecialMoveLibrary
    {
        const string PrefKey = "MojiBattle.Specials.v1";
        [Serializable] sealed class Saved { public SpecialMoveData[] moves = Array.Empty<SpecialMoveData>(); }
        static List<SpecialMoveData> moves;

        public static event Action Changed;

        public static List<SpecialMoveData> For(string character)
        {
            Load();
            return moves.FindAll(m => m.SameWeapon(character, null));
        }

        public static List<SpecialMoveData> For(FighterBuildData build)
        { Load(); return moves.FindAll(m => m.SameWeapon(build.character, build.HasFusion ? build.fusion : null)); }

        public static bool IsFull(string character) => For(character).Count >= SpecialMoveRules.MaxPerCharacter;
        public static int CountFor(SpecialMoveData move)
        { Load(); return moves.FindAll(m => m.SameWeapon(move.character, move.fusionEnabled ? move.fusion : null)).Count; }
        public static List<SpecialMoveData> All() { Load(); return moves.ConvertAll(m => m.Clone()); }

        /// <summary>登録する（同じ名前は上書き）。その文字の枠が埋まっていれば false。</summary>
        public static bool Add(SpecialMoveData move)
        {
            Load();
            int same = moves.FindIndex(m => m.SameAs(move));
            if (same >= 0) moves[same] = move.Clone();
            else if (CountFor(move) >= SpecialMoveRules.MaxPerCharacter) return false;
            else moves.Add(move.Clone());
            Save();
            AchievementStore.SpecialSaved(move.character);
            if (move.fusionEnabled) AchievementStore.Change(p => p.Set(4, CountFor(move)), true);
            return true;
        }

        public static void Remove(SpecialMoveData move)
        {
            Load();
            if (moves.RemoveAll(m => m.SameAs(move)) > 0) Save();
        }

        /// <summary>登録した必殺技をすべて消す。装備と永続化は呼び出し元で更新する。</summary>
        public static void ClearAll()
        {
            moves = new List<SpecialMoveData>();
            PlayerPrefs.DeleteKey(PrefKey);
            Changed?.Invoke();
        }

        public static void Reload() { moves = null; Load(); Changed?.Invoke(); }

        static void Load()
        {
            if (moves != null) return;
            moves = new List<SpecialMoveData>();
            try
            {
                var json = PlayerPrefs.GetString(PrefKey, "");
                if (!string.IsNullOrEmpty(json))
                    foreach (var m in JsonUtility.FromJson<Saved>(json)?.moves ?? Array.Empty<SpecialMoveData>())
                        if (m != null && m.IsSet) moves.Add(m);
            }
            catch (Exception e) { Debug.LogWarning("[MojiBattle] 保存した必殺技を読めません: " + e.Message); }
        }

        static void Save()
        {
            try
            {
                PlayerPrefs.SetString(PrefKey, JsonUtility.ToJson(new Saved { moves = moves.ToArray() }));
                PlayerPrefs.Save();
            }
            catch (Exception e) { Debug.LogWarning("[MojiBattle] 必殺技を保存できません: " + e.Message); }
            Changed?.Invoke();
        }
    }
}
