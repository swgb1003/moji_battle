using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MojiBattle.Tests
{
    public class BattleUiTests
    {
        FighterBuildData left, right;
        int reduced;
        [SetUp] public void Setup()
        {
            left = BuildRuntimeStore.Get(0).Clone(); right = BuildRuntimeStore.Get(1).Clone();
            reduced = PlayerPrefs.GetInt("MenuReducedMotion", 0); PlayerPrefs.SetInt("MenuReducedMotion", 0);
            TimeController.ResetAll();
        }
        [TearDown] public void Cleanup()
        {
            GameFlow.ToTitle(); BuildRuntimeStore.Set(0, left); BuildRuntimeStore.Set(1, right);
            BuildRuntimeStore.Active = false; PlayerPrefs.SetInt("MenuReducedMotion", reduced);
            SimHarness.End();
        }
        static Button Button(string name) => Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b => b.name == name);
        static IEnumerator StartBattle()
        {
            GameFlow.ToCustomize(); yield return null; yield return null;
            BuildRuntimeStore.Set(0, new FighterBuildData { character = "火", weaponSize = WeaponSize.L, battleStyle = BattleStyle.Aggressive });
            BuildRuntimeStore.Set(1, new FighterBuildData { character = "鬱", weaponSize = WeaponSize.L, battleStyle = BattleStyle.Aggressive });
            GameFlow.ToBattle(); yield return null; yield return null;
            var boot = Object.FindAnyObjectByType<BattleBootstrap>();
            boot.useCountdown = false; boot.StartMatch(801942925);
            yield return null; yield return null;
        }

        [UnityTest] public IEnumerator SpaciousArena_RendersAndSpectatorControlsWork()
        {
            yield return StartBattle();
            var boot = Object.FindAnyObjectByType<BattleBootstrap>();
            var battle = boot.Battle; var hud = battle.Root.GetComponent<BattleHudPresenter>();
            double deadline = Time.realtimeSinceStartupAsDouble + 8;
            while (battle.Director.Clock < 4 && !battle.Director.Ended && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            TimeController.SetPaused(true); yield return new WaitForSecondsRealtime(.8f);
            Assert.IsFalse(battle.Director.Ended, "The selected capture should be an ongoing fight.");
            Assert.Greater(BattleHudPresenter.ArenaHeight / 1080f, .72f);
            Assert.AreEqual(battle.CameraRig.Cam.targetTexture, hud.BattleImage.texture);
            Assert.AreEqual(0, battle.CameraRig.Cam.backgroundColor.a);
            Assert.IsTrue(hud.DisplayCamera.isActiveAndEnabled);
            Assert.IsNull(hud.DisplayCamera.targetTexture, "Display 1 needs an enabled onscreen camera, even with overlay UI.");
            Assert.AreEqual(0, hud.DisplayCamera.cullingMask, "The screen camera must not render fighters a second time.");
            float ground = battle.CameraRig.Cam.WorldToViewportPoint(Vector3.zero).y;
            var groundLine = battle.Root.GetComponentsInChildren<RectTransform>().Single(t => t.name == "GroundLine");
            Assert.AreEqual(-(1 - ground) * BattleHudPresenter.ArenaHeight - 2, groundLine.anchoredPosition.y, .1f);
            Assert.AreEqual(RetroUi.Red, battle.Left.WeaponSprite.color);
            Assert.AreEqual(RetroUi.Blue, battle.Right.WeaponSprite.color);
            hud.ShowTechnique("突き", "THRUST", 0, battle.Left.HeadWorld);
            yield return new WaitForSecondsRealtime(.6f);
            MenuUiTests.Capture("battle_01_live");
            MenuUiTests.Capture("battle_01_880x450", 880, 450);
            MenuUiTests.Capture("battle_01_1280x960", 1280, 960);
            Button("BattleSpeed2").onClick.Invoke(); yield return null;
            Assert.AreEqual(2, TimeController.SpectatorSpeed); Assert.IsTrue(TimeController.Paused);
            Assert.AreEqual(RetroUi.Ink, Button("BattleSpeed2").GetComponent<Image>().color);
            Button("BattleSpeed0").onClick.Invoke(); yield return null; Assert.AreEqual(.5f, TimeController.SpectatorSpeed);
            Button("BattleDetails").onClick.Invoke(); yield return new WaitForSecondsRealtime(.65f);
            Assert.IsTrue(hud.DetailsVisible); MenuUiTests.Capture("battle_02_details");
            Button("BattleDetailsClose").onClick.Invoke(); Assert.IsFalse(hud.DetailsVisible);
            float previousHp = battle.Left.Runtime.hp;
            battle.Left.Runtime.hp -= 15;
            yield return null;
            var fill = battle.Root.GetComponentsInChildren<RectTransform>().Single(t => t.name == "HpFill0");
            var trail = battle.Root.GetComponentsInChildren<RectTransform>().Single(t => t.name == "HpTrail0");
            Assert.Less(fill.rect.width, trail.rect.width, "The damage trail remains visible after an HP drop.");
            float previousTrail = trail.rect.width;
            yield return new WaitForSecondsRealtime(.2f);
            Assert.Less(trail.rect.width, previousTrail, "The trail animates even while spectating is paused.");
            battle.Left.Runtime.hp = previousHp;
            float clock = battle.Director.Clock; yield return new WaitForSecondsRealtime(.2f);
            Assert.AreEqual(clock, battle.Director.Clock, "UI interactions preserve a paused simulation.");
            Button("BattlePause").onClick.Invoke(); yield return new WaitForSecondsRealtime(.2f);
            Assert.IsFalse(TimeController.Paused); Assert.Greater(battle.Director.Clock, clock);
            Button("BattlePause").onClick.Invoke(); yield return null;
            Assert.AreEqual("▶  再開", Button("BattlePause").GetComponentInChildren<Text>().text);
        }

        [UnityTest] public IEnumerator Rematch_ReplacesTextureAndHud_AndBackPreservesBuild()
        {
            yield return StartBattle(); yield return new WaitForSecondsRealtime(.8f);
            var boot = Object.FindAnyObjectByType<BattleBootstrap>();
            var oldRoot = boot.Battle.Root; var oldTexture = boot.Battle.CameraRig.Cam.targetTexture;
            var oldDisplay = oldRoot.GetComponent<BattleHudPresenter>().DisplayCamera;
            Button("BattleAgain").onClick.Invoke(); yield return null; yield return null;
            Assert.IsTrue(oldRoot == null);
            Assert.IsTrue(oldTexture == null);
            Assert.IsTrue(oldDisplay == null);
            Assert.AreEqual(1, Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Count(c => c.name == "BattleDisplayCamera"));
            Assert.AreEqual(1, Object.FindObjectsByType<BattleHudPresenter>(FindObjectsSortMode.None).Length);
            Assert.AreEqual("火", boot.Battle.Left.Loadout.grapheme);
            Assert.AreEqual(WeaponSize.L, boot.Battle.Left.Mods.size);
            boot.Battle.Right.Runtime.hp = 0;
            double deadline = Time.realtimeSinceStartupAsDouble + 5;
            while (!boot.Battle.Director.Ended && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.IsTrue(boot.Battle.Director.Ended);
            yield return new WaitForSecondsRealtime(.8f); MenuUiTests.Capture("battle_03_result");
            Button("BattleBack").onClick.Invoke(); yield return null; yield return null;
            Assert.IsNull(Camera.main.targetTexture);
            Assert.IsFalse(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Any(c => c.name == "BattleDisplayCamera"));
            Assert.AreEqual("火", Object.FindAnyObjectByType<CustomizeScreen>().Customizer.Get(0).character);
            Assert.AreEqual(1, Time.timeScale);
        }

        static void Move(Fighter f, Vector2 position)
        {
            Vector2 delta = position - f.Body.position;
            f.Body.position = position; f.Body.transform.position = position;
            f.WeaponBody.position += delta; f.WeaponBody.transform.position = f.WeaponBody.position;
            f.CapturePose(); f.CapturePose();
        }
        static void AssertFramed(CameraRig rig)
        {
            foreach (var p in new[] { rig.FocusBounds.min, rig.FocusBounds.max })
            {
                var v = rig.Cam.WorldToViewportPoint(p);
                Assert.That(v.x, Is.InRange(.01f, .99f), "Both glyph weapons must fit horizontally.");
                Assert.That(v.y, Is.InRange(.01f, .99f), "The action must fit vertically.");
            }
        }
        [UnityTest] public IEnumerator Camera_ClosesIn_FollowsAcrossArena_AndFramesAirborneFighters()
        {
            yield return StartBattle();
            var battle = Object.FindAnyObjectByType<BattleBootstrap>().Battle;
            battle.Director.enabled = false; TimeController.SetPaused(true);
            Move(battle.Left, new Vector2(-.8f, .02f)); Move(battle.Right, new Vector2(.8f, .02f));
            yield return new WaitForSecondsRealtime(1.2f);
            float closeSize = battle.CameraRig.Cam.orthographicSize;
            Assert.Less(closeSize, 3.6f, "Close combat should be visibly larger than the previous wide 4.6-unit view.");
            AssertFramed(battle.CameraRig);
            Move(battle.Left, new Vector2(3.2f, .02f)); Move(battle.Right, new Vector2(4.8f, .02f));
            yield return new WaitForSecondsRealtime(1);
            Assert.Greater(battle.CameraRig.transform.position.x, 2.5f, "The camera should follow action to the side.");
            AssertFramed(battle.CameraRig); MenuUiTests.Capture("battle_camera_close");
            Move(battle.Left, new Vector2(-7, .02f)); Move(battle.Right, new Vector2(7, .02f));
            yield return new WaitForSecondsRealtime(1);
            Assert.Greater(battle.CameraRig.Cam.orthographicSize, closeSize + .4f);
            AssertFramed(battle.CameraRig);
            Move(battle.Left, new Vector2(3.2f, 6)); Move(battle.Right, new Vector2(4.8f, 6));
            yield return new WaitForSecondsRealtime(1.3f);
            Assert.Greater(battle.CameraRig.transform.position.y, 5, "The camera must track both fighters in the air.");
            AssertFramed(battle.CameraRig);
            var line = battle.Root.GetComponentsInChildren<RectTransform>(true).Single(t => t.name == "GroundLine");
            Assert.IsFalse(line.gameObject.activeSelf, "The ground rule must disappear when the camera tracks action above it.");
            MenuUiTests.Capture("battle_camera_airborne");
        }

        [UnityTest] public IEnumerator OpponentSpecial_RendersBracePromptAndCheerGauges()
        {
            yield return StartBattle();
            var battle = Object.FindAnyObjectByType<BattleBootstrap>().Battle;
            battle.Director.Events.Raise(new InterventionEvent { fighter = 1, kind = InterventionKind.Special, success = true });
            yield return new WaitForSecondsRealtime(.55f);
            TimeController.SetPaused(true);
            Assert.IsTrue(battle.Root.GetComponent<SpecialCutIn>().Showing);
            var prompt = battle.Root.GetComponentsInChildren<Text>().Single(t => t.name == "BracePrompt");
            Assert.AreEqual("L / 声援ボタンで踏ん張れ！", prompt.text);
            Canvas.ForceUpdateCanvases();
            var mesh = prompt.canvasRenderer.GetMesh();
            Assert.That(mesh.vertexCount, Is.InRange(1, 64999), "踏ん張りの合図を描画上限内で実際に表示する");
            MenuUiTests.Capture("battle_opponent_special_brace_prompt");
        }

        [UnityTest] public IEnumerator CheerGauge_ShowsOpponentEffectCooldownAndSyncedReadiness()
        {
            yield return StartBattle();
            var battle = Object.FindAnyObjectByType<BattleBootstrap>().Battle;
            TimeController.SetPaused(true);
            var opponent=battle.Context.Interventions.Side(1);
            opponent.controller=InterventionController.Remote;
            opponent.cheerReadyAt=0; opponent.cheerUntil=-1;
            var opponentPanel = battle.Root.GetComponentsInChildren<RectTransform>().Single(t => t.name == "Intervention1");
            var fill = opponentPanel.GetComponentsInChildren<RectTransform>().Single(t => t.name == "CheerGaugeFill");
            var state = opponentPanel.GetComponentsInChildren<Text>().Single(t => t.name == "CheerGaugeState");
            yield return null;
            Assert.AreEqual("使用可", state.text);
            float fullWidth = fill.rect.width;
            Assert.Greater(fullWidth, 0);

            battle.Context.Interventions.Request(1, InterventionKind.Cheer, false);
            yield return null;
            Assert.That(state.text, Does.StartWith("効果 "));
            Assert.AreEqual(0, fill.rect.width, .01f);
            MenuUiTests.Capture("battle_cheer_opponent_effect");

            // ゲストが受信する残り時間で、相手の待機状態とゲージを更新する。
            var frame = FriendActorFrame.Capture(battle.Right);
            frame.cheer = -1;
            frame.cheerWait = battle.Context.Balance.intervention.cheerCooldown / 2;
            frame.Apply(battle.Right, null, 1);
            yield return null;
            Assert.That(state.text, Does.StartWith("あと "));
            Assert.AreEqual(fullWidth / 2, fill.rect.width, .01f);
            string pausedState = state.text;
            yield return new WaitForSecondsRealtime(.2f);
            Assert.AreEqual(pausedState, state.text, "一時停止中はゲーム内の残り時間を維持する");
            MenuUiTests.Capture("battle_cheer_opponent_cooldown");
            MenuUiTests.Capture("battle_cheer_880x450", 880, 450);

            frame.cheerWait = 0;
            frame.Apply(battle.Right, null, 1);
            yield return null;
            Assert.AreEqual("使用可", state.text);
            Assert.AreEqual(fullWidth, fill.rect.width, .01f);
            frame.braced = true;
            frame.Apply(battle.Right, null, 1);
            yield return null;
            Assert.AreEqual("踏ん張り", state.text);
        }

        [UnityTest] public IEnumerator Interventions_PlayerHasControls_ComShowsGauge_CueAndSpecialAreVisible()
        {
            yield return StartBattle();
            var battle = Object.FindAnyObjectByType<BattleBootstrap>().Battle;
            var iv = battle.Context.Interventions;
            Assert.IsNotNull(iv, "通常のバトルでは観戦者が関与できる");
            foreach(var text in battle.Root.GetComponentsInChildren<Text>(true))
                Assert.That(text.text,Does.Not.Contain("ジャストガード"),"戦闘開始とヘルプにも削除済み操作を表示しない");
            Assert.AreEqual(InterventionController.Local, iv.Side(0).controller);
            Assert.AreEqual(InterventionController.Com, iv.Side(1).controller);
            Assert.AreEqual(1, Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Count(b => b.name == "SpecialButton"), "操作ボタンは自分の陣営だけ");
            double deadline = Time.realtimeSinceStartupAsDouble + 8;
            while (battle.Director.Clock < 2 && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            TimeController.SetPaused(true);
            iv.Side(0).gauge = 1f; iv.Side(1).gauge = .55f;
            yield return null; yield return null;
            Assert.IsTrue(Button("SpecialButton").interactable == false, "一時停止中は押せない");
            Assert.AreEqual(0, Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Count(b => b.name == "JustGuardButton"), "ジャストガードは無い");
            yield return new WaitForSecondsRealtime(.6f);
            MenuUiTests.Capture("battle_intervention_controls");
            TimeController.SetPaused(false); yield return null;
            Assert.IsTrue(Button("SpecialButton").interactable);
            Button("SpecialButton").onClick.Invoke();
            deadline = Time.realtimeSinceStartupAsDouble + 10;
            while (!iv.IsSpecial(battle.Left) && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.IsTrue(iv.IsSpecial(battle.Left), "ボタンで必殺技が出る（離れていれば詰めてから）");
            yield return new WaitForSecondsRealtime(.25f);
            MenuUiTests.Capture("battle_intervention_special");
        }
    }
}
