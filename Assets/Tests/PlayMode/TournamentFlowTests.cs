using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MojiBattle.Tests
{
    public class TournamentFlowTests
    {
        FighterBuildData savedLeft, savedRight, savedDraft;
        [SetUp]
        public void Setup()
        {
            savedLeft = BuildRuntimeStore.Get(0).Clone();
            savedRight = BuildRuntimeStore.Get(1).Clone();
            savedDraft = TournamentRuntimeStore.Draft;
            TournamentRuntimeStore.Clear();
            TimeController.ResetAll();
            TimeController.PresentationEffectsEnabled = false;
            Time.captureDeltaTime = .02f;
        }
        [TearDown]
        public void Cleanup()
        {
            TournamentRuntimeStore.Clear();
            TournamentRuntimeStore.SetDraft(savedDraft);
            BuildRuntimeStore.Set(0, savedLeft);
            BuildRuntimeStore.Set(1, savedRight);
            BuildRuntimeStore.Active = false;
            ColliderDebugView.Visible = false;
            StickmanView.ShowDebugLabels = false;
            SimHarness.End();
            GameFlow.ToTitle();
        }
        static Button Button(string name) => Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b => b.name == name);
        static IEnumerator Frames(int count = 3) { for (int i = 0; i < count; i++) yield return null; }
        static IEnumerator BoardReady()
        {
            double until = Time.realtimeSinceStartupAsDouble + 90;
            while (Time.realtimeSinceStartupAsDouble < until)
            {
                var screen = Object.FindAnyObjectByType<TournamentScreen>();
                if (screen != null && screen.Busy)
                {
                    foreach (var view in Object.FindObjectsByType<StickmanView>(FindObjectsSortMode.None))
                    {
                        Assert.IsFalse(view.enabled, "COM集計中に棒人間の表示が有効になった。");
                        Assert.IsTrue(view.GetComponentsInChildren<Renderer>(true).All(r => !r.enabled),
                            "COM集計中に技名などの描画が有効になった。");
                        Assert.IsFalse(view.GetComponentsInChildren<TextMesh>(true)
                            .Single(t => t.name == "TechniqueCallout").gameObject.activeSelf,
                            "COM集計中に技名が再表示された。");
                    }
                }
                if (screen != null && screen.Session != null && !screen.Busy) yield break;
                yield return null;
            }
            Assert.Fail("Tournament bracket did not become ready.");
        }
        static IEnumerator EnterCup()
        {
            GameFlow.ToTitle();
            yield return Frames();
            Button("Start").onClick.Invoke();
            yield return Frames();
            Assert.IsNotNull(Object.FindAnyObjectByType<ModeSelectScreen>());
            Capture("tournament_mode_select");
            Button("TournamentModeStart").onClick.Invoke();
            yield return Frames();
            Assert.IsNotNull(Object.FindAnyObjectByType<WorldCupScreen>());
            Button("TournamentChoiceStart0").onClick.Invoke();
            yield return Frames();
            var screen = Object.FindAnyObjectByType<CustomizeScreen>();
            Assert.IsNotNull(screen);
            var c = screen.Customizer;
            Assert.IsTrue(c.SetCharacter("剣"), c.CharacterError);
            c.SetSize(WeaponSize.XL);
            Assert.AreEqual(WeaponSize.S, c.Current.weaponSize);
            c.SelectSide(1);
            Assert.AreEqual(0, c.Side);
            c.SetGrip(GripType.Reverse);
            c.SetStyle(BattleStyle.HitAndAway);
            c.SetFlip(WeaponFlip.Both);
            c.SetGripPoint(new Vector2(.3f, .7f));
            Assert.AreEqual(GripType.Reverse, c.Current.gripType);
            Assert.AreEqual(BattleStyle.HitAndAway, c.Current.battleStyle);
            Assert.AreEqual(RetroUi.Red, Button("Grip_Reverse").GetComponent<Image>().color, "Selected grip must be visible in the player's color.");
            Assert.AreEqual(RetroUi.Red, Button("Style_HitAndAway").GetComponent<Image>().color, "Selected style must be visible in the player's color.");
            foreach (var size in new[] { "S", "M", "L", "XL" }) Assert.IsFalse(Button("Size_" + size).interactable);
            Assert.IsFalse(Button("Side1").interactable);
            yield return Frames();
            Capture("tournament_customize");
            Button("Battle").onClick.Invoke();
            yield return Frames();
            yield return BoardReady();
            Assert.AreEqual(GameFlow.TournamentScene, SceneManager.GetActiveScene().name);
            Assert.AreEqual(8, TournamentRuntimeStore.Current.Entrants.Count);
            Assert.IsTrue(TournamentRuntimeStore.Current.Entrants.All(e => e.Build.weaponSize == WeaponSize.S));
            Assert.IsTrue(Button("NextTournamentMatch").interactable);
        }

        [UnityTest]
        public IEnumerator Title_ModeSelect_RegularBattleStillAllowsBothCustomizations()
        {
            GameFlow.ToTitle();
            yield return Frames();
            Button("Start").onClick.Invoke();
            yield return Frames();
            Assert.AreEqual(GameFlow.ModeSelectScene, SceneManager.GetActiveScene().name);
            Button("BattleModeStart").onClick.Invoke();
            yield return Frames();
            Assert.IsFalse(GameFlow.TournamentMode);
            var screen = Object.FindAnyObjectByType<CustomizeScreen>();
            screen.Customizer.SetSize(WeaponSize.XL);
            Assert.AreEqual(WeaponSize.XL, screen.Customizer.Current.weaponSize);
            screen.Customizer.SelectSide(1);
            Assert.AreEqual(1, screen.Customizer.Side);
            Assert.IsTrue(Button("Size_XL").interactable);
            screen.ShowVs();
            Assert.IsTrue(screen.VsVisible);
        }

        [UnityTest]
        public IEnumerator Tournament_ThreeWins_Champion_LocksBuild_AndShowsEveryBracket()
        {
            string regularLeft = JsonUtility.ToJson(BuildRuntimeStore.Get(0));
            string regularRight = JsonUtility.ToJson(BuildRuntimeStore.Get(1));
            yield return EnterCup();
            Assert.AreEqual(regularLeft, JsonUtility.ToJson(BuildRuntimeStore.Get(0)));
            Assert.AreEqual(regularRight, JsonUtility.ToJson(BuildRuntimeStore.Get(1)));
            var session = TournamentRuntimeStore.Current;
            string fixedBuild = JsonUtility.ToJson(session.Entrants[0].Build);
            Capture("tournament_initial_bracket");
            for (int round = 0; round < 3; round++)
            {
                var match = session.NextPlayerMatch;
                Assert.AreEqual(round, match.Round);
                Button("NextTournamentMatch").onClick.Invoke();
                yield return Frames();
                var boot = Object.FindAnyObjectByType<BattleBootstrap>();
                Assert.IsTrue(boot.IsTournament);
                Assert.IsNotNull(boot.Battle);
                string music = boot.Battle.Root.GetComponent<CombatAudioDirector>().MusicSource.clip.name;
                if (round == 2) Assert.AreEqual("final2", music);
                else CollectionAssert.Contains(new[] { "battle", "battle2", "battle3" }, music);
                Assert.AreEqual(WeaponSize.S, boot.Battle.Left.Mods.size);
                Assert.AreEqual(WeaponSize.S, boot.Battle.Right.Mods.size);
                Assert.AreEqual(fixedBuild, JsonUtility.ToJson(boot.Battle.Left.Loadout.build));
                Assert.AreEqual(JsonUtility.ToJson(session.Entrants[match.RightId].Build), JsonUtility.ToJson(boot.Battle.Right.Loadout.build));
                Assert.AreEqual(boot.Battle.Left.Runtime.maxHp, boot.Battle.Left.Runtime.hp);
                var battle = boot.Battle;
                int seed = boot.CurrentSeed;
                boot.RematchSameBuild();
                boot.StartMatch(seed + 10);
                Assert.AreSame(battle, boot.Battle);
                Assert.AreEqual(seed, boot.CurrentSeed);
                TournamentRuntimeStore.SetDraft(new FighterBuildData { character = "鬱", weaponSize = WeaponSize.XL });
                var copy = session.Entrants[0].Build;
                copy.weaponSize = WeaponSize.XL;
                copy.character = "山";
                Assert.AreEqual(fixedBuild, JsonUtility.ToJson(session.Entrants[0].Build));
                battle.Right.Runtime.hp = 0;
                yield return BoardReady();
                yield return Frames();
                Assert.AreEqual(round + 1, session.Wins);
                Assert.IsNotNull(Object.FindAnyObjectByType<TournamentScreen>());
                Capture(round == 2 ? "tournament_champion" : "tournament_after_win_" + (round + 1));
            }
            Assert.IsTrue(session.Champion);
            Assert.IsTrue(session.Matches.All(m => m.Complete));
            Assert.IsTrue(Button("NextTournamentMatch").GetComponentInChildren<Text>().text.Contains("もう一度"));
            Button("NextTournamentMatch").onClick.Invoke();
            yield return Frames();
            Assert.IsNull(TournamentRuntimeStore.Current);
            Assert.AreEqual(GameFlow.CustomizeScene, SceneManager.GetActiveScene().name);
            Assert.AreEqual(WeaponSize.S, Object.FindAnyObjectByType<CustomizeScreen>().Customizer.Current.weaponSize);
        }

        [UnityTest]
        public IEnumerator Tournament_DrawRematchesSameOpponent_ThenDefeatEndsRun()
        {
            yield return EnterCup();
            var session = TournamentRuntimeStore.Current;
            var first = session.NextPlayerMatch;
            int opponent = first.RightId;
            string opponentBuild = JsonUtility.ToJson(session.Entrants[opponent].Build);
            Button("NextTournamentMatch").onClick.Invoke();
            yield return Frames();
            var boot = Object.FindAnyObjectByType<BattleBootstrap>();
            int seed = boot.CurrentSeed;
            boot.Battle.Left.Runtime.hp = 0;
            boot.Battle.Right.Runtime.hp = 0;
            yield return BoardReady();
            Assert.AreEqual(0, session.Wins);
            Assert.IsFalse(session.Finished);
            Assert.AreEqual(1, first.Draws);
            Assert.AreEqual(opponent, session.NextPlayerMatch.RightId);
            Assert.IsTrue(Button("NextTournamentMatch").GetComponentInChildren<Text>().text.Contains("再試合"));
            Button("NextTournamentMatch").onClick.Invoke();
            yield return Frames();
            boot = Object.FindAnyObjectByType<BattleBootstrap>();
            Assert.AreNotEqual(seed, boot.CurrentSeed);
            Assert.AreEqual(opponentBuild, JsonUtility.ToJson(boot.Battle.Right.Loadout.build));
            Assert.AreEqual(boot.Battle.Left.Runtime.maxHp, boot.Battle.Left.Runtime.hp);
            boot.Battle.Left.Runtime.hp = 0;
            yield return BoardReady();
            Assert.IsTrue(session.Eliminated);
            Assert.AreEqual(0, session.Wins);
            Assert.IsNull(session.NextPlayerMatch);
            Assert.Greater(session.ChampionId, 0);
            Capture("tournament_defeat");
            Button("ExitTournament").onClick.Invoke();
            yield return Frames();
            Assert.IsNull(TournamentRuntimeStore.Current);
            Assert.AreEqual(GameFlow.ModeSelectScene, SceneManager.GetActiveScene().name);
        }

        [UnityTest]
        public IEnumerator DisabledFighterView_HidesExistingCallout_AndIgnoresCombatEvents()
        {
            var battle = SimHarness.Build(9902);
            try
            {
                battle.Director.enabled = false;
                var view = battle.Left.View;
                var label = view.GetComponentsInChildren<TextMesh>(true).Single(t => t.name == "TechniqueCallout");
                var plate = view.GetComponentsInChildren<SpriteRenderer>(true).Single(r => r.name == "TechniquePlate");
                battle.Context.Events.Raise(new FighterMotionEvent { fighter = 0, kind = FighterMotionKind.Dash });
                Assert.IsTrue(label.gameObject.activeSelf, "観戦時には通常の技名を表示する。");
                view.enabled = false;
                Assert.IsFalse(label.gameObject.activeSelf, "表示を止めたら既存の技名も消す。");
                Assert.IsFalse(plate.gameObject.activeSelf);
                foreach (FighterMotionKind kind in System.Enum.GetValues(typeof(FighterMotionKind)))
                    battle.Context.Events.Raise(new FighterMotionEvent { fighter = 0, kind = kind });
                battle.Context.Events.Raise(new GuardEvent { defender = 0, parried = true });
                yield return null;
                Assert.IsFalse(label.gameObject.activeSelf, "無効な表示は戦闘イベントで再表示しない。");
                Assert.IsFalse(plate.gameObject.activeSelf);
                view.enabled = true;
                battle.Context.Events.Raise(new GuardEvent { defender = 0, parried = true });
                Assert.IsTrue(label.gameObject.activeSelf, "観戦へ戻った場合は技名を表示できる。");
                Assert.AreEqual("受け流し！", label.text);
            }
            finally { battle.Destroy(); }
        }

        [UnityTest]
        public IEnumerator ComputerSimulation_UsesRealMatchRules_WithoutChangingSpectatorTime()
        {
            var config = new MatchConfig { left = SimHarness.Custom("火", WeaponSize.S),
                right = SimHarness.Custom("口", WeaponSize.S, style: BattleStyle.Defensive), seed = 7611, durationSeconds = 15 };
            var battle = BattleBuilder.Build(config, CombatBalance.Default, GlyphCalibration.Default, false, 0, out var error);
            Assert.IsNotNull(battle, error);
            battle.Director.enabled = false;
            battle.Director.SilentSimulation = true;
            TimeController.PresentationEffectsEnabled = true;
            TimeController.SetSpectatorSpeed(2);
            TimeController.SetPaused(true);
            for (int steps = 0; !battle.Director.Ended && steps < 3000; steps++) battle.Director.SimulateStep();
            Assert.IsTrue(battle.Director.Ended);
            Assert.IsTrue(TimeController.Paused);
            Assert.AreEqual(2, TimeController.SpectatorSpeed);
            Assert.IsFalse(TimeController.KoSlowActive);
            Assert.IsNull(battle.Root.GetComponent<CombatAudioDirector>());
            var expected = battle.Director.Result;
            battle.Destroy();
            yield return null;
            TimeController.ResetAll();
            TimeController.PresentationEffectsEnabled = false;
            var replay = BattleBuilder.Build(config, CombatBalance.Default, GlyphCalibration.Default, false, 0, out error);
            replay.Director.enabled = false;
            for (int steps = 0; !replay.Director.Ended && steps < 3000; steps++) replay.Director.SimulateStep();
            Assert.IsTrue(replay.Director.Ended);
            Assert.AreEqual(expected.winner, replay.Director.Result.winner);
            Assert.AreEqual(expected.elapsedSeconds, replay.Director.Result.elapsedSeconds);
            CollectionAssert.AreEqual(expected.hpRemaining, replay.Director.Result.hpRemaining);
            replay.Destroy();
        }

        static void Capture(string name)
        {
            var cam = Camera.main;
            if (cam == null) return;
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
            foreach (var c in canvases) { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = 1; }
            Canvas.ForceUpdateCanvases();
            var rt = new RenderTexture(1280, 720, 24);
            var oldTarget = cam.targetTexture;
            float oldAspect = cam.aspect;
            cam.targetTexture = rt;
            cam.aspect = 16f / 9f;
            cam.Render();
            var oldActive = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            tex.Apply();
            string dir = Path.Combine(SimHarness.ReportDir, "Screenshots");
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
            RenderTexture.active = oldActive;
            cam.targetTexture = oldTarget;
            cam.aspect = oldAspect;
            foreach (var c in canvases) c.renderMode = RenderMode.ScreenSpaceOverlay;
            Object.Destroy(tex);
            rt.Release();
            Object.Destroy(rt);
        }
    }
}
