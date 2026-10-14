using UnityEngine;

namespace MojiBattle
{
    /// <summary>自操作の相手（COM）の強さ。サンドバッグは練習モード専用（何もしない）。</summary>
    public enum ComDifficulty { Beginner, Weak, Normal, Strong, MojiFighter, TrueMojiFighter, Sandbag }

    /// <summary>COM の強さの調整値。1 は観戦（COM 同士）の既定の AI と同じ。</summary>
    public readonly struct DifficultyProfile
    {
        /// <summary>判断の間隔の倍率（大きいほど判断が遅い）</summary>
        public readonly float decisionScale;
        /// <summary>相手の攻撃予兆への反応・攻撃意欲・受け流し・技（蹴り・フェイントなど）の確率の倍率</summary>
        public readonly float reaction, willingness, parry, technique;
        /// <summary>1 回の判断で出す連撃数の上限</summary>
        public readonly int comboMax;
        /// <summary>与ダメージ・必殺ゲージの溜まり方・最大 HP の倍率</summary>
        public readonly float damage, gauge, hp;

        public DifficultyProfile(float decisionScale, float reaction, float willingness, float parry, float technique, int comboMax, float damage, float gauge, float hp = 1f)
        {
            this.decisionScale = decisionScale; this.reaction = reaction; this.willingness = willingness; this.parry = parry;
            this.technique = technique; this.comboMax = comboMax; this.damage = damage; this.gauge = gauge; this.hp = hp;
        }
    }

    /// <summary>
    /// 観戦／自操作の選択と COM の強さ。選んだ値は設定に保存し、各モードの対戦前画面で切り替える。
    /// 「真・mojiFighter」はワールド文字カップを「moji Fighter」以上で全試合自操作して優勝すると解放される。
    /// </summary>
    public static class PlayControl
    {
        public const string UnlockKey = "MojiBattle.TrueMojiFighter.v1";
        public static readonly ComDifficulty[] Levels =
            { ComDifficulty.Beginner, ComDifficulty.Weak, ComDifficulty.Normal, ComDifficulty.Strong, ComDifficulty.MojiFighter, ComDifficulty.TrueMojiFighter };

        /// <summary>自操作で戦う（友人対戦は対象外で、常に観戦）。</summary>
        public static bool SelfControl => GameSettings.SelfControl && !GameFlow.FriendMode;
        public static ComDifficulty Difficulty
        {
            get
            {
                var d = (ComDifficulty)GameSettings.ComDifficulty;
                return Available(d) && d != ComDifficulty.Sandbag ? d : ComDifficulty.Normal;
            }
        }
        public static void SetSelfControl(bool on) => GameSettings.Change(s => s.selfControl = on);
        public static void SetDifficulty(ComDifficulty d) { if (Available(d) && d != ComDifficulty.Sandbag) GameSettings.Change(s => s.comDifficulty = (int)d); }

