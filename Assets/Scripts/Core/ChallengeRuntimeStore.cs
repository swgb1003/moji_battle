using System;
using System.Collections.Generic;
using UnityEngine;

namespace MojiBattle
{
    [Serializable] public sealed class ChallengeProgress
    {
        public int id, attempts;
        public bool cleared;
        public float bestSeconds;
        public FighterBuildData draft;
    }
    public sealed class ChallengeOutcome
    {
        public ChallengeDefinition Definition;
        public MatchResult Match;
        public bool BuildMet, GoalMet, Cleared, NewReward;
    }
    public static class ChallengeRuntimeStore
    {
        public const string PrefKey = "MojiBattle.Challenges.v1";
        [Serializable] sealed class Saved { public int version = 1; public List<ChallengeProgress> entries = new List<ChallengeProgress>(); }
        static Saved saved;
        static FighterBuildData matchPlayer;
        public static int SelectedId { get; private set; } = -1;
        public static bool ShowDetail { get; set; }
        public static bool Active => SelectedId >= 0;
        public static ChallengeDefinition Current => ChallengeCatalog.Get(SelectedId);
        public static ChallengeOutcome Outcome { get; private set; }
        public static int ClearedCount { get { int n = 0; foreach (var d in ChallengeCatalog.All) if (Progress(d.Id).cleared) n++; return n; } }
        public static ChallengeProgress Progress(int id)
        {
            if (ChallengeCatalog.Get(id) == null) throw new ArgumentOutOfRangeException(nameof(id));
            if (saved == null)
            {
                try { saved = JsonUtility.FromJson<Saved>(PlayerPrefs.GetString(PrefKey,"")); }
                catch (Exception e) { Debug.LogWarning("お題の保存データを読めません: " + e.Message); }
                if (saved == null) saved = new Saved();
                if (saved.entries == null) saved.entries = new List<ChallengeProgress>();
                saved.entries.RemoveAll(x => x == null || ChallengeCatalog.Get(x.id) == null);
            }
            var p = saved.entries.Find(x => x.id == id);
            if (p == null) { p = new ChallengeProgress { id = id }; saved.entries.Add(p); }
            return p;
        }
        public static FighterBuildData Draft => Current == null ? null : Current.Constrain(Progress(SelectedId).draft ?? Current.DefaultPlayer);
        public static void Select(int id)
        {
            if (ChallengeCatalog.Get(id) == null) throw new ArgumentOutOfRangeException(nameof(id));
            SelectedId = id; Outcome = null; ShowDetail = true;
        }
        public static void SetDraft(FighterBuildData build)
        {
            if (!Active) return;
            Progress(SelectedId).draft = Current.Constrain(build); Save();
        }
        public static MatchConfig BeginMatch(int seed, float duration)
        {
            if (!Active) throw new InvalidOperationException("お題が選択されていません。");
            matchPlayer = Draft; SetDraft(matchPlayer);
            Outcome = null;
            return new MatchConfig { left = matchPlayer.ToLoadout(), right = Current.Opponent.ToLoadout(), seed = seed, durationSeconds = duration };
        }
        public static ChallengeOutcome Record(MatchResult result, AchievementMatchStats stats)
        {
            if (!Active) return null;
            if (Outcome != null) return Outcome;
            var d = Current; var p = Progress(d.Id);
            Outcome = new ChallengeOutcome { Definition = d, Match = result,
                BuildMet = d.BuildAllowed(matchPlayer), GoalMet = d.GoalMet(stats)
                    && (d.Goal != ChallengeGoal.SpecialFinish || result.finishReason == "KO") };
            Outcome.Cleared = result.winner == 0 && Outcome.BuildMet && Outcome.GoalMet;
            p.attempts++;
            if (Outcome.Cleared)
            {
                p.bestSeconds = !p.cleared ? result.elapsedSeconds : Mathf.Min(p.bestSeconds, result.elapsedSeconds);
                p.cleared = true;
                Outcome.NewReward = !AchievementStore.Profile.Has(d.RewardId);
                AchievementStore.Change(profile => {
                    profile.Set(d.RewardId,1);
                    // Present the first-clear reward before any ordinary match achievements.
                    if (Outcome.NewReward)
                    {
                        profile.notifications.Remove(d.RewardId);
                        profile.notifications.Insert(0,d.RewardId);
                    }
                },true);
            }
            Save(); return Outcome;
        }
        public static int NextId => Current != null && SelectedId + 1 < ChallengeCatalog.All.Length ? SelectedId + 1 : -1;
        public static void ClearSession() { SelectedId = -1; ShowDetail = false; Outcome = null; matchPlayer = null; }
        public static void Reload() { saved = null; ClearSession(); }
        static void Save()
        {
            try { PlayerPrefs.SetString(PrefKey,JsonUtility.ToJson(saved)); PlayerPrefs.Save(); }
            catch (Exception e) { Debug.LogWarning("お題を保存できません: " + e.Message); }
        }
    }
}
