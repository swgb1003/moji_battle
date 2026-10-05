using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MojiBattle.EditorTools
{
    /// <summary>字形ベイクの操作画面（メニュー: MojiBattle → Glyphs → Glyph Bake Window）。</summary>
    public sealed class GlyphBakeWindow : EditorWindow
    {
        List<GlyphBaker.Result> lastResults = new List<GlyphBaker.Result>();
        List<GlyphDefinition> defs = new List<GlyphDefinition>();
        Vector2 scroll;
        bool showColliders = true;

        [MenuItem("MojiBattle/Glyphs/Glyph Bake Window")]
        public static void Open() => GetWindow<GlyphBakeWindow>("Glyph Bake");

        void OnEnable() => defs = GlyphBaker.LoadDefinitions();

        void OnGUI()
        {
            EditorGUILayout.LabelField("対象文字", GlyphBaker.Characters);
            foreach (var f in GlyphBaker.Fonts)
                EditorGUILayout.LabelField(GlyphCatalog.FontDisplayName(f.id), string.IsNullOrEmpty(f.path) ? "（未設定・P3）" : f.path);

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("すべてベイク", GUILayout.Height(32)))
                {
                    lastResults = GlyphBaker.BakeAll();
                    GlyphBaker.RecomputeCalibration();
                    GlyphBaker.WriteStatsReport();
                    defs = GlyphBaker.LoadDefinitions();
                    GlyphCatalog.ClearCache();
                }
                if (GUILayout.Button("キャリブレーション再計算", GUILayout.Height(32)))
                {
                    GlyphBaker.RecomputeCalibration();
                    GlyphBaker.WriteStatsReport();
                    GlyphCatalog.ClearCache();
                }
            }
            showColliders = EditorGUILayout.Toggle("Collider を重ねる", showColliders);

            foreach (var r in lastResults.Where(r => !r.ok))
                EditorGUILayout.HelpBox(r.ToString(), r.skipped ? MessageType.Info : MessageType.Error);
            if (lastResults.Count > 0 && lastResults.All(r => r.ok || r.skipped))
                EditorGUILayout.HelpBox($"{lastResults.Count(r => r.ok)} 件ベイク完了（Reports/glyph_stats.md）", MessageType.Info);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            const float cell = 150f;
            int cols = Mathf.Max(1, Mathf.FloorToInt((position.width - 20f) / (cell + 8f)));
            for (int i = 0; i < defs.Count; i += cols)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int j = i; j < Mathf.Min(defs.Count, i + cols); j++) DrawCell(defs[j], cell);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        void DrawCell(GlyphDefinition d, float size)
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(size + 4f)))
            {
                var rect = GUILayoutUtility.GetRect(size, size);
                EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.13f));
                if (d.mask != null) GUI.DrawTexture(rect, d.mask, ScaleMode.StretchToFill, true);
                float s = size / GlyphBaker.MaskSize;
                if (showColliders && d.features.colliderRects != null)
                {
                    foreach (var r in d.features.colliderRects)
                    {
                        var rr = new Rect(rect.x + r.x * s, rect.yMax - (r.y + r.height) * s, r.width * s, r.height * s);
                        Handles.DrawSolidRectangleWithOutline(rr, new Color(0f, 0.8f, 0.3f, 0.15f), new Color(0f, 0.9f, 0.3f, 0.9f));
                    }
                }
                var com = d.features.centerOfMass;
                var grip = d.features.gripPoint;
                EditorGUI.DrawRect(new Rect(rect.x + com.x * s - 3, rect.yMax - com.y * s - 3, 6, 6), Color.red);
                EditorGUI.DrawRect(new Rect(rect.x + grip.x * s - 3, rect.yMax - grip.y * s - 3, 6, 6), Color.cyan);
                EditorGUILayout.LabelField($"{d.grapheme} {GlyphCatalog.FontDisplayName(d.font)}  Collider {d.features.colliderRects?.Length}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"ink {d.features.inkRatio:F3}  edge {d.features.edgeRatio:F1}", EditorStyles.miniLabel);
            }
        }
    }
}
