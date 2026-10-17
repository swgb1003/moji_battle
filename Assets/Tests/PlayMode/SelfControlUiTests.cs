using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MojiBattle.Tests
{
    /// <summary>練習モードの流れ・自操作の切り替え（各モードの対戦前画面）・操作説明の表示。画面を Reports/UI/SelfControl に保存する。</summary>
    public class SelfControlUiTests
    {
        static readonly string[] Keys = { GameSettings.PrefKey, RunRuntimeStore.PrefKey, WorldCupRuntimeStore.PrefKey, AchievementStore.PrefKey,
            ChallengeRuntimeStore.PrefKey };
        string[] saved; bool[] had; bool guideSeen, unlocked;
        FighterBuildData left, right;

        [SetUp] public void Setup()
        {
            saved = Keys.Select(k => PlayerPrefs.GetString(k)).ToArray(); had = Keys.Select(PlayerPrefs.HasKey).ToArray();
            guideSeen = PlayerPrefs.GetInt(ControlsGuide.SeenKey, 0) == 1; unlocked = PlayControl.TrueUnlocked;
            left = BuildRuntimeStore.Get(0).Clone(); right = BuildRuntimeStore.Get(1).Clone();
            PlayerPrefs.DeleteKey(ControlsGuide.SeenKey); PlayControl.ResetUnlockForTests();
            GameSettings.Change(s => { s.selfControl = false; s.comDifficulty = (int)ComDifficulty.Normal; });
            TimeController.ResetAll();
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(Application.dataPath, "../Reports/UI/SelfControl"));
        }
        [TearDown] public void Cleanup()
        {
            ControlsGuide.Close();
            GameFlow.ToTitle(); TimeController.ResetAll(); SimHarness.End();
            BuildRuntimeStore.Set(0, left); BuildRuntimeStore.Set(1, right); BuildRuntimeStore.Active = false;
            for (int i = 0; i < Keys.Length; i++) { if (had[i]) PlayerPrefs.SetString(Keys[i], saved[i]); else PlayerPrefs.DeleteKey(Keys[i]); }
            if (guideSeen) PlayerPrefs.SetInt(ControlsGuide.SeenKey, 1); else PlayerPrefs.DeleteKey(ControlsGuide.SeenKey);
            if (unlocked) PlayControl.Unlock(); else PlayControl.ResetUnlockForTests();
            PlayerPrefs.Save(); GameSettings.Reload(); AchievementStore.Reload();
        }

        static Button B(string name) => Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b => b.name == name);
        static Text T(string name) => Object.FindObjectsByType<Text>(FindObjectsSortMode.None).Single(t => t.name == name);
        static IEnumerator Ready() { yield return null; yield return null; yield return new WaitForSecondsRealtime(.7f); }
        static IEnumerator Scene(string scene)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 20;
            while (SceneManager.GetActiveScene().name != scene && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.AreEqual(scene, SceneManager.GetActiveScene().name);
            yield return Ready();
        }
        static void Capture(string name) { MenuUiTests.Capture("SelfControl/" + name); MenuUiTests.Capture("SelfControl/" + name + "_880x450", 880, 450); }

        [UnityTest] public IEnumerator PracticeFromMenu_SandbagThenDifficulty_NoHpLoss()
        {
            GameFlow.ToModeSelect(); yield return Ready(); Capture("01_menu");
            B("PracticeModeStart").onClick.Invoke(); yield return Scene(GameFlow.CustomizeScene);
            Assert.IsTrue(GameFlow.PracticeMode);
            Assert.AreEqual("練習開始 →", B("Battle").GetComponentInChildren<Text>().text);
            Capture("02_practice_customize");
            B("Battle").onClick.Invoke(); yield return Scene(GameFlow.BattleScene);
            Assert.IsTrue(ControlsGuide.IsOpen, "The first self-control battle opens the controls guide once.");
            Assert.IsTrue(TimeController.Paused);
            Capture("03_controls_guide");
            B("ControlsGuideClose").onClick.Invoke(); yield return Ready();
            Assert.IsFalse(ControlsGuide.IsOpen); Assert.IsFalse(TimeController.Paused);
            var boot = Object.FindAnyObjectByType<BattleBootstrap>();
            var ctx = boot.Battle.Context;
            Assert.IsTrue(ctx.Practice); Assert.AreEqual(0, ctx.PlayerSide);
            Assert.IsTrue(boot.Battle.Right.Brain.Frozen);
            Assert.AreEqual("相手：サンドバッグ", T("PracticeLevel").text);
            Assert.AreEqual("∞", T("BattleTimer").text);
            Assert.IsFalse(Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Any(b => b.name == "BattleSpeed0"), "Speed buttons are hidden in self-control.");
            Capture("04_practice_battle");
            B("PracticeHarder").onClick.Invoke(); yield return Ready();
            Assert.AreEqual(ComDifficulty.Beginner, PracticeSession.Difficulty);
            Assert.IsFalse(boot.Battle.Right.Brain.Frozen);
            double deadline = Time.realtimeSinceStartupAsDouble + 15;
            while (boot.Battle.Right.Runtime.metrics.attacksStarted == 0 && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.Greater(boot.Battle.Right.Runtime.metrics.attacksStarted, 0, "The beginner COM starts moving.");
            foreach (var f in boot.Battle.Director.Fighters) Assert.AreEqual(f.Runtime.maxHp, f.Runtime.hp);
            Capture("05_practice_beginner");
            // 真・mojiFighter は解放するまで選べない
            for (int i = 0; i < 8; i++) B("PracticeHarder").onClick.Invoke();
            Assert.AreEqual(ComDifficulty.MojiFighter, PracticeSession.Difficulty);
        }

        [UnityTest] public IEnumerator NormalBattle_VsPickerSwitchesToSelfControl()
        {
            PlayerPrefs.SetInt(ControlsGuide.SeenKey, 1);
            GameFlow.ToCustomize(); yield return Scene(GameFlow.CustomizeScene);
            B("Battle").onClick.Invoke(); yield return Ready();
            B("ConfirmStage").onClick.Invoke();yield return Ready();
            Assert.AreEqual("COM 同士", T("PlayModeLevel").text);
            B("PlayModeSelf").onClick.Invoke(); B("PlayModeHarder").onClick.Invoke(); yield return null;
            Assert.IsTrue(GameSettings.SelfControl); Assert.AreEqual(ComDifficulty.Strong, PlayControl.Difficulty);
            Assert.AreEqual("COM：強い", T("PlayModeLevel").text);
            Capture("06_vs_picker");
            B("Start").onClick.Invoke(); yield return Scene(GameFlow.BattleScene);
            var boot = Object.FindAnyObjectByType<BattleBootstrap>();
            Assert.AreEqual(0, boot.Battle.Context.PlayerSide);
            Assert.IsTrue(boot.Battle.Left.Brain.IsPlayer); Assert.IsFalse(boot.Battle.Right.Brain.IsPlayer);
            Assert.IsNotNull(B("BattleControlsGuide"));
            yield return new WaitForSecondsRealtime(3.5f);
            Capture("07_self_battle");
        }

        [UnityTest] public IEnumerator PickerAppearsBeforeRunTournamentChallengeAndWorldCupMatches()
        {
            GameFlow.BeginRun(4321); yield return Scene(GameFlow.CustomizeScene);
            B("Battle").onClick.Invoke(); yield return Scene(GameFlow.RunScene);
            B("RunVersus").onClick.Invoke(); yield return Ready();
            Assert.IsNotNull(Object.FindAnyObjectByType<PlayModePicker>()); Capture("08_run_vs");

            GameFlow.BeginTournament(FighterBuildData.Default("火"), 55); yield return Scene(GameFlow.TournamentScene);
            Assert.IsNotNull(Object.FindAnyObjectByType<PlayModePicker>()); Capture("09_tournament");

            GameFlow.ToChallengeDetail(0); yield return Scene(GameFlow.ChallengeScene);
            Assert.IsNotNull(Object.FindAnyObjectByType<PlayModePicker>()); Capture("10_challenge");

            GameFlow.ToWorldCupCustomize(); yield return Scene(GameFlow.CustomizeScene);
            B("Battle").onClick.Invoke(); yield return Scene(GameFlow.WorldCupScene);
            var s = WorldCupRuntimeStore.Current;
            foreach (var m in s.Matches.Where(m => !m.IncludesPlayer).ToArray())
            {
                s.BeginMatch(m.Id, 60);
                Assert.IsTrue(s.Record(m.Id, new MatchResult { seed = m.Seed, winner = 0, finishReason = "KO", elapsedSeconds = 20,
                    hpRemaining = new[] { 50f, 0f }, maxHp = new[] { 100f, 100f } }));
            }
            B("WorldCupDrawNext").onClick.Invoke();
            double deadline = Time.realtimeSinceStartupAsDouble + 60;
            while (Object.FindAnyObjectByType<WorldCupScreen>().Busy && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            yield return Ready();
            B("WorldCupNext").onClick.Invoke(); yield return Ready();
            Assert.IsNotNull(Object.FindAnyObjectByType<PlayModePicker>()); Capture("11_worldcup_vs");
        }
    }
}
