using System;
using System.Collections.Generic;
using UnityEngine;

namespace MojiBattle
{
    [Serializable] public sealed class AchievementProgress { public int id, value; }
    [Serializable] public sealed class AchievementUnlock { public int id; public string atUtc; public bool seen; }
    [Serializable]
    public sealed class AchievementProfile
    {
        public int version = 1, streak, fontMask, gripMask, styleMask;
        public bool migrated, matchInProgress;
        public List<AchievementProgress> progress = new List<AchievementProgress>();
        public List<AchievementUnlock> unlocked = new List<AchievementUnlock>();
        public List<int> notifications = new List<int>();
        public List<string> draftWinningCharacters = new List<string>();
        public string draftSessionId;
        public string runSessionId;
        public int runRecordedMatches;
        public List<string> runWinningCharacters=new List<string>();
        public int draftRecordedMatches;
        public List<string> worldCupChampionCharacters = new List<string>();
        public List<string> worldCupChampionSessions = new List<string>();
        public int Count => unlocked.Count;
        public int UnseenCount => unlocked.FindAll(x => !x.seen).Count;
        public bool Has(int id) => unlocked.Exists(x => x.id == id);
        public bool IsNew(int id) => unlocked.Exists(x => x.id == id && !x.seen);
        public int Value(int id) => progress.Find(x => x.id == id)?.value ?? 0;
        public AchievementUnlock Unlock(int id) => unlocked.Find(x => x.id == id);
        public void Set(int id, int value)
        {
            if (AchievementCatalog.Get(id) == null) return;
            var p = progress.Find(x => x.id == id);
            if (p == null) { p = new AchievementProgress { id = id }; progress.Add(p); }
            p.value = Math.Max(p.value, Math.Max(0, value));
            Check();
        }
        public void Add(int id, int amount = 1)
        { if (amount > 0) Set(id, (int)Math.Min(int.MaxValue, (long)Value(id) + amount)); }
        public void Check()
        {
            foreach (var d in AchievementCatalog.All)
                if (d.Id != 50 && Value(d.Id) >= d.Goal && !Has(d.Id)) Grant(d.Id);
            int others = unlocked.FindAll(x => x.id != 50).Count;
            var meta = progress.Find(x => x.id == 50);
            if (meta == null) { meta = new AchievementProgress { id = 50 }; progress.Add(meta); }
            meta.value = others;
            if (others >= 40 && !Has(50)) Grant(50);
        }
        void Grant(int id)
        {
            unlocked.Add(new AchievementUnlock { id = id, atUtc = DateTime.UtcNow.ToString("O") });
            notifications.Add(id);
        }
        public void Interrupted() { streak = 0; matchInProgress = false; }
        public void Result(int winner, int localSide)
        {
            matchInProgress = false;
            if (winner == localSide)
            { streak++; Add(2); Add(17); Set(15,streak); Set(16,streak); }
            else if (winner >= 0) streak = 0;
        }
        public void WinBuild(FighterBuildData build, FighterBuildData opponent)
        {
            fontMask |= 1 << (int)build.fontType;
            gripMask |= 1 << (int)build.gripType;
            styleMask |= 1 << (int)build.battleStyle;
            Set(36,Bits(fontMask)); Set(40,Bits(gripMask)); Set(41,Bits(styleMask));
            if (build.weaponSize == WeaponSize.XL) Add(37);
            if (build.weaponSize == WeaponSize.S && opponent.weaponSize == WeaponSize.XL) Add(38);
            if (build.weaponFlip != WeaponFlip.None) Add(39);
            if (AchievementCharacters.IsKana(build.character)) Add(42);
            if (AchievementCharacters.IsLatinOrDigit(build.character)) Add(43);
            if (AchievementCharacters.IsSymbol(build.character)) Add(44);
            if (AchievementCharacters.Strokes(build.character) >= 15) Add(45);
        }
        /// <summary>Replay the saved history once per result, including after resuming an unfinished draft.</summary>
        public void RecordDraft(DraftSession session)
        {
            if (session == null || string.IsNullOrEmpty(session.achievementId) || session.history == null) return;
            if (draftSessionId != session.achievementId)
            { draftSessionId = session.achievementId; draftRecordedMatches = 0; }
            draftWinningCharacters = draftWinningCharacters ?? new List<string>();
            var previouslyUnlocked = new HashSet<int>();
            for (int id=57; id<=64; id++) if (Has(id)) previouslyUnlocked.Add(id);
            int playerWins=0, comWins=0, firstWinner=-1;
            for (int i=0; i<session.history.Count; i++)
            {
                var round=session.history[i];
                if (round.winner>=0 && firstWinner<0) firstWinner=round.winner;
                if (round.winner==0) playerWins++; else if (round.winner==1) comWins++;
                if (i<draftRecordedMatches) continue;
                Set(57,1);
                if (round.winner==0)
                {
                    if (!string.IsNullOrEmpty(round.player) && !draftWinningCharacters.Contains(round.player))
                        draftWinningCharacters.Add(round.player);
                    Set(62,draftWinningCharacters.Count); Set(63,draftWinningCharacters.Count);
                    if (round.round%2==0) Set(61,1);
                    if (playerWins==2)
                    {
                        Set(58,1); Add(64);
                        if (comWins==0) Set(59,1);
                        if (comWins==1 && firstWinner==1) Set(60,1);
                    }
                }
                draftRecordedMatches=i+1;
            }
            // Newly earned draft medals precede ordinary match notifications; retain the older queue.
            for (int id=64; id>=57; id--)
                if (!previouslyUnlocked.Contains(id) && Has(id) && notifications.Remove(id)) notifications.Insert(0,id);
        }
        public void RecordRun(RunSession session)
        {
            if(session==null || string.IsNullOrEmpty(session.id) || session.history==null)return;
            if(runSessionId!=session.id){runSessionId=session.id;runRecordedMatches=0;}
            runWinningCharacters=runWinningCharacters??new List<string>();
            var old=new HashSet<int>();for(int id=65;id<=68;id++)if(Has(id))old.Add(id);
            for(int i=runRecordedMatches;i<session.history.Count;i++)
            {
                var r=session.history[i];
                if(r.round==5 && r.winner==0)
                {
                    Set(65,1);Add(68);if(r.hp/r.maxHp<=.1f)Set(66,1);
                    if(!runWinningCharacters.Contains(session.player.character))runWinningCharacters.Add(session.player.character);
                    Set(67,runWinningCharacters.Count);
                }
                runRecordedMatches=i+1;
            }
            for(int id=68;id>=65;id--)if(!old.Contains(id)&&Has(id)&&notifications.Remove(id))notifications.Insert(0,id);
        }
        /// <summary>
        /// ワールド文字カップの称号。進行状況から決まるので、結果を記録するたびに評価し直してよい（何度呼んでも二重に数えない）。
        /// </summary>
        public void RecordWorldCup(WorldCupSession s)
        {
            if (s == null) return;
            var old = new HashSet<int>(); for (int id = 69; id <= 77; id++) if (Has(id)) old.Add(id);
            Set(69, 1);
            if (s.Qualified)
            {
                Set(70, 1);
                var standings = s.Standings(s.PlayerGroup);
                if (standings[0].Id == 0) Set(71, 1);
                if (standings.Count > 2 && standings[2].Id == 0) Set(72, 1);
            }
            // 準々決勝（ベスト8）の組み合わせに入った
            for (int id = 96; id <= 99; id++) { var m = s.Match(id); if (m != null && (m.LeftId == 0 || m.RightId == 0)) { Set(73, 1); break; } }
            if (s.ChampionId == 0 || s.RunnerUpId == 0 || s.ThirdId == 0) Set(74, 1);
            if (s.ChampionId == 0)
            {
                Set(75, 1);
                bool lost = false;
                foreach (var m in s.Matches) if (m.Complete && m.IncludesPlayer && m.WinnerId >= 0 && m.WinnerId != 0) { lost = true; break; }
                if (!lost) Set(76, 1);
                worldCupChampionSessions = worldCupChampionSessions ?? new List<string>();
                worldCupChampionCharacters = worldCupChampionCharacters ?? new List<string>();
                if (!string.IsNullOrEmpty(s.Id) && !worldCupChampionSessions.Contains(s.Id))
                {
                    worldCupChampionSessions.Add(s.Id);
                    string ch = s.Entrants[0].Build.character;
                    if (!worldCupChampionCharacters.Contains(ch)) worldCupChampionCharacters.Add(ch);
                }
                Set(77, worldCupChampionCharacters.Count);
            }
            // 新しく得た大会の称号を先に知らせる
            for (int id = 77; id >= 69; id--) if (!old.Contains(id) && Has(id) && notifications.Remove(id)) notifications.Insert(0, id);
        }
        public static int Bits(int value) { int n=0; for (int i=0;i<4;i++) if ((value & (1<<i)) != 0) n++; return n; }
    }

