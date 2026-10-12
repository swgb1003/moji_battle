using UnityEngine.SceneManagement;

namespace MojiBattle
{
    /// <summary>
    /// 画面遷移（カスタマイズ仕様 3）: タイトル → カスタマイズ（文字入力・字体・サイズ・持ち方・握る位置・スタイル）→ VS確認 → BATTLE → リザルト。
    /// リザルトは Battle シーン内のオーバーレイ（同じ設定でもう一度 / カスタマイズへ戻る）。
    /// </summary>
    public static class GameFlow
    {
        public const string TitleScene = "Title";
        public const string ModeSelectScene = "ModeSelect";
        public const string CustomizeScene = "Customize";
        public const string BattleScene = "Battle";
        public const string TournamentScene = "Tournament";
        public const string FriendRoomScene = "FriendRoom";
        public const string AchievementsScene = "Achievements";
        public const string ChallengeScene = "Challenges";
        public const string DraftScene = "Draft";
        public const string RunScene = "Run";
        public const string WorldCupScene = "WorldCup";
        public static bool WorldCupMode => WorldCupRuntimeStore.Active;
        public static bool TournamentSelect {get;private set;}
        public static bool RunMode => RunRuntimeStore.Active;
        public static bool RunIntro { get; private set; }
        public static bool DraftMode => DraftRuntimeStore.Active;
        public static bool DraftIntro { get; private set; }
        public static bool ChallengeMode => ChallengeRuntimeStore.Active;
        public static bool FriendMode { get; private set; }
        public static bool TournamentMode { get; private set; }

        static void LeaveTournament()
        {
            FriendMode = false;
            if (FriendRoomSession.Instance != null) FriendRoomSession.Instance.CloseImmediately();
            TournamentRuntimeStore.Clear();
            TournamentMode = false;
            ChallengeRuntimeStore.ClearSession();
            DraftRuntimeStore.ClearSession();
            RunRuntimeStore.ClearSession();
            WorldCupRuntimeStore.ClearSession();
            TournamentSelect=false;
            TimeController.ResetAll();
        }

