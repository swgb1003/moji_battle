using System;
using System.Collections.Generic;
using UnityEngine;

namespace MojiBattle
{
    public enum DraftStage { Selecting, Build, Battle, Result }
    [Serializable] public sealed class DraftRound
    {
        public int round, winner;
        public string player, com;
        public float seconds, hp, maxHp;
    }
    [Serializable] public sealed class DraftSession
    {
        public int version = 1, seed, round = 1, playerWins, comWins;
        public string achievementId;
        public DraftStage stage;
        public string[] pool;
        public FighterBuildData player, com;
        public bool playerPicked, comPicked;
        public List<DraftRound> history = new List<DraftRound>();
        public bool Finished => playerWins >= 2 || comWins >= 2;
        public bool PlayerFirst => round % 2 == 1;
        public bool PlayerTurn => stage == DraftStage.Selecting && player == null && (PlayerFirst || com != null);
        public bool ComTurn => stage == DraftStage.Selecting && com == null && (!PlayerFirst || player != null);
    }
    /// <summary>Draft picks and scores are isolated from regular saved builds. Every result is accepted once.</summary>
    public static class DraftRuntimeStore
    {
        public const string PrefKey = "MojiBattle.Draft.v1";
        public static DraftSession Current { get; private set; }
        public static bool Active => Current != null;
        // All candidates must have a baked brush glyph, including in WebGL where runtime fonts differ.
        public static readonly string[] Candidates = { "火", "刀", "龍", "盾", "風", "雷", "一", "山", "木", "水", "剣", "光", "闇", "炎", "氷", "虎", "拳", "破", "空", "星", "月", "海", "石", "鉄", "天", "力", "心", "夢", "翼", "斬" };
        public static void Begin(int seed)
        {
            Current = new DraftSession { seed = seed, achievementId = Guid.NewGuid().ToString("N") };
            Deal();
        }
        static void Deal()
        {
            var s = Current;
            var available = GlyphCatalog.AvailableGraphemes(FontStyleId.Brush);
            var bag = new List<string>();
            foreach (var ch in Candidates) if (available.Contains(ch)) bag.Add(ch);
            if (bag.Count < 6) foreach (var ch in available) if (!bag.Contains(ch)) bag.Add(ch);
            if (bag.Count < 6) throw new InvalidOperationException("ドラフト用の字形が6種類必要です。");
            var rng = new System.Random(unchecked(s.seed + s.round * 7919));
            for (int i = bag.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); var tmp = bag[i]; bag[i] = bag[j]; bag[j] = tmp; }
            s.pool = bag.GetRange(0, 6).ToArray(); s.player = s.com = null;s.playerPicked=s.comPicked=false;
            s.stage = DraftStage.Selecting; Save();
        }
        public static bool PickPlayer(string ch)
        {
            var s = Current;
            if (s == null || !s.PlayerTurn || Array.IndexOf(s.pool, ch) < 0 || s.com?.character == ch) return false;
            s.player = Build(ch, false); AfterPick(); return true;
        }
        public static bool PickCom()
        {
            var s = Current;
            if (s == null || !s.ComTurn) return false;
            var bag = new List<string>(s.pool); bag.Remove(s.player?.character);
            var rng = new System.Random(unchecked(s.seed + s.round * 3571));
            // COM evaluates three different builds, then picks a strong option with some variety.
            var scores = new List<(string ch, float score)>();
            foreach (var ch in bag)
            {
                var b = Build(ch, true); var stats = DraftUiStats.Values(b);
                scores.Add((ch, stats.x + stats.y + stats.z + (float)rng.NextDouble() * .6f));
            }
            scores.Sort((a,b) => b.score.CompareTo(a.score));
            s.com = Build(scores[0].ch, true); AfterPick(); return true;
        }
        static FighterBuildData Build(string ch, bool com)
        {
            var b = FighterBuildData.Default(ch); b.fontType = FontStyleId.Brush;
            if (com)
            {
                var rng = new System.Random(unchecked(Current.seed + Current.round * 101 + ch[0]));
                b.weaponSize = (WeaponSize)rng.Next(4);
                b.gripType = (GripType)rng.Next(4); b.battleStyle = (BattleStyle)rng.Next(4);
                b.MaybeDualWield(Current.seed + Current.round * 101);
            }
            var moves = SpecialMoveLibrary.For(ch); b.special = moves.Count > 0 ? moves[0].Clone() : null;
            return b;
        }
        static void AfterPick()
        {
            Current.playerPicked=Current.player!=null;Current.comPicked=Current.com!=null;
            if (Current.playerPicked && Current.comPicked) Current.stage = DraftStage.Build; Save();
        }
        public static void SetBuild(FighterBuildData b)
        {
            var s = Current;
            if (s == null || s.stage != DraftStage.Build || b == null || b.character != s.player?.character) return;
            if (!Enum.IsDefined(typeof(FontStyleId), b.fontType) || !Enum.IsDefined(typeof(WeaponSize), b.weaponSize)
                || !Enum.IsDefined(typeof(GripType), b.gripType) || !Enum.IsDefined(typeof(BattleStyle), b.battleStyle)
                || !FighterCustomizer.IsAvailable(b.character, b.fontType, out _)) return;
            s.player = b.Clone(); Save();
        }
        public static bool CanBattle => Current != null && Current.stage == DraftStage.Build && Current.player != null && Current.com != null;
        public static MatchConfig BeginMatch(int seed, float duration)
        {
            if (!CanBattle) throw new InvalidOperationException("ドラフトの構成を確定してください。");
            Current.stage = DraftStage.Battle; Save();
            return new MatchConfig { left = Current.player.ToLoadout(), right = Current.com.ToLoadout(), seed = seed, stage=StageCatalog.ForSeed(seed),durationSeconds = duration };
        }
        public static bool Record(MatchResult r)
        {
            var s = Current;
            if (s == null || s.stage != DraftStage.Battle || r.winner < -1 || r.winner > 1
                || r.hpRemaining==null || r.maxHp==null || r.hpRemaining.Length!=2 || r.maxHp.Length!=2) return false;
            if (r.winner == 0) s.playerWins++; else if (r.winner == 1) s.comWins++;
            s.history.Add(new DraftRound { round = s.round, player = s.player.character, com = s.com.character,
                winner = r.winner, seconds = r.elapsedSeconds, hp = r.hpRemaining[0], maxHp = r.maxHp[0] });
            s.stage = DraftStage.Result; Save();
            AchievementStore.Change(p => p.RecordDraft(s),true);
            return true;
        }
        public static bool Next()
        {
            if (Current == null || Current.stage != DraftStage.Result || Current.Finished) return false;
            if (Current.history[Current.history.Count - 1].winner < 0) Current.stage = DraftStage.Build;
            else { Current.round++; Deal(); }
            Save(); return true;
        }
        public static bool BackToBuild()
        { if (Current?.stage != DraftStage.Battle) return false; Current.stage = DraftStage.Build; Save(); return true; }
        public static void ClearSession() => Current = null;
        public static bool CanResume => ReadSaved() != null;
        public static bool Resume()
        {
            var saved = ReadSaved(); if (saved == null) return false;
            Current = saved;
            if (string.IsNullOrEmpty(Current.achievementId)) Current.achievementId = Guid.NewGuid().ToString("N");
            if (Current.stage == DraftStage.Battle) Current.stage = DraftStage.Build;
            Save(); AchievementStore.Change(p => p.RecordDraft(Current),true); return true;
        }
        static DraftSession ReadSaved()
        {
            try
            {
                var s = JsonUtility.FromJson<DraftSession>(PlayerPrefs.GetString(PrefKey, ""));
                // Unity's inline serializer materializes empty nested objects instead of preserving null.
                if(s!=null){if(!s.playerPicked)s.player=null;if(!s.comPicked)s.com=null;}
                if (s == null || s.version != 1 || s.Finished || s.round < 1 || s.round > 3 || s.history == null
                    || s.playerWins < 0 || s.comWins < 0
                    || !Enum.IsDefined(typeof(DraftStage), s.stage) || s.pool == null || s.pool.Length != 6) return null;
                var seen = new HashSet<string>();
                foreach (var ch in s.pool) if (!seen.Add(ch) || !FighterCustomizer.IsAvailable(ch, FontStyleId.Brush, out _)) return null;
                foreach (var b in new[] { s.player, s.com }) if (b != null && (!seen.Contains(b.character)
                    || !Enum.IsDefined(typeof(FontStyleId),b.fontType) || !Enum.IsDefined(typeof(WeaponSize), b.weaponSize) || !Enum.IsDefined(typeof(GripType), b.gripType)
                    || !Enum.IsDefined(typeof(BattleStyle), b.battleStyle) || !FighterCustomizer.IsAvailable(b.character, b.fontType, out _))) return null;
                if (s.player != null && s.com != null && s.player.character == s.com.character) return null;
                if (s.stage != DraftStage.Selecting && (s.player == null || s.com == null)) return null;
                if(s.stage==DraftStage.Selecting && (s.player!=null && s.com!=null
                    || s.PlayerFirst && s.com!=null || !s.PlayerFirst && s.player!=null))return null;
                int round=1,pw=0,cw=0;
                foreach(var h in s.history)
                {
                    if(h==null || h.round!=round || h.winner < -1 || h.winner > 1 || pw>=2 || cw>=2)return null;
                    if(h.winner==0)pw++;else if(h.winner==1)cw++;
                    if(h.winner>=0)round++;
                }
                if(pw!=s.playerWins || cw!=s.comWins)return null;
                if(s.stage==DraftStage.Result)
                {if(s.history.Count==0 || s.history[s.history.Count-1].round!=s.round)return null;}
                else if(s.round!=round)return null;
                return s;
            }
            catch (Exception) { return null; }
        }
        static void Save() { PlayerPrefs.SetString(PrefKey, JsonUtility.ToJson(Current)); PlayerPrefs.Save(); }
    }
    public static class DraftUiStats
    {
        // Same glyph, size, grip and modifier calculations as the actual battle and customization preview.
        public static Vector3 Values(FighterBuildData b)
        {
            if (!GlyphCatalog.TryGet(b.character, b.fontType, CombatBalance.Default, GlyphCalibration.Default, out var glyph, out _)) return Vector3.zero;
            glyph = GlyphFlip.Apply(glyph, b.weaponFlip);
            var stats = StatCalculator.Compute(glyph, CombatBalance.Default);
            var geometry = FighterFactory.ResolveWeapon(b, glyph, stats, CombatBalance.Default, CustomizeBalance.Default, out float mass);
            var mods = FighterModifiers.From(b, glyph, stats, geometry, mass, CombatBalance.Default, CustomizeBalance.Default);
            float range = (CombatBalance.Default.shoulderLocal.x + geometry.length * CombatBalance.Default.reachFactor + .1f) * mods.reach;
            return new Vector3(Mathf.InverseLerp(35,85,stats.attack * mods.damage), Mathf.Clamp01(stats.speed * mods.attackSpeed / 100f), Mathf.Clamp01(range / 4f));
        }
    }
}
