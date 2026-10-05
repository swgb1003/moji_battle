using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MojiBattle.EditorTools
{
    /// <summary>
    /// 字形ベイク（5.1）。文字×字体を同じ em 枠・同じ基準線・256×256 の白黒マスクへ描画し、
    /// 特徴量と Collider 矩形を解析して GlyphDefinition として保存する。実行時にフォントを読まない。
    /// バッチ: Unity -batchmode -executeMethod MojiBattle.EditorTools.GlyphBaker.BatchBake -quit
    /// </summary>
    public static class GlyphBaker
    {
        /// <summary>初期 9 文字（UI・データ生成の対象範囲。解析器は文字を決め打ちしない）</summary>
        public const string Characters = "一口山火鬱AIOX";
        public const int MaskSize = 256;
        /// <summary>em 枠の左下の余白（em 枠 = [8, 8+em]）</summary>
        public const int Margin = 8;
        /// <summary>em 枠の下端は基準線の 0.12 em 下（CJK の仮想ボディ）</summary>
        public const float IdeographicDescent = 0.12f;
        public const int MinEdgeMargin = 4;
        public const string DefinitionFolder = "Assets/Resources/Glyphs/Definitions";
        public const string MaskFolder = "Assets/Art/Glyphs/Baked";
        public const string CalibrationPath = "Assets/Resources/Glyphs/GlyphCalibration.asset";

        public struct FontSource
        {
            public FontStyleId id;
            public string path;
        }

        /// <summary>字体ごとの同梱フォント。P3 で明朝・丸ゴシック・筆文字を追加する。</summary>
        public static readonly FontSource[] Fonts =
        {
            new FontSource { id = FontStyleId.Gothic, path = "Assets/Art/Fonts/Gothic/NotoSansJP-Bold.ttf" },
            new FontSource { id = FontStyleId.Mincho, path = null },
            new FontSource { id = FontStyleId.RoundedGothic, path = null },
            new FontSource { id = FontStyleId.Brush, path = null },
        };

        public sealed class Result
        {
            public string grapheme;
            public FontStyleId font;
            public bool ok, skipped;
            public readonly List<string> errors = new List<string>();
            public GlyphDefinition definition;
            public override string ToString() =>
                $"{grapheme} {font}: {(skipped ? "SKIP" : ok ? "OK" : "ERROR")} {string.Join(" / ", errors)}";
        }

        [Serializable]
        class BakeRecord
        {
            public string grapheme, font, fontAssetPath, fontFamily, fontHash, settingsHash, bakedAt, unityVersion;
            public GlyphFeatures features;
        }

        static Material copyMaterial;

        [MenuItem("MojiBattle/Glyphs/Bake All (9 chars × fonts)")]
        public static void BakeAllMenu()
        {
            var results = BakeAll();
            RecomputeCalibration();
            WriteStatsReport();
            Debug.Log("[GlyphBake]\n" + string.Join("\n", results));
        }

        /// <summary>バッチ実行用。エラーがあれば終了コード 1。</summary>
        public static void BatchBake()
        {
            var results = BakeAll();
            RecomputeCalibration();
            string report = WriteStatsReport();
            Debug.Log("[GlyphBake]\n" + string.Join("\n", results) + "\n" + report);
            bool failed = results.Any(r => !r.ok && !r.skipped);
            EditorApplication.Exit(failed ? 1 : 0);
        }

        public static List<Result> BakeAll()
        {
            var results = new List<Result>();
            var balance = AssetDatabase.LoadAssetAtPath<CombatBalance>("Assets/Resources/Balance/CombatBalance.asset") ?? CombatBalance.Default;
            Directory.CreateDirectory(DefinitionFolder);
            foreach (var src in Fonts)
            {
                if (string.IsNullOrEmpty(src.path))
                {
                    var skip = new Result { grapheme = "*", font = src.id, skipped = true };
                    skip.errors.Add("同梱フォント未設定（P3 で追加）");
                    results.Add(skip);
                    continue;
                }
                var font = AssetDatabase.LoadAssetAtPath<Font>(src.path);
                if (font == null)
                {
                    var r = new Result { grapheme = "*", font = src.id };
                    r.errors.Add("フォントを読み込めない: " + src.path);
                    results.Add(r);
                    continue;
                }
                byte[] bytes = File.ReadAllBytes(src.path);
                string fontHash = Sha256(bytes);
                var cmap = TrueTypeCmap.Load(src.path);
                foreach (char ch in Characters)
                    results.Add(BakeOne(font, src, fontHash, cmap, ch.ToString(), balance));
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return results;
        }

        static Result BakeOne(Font font, FontSource src, string fontHash, TrueTypeCmap cmap, string grapheme, CombatBalance b)
        {
            var r = new Result { grapheme = grapheme, font = src.id };
            int codepoint = char.ConvertToUtf32(grapheme, 0);
            if (!cmap.Has(codepoint))
            {
                r.errors.Add($"欠字（U+{codepoint:X4} がフォントに無い。代替グリフになる）");
                return r;
            }
            var pixels = Render(font, grapheme[0], Mathf.RoundToInt(b.emSizePx), out string renderError);
            if (pixels == null)
            {
                r.errors.Add(renderError);
                return r;
            }

            var mask = GlyphMask.FromAlpha(pixels, MaskSize, b.alphaThreshold);
            if (!InkBounds(mask, out var bounds)) { r.errors.Add("空マスク"); return r; }
            if (bounds.xMin < MinEdgeMargin || bounds.yMin < MinEdgeMargin ||
                bounds.xMax > MaskSize - MinEdgeMargin || bounds.yMax > MaskSize - MinEdgeMargin)
                r.errors.Add($"マスク端からの余白が {MinEdgeMargin}px 未満（字形が切れている可能性）: {bounds}");

            string key = GlyphCatalog.ResourceName(grapheme, src.id);
            string dir = $"{MaskFolder}/{GlyphCatalog.FontKey(src.id)}";
            Directory.CreateDirectory(dir);
            string pngPath = $"{dir}/{key}.png";
            var tex = new Texture2D(MaskSize, MaskSize, TextureFormat.RGBA32, false, true);
            tex.SetPixels32(pixels);
            tex.Apply();
            File.WriteAllBytes(pngPath, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate);
            var maskTex = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);

            var features = GlyphMaskAnalyzer.Analyze(mask, AnalyzerSettings.From(b));
            if (features.colliderRects.Length == 0) r.errors.Add("Collider が生成されない");
            if (features.colliderRects.Length > b.maxColliders) r.errors.Add($"Collider {features.colliderRects.Length} 個（上限 {b.maxColliders}）");

            string defPath = $"{DefinitionFolder}/{key}.asset";
            var def = AssetDatabase.LoadAssetAtPath<GlyphDefinition>(defPath);
            if (def == null)
            {
                def = ScriptableObject.CreateInstance<GlyphDefinition>();
                AssetDatabase.CreateAsset(def, defPath);
            }
            def.grapheme = grapheme;
            def.font = src.id;
            def.mask = maskTex;
            def.features = features;
            def.fontAssetPath = src.path;
            def.fontFamily = font.fontNames != null && font.fontNames.Length > 0 ? font.fontNames[0] : font.name;
            def.fontHash = fontHash;
            def.settingsHash = SettingsHash(b);
            def.bakedAt = DateTime.Now.ToString("s");
            def.unityVersion = Application.unityVersion;
            EditorUtility.SetDirty(def);

            var record = new BakeRecord
            {
                grapheme = grapheme, font = src.id.ToString(), fontAssetPath = src.path, fontFamily = def.fontFamily,
                fontHash = fontHash, settingsHash = def.settingsHash, bakedAt = def.bakedAt, unityVersion = def.unityVersion,
                features = features,
            };
            File.WriteAllText($"{dir}/{key}.json", JsonUtility.ToJson(record, true), Encoding.UTF8);

            r.definition = def;
            r.ok = r.errors.Count == 0;
            return r;
        }

        /// <summary>
        /// フォントアトラスの字形を 256×256 へ描画する。em 枠は [Margin, Margin+em]、基準線は em 枠下端から 0.12 em 上。
        /// 横方向は送り幅（advance）の中心を em 枠の中心に合わせる（全角は em と一致、欧文は中央寄せ）。横伸縮はしない。
        /// </summary>
        static Color32[] Render(Font font, char ch, int emSize, out string error)
        {
            error = null;
            string s = ch.ToString();
            font.RequestCharactersInTexture(s, emSize, FontStyle.Normal);
            if (!font.GetCharacterInfo(ch, out var ci, emSize, FontStyle.Normal))
            {
                error = "字形情報を取得できない";
                return null;
            }
            if (copyMaterial == null) copyMaterial = new Material(Shader.Find("Hidden/MojiBattle/GlyphCopy"));
            copyMaterial.mainTexture = font.material.mainTexture;

            var rt = RenderTexture.GetTemporary(MaskSize, MaskSize, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            GL.Clear(true, true, new Color(0f, 0f, 0f, 0f));
            GL.PushMatrix();
            GL.LoadPixelMatrix(0, MaskSize, 0, MaskSize);
            copyMaterial.SetPass(0);
            float originX = Mathf.Round(Margin + (emSize - ci.advance) * 0.5f);
            float baseline = Mathf.Round(Margin + IdeographicDescent * emSize);
            float x0 = originX + ci.minX, x1 = originX + ci.maxX;
            float y0 = baseline + ci.minY, y1 = baseline + ci.maxY;
            GL.Begin(GL.QUADS);
            GL.TexCoord(ci.uvBottomLeft); GL.Vertex3(x0, y0, 0f);
            GL.TexCoord(ci.uvTopLeft); GL.Vertex3(x0, y1, 0f);
            GL.TexCoord(ci.uvTopRight); GL.Vertex3(x1, y1, 0f);
            GL.TexCoord(ci.uvBottomRight); GL.Vertex3(x1, y0, 0f);
            GL.End();
            GL.PopMatrix();

            var tex = new Texture2D(MaskSize, MaskSize, TextureFormat.RGBA32, false, true);
            tex.ReadPixels(new Rect(0, 0, MaskSize, MaskSize), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            var px = tex.GetPixels32();
            UnityEngine.Object.DestroyImmediate(tex);
            return px;
        }

        static bool InkBounds(GlyphMask m, out RectInt bounds)
        {
            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = -1, y1 = -1;
            for (int y = 0; y < m.Size; y++)
            for (int x = 0; x < m.Size; x++)
            {
                if (!m[x, y]) continue;
                if (x < x0) x0 = x;
                if (x > x1) x1 = x;
                if (y < y0) y0 = y;
                if (y > y1) y1 = y;
            }
            bounds = x1 < 0 ? default : new RectInt(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
            return x1 >= 0;
        }

        public static List<GlyphDefinition> LoadDefinitions() =>
            AssetDatabase.FindAssets("t:GlyphDefinition", new[] { DefinitionFolder })
                .Select(g => AssetDatabase.LoadAssetAtPath<GlyphDefinition>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(d => d != null)
                .OrderBy(d => d.font).ThenBy(d => Characters.IndexOf(d.grapheme, StringComparison.Ordinal))
                .ToList();

        /// <summary>
        /// 参照集合（ベイク済みの全文字×字体）から各生値の下限・上限を決めて GlyphCalibration へ保存する（5.2）。
        /// 4 字体がそろうまでは provisional。対戦ごとの再正規化はしない。
        /// </summary>
        public static GlyphCalibration RecomputeCalibration()
        {
            var defs = LoadDefinitions();
            var cal = AssetDatabase.LoadAssetAtPath<GlyphCalibration>(CalibrationPath);
            if (cal == null)
            {
                cal = ScriptableObject.CreateInstance<GlyphCalibration>();
                AssetDatabase.CreateAsset(cal, CalibrationPath);
            }
            if (defs.Count == 0) return cal;
            Vector2 R(Func<GlyphFeatures, float> f) => new Vector2(defs.Min(d => f(d.features)), defs.Max(d => f(d.features)));
            cal.inkRatio = R(f => f.inkRatio);
            cal.widthRatio = R(f => f.widthRatio);
            cal.heightRatio = R(f => f.heightRatio);
            cal.edgeRatio = R(f => f.edgeRatio);
            cal.balance = R(f => f.balance);
            int fontCount = defs.Select(d => d.font).Distinct().Count();
            cal.provisional = fontCount < 4;
            cal.referenceSet = $"{defs.Select(d => d.grapheme).Distinct().Count()} chars × {fontCount} fonts ({string.Join(",", defs.Select(d => d.font).Distinct())})";
            EditorUtility.SetDirty(cal);
            AssetDatabase.SaveAssets();
            return cal;
        }

        /// <summary>全ベイク結果の特徴量・能力の一覧を保存する（5.3「出力を一覧で保存し、極端な逆転や全員同値を確認」）。</summary>
        public static string WriteStatsReport()
        {
            var defs = LoadDefinitions();
            var b = AssetDatabase.LoadAssetAtPath<CombatBalance>("Assets/Resources/Balance/CombatBalance.asset") ?? CombatBalance.Default;
            var cal = AssetDatabase.LoadAssetAtPath<GlyphCalibration>(CalibrationPath);
            var sb = new StringBuilder();
            sb.AppendLine("# Glyph stats (baked)");
            sb.AppendLine();
            sb.AppendLine($"calibration: {cal?.referenceSet} provisional={cal?.provisional}");
            sb.AppendLine();
            sb.AppendLine("| 文字 | 字体 | ink | W | H | edge | bal | A | W | H | C | B | M | 攻撃 | 防御 | 速度 | 耐久 | HP | 武器kg | 本体kg | 重量 | 溜め s | Collider | 武器長 |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
            var stats = new List<FighterStats>();
            foreach (var d in defs)
            {
                var f = d.features;
                var n = cal != null ? cal.Normalize(f) : default;
                var s = StatCalculator.Compute(n, b);
                stats.Add(s);
                var rt = new GlyphDefinitionRuntime { grapheme = d.grapheme, font = d.font, texture = d.mask, features = f, normalized = n };
                var geo = FighterFactory.BuildGeometry(rt, b);
                sb.AppendLine($"| {d.grapheme} | {GlyphCatalog.FontDisplayName(d.font)} | {f.inkRatio:F3} | {f.widthRatio:F2} | {f.heightRatio:F2} | {f.edgeRatio:F1} | {f.balance:F2} | " +
                              $"{n.A:F0} | {n.W:F0} | {n.H:F0} | {n.C:F0} | {n.B:F0} | {s.weightScore} | {s.attack} | {s.defense} | {s.speed} | {s.durability} | {s.maxHp:F0} | " +
                              $"{s.weaponMass:F2} | {s.bodyMass:F2} | {StatCalculator.Classify(s.weightScore, b)} {new string('★', s.WeightStars)} | {StatCalculator.Windup(s.weightScore, b):F2} | " +
                              $"{f.colliderRects.Length} | {geo.length:F2} |");
            }
            sb.AppendLine();
            // 全員同値・極端な逆転の自動チェック
            void Spread(string name, Func<FighterStats, int> get)
            {
                int min = stats.Min(get), max = stats.Max(get);
                sb.AppendLine($"- {name}: {min}〜{max}{(max - min < 10 ? "  ⚠ ほぼ同値" : "")}");
            }
            if (stats.Count > 0)
            {
                Spread("攻撃", s => s.attack);
                Spread("防御", s => s.defense);
                Spread("速度", s => s.speed);
                Spread("耐久", s => s.durability);
                Spread("重量M", s => s.weightScore);
            }
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Reports", "glyph_stats.md"));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            return sb.ToString();
        }

        public static string SettingsHash(CombatBalance b)
        {
            string s = $"{MaskSize}|{Margin}|{IdeographicDescent}|{b.emSizePx}|{b.alphaThreshold}|{b.colliderGrid}|{b.cellOccupancy}|{b.maxColliders}|{b.minComponentPixels}|{b.dilateForColliders}";
            return Sha256(Encoding.UTF8.GetBytes(s)).Substring(0, 16);
        }

        static string Sha256(byte[] data)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "").ToLowerInvariant();
        }
    }
}