        public static void ToTitle() { LeaveTournament(); SceneManager.LoadScene(TitleScene); }
        public static void ToModeSelect() { LeaveTournament(); SceneManager.LoadScene(ModeSelectScene); }
        public static void ToAchievements() { LeaveTournament(); SceneManager.LoadScene(AchievementsScene); }
        public static void ToCustomize() { LeaveTournament(); SceneManager.LoadScene(CustomizeScene); }
        public static void ToChallenges()
        {
            LeaveTournament(); SceneManager.LoadScene(ChallengeScene);
        }
        public static void ToDraftIntro()
        { LeaveTournament(); DraftIntro = true; SceneManager.LoadScene(DraftScene); }
        public static void ToRunIntro()
        { LeaveTournament(); RunRuntimeStore.ReconcileSavedRewards(); RunIntro=true; SceneManager.LoadScene(RunScene); }
        public static void BeginRun(int seed)
        { LeaveTournament(); RunRuntimeStore.Begin(seed); RunIntro=false; SceneManager.LoadScene(CustomizeScene); }
        public static void ResumeRun()
        {
            LeaveTournament(); if(!RunRuntimeStore.Resume()){ToRunIntro();return;}
            RunIntro=false; SceneManager.LoadScene(RunRuntimeStore.Current.stage==RunStage.Build?CustomizeScene:RunScene);
        }
        public static void ToRunBuild()
        { if(!RunRuntimeStore.EditBuild())return;TimeController.ResetAll();SceneManager.LoadScene(CustomizeScene); }
        public static void ToRunPrepare()
        { if(!RunRuntimeStore.Prepare())return;ToRunScreen(); }
        public static void ToRunScreen()
        { if(!RunMode)return;RunIntro=false;TimeController.ResetAll();SceneManager.LoadScene(RunScene); }
        public static void ToRunUpgrade()
        { if(RunRuntimeStore.OpenUpgrade())ToRunScreen(); }
        public static void ToRunBattle()
        { if(!RunRuntimeStore.CanBattle)return;TimeController.ResetAll();SceneManager.LoadScene(BattleScene); }
        public static void BackFromRunBattle()
        { RunRuntimeStore.AbortMatch();ToRunScreen(); }
        public static void BeginDraft(int seed)
        { LeaveTournament(); DraftRuntimeStore.Begin(seed); DraftIntro = false; SceneManager.LoadScene(DraftScene); }
        public static void ResumeDraft()
        {
            LeaveTournament(); if (!DraftRuntimeStore.Resume()) { ToDraftIntro(); return; }
            DraftIntro = false; SceneManager.LoadScene(DraftScene);
        }
        public static void ToDraftPicks()
        { if (!DraftMode) return; DraftIntro = false; TimeController.ResetAll(); SceneManager.LoadScene(DraftScene); }
        public static void ToDraftBuild()
        { if (!DraftRuntimeStore.CanBattle) return; TimeController.ResetAll(); SceneManager.LoadScene(CustomizeScene); }
        public static void NextDraftRound()
        { if (DraftRuntimeStore.Next()) { if (DraftRuntimeStore.CanBattle) ToDraftBuild(); else ToDraftPicks(); } }
        public static void ToDraftBattle()
        { if (!DraftRuntimeStore.CanBattle) return; TimeController.ResetAll(); SceneManager.LoadScene(BattleScene); }
        public static void ToChallengeDetail(int id)
        {
            LeaveTournament(); ChallengeRuntimeStore.Select(id); SceneManager.LoadScene(ChallengeScene);
        }
        public static void ToChallengePrepare()
        {
            if (!ChallengeMode) return;
            TimeController.ResetAll(); SceneManager.LoadScene(CustomizeScene);
        }
        public static void BackToChallengeDetail()
        {
            if (!ChallengeMode) { ToChallenges(); return; }
            ChallengeRuntimeStore.ShowDetail = true; TimeController.ResetAll(); SceneManager.LoadScene(ChallengeScene);
        }
        public static void ToChallengeBattle()
        {
            if (!ChallengeMode) return;
            TimeController.ResetAll(); SceneManager.LoadScene(BattleScene);
        }
        public static void ToFriendRoom(bool entering = true)
        {
            if (entering) LeaveTournament();
            EnterFriendMode();
            SceneManager.LoadScene(FriendRoomScene);
        }
        public static void EnterFriendMode() { WorldCupRuntimeStore.ClearSession();TournamentSelect=false;RunRuntimeStore.ClearSession(); DraftRuntimeStore.ClearSession(); ChallengeRuntimeStore.ClearSession(); TournamentRuntimeStore.Clear(); TournamentMode = false; FriendMode = true; TimeController.ResetAll(); }
        public static void ToFriendCustomize() { TimeController.ResetAll(); SceneManager.LoadScene(CustomizeScene); }
        public static void ToFriendBattle()
        {
            if (FriendRoomSession.Instance?.Connected != true || FriendRoomSession.Instance.Room.stage != "battle") return;
            TimeController.ResetAll(); SceneManager.LoadScene(BattleScene);
        }
        public static void ToTournamentCustomize()
        {
            WorldCupRuntimeStore.ClearSession();TournamentSelect=false;
            RunRuntimeStore.ClearSession();
            DraftRuntimeStore.ClearSession();
            ChallengeRuntimeStore.ClearSession();
            if (TournamentRuntimeStore.Current != null && !TournamentRuntimeStore.Current.Finished)
            { ToTournamentBoard(); return; }
            TournamentRuntimeStore.Clear();
            TournamentMode = true;
            TimeController.ResetAll();
            SceneManager.LoadScene(CustomizeScene);
        }
        public static void BeginTournament(FighterBuildData player, int seed)
        {
            if (!FighterCustomizer.IsAvailable(player.character, player.fontType, out var reason))
                throw new System.ArgumentException(reason);
            TournamentRuntimeStore.Begin(player, seed);
            AchievementStore.Change(p => p.Set(46,1),true);
            TournamentMode = true;
            ToTournamentBoard();
        }
        public static void ToTournamentBoard()
        {
            TimeController.ResetAll();
            SceneManager.LoadScene(TournamentScene);
        }
        public static void ToTournamentBattle()
        {
            if (TournamentRuntimeStore.Current?.NextPlayerMatch == null) return;
            TournamentMode = true;
            SceneManager.LoadScene(BattleScene);
        }

        public static void ToTournamentSelect()
        { LeaveTournament();TournamentSelect=true;SceneManager.LoadScene(WorldCupScene); }
        public static void ToWorldCupGuide()
        { LeaveTournament();SceneManager.LoadScene(WorldCupScene); }
        public static void ToWorldCupCustomize()
        { LeaveTournament();WorldCupRuntimeStore.Prepare();SceneManager.LoadScene(CustomizeScene); }
        public static void BeginWorldCup(FighterBuildData build,int seed)
        { WorldCupRuntimeStore.Begin(build,seed);ToWorldCupScreen(); }
        public static void ResumeWorldCup()
        { LeaveTournament();if(!WorldCupRuntimeStore.Resume()){ToWorldCupGuide();return;}ToWorldCupScreen(); }
        public static void ToWorldCupScreen()
        { if(WorldCupRuntimeStore.Current==null)return;TimeController.ResetAll();SceneManager.LoadScene(WorldCupScene); }
        public static void ToWorldCupBattle()
        {
            var s=WorldCupRuntimeStore.Current;var m=s?.Match(s.SelectedMatch);
            if(m==null || !m.Ready)return;
            TimeController.ResetAll();SceneManager.LoadScene(BattleScene);
        }
        public static void BackFromWorldCupBattle()
        { WorldCupRuntimeStore.AbortMatch();ToWorldCupScreen(); }

        /// <summary>カスタマイズ内容（BuildRuntimeStore）で試合を始める。</summary>
        public static void ToBattle()
        {
            if(WorldCupMode){ToWorldCupBattle();return;}
            if (RunMode) { ToRunBattle(); return; }
            if (DraftMode) { ToDraftBattle(); return; }
            if (ChallengeMode) { ToChallengeBattle(); return; }
            if (TournamentMode) { ToTournamentBattle(); return; }
            BuildRuntimeStore.Active = true;
            SceneManager.LoadScene(BattleScene);
        }
    }
}