        public static bool TrueUnlocked => PlayerPrefs.GetInt(UnlockKey, 0) == 1;
        public static bool Available(ComDifficulty d) => d != ComDifficulty.TrueMojiFighter || TrueUnlocked;
        /// <summary>解放した時だけ true（既に解放済みなら false）。</summary>
        public static bool Unlock()
        {
            if (TrueUnlocked) return false;
            PlayerPrefs.SetInt(UnlockKey, 1); PlayerPrefs.Save();
            return true;
        }
        /// <summary>
        /// COM の選手に強さを当てる。練習で途中から変えられるよう、最初に当てた時の値（性格・威力・ゲージ）を覚えて、そこから計算する。
        /// </summary>
        public static void ApplyDifficulty(Fighter com, ComDifficulty d, BattleIntervention iv)
        {
            var brain = com.Brain;
            if (brain.Baseline == null)
                brain.Baseline = new DifficultyBaseline { tendency = com.Tendency.Clone(), damage = com.Mods.damage,
                    gauge = iv != null ? iv.Side(com.Id).gaugeRateScale : 1f, maxHp = com.Runtime.maxHp };
            var baseLine = brain.Baseline;
            brain.Frozen = d == ComDifficulty.Sandbag;
            var p = Profile(d);
            brain.DecisionScale = p.decisionScale; brain.ParryScale = p.parry; brain.TechniqueScale = p.technique;
            var t = baseLine.tendency.Clone();
            t.reactionChance = Mathf.Min(.98f, t.reactionChance * p.reaction);
            t.attackWillingness = Mathf.Min(.98f, t.attackWillingness * p.willingness);
            t.counterBias = Mathf.Min(.98f, t.counterBias * p.reaction);
            t.comboMax = Mathf.Max(1, Mathf.Min(t.comboMax, p.comboMax));
            t.comboMin = Mathf.Min(t.comboMin, t.comboMax);
            com.SetTendency(t);
            var mods = com.Mods; mods.damage = baseLine.damage * p.damage; com.Mods = mods;
            // 最大 HP は今の割合を保って変える（練習で途中から変えても減ったように見えない）
            float share = com.Runtime.maxHp > 0f ? com.Runtime.hp / com.Runtime.maxHp : 1f;
            var stats = com.Stats; stats.maxHp = baseLine.maxHp * p.hp; com.Stats = stats;
            com.Runtime.maxHp = stats.maxHp; com.Runtime.hp = stats.maxHp * share;
            if (iv != null) iv.Side(com.Id).gaugeRateScale = baseLine.gauge * p.gauge;
        }

        public static void ResetUnlockForTests() { PlayerPrefs.DeleteKey(UnlockKey); PlayerPrefs.Save(); }

        public static string Label(ComDifficulty d)
        {
            switch (d)
            {
                case ComDifficulty.Beginner: return "初心者";
                case ComDifficulty.Weak: return "弱い";
                case ComDifficulty.Normal: return "ふつう";
                case ComDifficulty.Strong: return "強い";
                case ComDifficulty.MojiFighter: return "moji Fighter";
                case ComDifficulty.TrueMojiFighter: return "真・mojiFighter";
                default: return "サンドバッグ";
            }
        }

        /// <summary>
        /// 強さの調整値。「強い」が観戦の COM と同じ AI。下の段階は判断が遅く、反応・受け流し・技が減る。
        /// 上の段階は判断が速く、反応・受け流しが冴え、一撃も重い。
        /// </summary>
        public static DifficultyProfile Profile(ComDifficulty d)
        {
            switch (d)
            {
                case ComDifficulty.Beginner: return new DifficultyProfile(3f, .1f, .45f, 0f, 0f, 1, .7f, .5f);
                case ComDifficulty.Weak: return new DifficultyProfile(2f, .35f, .65f, .3f, .35f, 1, .85f, .75f);
                case ComDifficulty.Normal: return new DifficultyProfile(1.4f, .65f, .85f, .6f, .7f, 2, .95f, .9f);
                case ComDifficulty.Strong: return new DifficultyProfile(1f, 1f, 1f, 1f, 1f, 99, 1f, 1f);
                case ComDifficulty.MojiFighter: return new DifficultyProfile(.7f, 1.4f, 1.1f, 1.8f, 1.2f, 99, 1.12f, 1.15f, 1.1f);
                case ComDifficulty.TrueMojiFighter: return new DifficultyProfile(.5f, 2f, 1.2f, 2.6f, 1.4f, 99, 1.25f, 1.3f, 1.25f);
                default: return new DifficultyProfile(1f, 0f, 0f, 0f, 0f, 1, 1f, 1f);
            }
        }
    }

    /// <summary>強さを当てる前の COM の値（練習で強さを変え直す時の基準）。</summary>
    public sealed class DifficultyBaseline
    {
        public ClassTendency tendency;
        public float damage, gauge, maxHp;
    }
}
