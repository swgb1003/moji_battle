using UnityEngine;

namespace MojiBattle
{
    /// <summary>
    /// 選択したカスタマイズのシーン間保持（Customize → Battle → Result → Rematch）。
    /// 再起動後も残るよう PlayerPrefs へ JSON で保存する（必須ではないが簡単なため）。
    /// </summary>
    public static class BuildRuntimeStore
    {
        const string PrefKey = "MojiBattle.Builds.v1";
        static readonly FighterBuildData[] builds = new FighterBuildData[2];
        static bool loaded;

        [System.Serializable]
        sealed class Saved { public FighterBuildData left, right; }

        /// <summary>カスタマイズ画面から試合を始めた（設定を使う）か。Battle シーン単体の再生では false。</summary>
        public static bool Active { get; set; }

        public static FighterBuildData Get(int side)
        {
            EnsureLoaded();
            return builds[side];
        }

        public static void Set(int side, FighterBuildData build)
        {
            EnsureLoaded();
            builds[side] = build.Clone();
            Save();
        }

        public static void ResetToDefaults()
        {
            builds[0] = FighterBuildData.Default("一");
            builds[1] = FighterBuildData.Default("鬱");
            loaded = true;
        }

        static void EnsureLoaded()
        {
            if (loaded) return;
            ResetToDefaults();
            try
            {
                var json = PlayerPrefs.GetString(PrefKey, "");
                if (!string.IsNullOrEmpty(json))
                {
                    var s = JsonUtility.FromJson<Saved>(json);
                    if (s?.left != null) builds[0] = s.left;
                    if (s?.right != null) builds[1] = s.right;
                }
            }
            catch (System.Exception e) { Debug.LogWarning("[MojiBattle] 保存したカスタマイズを読めません: " + e.Message); }
        }

        static void Save()
        {
            try
            {
                PlayerPrefs.SetString(PrefKey, JsonUtility.ToJson(new Saved { left = builds[0], right = builds[1] }));
                PlayerPrefs.Save();
            }
            catch (System.Exception e) { Debug.LogWarning("[MojiBattle] カスタマイズを保存できません: " + e.Message); }
        }
    }
}