    public static class AchievementStore
    {
        public const string PrefKey = "MojiBattle.Achievements.v1";
        static AchievementProfile profile;
        static bool dirty;
        public static event Action Changed;
        public static AchievementProfile Profile { get { Load(); return profile; } }
        public static void Load()
        {
            if (profile != null) return;
            try { profile = JsonUtility.FromJson<AchievementProfile>(PlayerPrefs.GetString(PrefKey,"")); }
            catch (Exception e) { Debug.LogWarning("称号データを読めません: " + e.Message); }
            if (profile == null) profile = new AchievementProfile();
            profile.progress = profile.progress ?? new List<AchievementProgress>();
            profile.unlocked = profile.unlocked ?? new List<AchievementUnlock>();
            profile.notifications = profile.notifications ?? new List<int>();
            profile.runWinningCharacters=profile.runWinningCharacters??new List<string>();
            profile.draftWinningCharacters = profile.draftWinningCharacters ?? new List<string>();
            if (profile.matchInProgress) profile.Interrupted(); // Closing the browser during a match is an exit.
            if (!profile.migrated)
            {
                profile.migrated = true;
                if (PlayerPrefs.HasKey("MojiBattle.Builds.v1")) profile.Set(6,1);
                foreach (var move in SpecialMoveLibrary.All())
                { profile.Set(3,1); profile.Set(4,SpecialMoveLibrary.For(move.character).Count); }
            }
            profile.Check(); dirty = true; Save();
        }
        public static void Change(Action<AchievementProfile> action, bool immediate = false)
        {
            var p = Profile; int before = p.Count; action(p); dirty = true;
            if (immediate || before != p.Count) Save();
            Changed?.Invoke();
        }
        public static void Customized() => Change(p => p.Set(6,1),true);
        public static void SpecialSaved(string ch) => Change(p => { p.Set(3,1); p.Set(4,SpecialMoveLibrary.For(ch).Count); },true);
        public static void Save()
        {
            if (!dirty || profile == null) return;
            try { PlayerPrefs.SetString(PrefKey,JsonUtility.ToJson(profile)); PlayerPrefs.Save(); dirty = false; }
            catch (Exception e) { Debug.LogWarning("称号を保存できません: " + e.Message); }
        }
        public static void Reload() { profile = null; dirty = false; }
        public static void Reset()
        {
            // A user reset must not re-grant achievements from the migration of old builds.
            profile = new AchievementProfile { migrated = true };
            dirty = true; Save(); Changed?.Invoke();
        }
    }
}
