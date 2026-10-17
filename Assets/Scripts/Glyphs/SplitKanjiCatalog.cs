using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace MojiBattle
{
    public enum KanjiLayout { LeftRight, TopBottom, Enclosure }

    /// <summary>Curated structural data shared with the room server. Unknown characters are never guessed.</summary>
    public static class SplitKanjiCatalog
    {
        public readonly struct Entry
        {
            public readonly string Character, First, Second;
            public readonly KanjiLayout Layout;
            public readonly float Divider;
            public readonly Rect Inner;
            // Legacy aliases now mean the first/second structural component.
            public string Left => First;
            public string Right => Second;
            public string LayoutLabel => Layout == KanjiLayout.TopBottom ? "上下" : Layout == KanjiLayout.Enclosure ? "囲み" : "左右";
            public Entry(string character, string first, string second, KanjiLayout layout, float divider, Rect inner)
            { Character = character; First = first; Second = second; Layout = layout; Divider = divider; Inner = inner; }
        }

        public static readonly Entry[] Entries = Load();
        static readonly Dictionary<string, Entry> lookup = Index();

        static Entry[] Load()
        {
            var data = Resources.Load<TextAsset>("Glyphs/SplitKanji");
            if (data == null) throw new InvalidOperationException("SplitKanji structural data is missing.");
            var result = new List<Entry>(); var seen = new HashSet<string>();
            foreach (string raw in data.text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                var c = line.Split(',');
                if (c.Length != 9 || c[0].Length != 1 || c[1].Length == 0 || c[2].Length == 0 || !seen.Add(c[0]))
                    throw new InvalidOperationException("Invalid split data: " + line);
                KanjiLayout layout = c[3] == "lr" ? KanjiLayout.LeftRight : c[3] == "tb" ? KanjiLayout.TopBottom
                    : c[3] == "enc" ? KanjiLayout.Enclosure : throw new InvalidOperationException("Unknown kanji layout: " + line);
                float Parse(int i) => float.Parse(c[i], CultureInfo.InvariantCulture);
                result.Add(new Entry(c[0], c[1], c[2], layout, Parse(4), Rect.MinMaxRect(Parse(5), Parse(6), Parse(7), Parse(8))));
            }
            return result.ToArray();
        }
        static Dictionary<string, Entry> Index()
        {
            var result = new Dictionary<string, Entry>();
            foreach (var entry in Entries) result.Add(entry.Character, entry);
            return result;
        }

        public static bool TryGet(string character, out Entry entry)
        {
            if (character != null) return lookup.TryGetValue(character, out entry);
            entry = default; return false;
        }
        public static bool Supports(string character) => TryGet(character, out _);
    }
}
